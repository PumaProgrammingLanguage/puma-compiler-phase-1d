using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class CharacterRuntimeExpressionTest
    {
        [DataTestMethod]
        [DataRow("'A'", "\\x41")]
        [DataRow("'\\n'", "\\x0A")]
        [DataRow("'\\0'", "\\x00")]
        [DataRow("'\\a'", "\\x07")]
        [DataRow("'\\b'", "\\x08")]
        [DataRow("'\\f'", "\\x0C")]
        [DataRow("'\\r'", "\\x0D")]
        [DataRow("'\\t'", "\\x09")]
        [DataRow("'\\v'", "\\x0B")]
        [DataRow("'\\\\'", "\\x5C")]
        [DataRow("'\\\''", "\\x27")]
        [DataRow("'\"'", "\\x22")]
        [DataRow("'\\\"'", "\\x22")]
        [DataRow("'\\x41'", "\\x41")]
        [DataRow("'\\xE9'", "\\xC3\\xA9")]
        [DataRow("'é'", "\\xC3\\xA9")]
        [DataRow("'\\u20AC'", "\\xE2\\x82\\xAC")]
        [DataRow("'\\U0001F600'", "\\xF0\\x9F\\x98\\x80")]
        public void CharacterExpressions_EmitExactUtf8AndExecuteAgainstRuntime(string literal, string bytes)
        {
            var source = $"properties\n    value = {literal}\n\nfunctions\n    Echo(c char = {literal}) char\n        return c\n    Literal() char\n        return {literal}\n    Apply()\n        value = Echo(Literal())\n    Default()\n        value = Echo()\n";
            var character = $"PumaType::Character(reinterpret_cast<const uint8_t*>(\"{bytes}\"))";
            var expected = $"#include <PumaType/Character.hpp>\n\n// properties\nauto value = {character};\n\n// functions\nPumaType::Character Echo(PumaType::Character c)\n{{\n    return c;\n}}\n\nPumaType::Character Literal(void)\n{{\n    return {character};\n}}\n\nvoid Apply(void)\n{{\n    value = Echo(Literal());\n}}\n\nvoid Default(void)\n{{\n    value = Echo({character});\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var generated = new Codegen().GenerateResult(ast);
            Assert.AreEqual(expected, generated.SourceCode.Replace("\r\n", "\n").Trim());
            CollectionAssert.AreEqual(new[] { "PumaType" }, generated.RequiredRuntimeLibraries.ToArray());

            var nativeAssertions = $"\n#include <cstring>\nint main()\n{{\n    const char expected[] = \"{bytes}\";\n    if (value.GetCharSize() != sizeof(expected) - 1 || std::memcmp(value.ToUTF8(), expected, sizeof(expected) - 1) != 0) return 1;\n    Apply();\n    if (std::memcmp(value.ToUTF8(), expected, sizeof(expected) - 1) != 0) return 2;\n    Default();\n    if (std::memcmp(value.ToUTF8(), expected, sizeof(expected) - 1) != 0) return 3;\n    return 0;\n}}\n";
            CompileAndRun(generated.SourceCode + nativeAssertions);
        }

        [TestMethod]
        public void CharacterAstReplacement_UpdatesUtf8OutputAndSupportsCodegenReuse()
        {
            const string source = "start\n    value = 'A'\n";
            const string replacementSource = "start\n    value = '\\u20AC'\n";
            const string expected = "#include <PumaType/Character.hpp>\n\n// start\nint main()\n{\n    auto value = PumaType::Character(reinterpret_cast<const uint8_t*>(\"\\xE2\\x82\\xAC\"));\n\n    return 0;\n}";
            var parser = new Parser();
            var ast = parser.Parse(new Lexer().Tokenize(source));
            var replacement = parser.Parse(new Lexer().Tokenize(replacementSource));
            ast.OfType<AssignmentStatementAstNode>().Single().AssignmentRightExpression =
                replacement.OfType<AssignmentStatementAstNode>().Single().AssignmentRightExpression;
            var codegen = new Codegen();
            Assert.AreEqual(expected, codegen.Generate(ast).Replace("\r\n", "\n").Trim());
            CompileAndRun(codegen.Generate(ast));
            Assert.AreEqual(expected, codegen.Generate(replacement).Replace("\r\n", "\n").Trim());
        }

        private static void CompileAndRun(string generated)
        {
            var runtimeRoot = Environment.GetEnvironmentVariable("PUMA_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Puma");
            var compilerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LLVM", "bin", "clang++.exe");
            var includePath = Path.Combine(runtimeRoot, "include");
            var typeLibrary = Path.Combine(runtimeRoot, "lib", "x64", "Release", "PumaType.lib");
            var directory = Path.Combine(Path.GetTempPath(), $"PumaCharacterTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                Assert.IsTrue(File.Exists(compilerPath), $"clang++ was not found at '{compilerPath}'.");
                Assert.IsTrue(File.Exists(Path.Combine(includePath, "PumaType", "Character.hpp")), "Character runtime header was not found.");
                Assert.IsTrue(File.Exists(typeLibrary), $"PumaType library was not found at '{typeLibrary}'.");
                var sourcePath = Path.Combine(directory, "generated.cpp");
                var executablePath = Path.Combine(directory, "generated.exe");
                File.WriteAllText(sourcePath, generated);
                var startInfo = new ProcessStartInfo
                {
                    FileName = compilerPath,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    ArgumentList = { "-std=c++20", "--target=x86_64-pc-windows-msvc", "-fms-runtime-lib=dll", "-I", includePath, sourcePath, typeLibrary, "-o", executablePath }
                };
                var libraryPaths = (Environment.GetEnvironmentVariable("LIB") ?? string.Empty)
                    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => path.Replace("\\x86", "\\x64", StringComparison.OrdinalIgnoreCase))
                    .Where(Directory.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (libraryPaths.Length > 0)
                {
                    startInfo.Environment["LIB"] = string.Join(Path.PathSeparator, libraryPaths);
                }
                using var process = Process.Start(startInfo);
                Assert.IsNotNull(process);
                var standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.AreEqual(0, process.ExitCode, standardError);
                using var executable = Process.Start(new ProcessStartInfo { FileName = executablePath, UseShellExecute = false });
                Assert.IsNotNull(executable);
                executable.WaitForExit();
                Assert.AreEqual(0, executable.ExitCode, "Generated character program failed its UTF-8 assertions.");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

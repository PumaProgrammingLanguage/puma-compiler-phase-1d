using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class FileRuntimeExpressionTest
    {
        private static readonly ExternalSymbol[] Symbols =
        {
            new("PumaFile.Text", ExternalSymbolKind.ValueType, CppName: "PumaFile::Text"),
            new("PumaFile.Text.OpenMode.READ", ExternalSymbolKind.ValueType, CppName: "PumaFile::Text::OpenMode::READ"),
            new("PumaFile.Text.OpenMode.WRITE_NEW", ExternalSymbolKind.ValueType, CppName: "PumaFile::Text::OpenMode::WRITE_NEW"),
            new("PumaFile.Directory.GetCurrentDirectory", ExternalSymbolKind.Function, CppName: "PumaFile::Directory::GetCurrentDirectory"),
            new("PumaFile.Directory.SetCurrentDirectory", ExternalSymbolKind.Function, CppName: "PumaFile::Directory::SetCurrentDirectory")
        };

        [DataTestMethod]
        [DataRow("\"hello file\"", "PumaType::String(\"hello file\", sizeof(\"hello file\") - 1)", "hello file", false)]
        [DataRow("\"café\"", "PumaType::String(\"café\", sizeof(\"café\") - 1)", "café", false)]
        [DataRow("'A'", "PumaType::Character(reinterpret_cast<const uint8_t*>(\"\\x41\"))", "A", true)]
        public void TextWriteAndRead_EmitExactOutputAndExecuteRoundTrip(string literal, string initializer, string text, bool character)
        {
            var source = $"use\n    PumaFile.Text\nfunctions\n    Save(path str) bool\n        file = PumaFile.Text(path, PumaFile.Text.OpenMode.WRITE_NEW)\n        text = {literal}\n        return file.WriteLn(text)\n    Load(path str) str\n        file = PumaFile.Text(path, PumaFile.Text.OpenMode.READ)\n        return file.ReadLn()\n";
            var characterHeader = character ? "#include <PumaType/Character.hpp>\n" : string.Empty;
            var expected = $"{characterHeader}#include <PumaType/String.hpp>\n#include <PumaFile/Text.hpp>\n\n// functions\nbool Save(PumaType::String path)\n{{\n    auto file = PumaFile::Text(path, PumaFile::Text::OpenMode::WRITE_NEW);\n    auto text = {initializer};\n    return file.WriteLn(text);\n}}\n\nPumaType::String Load(PumaType::String path)\n{{\n    auto file = PumaFile::Text(path, PumaFile::Text::OpenMode::READ);\n    return file.ReadLn();\n}}";
            var result = Generate(source);
            Assert.AreEqual(expected, Normalize(result.SourceCode));
            CollectionAssert.AreEqual(new[] { "PumaFile", "PumaType" }, result.RequiredRuntimeLibraries.ToArray());
            var nativeAssertions = $"\n#include <cstring>\nint main()\n{{\n    PumaType::String path(\"roundtrip.txt\", sizeof(\"roundtrip.txt\") - 1);\n    if (!Save(path)) return 1;\n    auto loaded = Load(path);\n    const char expected[] = \"{text}\";\n    if (loaded.Size() != sizeof(expected) - 1 || std::memcmp(loaded.ToUTF8(), expected, sizeof(expected) - 1) != 0) return 2;\n    return 0;\n}}\n";
            CompileAndRun(result, nativeAssertions, directory =>
            {
                var written = File.ReadAllText(Path.Combine(directory, "roundtrip.txt"));
                Assert.AreEqual(text + "\n", written.Replace("\r\n", "\n"));
            });
        }

        [TestMethod]
        public void MissingTextFile_EmitsExactOutputAndReportsNotOpen()
        {
            const string source = "use\n    PumaFile.Text\nfunctions\n    OpenMissing(path str) bool\n        file = PumaFile.Text(path, PumaFile.Text.OpenMode.READ)\n        return file.IsOpen()\n";
            const string expected = "#include <PumaType/String.hpp>\n#include <PumaFile/Text.hpp>\n\n// functions\nbool OpenMissing(PumaType::String path)\n{\n    auto file = PumaFile::Text(path, PumaFile::Text::OpenMode::READ);\n    return file.IsOpen();\n}";
            var result = Generate(source);
            Assert.AreEqual(expected, Normalize(result.SourceCode));
            CollectionAssert.AreEqual(new[] { "PumaFile", "PumaType" }, result.RequiredRuntimeLibraries.ToArray());
            const string nativeAssertions = "\nint main()\n{\n    PumaType::String path(\"missing.txt\", sizeof(\"missing.txt\") - 1);\n    return OpenMissing(path) ? 1 : 0;\n}\n";
            CompileAndRun(result, nativeAssertions, directory => Assert.IsFalse(File.Exists(Path.Combine(directory, "missing.txt"))));
        }

        [TestMethod]
        public void DirectoryCalls_EmitExactOutputAndExecuteAgainstRuntime()
        {
            const string source = "use\n    PumaFile.Directory\nfunctions\n    Current() str\n        return PumaFile.Directory.GetCurrentDirectory()\n    Change(path str) bool\n        return PumaFile.Directory.SetCurrentDirectory(path)\n";
            const string expected = "#include <PumaType/String.hpp>\n#include <PumaFile/Directory.hpp>\n\n// functions\nPumaType::String Current(void)\n{\n    return PumaFile::Directory::GetCurrentDirectory();\n}\n\nbool Change(PumaType::String path)\n{\n    return PumaFile::Directory::SetCurrentDirectory(path);\n}";
            var result = Generate(source);
            Assert.AreEqual(expected, Normalize(result.SourceCode));
            CollectionAssert.AreEqual(new[] { "PumaFile", "PumaType" }, result.RequiredRuntimeLibraries.ToArray());
            const string nativeAssertions = "\n#include <cstring>\nint main()\n{\n    auto original = Current();\n    PumaType::String child(\"child\", sizeof(\"child\") - 1);\n    if (!Change(child)) return 1;\n    auto changed = Current();\n    if (changed.Size() != original.Size() + 6) return 2;\n    if (std::memcmp(changed.ToUTF8(), original.ToUTF8(), original.Size()) != 0) return 3;\n    if (std::memcmp(changed.ToUTF8() + original.Size(), \"/child\", 6) != 0) return 4;\n    if (!Change(original)) return 5;\n    PumaType::String missing(\"missing\", sizeof(\"missing\") - 1);\n    if (Change(missing)) return 6;\n    auto restored = Current();\n    if (restored.Size() != original.Size() || std::memcmp(restored.ToUTF8(), original.ToUTF8(), original.Size()) != 0) return 7;\n    return 0;\n}\n";
            CompileAndRun(result, nativeAssertions);
        }

        [TestMethod]
        public void TextOpenAndClose_EmitExactOutputAndExecuteAgainstRuntime()
        {
            const string source = "use\n    PumaFile.Text\nfunctions\n    Save(path str)\n        file = PumaFile.Text()\n        file.Open(path, PumaFile.Text.OpenMode.WRITE_NEW)\n        text = \"hello file\"\n        file.WriteLn(text)\n        file.Close()\n";
            const string expected = "#include <PumaType/String.hpp>\n#include <PumaFile/Text.hpp>\n\n// functions\nvoid Save(PumaType::String path)\n{\n    auto file = PumaFile::Text();\n    file.Open(path, PumaFile::Text::OpenMode::WRITE_NEW);\n    auto text = PumaType::String(\"hello file\", sizeof(\"hello file\") - 1);\n    file.WriteLn(text);\n    file.Close();\n}";
            var result = Generate(source);
            Assert.AreEqual(expected, Normalize(result.SourceCode));
            CollectionAssert.AreEqual(new[] { "PumaFile", "PumaType" }, result.RequiredRuntimeLibraries.ToArray());
            const string nativeAssertions = "\nint main()\n{\n    PumaType::String path(\"opened.txt\", sizeof(\"opened.txt\") - 1);\n    Save(path);\n    PumaFile::Text file;\n    if (!file.Open(path, PumaFile::Text::OpenMode::READ)) return 1;\n    file.Close();\n    return file.IsOpen() ? 2 : 0;\n}\n";
            CompileAndRun(result, nativeAssertions, directory =>
                Assert.AreEqual("hello file\n", File.ReadAllText(Path.Combine(directory, "opened.txt")).Replace("\r\n", "\n")));
        }

        private static CodeGenerationResult Generate(string source)
        {
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            return new Codegen().GenerateResult(ast, Symbols);
        }

        private static string Normalize(string source) => source.Replace("\r\n", "\n").Trim();

        private static void CompileAndRun(CodeGenerationResult result, string assertions, Action<string>? verify = null)
        {
            var runtime = Program.FindInstalledPumaRuntime();
            Assert.IsNotNull(runtime, "Set PUMA_STDLIB_ROOT to the installed standard-library root.");
            var compilerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LLVM", "bin", "clang++.exe");
            var includePath = runtime.IncludeDirectory;
            var directory = Path.Combine(Path.GetTempPath(), $"PumaFileTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(Path.Combine(directory, "child"));
            try
            {
                Assert.IsTrue(File.Exists(compilerPath), $"clang++ was not found at '{compilerPath}'.");
                Assert.IsTrue(File.Exists(Path.Combine(includePath, "PumaFile", "Text.hpp")), "File runtime header was not found.");
                var sourcePath = Path.Combine(directory, "generated.cpp");
                var executablePath = Path.Combine(directory, "generated.exe");
                File.WriteAllText(sourcePath, result.SourceCode + assertions);
                var startInfo = new ProcessStartInfo
                {
                    FileName = compilerPath,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    ArgumentList = { "-std=c++20", "--target=x86_64-pc-windows-msvc", "-fms-runtime-lib=dll", "-I", includePath, sourcePath }
                };
                foreach (var library in result.RequiredRuntimeLibraries)
                {
                    var path = Path.Combine(runtime.LibraryDirectory, library + ".lib");
                    Assert.IsTrue(File.Exists(path), $"Runtime library was not found at '{path}'.");
                    startInfo.ArgumentList.Add(path);
                }
                startInfo.ArgumentList.Add("-o");
                startInfo.ArgumentList.Add(executablePath);
                var libraryPaths = (Environment.GetEnvironmentVariable("LIB") ?? string.Empty)
                    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => path.Replace("\\x86", "\\x64", StringComparison.OrdinalIgnoreCase))
                    .Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (libraryPaths.Length > 0) startInfo.Environment["LIB"] = string.Join(Path.PathSeparator, libraryPaths);
                using var process = Process.Start(startInfo);
                Assert.IsNotNull(process);
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                output.GetAwaiter().GetResult();
                Assert.AreEqual(0, process.ExitCode, error.GetAwaiter().GetResult());
                using var executable = Process.Start(new ProcessStartInfo { FileName = executablePath, WorkingDirectory = directory, UseShellExecute = false });
                Assert.IsNotNull(executable);
                executable.WaitForExit();
                Assert.AreEqual(0, executable.ExitCode, "Generated file program failed its runtime assertions.");
                verify?.Invoke(directory);
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }
}

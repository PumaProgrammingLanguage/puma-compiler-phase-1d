// LLVM Compiler for the Puma programming language
//   as defined in the document "The Puma Programming Language Specification"
//   available at https://github.com/ThePumaProgrammingLanguage
//
// Copyright © 2024-2026 by Darryl Anthony Burchfield
//
//   Licensed under the Apache License, Version 2.0 (the "License") WITH LLVM-exception;
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class StringInterpolationExpressionTest
    {
        private const string ExpectedValueConversion = "[](const auto& value) { if constexpr (requires { value.ToUTF8(); value.Size(); }) { return PumaType::String(value.ToUTF8(), (size_t)value.Size()); } else if constexpr (requires { value.GetCharSize(); }) { return PumaType::String(value); } else { const auto text = std::to_string(value); return PumaType::String(text.data(), (size_t)text.size()); } }";

        [DataTestMethod]
        [DataRow("\"c:\\directory\\file.ext {missing}\" vstr", "\"c:\\\\directory\\\\file.ext {missing}\"")]
        [DataRow("\"C:\\\" vstr", "\"C:\\\\\"")]
        [DataRow("\"\\n{missing}\\u20AC\" vstr", "\"\\\\n{missing}\\\\u20AC\"")]
        [DataRow("\"line\\n\\t\\u20AC\"", "\"line\\n\\t\\u20AC\"")]
        [DataRow("\"{{literal}}\"", "\"{literal}\"")]
        [DataRow("\"plain\" str", "\"plain\"")]
        public void StringModes_EmitExactCppOutput(string literal, string cppLiteral)
        {
            var source = $"start\n    s = {literal}\n";
            var expected = $"#include <PumaType/String.hpp>\n\n// start\nint main()\n{{\n    auto s = PumaType::String({cppLiteral}, (size_t)(sizeof({cppLiteral}) - 1));\n    return 0;\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void MultiplePlaceholders_EmitExactCppOutput()
        {
            const string source = "start\n    x = 5\n    s = \"{x} + {x} = ten\"\n";
            const string expected = "#include <cstdint>\n#include <string>\n#include <PumaType/String.hpp>\n\n// start\nint main()\n{\n    auto x = (int64_t)5;\n    auto s = PumaType::String(\"\", (size_t)(sizeof(\"\") - 1));\n    s += PumaType::String(std::to_string(x).c_str(), (size_t)std::to_string(x).size());\n    s += PumaType::String(\" + \", (size_t)(sizeof(\" + \") - 1));\n    s += PumaType::String(std::to_string(x).c_str(), (size_t)std::to_string(x).size());\n    s += PumaType::String(\" = ten\", (size_t)(sizeof(\" = ten\") - 1));\n\n    return 0;\n}";
            Assert.AreEqual(expected, new Codegen().Generate(new Parser().Parse(new Lexer().Tokenize(source))).Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow("\"{5}\"", "", "PumaType::String(std::to_string(5).c_str(), (size_t)std::to_string(5).size())")]
        [DataRow("\"value {5}\" str", "value ", "PumaType::String(std::to_string(5).c_str(), (size_t)std::to_string(5).size())")]
        [DataRow("\"text {\"literal\"}\"", "text ", "PumaType::String(\"literal\", (size_t)(sizeof(\"literal\") - 1))")]
        [DataRow("\"text {\"c:\\directory\" vstr}\"", "text ", "PumaType::String(\"c:\\\\directory\", (size_t)(sizeof(\"c:\\\\directory\") - 1))")]
        public void InterpolatedLiteralParts_EmitExactCppOutput(string literal, string prefix, string converted)
        {
            var source = $"start\n    s = {literal}\n";
            var expected = $"#include <string>\n#include <PumaType/String.hpp>\n\n// start\nint main()\n{{\n    auto s = PumaType::String(\"{prefix}\", (size_t)(sizeof(\"{prefix}\") - 1));\n    s += {converted};\n\n    return 0;\n}}";
            Assert.AreEqual(expected, new Codegen().Generate(new Parser().Parse(new Lexer().Tokenize(source))).Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow("\"{}\"")]
        [DataRow("\"{x\"")]
        [DataRow("\"x}\"")]
        [DataRow("\"{x +}\"")]
        [DataRow("\"{x y}\"")]
        [DataRow("\"{x:f0.2}\"")]
        [DataRow("\"{(x}\"")]
        [DataRow("\"{F(}\"")]
        [DataRow("\"{x[0}\"")]
        [DataRow("\"{x + )}\"")]
        [DataRow("\"{=}\"")]
        public void MalformedInterpolation_ReportsParserError(string literal)
        {
            var error = Assert.ThrowsException<InvalidOperationException>(() => new Parser().Parse(new Lexer().Tokenize($"start\n    s = {literal}\n")));
            StringAssert.Contains(error.Message, "interpolation");
        }

        [TestMethod]
        public void EmbeddedExpression_IsStructuredAndRetainsLocation()
        {
            const string source = "start\n    x = 5\n    s = \"value {x + 1}\"\n";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var expression = ast.OfType<AssignmentStatementAstNode>().Last().AssignmentRightExpression!;
            Assert.AreEqual("InterpolatedString", expression.Kind.ToString());
            var embedded = expression.Arguments[1].Left!;
            Assert.AreEqual(ExpressionKind.Binary, embedded.Kind);
            Assert.AreEqual("+", embedded.Value);
            Assert.AreEqual(new SourceSpan(3, 17, 3, 18), embedded.Left!.SourceSpan);
        }

        [TestMethod]
        public void InterpolatedReturn_EmitsExactCppAndExecutesAgainstRuntime()
        {
            const string source = "functions\n    Render(n int) str\n        return \"A long interpolation prefix: {n} :suffix\"\n";
            const string expected = "#include <cstdint>\n#include <string>\n#include <PumaType/String.hpp>\n\n// functions\nPumaType::String Render(int64_t n)\n{\n    return PumaType::String(\"A long interpolation prefix: \", (size_t)(sizeof(\"A long interpolation prefix: \") - 1)) + PumaType::String(std::to_string(n).c_str(), (size_t)std::to_string(n).size()) + PumaType::String(\" :suffix\", (size_t)(sizeof(\" :suffix\") - 1));\n}";
            var generated = new Codegen().GenerateResult(new Parser().Parse(new Lexer().Tokenize(source)));
            Assert.AreEqual(expected, generated.SourceCode.Replace("\r\n", "\n").Trim());
            CollectionAssert.AreEqual(new[] { "PumaType" }, generated.RequiredRuntimeLibraries.ToArray());
            CompileAndRun(generated.SourceCode + "\n#include <cstring>\nint main() { auto result = Render(15); const char expected[] = \"A long interpolation prefix: 15 :suffix\"; return result.Size() == sizeof(expected) - 1 && std::memcmp(result.ToUTF8(), expected, sizeof(expected) - 1) == 0 ? 0 : 1; }\n");
        }

        [DataTestMethod]
        [DataRow("str", "value", "PumaType::String", "", "PumaType::String(\"hello\", (size_t)5)", "hello")]
        [DataRow("str", "value", "PumaType::String", "", "PumaType::String(\"a long UTF-8 string\", (size_t)(sizeof(\"a long UTF-8 string\") - 1))", "a long UTF-8 string")]
        [DataRow("char", "value", "PumaType::Character", "#include <PumaType/Character.hpp>\n", "PumaType::Character(reinterpret_cast<const uint8_t*>(\"\\xE2\\x82\\xAC\"))", "\\xE2\\x82\\xAC")]
        [DataRow("int", "value + 1", "int64_t", "#include <cstdint>\n", "15", "16")]
        [DataRow("flt", "value + 1", "double", "", "15.5", "16.500000")]
        public void InterpolatedValues_EmitExactCppAndExecuteAgainstRuntime(string type, string expression, string cppType, string header, string argument, string expectedText)
        {
            var source = $"functions\n    Render(value {type}) str\n        return \"prefix {{{expression}}} suffix\"\n";
            var conversion = ExpectedValueConversion + (expression == "value" ? "(value)" : "((value + 1))");
            var headers = type == "int" ? header + "#include <string>\n" : "#include <string>\n" + header;
            var expected = $"{headers}#include <PumaType/String.hpp>\n\n// functions\nPumaType::String Render({cppType} value)\n{{\n    return PumaType::String(\"prefix \", (size_t)(sizeof(\"prefix \") - 1)) + {conversion} + PumaType::String(\" suffix\", (size_t)(sizeof(\" suffix\") - 1));\n}}";
            var generated = new Codegen().Generate(new Parser().Parse(new Lexer().Tokenize(source)));
            Assert.AreEqual(expected, generated.Replace("\r\n", "\n").Trim());
            CompileAndRun(generated + $"\n#include <cstring>\nint main() {{ auto result = Render({argument}); const char expected[] = \"prefix {expectedText} suffix\"; return result.Size() == sizeof(expected) - 1 && std::memcmp(result.ToUTF8(), expected, sizeof(expected) - 1) == 0 ? 0 : 1; }}\n");
        }

        [TestMethod]
        public void InterpolatedCall_EvaluatesPlaceholderOnce()
        {
            const string source = "properties\n    interpolationCalls = 0\n\nfunctions\n    Next() int\n        interpolationCalls += 1\n        return interpolationCalls\n    Render() str\n        return \"value {Next()}\"\n";
            var expected = "#include <cstdint>\n#include <string>\n#include <PumaType/String.hpp>\n\n// properties\nauto interpolationCalls = (int64_t)0;\n\n// functions\nint64_t Next(void)\n{\n    interpolationCalls += 1;\n    return interpolationCalls;\n}\n\nPumaType::String Render(void)\n{\n    return PumaType::String(\"value \", (size_t)(sizeof(\"value \") - 1)) + " + ExpectedValueConversion + "(Next());\n}";
            var generated = new Codegen().Generate(new Parser().Parse(new Lexer().Tokenize(source)));
            Assert.AreEqual(expected, generated.Replace("\r\n", "\n").Trim());
            CompileAndRun(generated + "\n#include <cstring>\nint main() { auto result = Render(); return interpolationCalls == 1 && result.Size() == 7 && std::memcmp(result.ToUTF8(), \"value 1\", 7) == 0 ? 0 : 1; }\n");
        }

        [TestMethod]
        public void InterpolatedReassignment_PreservesLongStringOwnership()
        {
            const string source = "functions\n    Render(n int) str\n        s = \"initial long string {n}\"\n        s = \"replacement long string {n}\"\n        return s\n";
            const string expected = "#include <cstdint>\n#include <string>\n#include <PumaType/String.hpp>\n\n// functions\nPumaType::String Render(int64_t n)\n{\n    auto s = PumaType::String(\"initial long string \", (size_t)(sizeof(\"initial long string \") - 1));\n    s += PumaType::String(std::to_string(n).c_str(), (size_t)std::to_string(n).size());\n    [](auto& target, auto value) { target = value; value.SetBorrower(); target.SetOwner(); }(s, (PumaType::String(\"replacement long string \", (size_t)(sizeof(\"replacement long string \") - 1)) + PumaType::String(std::to_string(n).c_str(), (size_t)std::to_string(n).size())));\n    return s;\n}";
            var generated = new Codegen().Generate(new Parser().Parse(new Lexer().Tokenize(source)));
            Assert.AreEqual(expected, generated.Replace("\r\n", "\n").Trim());
            CompileAndRun(generated + "\n#include <cstring>\nint main() { auto result = Render(15); const char expected[] = \"replacement long string 15\"; return result.IsOwner() && result.Size() == sizeof(expected) - 1 && std::memcmp(result.ToUTF8(), expected, sizeof(expected) - 1) == 0 ? 0 : 1; }\n");
        }

        [TestMethod]
        public void GlobalInterpolation_EmitsExactCppAndExecutesAgainstRuntime()
        {
            const string source = "properties\n    message = \"value {5}\"\n";
            const string expected = "#include <string>\n#include <PumaType/String.hpp>\n\n// properties\nauto message = (PumaType::String(\"value \", (size_t)(sizeof(\"value \") - 1)) + PumaType::String(std::to_string(5).c_str(), (size_t)std::to_string(5).size()));";
            var generated = new Codegen().Generate(new Parser().Parse(new Lexer().Tokenize(source)));
            Assert.AreEqual(expected, generated.Replace("\r\n", "\n").Trim());
            CompileAndRun(generated + "\n#include <cstring>\nint main() { return message.Size() == 7 && std::memcmp(message.ToUTF8(), \"value 5\", 7) == 0 ? 0 : 1; }\n");
        }

        private static void CompileAndRun(string generated)
        {
            var runtime = Program.FindInstalledPumaRuntime();
            Assert.IsNotNull(runtime, "Set PUMA_STDLIB_ROOT to the installed standard-library root.");
            var compilerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LLVM", "bin", "clang++.exe");
            var directory = Path.Combine(Path.GetTempPath(), $"PumaInterpolationTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                Assert.IsTrue(File.Exists(compilerPath), $"clang++ was not found at '{compilerPath}'.");
                var sourcePath = Path.Combine(directory, "generated.cpp");
                var executablePath = Path.Combine(directory, "generated.exe");
                File.WriteAllText(sourcePath, generated);
                var startInfo = new ProcessStartInfo
                {
                    FileName = compilerPath,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    ArgumentList = { "-std=c++20", "--target=x86_64-pc-windows-msvc", "-fms-runtime-lib=dll", "-I", runtime.IncludeDirectory, sourcePath, Path.Combine(runtime.LibraryDirectory, "PumaType.lib"), "-o", executablePath }
                };
                var libraryPaths = (Environment.GetEnvironmentVariable("LIB") ?? string.Empty)
                    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => path.Replace("\\x86", "\\x64", StringComparison.OrdinalIgnoreCase))
                    .Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (libraryPaths.Length > 0) startInfo.Environment["LIB"] = string.Join(Path.PathSeparator, libraryPaths);
                using var process = Process.Start(startInfo);
                Assert.IsNotNull(process);
                var standardError = process.StandardError.ReadToEnd();
                Assert.IsTrue(process.WaitForExit(30000), "C++ compilation timed out.");
                Assert.AreEqual(0, process.ExitCode, standardError);
                using var executable = Process.Start(new ProcessStartInfo { FileName = executablePath, UseShellExecute = false });
                Assert.IsNotNull(executable);
                Assert.IsTrue(executable.WaitForExit(10000), "Generated interpolation program timed out.");
                Assert.AreEqual(0, executable.ExitCode, "Generated interpolation program failed its UTF-8/length assertions.");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

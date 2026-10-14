using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class NumericReturnTest
    {
        [DataTestMethod]
        [DataRow("int", "int64_t", "#include <cstdint>\n\n")]
        [DataRow("int64", "int64_t", "#include <cstdint>\n\n")]
        [DataRow("int32", "int32_t", "#include <cstdint>\n\n")]
        [DataRow("int16", "int16_t", "#include <cstdint>\n\n")]
        [DataRow("int8", "int8_t", "#include <cstdint>\n\n")]
        [DataRow("uint", "uint64_t", "#include <cstdint>\n\n")]
        [DataRow("uint64", "uint64_t", "#include <cstdint>\n\n")]
        [DataRow("uint32", "uint32_t", "#include <cstdint>\n\n")]
        [DataRow("uint16", "uint16_t", "#include <cstdint>\n\n")]
        [DataRow("uint8", "uint8_t", "#include <cstdint>\n\n")]
        [DataRow("flt", "double", "")]
        [DataRow("flt64", "double", "")]
        [DataRow("flt32", "float", "")]
        public void NumericReturn_EmitsMappedSignatureAndCompiles(string returnType, string expectedType, string expectedIncludes)
        {
            var source = $"functions\n    Value() {returnType}\n        return 1\n";
            var expected = $"{expectedIncludes}// functions\n{expectedType} Value(void)\n{{\n    return 1;\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var generated = new Codegen().Generate(ast);
            Assert.AreEqual(returnType, ast.OfType<FunctionDeclarationAstNode>().Single().FunctionDeclarationReturnType);
            Assert.AreEqual(expected, generated.Replace("\r\n", "\n").Trim());

            var compilerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LLVM", "bin", "clang++.exe");
            var directory = Path.Combine(Path.GetTempPath(), $"PumaTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                Assert.IsTrue(File.Exists(compilerPath), $"clang++ was not found at '{compilerPath}'.");
                var sourcePath = Path.Combine(directory, "generated.cpp");
                var objectPath = Path.Combine(directory, "generated.obj");
                File.WriteAllText(sourcePath, generated);
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = compilerPath,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    ArgumentList = { "-std=c++20", "-c", sourcePath, "-o", objectPath }
                });
                Assert.IsNotNull(process);
                var standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.AreEqual(0, process.ExitCode, standardError);
                Assert.IsTrue(File.Exists(objectPath));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void ReturnDeclarationAstChange_UpdatesSignatureAndHeaders()
        {
            const string source = "functions\n    Value() flt32\n        return 1\n";
            const string expectedInteger = "#include <cstdint>\n\n// functions\nint32_t Value(void)\n{\n    return 1;\n}";
            const string expectedFloat = "// functions\nfloat Value(void)\n{\n    return 1;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var function = ast.OfType<FunctionDeclarationAstNode>().Single();
            var codegen = new Codegen();
            Assert.AreEqual(expectedFloat, codegen.Generate(ast).Replace("\r\n", "\n").Trim());
            function.FunctionDeclarationReturnType = "int32";
            Assert.AreEqual(expectedInteger, codegen.Generate(ast).Replace("\r\n", "\n").Trim());
            function.FunctionDeclarationReturnType = "flt32";
            Assert.AreEqual(expectedFloat, codegen.Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void CodegenReuse_DoesNotRetainReturnSignatureHeaders()
        {
            const string integerSource = "functions\n    Value() uint16\n        return 1\n";
            const string floatSource = "functions\n    Value() flt64\n        return 1\n";
            const string expectedInteger = "#include <cstdint>\n\n// functions\nuint16_t Value(void)\n{\n    return 1;\n}";
            const string expectedFloat = "// functions\ndouble Value(void)\n{\n    return 1;\n}";
            var parser = new Parser();
            var codegen = new Codegen();
            Assert.AreEqual(expectedInteger, codegen.Generate(parser.Parse(new Lexer().Tokenize(integerSource))).Replace("\r\n", "\n").Trim());
            Assert.AreEqual(expectedFloat, codegen.Generate(parser.Parse(new Lexer().Tokenize(floatSource))).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void NonNumericReturnSignatures_PreserveExistingOutput()
        {
            const string source = "functions\n    Empty()\n    Read() Shape\n        return Create()\n";
            const string expected = "// functions\nvoid Empty(void)\n{\n}\n\nShape Read(void)\n{\n    return Create();\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }
    }
}

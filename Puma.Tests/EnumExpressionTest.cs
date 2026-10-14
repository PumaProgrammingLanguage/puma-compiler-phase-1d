using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class EnumExpressionTest
    {
        [DataTestMethod]
        [DataRow("enums\n    Values\n        A\n        B\n        C\n", "// enums\nenum Values\n{\n    A,\n    B,\n    C,\n};")]
        [DataRow("enums\n    Values\n        A\n        B = 3\n        C\n", "// enums\nenum Values\n{\n    A,\n    B=3,\n    C,\n};")]
        [DataRow("enums\n    Values\n        A = 1 uint8 + 2 int16\n", "#include <cstdint>\n\n// enums\nenum Values\n{\n    A=((uint8_t)1 + (int16_t)2),\n};")]
        [DataRow("enums\n    Values\n        A = -42 int16\n", "#include <cstdint>\n\n// enums\nenum Values\n{\n    A=-(int16_t)42,\n};")]
        [DataRow("enums\n    Values\n        A = 1 int16 if true else 2 int16\n", "#include <cstdint>\n\n// enums\nenum Values\n{\n    A=(true ? (int16_t)1 : (int16_t)2),\n};")]
        [DataRow("enums\n    First\n        A = 1 int16\n    Second\n        B\n        C = A + 2\n", "#include <cstdint>\n\n// enums\nenum First\n{\n    A=(int16_t)1,\n};\n\n// enums\nenum Second\n{\n    B,\n    C=(A + 2),\n};")]
        public void GeneratedEnums_EmitStandardDeclarationsAndCompile(string source, string expected)
        {
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var generated = new Codegen().Generate(ast);
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
                    ArgumentList = { "-std=c++20", "-pedantic-errors", "-c", sourcePath, "-o", objectPath }
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

        [DataTestMethod]
        [DataRow("1 + 2", "(1 + 2)", "")]
        [DataRow("1 uint8 + 2 int16", "((uint8_t)1 + (int16_t)2)", "#include <cstdint>\n\n")]
        [DataRow("-42 int16", "-(int16_t)42", "#include <cstdint>\n\n")]
        [DataRow("(1) int16", "(int16_t) 1", "#include <cstdint>\n\n")]
        [DataRow("Pick(1, Pick(2, 3))", "Pick(1, Pick(2, 3))", "")]
        [DataRow("1 int16 if true else 2 int16", "(true ? (int16_t)1 : (int16_t)2)", "#include <cstdint>\n\n")]
        [DataRow("Other", "Other", "")]
        public void EnumInitializer_EmitsStructuredExpressionAndRetainsSpan(string expression, string initializer, string headers)
        {
            var source = $"enums\n    Values\n        A = {expression}\n";
            var expected = $"{headers}// enums\nenum Values\n{{\n    A={initializer},\n}};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var member = ast.OfType<EnumDeclarationAstNode>().Single().MemberDeclarations.Single();
            Assert.AreEqual("A", member.Name);
            Assert.AreEqual(new SourceSpan(3, 13, 3, 13 + expression.Length), member.ValueExpression?.SourceSpan);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void MixedMembers_PreserveUnassignedAndAssignedDeclarations()
        {
            const string source = "enums\n    Values\n        A\n        B = 3\n        C\n";
            const string expected = "// enums\nenum Values\n{\n    A,\n    B=3,\n    C,\n};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var members = ast.OfType<EnumDeclarationAstNode>().Single().MemberDeclarations;
            Assert.IsNull(members[0].ValueExpression);
            Assert.AreEqual("3", members[1].ValueExpression?.Value);
            Assert.IsNull(members[2].ValueExpression);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void EnumAstReplacement_UpdatesNameValueAndDependencies()
        {
            const string source = "enums\n    Values\n        A = 1 int16\n";
            const string replacementSource = "enums\n    Values\n        A = Pick(1, 2)\n";
            const string expected = "// enums\nenum Values\n{\n    Renamed=Pick(1, 2),\n};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var member = ast.OfType<EnumDeclarationAstNode>().Single().MemberDeclarations.Single();
            member.Name = "Renamed";
            member.ValueExpression = new Parser().Parse(new Lexer().Tokenize(replacementSource))
                .OfType<EnumDeclarationAstNode>().Single().MemberDeclarations.Single().ValueExpression;
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void MultipleEnums_FinalizeIndependentMemberTrees()
        {
            const string source = "enums\n    First\n        A = 1 int16\n    Second\n        B\n        C = A + 2\n";
            const string expected = "#include <cstdint>\n\n// enums\nenum First\n{\n    A=(int16_t)1,\n};\n\n// enums\nenum Second\n{\n    B,\n    C=(A + 2),\n};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            CollectionAssert.AreEqual(new[] { 1, 2 }, ast.OfType<EnumDeclarationAstNode>().Select(node => node.MemberDeclarations.Count).ToArray());
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow("A =", "Line 3, column 11: Enum member 'A' is missing the initializer expression.")]
        [DataRow("= 1", "Line 3, column 9: Enum members require a name.")]
        [DataRow("A = 1 2", "Line 3, column 13: Unable to parse full expression.")]
        public void InvalidEnumInitializer_ReportsExactDiagnostic(string declaration, string expected)
        {
            var source = $"enums\n    Values\n        {declaration}\n";
            var error = Assert.ThrowsException<InvalidOperationException>(() => new Parser().Parse(new Lexer().Tokenize(source)));
            Assert.AreEqual(expected, error.Message);
        }

        [TestMethod]
        public void ParserReuse_DoesNotRetainEnumMembersOrDependencies()
        {
            const string firstSource = "enums\n    First\n        A = 1 int16\n";
            const string secondSource = "enums\n    Second\n        B\n";
            const string expected = "// enums\nenum Second\n{\n    B,\n};";
            var parser = new Parser();
            parser.Parse(new Lexer().Tokenize(firstSource));
            var ast = parser.Parse(new Lexer().Tokenize(secondSource));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void ParserReuse_AfterMissingInitializer_DoesNotRetainFailedEnum()
        {
            const string invalidSource = "enums\n    Failed\n        A =\n";
            const string source = "enums\n    Valid\n        B = 3\n";
            const string expected = "// enums\nenum Valid\n{\n    B=3,\n};";
            var parser = new Parser();
            Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(new Lexer().Tokenize(invalidSource)));
            var ast = parser.Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }
    }
}

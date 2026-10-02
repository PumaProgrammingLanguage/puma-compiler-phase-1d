using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class EnumExpressionTest
    {
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
            var expected = $"{headers}// enums\nEnums Values\n{{\n    A={initializer},\n}}";
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
            const string expected = "// enums\nEnums Values\n{\n    A,\n    B=3,\n    C,\n}";
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
            const string expected = "// enums\nEnums Values\n{\n    Renamed=Pick(1, 2),\n}";
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
            const string expected = "#include <cstdint>\n\n// enums\nEnums First\n{\n    A=(int16_t)1,\n}\n\n// enums\nEnums Second\n{\n    B,\n    C=(A + 2),\n}";
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
            const string expected = "// enums\nEnums Second\n{\n    B,\n}";
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
            const string expected = "// enums\nEnums Valid\n{\n    B=3,\n}";
            var parser = new Parser();
            Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(new Lexer().Tokenize(invalidSource)));
            var ast = parser.Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }
    }
}

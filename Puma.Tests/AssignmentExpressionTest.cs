using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class AssignmentExpressionTest
    {
        [DataTestMethod]
        [DataRow("1 int16", "int16", "(int16_t)1", "#include <cstdint>\n\n")]
        [DataRow("0xFE uint8", "uint8", "(uint8_t)0xFE", "#include <cstdint>\n\n")]
        [DataRow("1.25e+3 flt32", "flt32", "(float)1.25e+3", "")]
        public void Assignment_StoresOperandsTypeAndDiagnosticSpan(string expression, string type, string initializer, string headers)
        {
            var source = $"start\n    value = {expression}\n";
            var expected = $"{headers}// start\nint main()\n{{\n    auto value = {initializer};\n    return 0;\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var assignment = ast.OfType<AssignmentStatementAstNode>().Single();
            Assert.AreEqual("value", assignment.AssignmentLeftExpression?.Value);
            Assert.AreEqual(type, assignment.AssignmentRightExpression?.DeclaredType);
            Assert.AreEqual(new SourceSpan(2, 5, 2, 10), assignment.AssignmentLeftSourceSpan);
            Assert.AreEqual(assignment.AssignmentLeftExpression?.SourceSpan, assignment.AssignmentLeftSourceSpan);
            Assert.IsFalse(assignment.IsLoweredPostfixMutation);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow("value++", "+=")]
        [DataRow("obj.Value--", "-=")]
        [DataRow("items[0]++", "+=")]
        public void PostfixLowering_PreservesStructuredTargetSpanAndEmission(string statement, string assignmentOperator)
        {
            var source = $"functions\n    F()\n        {statement}\n";
            var expected = $"// functions\nvoid F(void)\n{{\n    {statement};\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var assignment = ast.OfType<FunctionDeclarationAstNode>().Single().FunctionBody.OfType<AssignmentStatementAstNode>().Single();
            Assert.IsTrue(assignment.IsLoweredPostfixMutation);
            Assert.AreEqual(assignmentOperator, assignment.AssignmentOperator);
            Assert.AreEqual("1", assignment.AssignmentRightExpression?.Value);
            Assert.AreEqual(new SourceSpan(3, 9, 3, 9 + statement.Length - 2), assignment.AssignmentLeftSourceSpan);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void MissingAssignmentOperand_IsRejectedWithoutTextFallback(bool removeLeft)
        {
            const string source = "start\n    value = 1\n";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var assignment = ast.OfType<AssignmentStatementAstNode>().Single();
            if (removeLeft) assignment.AssignmentLeftExpression = null;
            else assignment.AssignmentRightExpression = null;
            var error = Assert.ThrowsException<InvalidOperationException>(() => new Codegen().Generate(ast));
            Assert.AreEqual("Expression AST node is required for code generation.", error.Message);
        }

        [TestMethod]
        public void FunctionParameterListReplacement_UpdatesSignatureDefaultsAndDependencies()
        {
            const string source = "functions\n    F(value int16 = 1 int16)\n    Caller()\n        F()\n";
            const string replacementSource = "functions\n    F(value double = 1.25e+3)\n";
            const string expected = "// functions\nvoid F(double value)\n{\n}\n\nvoid Caller(void)\n{\n    F(1.25e+3);\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var parameters = ast.OfType<FunctionDeclarationAstNode>().First().FunctionParameterList;
            parameters.Clear();
            parameters.AddRange(new Parser().Parse(new Lexer().Tokenize(replacementSource))
                .OfType<FunctionDeclarationAstNode>().Single().FunctionParameterList);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void SectionParameterListReplacement_UpdatesSignatureAndDependencies()
        {
            const string source = "initialize(value int16 = 1 int16)\n";
            const string replacementSource = "initialize(value double = Pick(1, 2))\n";
            const string expected = "// initialize\nvoid initialize(double value)\n{\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var parameters = ast.OfType<SectionAstNode>().Single().SectionParameterList;
            parameters.Clear();
            parameters.AddRange(new Parser().Parse(new Lexer().Tokenize(replacementSource))
                .OfType<SectionAstNode>().Single().SectionParameterList);
            Assert.IsNotNull(parameters.Single().DefaultExpression?.SourceSpan);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void AssignmentTypeInference_StillRejectsInvalidImplicitCallArgument()
        {
            const string source = "functions\n    Take(value int16)\n    Caller()\n        value = 1.5 flt32\n        Take(value)\n";
            var error = Assert.ThrowsException<InvalidOperationException>(() => new Parser().Parse(new Lexer().Tokenize(source)));
            Assert.AreEqual("Implicit conversion is not valid: FLT32 -> INT16", error.Message);
        }

        [TestMethod]
        public void ParserReuse_DoesNotRetainAssignmentMetadataOrHeaderDependencies()
        {
            const string firstSource = "start\n    value = 1 int16\n";
            const string secondSource = "start\n    value = 1.25 flt32\n";
            const string expected = "// start\nint main()\n{\n    auto value = (float)1.25;\n    return 0;\n}";
            var parser = new Parser();
            parser.Parse(new Lexer().Tokenize(firstSource));
            var ast = parser.Parse(new Lexer().Tokenize(secondSource));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }
    }
}

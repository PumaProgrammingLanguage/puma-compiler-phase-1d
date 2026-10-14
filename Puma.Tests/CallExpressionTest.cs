using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class CallExpressionTest
    {
        [DataTestMethod]
        [DataRow("F()", "F();")]
        [DataRow("F(1, Pick(2, 3))", "F(1, Pick(2, 3));")]
        [DataRow("obj.Print(Pick(1, 2))", "obj.Print(Pick(1, 2));")]
        [DataRow("F() + 1", "(F() + 1);")]
        public void CallStatement_EmitsFullStructuredExpression(string expression, string statement)
        {
            var source = $"start\n    {expression}\n";
            var expected = $"// start\nint main()\n{{\n    {statement}\n    return 0;\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var call = ast.OfType<FunctionCallAstNode>().Single();
            Assert.IsNotNull(call.Expression);
            Assert.AreEqual(new SourceSpan(2, 5, 2, 5 + expression.Length), call.Expression.SourceSpan);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void CallAstReplacement_ChangesCalleeArgumentsAndDependencies()
        {
            const string source = "start\n    Original(1)\n";
            const string replacementSource = "start\n    obj.Replacement(Pick(2 int16, 3 uint8))\n";
            const string expected = "#include <cstdint>\n\n// start\nint main()\n{\n    obj.Replacement(Pick((int16_t)2, (uint8_t)3));\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var replacement = new Parser().Parse(new Lexer().Tokenize(replacementSource));
            ast.OfType<FunctionCallAstNode>().Single().Expression = replacement.OfType<FunctionCallAstNode>().Single().Expression;
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void CallAstReplacement_SelectsDefaultsFromNewCallee()
        {
            const string source = "functions\n    F(value double = 1)\n    G(value double = 2)\n    Caller()\n        F()\n";
            const string replacementSource = "start\n    G()\n";
            const string expected = "// functions\nvoid F(double value)\n{\n}\n\nvoid G(double value)\n{\n}\n\nvoid Caller(void)\n{\n    G(2);\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var replacement = new Parser().Parse(new Lexer().Tokenize(replacementSource));
            ast.OfType<FunctionDeclarationAstNode>().Last().FunctionBody.OfType<FunctionCallAstNode>().Single().Expression =
                replacement.OfType<FunctionCallAstNode>().Single().Expression;
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void MemberCall_DoesNotUseUnrelatedGlobalDefaults()
        {
            const string source = "functions\n    Print(value double = 1)\n    Caller()\n        obj.Print()\n";
            const string expected = "// functions\nvoid Print(double value)\n{\n}\n\nvoid Caller(void)\n{\n    obj.Print();\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void MissingCallExpression_IsRejectedWithoutRawFallback()
        {
            const string source = "start\n    F(1)\n";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            ast.OfType<FunctionCallAstNode>().Single().Expression = null;
            var error = Assert.ThrowsException<InvalidOperationException>(() => new Codegen().Generate(ast));
            Assert.AreEqual("Expression AST node is required for code generation.", error.Message);
        }

        [DataTestMethod]
        [DataRow("return", "return Pick(1, 2);")]
        [DataRow("yield", "/* yield Pick(1, 2) */")]
        [DataRow("error", "/* error Pick(1, 2) */")]
        [DataRow("catch", "/* catch Pick(1, 2) */")]
        public void GenericStatement_StoresAndEmitsOnlyStructuredValue(string keyword, string statement)
        {
            var source = $"functions\n    Caller() double\n        {keyword} Pick(1, 2)\n";
            var expected = $"// functions\ndouble Caller(void)\n{{\n    {statement}\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var expression = ast.OfType<FunctionDeclarationAstNode>().Single().FunctionBody.OfType<StatementAstNode>().Single().StatementExpression;
            Assert.IsNotNull(expression?.SourceSpan);
            Assert.AreEqual(ExpressionKind.Call, expression?.Kind);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void GenericStatementAstReplacement_UpdatesOutputAndDependencies()
        {
            const string source = "functions\n    F() double\n        return 1\n";
            const string replacementSource = "start\n    value = 2 int16\n";
            const string expected = "#include <cstdint>\n\n// functions\ndouble F(void)\n{\n    return (int16_t)2;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var replacement = new Parser().Parse(new Lexer().Tokenize(replacementSource));
            ast.OfType<FunctionDeclarationAstNode>().Single().FunctionBody.OfType<StatementAstNode>().Single().StatementExpression =
                replacement.OfType<AssignmentStatementAstNode>().Single().AssignmentRightExpression;
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void ParserReuse_DoesNotRetainCallArguments()
        {
            const string firstSource = "start\n    F(1 int16)\n";
            const string secondSource = "start\n    G()\n";
            const string expected = "// start\nint main()\n{\n    G();\n    return 0;\n}";
            var parser = new Parser();
            parser.Parse(new Lexer().Tokenize(firstSource));
            var ast = parser.Parse(new Lexer().Tokenize(secondSource));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }
    }
}

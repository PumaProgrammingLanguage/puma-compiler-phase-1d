using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class WriteLineExpressionTest
    {
        [DataTestMethod]
        [DataRow("\"Hello\"")]
        [DataRow("\"\"")]
        [DataRow("\"a, (b)\"")]
        [DataRow("\"Hello\\nworld\"")]
        public void StringArgument_EmitsExactOutputAndRetainsSourceSpan(string literal)
        {
            var source = $"start\n    WriteLn({literal})\n";
            var expected = $"#include <PumaConsole/Console.hpp>\n\n// start\nint main()\n{{\n    PumaConsole::WriteLn({literal});\n    return 0;\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var argument = ast.OfType<WriteLineAstNode>().Single().ArgumentExpression;
            Assert.IsNotNull(argument);
            Assert.AreEqual(ExpressionKind.Literal, argument.Kind);
            Assert.AreEqual(literal, argument.Value);
            Assert.AreEqual(new SourceSpan(2, 13, 2, 13 + literal.Length), argument.SourceSpan);
            var result = new Codegen().GenerateResult(ast);
            Assert.AreEqual(expected, result.SourceCode.Replace("\r\n", "\n").Trim());
            CollectionAssert.AreEqual(new[] { "PumaConsole", "PumaType" }, result.RequiredRuntimeLibraries.ToArray());
        }

        [TestMethod]
        public void ArgumentAstReplacement_UpdatesOutputWithoutStringRuntimeDependency()
        {
            const string source = "start\n    WriteLn(\"original\")\n";
            const string replacementSource = "start\n    WriteLn(\"replacement\")\n";
            const string expected = "#include <PumaConsole/Console.hpp>\n\n// start\nint main()\n{\n    PumaConsole::WriteLn(\"replacement\");\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var replacement = new Parser().Parse(new Lexer().Tokenize(replacementSource));
            ast.OfType<WriteLineAstNode>().Single().ArgumentExpression =
                replacement.OfType<WriteLineAstNode>().Single().ArgumentExpression;
            var result = new Codegen().GenerateResult(ast);
            Assert.AreEqual(expected, result.SourceCode.Replace("\r\n", "\n").Trim());
            CollectionAssert.AreEqual(new[] { "PumaConsole", "PumaType" }, result.RequiredRuntimeLibraries.ToArray());
        }

        [TestMethod]
        public void ArgumentAstReplacement_TraversesTypedExpressionDependencies()
        {
            const string source = "start\n    WriteLn(\"original\")\n";
            const string replacementSource = "start\n    value = 1 int16 + 2 uint8\n";
            const string expected = "#include <cstdint>\n#include <PumaConsole/Console.hpp>\n\n// start\nint main()\n{\n    PumaConsole::WriteLn(((int16_t)1 + (uint8_t)2));\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var replacement = new Parser().Parse(new Lexer().Tokenize(replacementSource));
            ast.OfType<WriteLineAstNode>().Single().ArgumentExpression =
                replacement.OfType<AssignmentStatementAstNode>().Single().AssignmentRightExpression;
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void MissingArgumentAst_IsRejectedRatherThanOmitted()
        {
            const string source = "start\n    WriteLn(\"Hello\")\n";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            ast.OfType<WriteLineAstNode>().Single().ArgumentExpression = null;
            var error = Assert.ThrowsException<InvalidOperationException>(() => new Codegen().Generate(ast));
            Assert.AreEqual("Expression AST node is required for code generation.", error.Message);
        }

        [TestMethod]
        public void ParserReuse_DoesNotRetainPreviousArgument()
        {
            const string firstSource = "start\n    WriteLn(\"first\")\n";
            const string secondSource = "start\n    WriteLn(\"second\")\n";
            const string expected = "#include <PumaConsole/Console.hpp>\n\n// start\nint main()\n{\n    PumaConsole::WriteLn(\"second\");\n    return 0;\n}";
            var parser = new Parser();
            parser.Parse(new Lexer().Tokenize(firstSource));
            var ast = parser.Parse(new Lexer().Tokenize(secondSource));
            Assert.AreEqual(1, ast.OfType<WriteLineAstNode>().Count());
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow("WriteLn \"x\")", "Expected '(' after WriteLn.")]
        [DataRow("WriteLn(1)", "Expected string literal in WriteLn(...)")]
        [DataRow("WriteLn()", "Expected string literal in WriteLn(...)")]
        [DataRow("WriteLn(\"x\"", "Expected ')' after WriteLn argument.")]
        [DataRow("WriteLn(\"x\", \"y\")", "Expected ')' after WriteLn argument.")]
        public void InvalidArgument_PreservesParserDiagnostic(string statement, string expected)
        {
            var source = $"start\n    {statement}\n";
            var error = Assert.ThrowsException<InvalidOperationException>(() => new Parser().Parse(new Lexer().Tokenize(source)));
            Assert.AreEqual(expected, error.Message);
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class ControlFlowExpressionTest
    {
        [DataTestMethod]
        [DataRow("if Pick(1, 2) > 0", "    if (Pick(1, 2) > 0)\n    {\n    }", "")]
        [DataRow("while Ready(1, 2)", "    while (Ready(1, 2))\n    {\n    }", "")]
        [DataRow("when 2 int16", "    /* when (int16_t)2 */", "#include <cstdint>\n\n")]
        [DataRow("repeat 2 int16", "    do\n    {\n    } while ((int16_t)2);", "#include <cstdint>\n\n")]
        [DataRow("has obj.Value", "    if (obj.Value != null)\n    {\n    }", "")]
        [DataRow("has trait Printable obj.Value", "    if (obj.Value != null && typeof(obj.Value) == typeof(Printable))\n    {\n    }", "")]
        public void ControlFlow_EmitsStructuredExpressionAndRetainsSpan(string statement, string body, string headers)
        {
            var source = $"start\n    {statement}\n";
            var expected = $"{headers}// start\nint main()\n{{\n{body}\n    return 0;\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var expression = ast.Single(node => node.Kind != NodeKind.Section) switch
            {
                IfStatementAstNode node => node.ConditionExpression,
                WhileStatementAstNode node => node.WhileExpression,
                WhenStatementAstNode node => node.WhenExpression,
                RepeatStatementAstNode node => node.RepeatExpressionNode,
                HasStatementAstNode node => node.HasExpression,
                HasTraitStatementAstNode node => node.HasTraitExpression,
                _ => null
            };
            Assert.IsNotNull(expression?.SourceSpan);
            Assert.AreEqual(2, expression.SourceSpan.Value.StartLine);
            Assert.AreEqual(5 + statement.Length, expression.SourceSpan.Value.EndColumn);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow("if original", "    if ((int16_t)2)\n    {\n    }")]
        [DataRow("while original", "    while ((int16_t)2)\n    {\n    }")]
        [DataRow("when original", "    /* when (int16_t)2 */")]
        [DataRow("match original", "    switch ((int16_t)2)\n    {\n    }")]
        [DataRow("repeat original", "    do\n    {\n    } while ((int16_t)2);")]
        [DataRow("has original", "    if ((int16_t)2 != null)\n    {\n    }")]
        [DataRow("has trait Printable original", "    if ((int16_t)2 != null && typeof((int16_t)2) == typeof(Printable))\n    {\n    }")]
        public void ControlFlowAstReplacement_UpdatesOutputAndDependencies(string statement, string body)
        {
            var source = $"start\n    {statement}\n";
            const string replacementSource = "start\n    value = 2 int16\n";
            var expected = $"#include <cstdint>\n\n// start\nint main()\n{{\n{body}\n    return 0;\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var replacement = new Parser().Parse(new Lexer().Tokenize(replacementSource))
                .OfType<AssignmentStatementAstNode>().Single().AssignmentRightExpression;
            switch (ast.Single(node => node.Kind != NodeKind.Section))
            {
                case IfStatementAstNode node: node.ConditionExpression = replacement; break;
                case WhileStatementAstNode node: node.WhileExpression = replacement; break;
                case WhenStatementAstNode node: node.WhenExpression = replacement; break;
                case MatchStatementAstNode node: node.ExpressionNode = replacement; break;
                case RepeatStatementAstNode node: node.RepeatExpressionNode = replacement; break;
                case HasStatementAstNode node: node.HasExpression = replacement; break;
                case HasTraitStatementAstNode node: node.HasTraitExpression = replacement; break;
            }
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void BareRepeat_PreservesOptionalExpressionAndInfiniteLoop()
        {
            const string source = "start\n    repeat\n";
            const string expected = "#include <stdbool>\n\n// start\nint main()\n{\n    do\n    {\n    } while (true);\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.IsNull(ast.OfType<RepeatStatementAstNode>().Single().RepeatExpressionNode);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void MatchWhen_PreservesNestedExpressionTrees()
        {
            const string source = "start\n    match Pick(1, 2)\n        when 2 int16\n";
            const string expected = "#include <cstdint>\n\n// start\nint main()\n{\n    switch (Pick(1, 2))\n    {\n        case (int16_t)2:\n            break;\n    }\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var match = ast.OfType<MatchStatementAstNode>().Single();
            Assert.AreEqual(ExpressionKind.Call, match.ExpressionNode?.Kind);
            Assert.IsNotNull(match.StatementBody.OfType<WhenStatementAstNode>().Single().WhenExpression?.SourceSpan);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow("if", "If statements require a condition expression.")]
        [DataRow("match", "Match statements require an expression.")]
        [DataRow("when", "When statements require a condition.")]
        [DataRow("has", "Has statements require a condition.")]
        [DataRow("while", "Line 2, column 5: While statements require a condition.")]
        public void MissingCondition_PreservesExactDiagnostic(string keyword, string expected)
        {
            var source = $"start\n    {keyword}\n";
            var error = Assert.ThrowsException<InvalidOperationException>(() => new Parser().Parse(new Lexer().Tokenize(source)));
            Assert.AreEqual(expected, error.Message);
        }

        [TestMethod]
        public void ParserReuse_DoesNotRetainConditionOrDependencies()
        {
            const string firstSource = "start\n    if 1 int16\n";
            const string secondSource = "start\n    while Ready()\n";
            const string expected = "// start\nint main()\n{\n    while (Ready())\n    {\n    }\n    return 0;\n}";
            var parser = new Parser();
            parser.Parse(new Lexer().Tokenize(firstSource));
            var ast = parser.Parse(new Lexer().Tokenize(secondSource));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class InitializerExpressionTest
    {
        [DataTestMethod]
        [DataRow("\"hi\"", "PumaType::String(\"hi\", sizeof(\"hi\") - 1)")]
        [DataRow("\"\"", "PumaType::String(\"\", sizeof(\"\") - 1)")]
        [DataRow("Length(\"hello\")", "Length(\"hello\")")]
        [DataRow("Pick(\"a\", Inner(\"b\"))", "Pick(\"a\", Inner(\"b\"))")]
        [DataRow("\"yes\" if flag else \"no\"", "(flag ? \"yes\" : \"no\")")]
        [DataRow("\"a\" == \"b\"", "(\"a\" == \"b\")")]
        public void FunctionInitializer_WrapsOnlyLiteralRoots(string expression, string initializer)
        {
            var source = $"functions\n    F()\n        value = {expression}\n";
            var expected = $"#include <PumaType/String.hpp>\n\n// functions\nvoid F(void)\n{{\n    auto value = {initializer};\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow("\"original\"", "Length(\"replacement\")", "Length(\"replacement\")")]
        [DataRow("Length(\"original\")", "\"replacement\"", "PumaType::String(\"replacement\", sizeof(\"replacement\") - 1)")]
        public void InitializerAstReplacement_ChangesLiteralConstruction(string original, string replacement, string initializer)
        {
            var source = $"functions\n    F()\n        value = {original}\n";
            var replacementSource = $"functions\n    F()\n        value = {replacement}\n";
            var expected = $"#include <PumaType/String.hpp>\n\n// functions\nvoid F(void)\n{{\n    auto value = {initializer};\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            ast.OfType<FunctionDeclarationAstNode>().Single().FunctionBody.OfType<AssignmentStatementAstNode>().Single().AssignmentRightExpression =
                new Parser().Parse(new Lexer().Tokenize(replacementSource)).OfType<FunctionDeclarationAstNode>().Single()
                    .FunctionBody.OfType<AssignmentStatementAstNode>().Single().AssignmentRightExpression;
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow("1 int16", "(int16_t)1", "#include <cstdint>\n\n")]
        [DataRow("-42 int16", "(int16_t)-42", "#include <cstdint>\n\n")]
        [DataRow("1.25e+3 flt32", "(float)1.25e+3", "")]
        public void NumericInitializer_EmitsSingleAstDerivedCast(string expression, string initializer, string headers)
        {
            var source = $"start\n    value = {expression}\n";
            var expected = $"{headers}// start\nint main()\n{{\n    auto value = {initializer};\n    return 0;\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow("count()")]
        [DataRow("count(1.25)")]
        [DataRow("obj.value")]
        public void GeneralInitializer_EmitsStructuredValueWithoutTypeGuessing(string expression)
        {
            var source = $"start\n    value = {expression}\n";
            var expected = $"// start\nint main()\n{{\n    auto value = {expression};\n\n    return 0;\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow("bool", "false", "#include <stdbool>\n\n")]
        [DataRow("str", "PumaType::String(\"\", sizeof(\"\") - 1)", "#include <PumaType/String.hpp>\n\n")]
        public void BareTypeInitializer_PreservesAstSelectedDefault(string expression, string initializer, string headers)
        {
            var source = $"start\n    value = {expression}\n";
            var expected = $"{headers}// start\nint main()\n{{\n    auto value = {initializer};\n    return 0;\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }
    }
}

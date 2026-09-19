using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class ParameterDefaultExpressionTest
    {
        [DataTestMethod]
        [DataRow("double", "1.25e+3", "1.25e+3", "")]
        [DataRow("double", "6.5e-1 flt32", "(float)6.5e-1", "")]
        [DataRow("double", "-42 int16", "-(int16_t)42", "#include <cstdint>\n\n")]
        [DataRow("double", "0xFE uint8", "(uint8_t)0xFE", "#include <cstdint>\n\n")]
        [DataRow("double", "(1) int16", "(int16_t) 1", "#include <cstdint>\n\n")]
        [DataRow("double", "1 uint8 + 2 int16", "((uint8_t)1 + (int16_t)2)", "#include <cstdint>\n\n")]
        [DataRow("PumaType::String", "\"hello\"", "PumaType::String(\"hello\", sizeof(\"hello\") - 1)", "#include <PumaType/String.hpp>\n\n")]
        [DataRow("PumaType::Character", "'x'", "Character('x')", "#include <PumaType/Character.hpp>\n\n")]
        [DataRow("bool_t", "true", "true", "#include <stdbool>\n\n")]
        public void DefaultExpression_EmitsStructuredValueAndDependencies(string type, string expression, string argument, string headers)
        {
            var pumaType = type switch { "PumaType::String" => "str", "PumaType::Character" => "char", "bool_t" => "bool", _ => type };
            var source = $"functions\n    F(value {pumaType} = {expression})\n    Caller()\n        F()\n";
            var expected = $"{headers}// functions\nvoid F({type} value)\n{{\n}}\n\nvoid Caller(void)\n{{\n    F({argument});\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var function = ast.OfType<FunctionDeclarationAstNode>().First();
            Assert.IsNotNull(function.FunctionParameterList.Single().DefaultExpression?.SourceSpan);
            function.FunctionDeclarationParameters = "stale text";
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void NestedDefaultCalls_PreserveCommasAndFunctionReturnType()
        {
            const string source = "functions\n    F(value double = Pick(1, Pick(2, 3)), other double = (4 + 5)) double\n        return value\n    Caller()\n        F()\n";
            const string expected = "// functions\ndouble F(double value, double other)\n{\n    return value;\n}\n\nvoid Caller(void)\n{\n    F(Pick(1, Pick(2, 3)), (4 + 5));\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(2, ast.OfType<FunctionDeclarationAstNode>().First().FunctionParameterList.Count);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void DefaultAstReplacement_UpdatesOutputAndDependencies()
        {
            const string source = "functions\n    F(value double = 1 int16)\n    Caller()\n        F()\n";
            const string replacementSource = "functions\n    F(value double = 1.25e+3)\n";
            const string expected = "// functions\nvoid F(double value)\n{\n}\n\nvoid Caller(void)\n{\n    F(1.25e+3);\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var replacement = new Parser().Parse(new Lexer().Tokenize(replacementSource));
            ast.OfType<FunctionDeclarationAstNode>().First().FunctionParameterList.Single().DefaultExpression =
                replacement.OfType<FunctionDeclarationAstNode>().Single().FunctionParameterList.Single().DefaultExpression;
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void SectionDefault_ParsesNestedCallAndFollowingParameter()
        {
            const string source = "initialize(value double = Pick(1, 2), other double = (3 + 4))\n";
            const string expected = "// initialize\nvoid initialize(double value, double other)\n{\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var parameters = ast.OfType<SectionAstNode>().Single().SectionParameterList;
            Assert.AreEqual(2, parameters.Count);
            Assert.AreEqual(ExpressionKind.Call, parameters[0].DefaultExpression?.Kind);
            Assert.AreEqual(2, parameters[0].DefaultExpression?.Arguments.Count);
            Assert.AreEqual(ExpressionKind.Binary, parameters[1].DefaultExpression?.Kind);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void MissingDefault_ReportsAssignmentLocation()
        {
            const string source = "functions\n    F(value int =)\n";
            var error = Assert.ThrowsException<InvalidOperationException>(() => new Parser().Parse(new Lexer().Tokenize(source)));
            Assert.AreEqual("Line 2, column 17: Parameter 'value' is missing the default expression.", error.Message);
        }

        [TestMethod]
        public void ParenthesizedConstantParameterMutation_IsRejected()
        {
            const string source = "functions\n    F(value int const)\n        (value) = 2\n";
            var error = Assert.ThrowsException<InvalidOperationException>(() => new Parser().Parse(new Lexer().Tokenize(source)));
            Assert.AreEqual("Line 3, column 9: Cannot assign to constant parameter 'value'.", error.Message);
        }

        [TestMethod]
        public void ParenthesizedNonOptionalPropertyNoneAssignment_IsRejected()
        {
            const string source = "properties\n    shape = Shape()\nstart\n    (shape) = (none)\n";
            var error = Assert.ThrowsException<InvalidOperationException>(() => new Parser().Parse(new Lexer().Tokenize(source)));
            Assert.AreEqual("Line 4, column 5: Cannot assign none to non-optional property 'shape'.", error.Message);
        }

        [TestMethod]
        public void HasTraitAstReplacement_IgnoresLegacyVariableText()
        {
            const string source = "start\n    has trait Printable obj\n";
            const string expected = "// start\nint main()\n{\n    if (replacement != null && typeof(replacement) == typeof(Printable))\n    {\n    }\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var has = ast.OfType<HasTraitStatementAstNode>().Single();
            has.HasTraitExpression = new ExpressionNode { Kind = ExpressionKind.Identifier, Value = "replacement" };
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }
    }
}

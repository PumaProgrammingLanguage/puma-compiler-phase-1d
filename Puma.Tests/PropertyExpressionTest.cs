using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class PropertyExpressionTest
    {
        [DataTestMethod]
        [DataRow("1.25e+3", "(double)1.25e+3", "")]
        [DataRow("6.5e-1 flt32", "(float)6.5e-1", "")]
        [DataRow("-42 int16", "(int16_t)-42", "#include <cstdint>\n\n")]
        [DataRow("0xFE", "(int64_t)0xFE", "#include <cstdint>\n\n")]
        [DataRow("(1) int16", "(int16_t) 1", "#include <cstdint>\n\n")]
        [DataRow("1 uint8 + 2 int16", "((uint8_t)1 + (int16_t)2)", "#include <cstdint>\n\n")]
        [DataRow("str", "PumaType::String(\"\", sizeof(\"\") - 1)", "#include <PumaType/String.hpp>\n\n")]
        public void GlobalProperty_FormatsStructuredInitializer(string expression, string expectedInitializer, string headers)
        {
            var source = $"properties\n    value = {expression}\n";
            var expected = $"{headers}// properties\nauto value = {expectedInitializer};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            ast.OfType<PropertyDeclarationAstNode>().Single().PropertyValue = "incorrect";

            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void TypeProperties_UseStructuredNumericInitializers()
        {
            const string source =
@"type
    Values is object
properties
    exponent = 1.25e+3
    negative = -42 int16
    hex = 0xFE
";
            const string expected =
@"#include <cstdint>

class Values : public object
{
    // properties
    protected:
    auto exponent = (double)1.25e+3;
    auto negative = (int16_t)-42;
    auto hex = (int64_t)0xFE;
};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected.Replace("\r\n", "\n"), new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void PropertyAstChange_UpdatesTypeAndDependencies()
        {
            const string source = "properties\n    value = 1 int16\n";
            const string replacementSource = "properties\n    value = 1.25e+3\n";
            const string expected = "// properties\nauto value = (double)1.25e+3;";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var replacement = new Parser().Parse(new Lexer().Tokenize(replacementSource));
            ast.OfType<PropertyDeclarationAstNode>().Single().PropertyValueExpression =
                replacement.OfType<PropertyDeclarationAstNode>().Single().PropertyValueExpression;

            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void GlobalConstructorProperty_PreservesOwnerDeletion()
        {
            const string source = "properties\n    shape = Shape()\nstart\n";
            const string expected =
@"auto shape = new Shape();

// start
int main()
{
    delete shape;
    return 0;
}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected.Replace("\r\n", "\n"), new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void GlobalBareTypeProperty_PreservesDefaultInitialization()
        {
            const string source = "properties\n    value = Shape\nstart\n";
            const string expected =
@"Shape value = {0};

// start
int main()
{
    return 0;
}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected.Replace("\r\n", "\n"), new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }
    }
}

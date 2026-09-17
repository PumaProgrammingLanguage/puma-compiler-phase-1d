using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class TypedExpressionTest
    {
        [DataTestMethod]
        [DataRow("2 int16", "(int16_t)2")]
        [DataRow("-(2 int16)", "-(int16_t)2")]
        [DataRow("2 int16 if true else 3 int16", "true ? (int16_t)2 : (int16_t)3")]
        [DataRow("(int16) 2", "(int16_t) 2")]
        [DataRow("(2) int16", "(int16_t) 2")]
        public void TypedReturn_EmitsCastsAndRequiredHeader(string expression, string expectedExpression)
        {
            var source = $"functions\n    Value() int\n        return {expression}\n";
            var expected = $"#include <cstdint>\n\n// functions\nint Value(void)\n{{\n    return {expectedExpression};\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));

            var generated = new Codegen().Generate(ast);

            Assert.AreEqual(expected, generated.Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void TypedCallArgument_EmitsNestedCastAndRequiredHeader()
        {
            const string source =
@"functions
    Caller()
        Consume(1 uint8 + 2 int16)
";
            const string expected =
@"#include <cstdint>

// functions
void Caller(void)
{
    Consume(((uint8_t)1 + (int16_t)2));
}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));

            var generated = new Codegen().Generate(ast);

            Assert.AreEqual(expected.Replace("\r\n", "\n"), generated.Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void TypedLocal_UsesExpressionMetadataNotLegacyAssignmentInference()
        {
            const string source =
@"start
    value = 1.25e+3 flt32
";
            const string expected =
@"// start
int main()
{
    auto value = (float)1.25e+3;
    return 0;
}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var assignment = ast.OfType<AssignmentStatementAstNode>().Single();
            assignment.AssignmentInferredType = "int16";
            assignment.AssignmentRight = "0 int16";

            var generated = new Codegen().Generate(ast);

            Assert.AreEqual(expected.Replace("\r\n", "\n"), generated.Replace("\r\n", "\n").Trim());
        }
    }
}

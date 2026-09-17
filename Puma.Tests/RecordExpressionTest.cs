using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class RecordExpressionTest
    {
        [TestMethod]
        public void RecordNumericInitializers_PreserveLiteralFormsAndTypes()
        {
            const string source =
@"records
    Numbers
        exponent = 1.25e+3
        negative = -42 int16
        hex = 0xFE
        bits = 0b1010 uint8
        sum = 1 uint8 + 2 int16
";
            const string expected =
@"#include <cstdint>

// records
struct Numbers
{
    auto exponent = (double)1.25e+3;
    auto negative = (int16_t)-42;
    auto hex = (int64_t)0xFE;
    auto bits = (uint8_t)0b1010;
    auto sum = ((uint8_t)1 + (int16_t)2);
};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var result = new Codegen().GenerateResult(ast);

            Assert.AreEqual(expected.Replace("\r\n", "\n"), result.SourceCode.Replace("\r\n", "\n").Trim());
            Assert.AreEqual(0, result.RequiredRuntimeLibraries.Count);
        }

        [TestMethod]
        public void RecordRuntimeInitializers_SelectHeadersFromExpressions()
        {
            const string source =
@"records
    Values
        enabled = bool
        label = ""x=y""
        marker = 'A'
";
            const string expected =
@"#include <stdbool>
#include <PumaType/Character.hpp>
#include <PumaType/String.hpp>

// records
struct Values
{
    auto enabled = false;
    auto label = PumaType::String(""x=y"", sizeof(""x=y"") - 1);
    auto marker = Character('A');
};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var result = new Codegen().GenerateResult(ast);

            Assert.AreEqual(expected.Replace("\r\n", "\n"), result.SourceCode.Replace("\r\n", "\n").Trim());
            CollectionAssert.AreEqual(new[] { "PumaType" }, result.RequiredRuntimeLibraries.ToArray());
        }

        [TestMethod]
        public void RecordInitializerAstChange_UpdatesOutputAndDependencies()
        {
            const string source = "records\n    Value\n        item = 1\n";
            const string replacementSource = "records\n    Replacement\n        item = \"updated\"\n";
            const string expected =
@"#include <PumaType/String.hpp>

// records
struct Value
{
    auto item = PumaType::String(""updated"", sizeof(""updated"") - 1);
};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var replacement = new Parser().Parse(new Lexer().Tokenize(replacementSource));
            ast.OfType<RecordDeclarationAstNode>().Single().MemberDeclarations.Single().ValueExpression =
                replacement.OfType<RecordDeclarationAstNode>().Single().MemberDeclarations.Single().ValueExpression;

            var result = new Codegen().GenerateResult(ast);

            Assert.AreEqual(expected.Replace("\r\n", "\n"), result.SourceCode.Replace("\r\n", "\n").Trim());
            CollectionAssert.AreEqual(new[] { "PumaType" }, result.RequiredRuntimeLibraries.ToArray());
        }

        [TestMethod]
        public void RecordParserReuse_DoesNotRetainPreviousMembers()
        {
            const string firstSource = "records\n    First\n        text = \"discard\"\n";
            const string secondSource = "records\n    Second\n        number = 7 uint8\n";
            const string expected =
@"#include <cstdint>

// records
struct Second
{
    auto number = (uint8_t)7;
};";
            var parser = new Parser();
            parser.Parse(new Lexer().Tokenize(firstSource));
            var ast = parser.Parse(new Lexer().Tokenize(secondSource));
            var record = ast.OfType<RecordDeclarationAstNode>().Single();

            Assert.AreEqual("number", record.MemberDeclarations.Single().Name);
            Assert.AreEqual("uint8", record.MemberDeclarations.Single().ValueExpression?.DeclaredType);
            Assert.AreEqual(expected.Replace("\r\n", "\n"), new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }
    }
}

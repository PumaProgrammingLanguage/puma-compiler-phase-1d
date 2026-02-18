using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Puma.Parser;

namespace Puma.Tests
{
    [TestClass]
    public class ParserTests
    {
        private const string Sample =
@"using

module

enums

records

initialize

finalize

functions

end
";

        [TestMethod]
        public void Sections_AreParsedIntoAstInOrder()
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(Sample);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();

            var expected = new[]
            {
                Section.Using,
                Section.Module,
                Section.Enums,
                Section.Records,
                Section.Initialize,
                Section.Finalize,
                Section.Functions,
                Section.end
            };

            CollectionAssert.AreEqual(expected, sections);
        }

        [TestMethod]
        public void UseSection_ParsesNamespaceAndAlias()
        {
            const string src =
@"use System.Console as Console

module

end
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var useNode = ast.Single(n => n.Kind == NodeKind.UseStatement);
            Assert.AreEqual("System.Console", useNode.UseTarget);
            Assert.AreEqual("Console", useNode.UseAlias);
            Assert.IsFalse(useNode.UseIsFilePath);
        }

        [TestMethod]
        public void TypeSection_ParsesTypeWithBaseAndTraits()
        {
            const string src =
@"type Sample.Type is object has Alpha, Beta

end
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var typeNode = ast.Single(n => n.Kind == NodeKind.TypeDeclaration);
            Assert.AreEqual("type", typeNode.DeclarationKind);
            Assert.AreEqual("Sample.Type", typeNode.DeclarationName);
            Assert.AreEqual("object", typeNode.BaseTypeName);
            CollectionAssert.AreEqual(new[] { "Alpha", "Beta" }, typeNode.TraitNames);
        }
    }
}
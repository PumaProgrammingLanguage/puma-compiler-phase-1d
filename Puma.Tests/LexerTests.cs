using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Puma.Lexer;

namespace Puma.Tests
{
    [TestClass]
    public class LexerTests
    {
        private const string Sample =
@"use

module

enums

records

initialize

finalize

functions
";

        [TestMethod]
        public void SectionHeaders_AreTokenizedAsKeywordsInOrder()
        {
            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(Sample);

            var identifiers = tokens
                .Where(t => t.Category == TokenCategory.Keyword)
                .Select(t => t.TokenText)
                .ToArray();

            var expected = new[]
            {
                "use","module","enums","records","initialize","finalize","functions"
            };

            CollectionAssert.AreEqual(expected, identifiers);
        }

        [TestMethod]
        public void NumericSuffixes_AreTokenizedSeparately()
        {
            const string src = "value = 10int32\n";
            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(src);

            var numeric = tokens.First(t => t.Category == TokenCategory.NumericLiteral);
            Assert.AreEqual("10", numeric.TokenText);

            var suffix = tokens.First(t => t.Category == TokenCategory.Keyword && t.TokenText == "int32");
            Assert.AreEqual("int32", suffix.TokenText);
        }
    }
}
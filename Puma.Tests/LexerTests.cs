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

        [TestMethod]
        public void UnaryBang_IsTokenizedAsOperator()
        {
            const string src = "value = !flag\n";
            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(src);

            var bang = tokens.First(t => t.Category == TokenCategory.Operator && t.TokenText == "!");
            Assert.AreEqual("!", bang.TokenText);
        }

        [TestMethod]
        public void UnaryTilde_IsTokenizedAsOperator()
        {
            const string src = "value = ~flag\n";
            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(src);

            var tilde = tokens.First(t => t.Category == TokenCategory.Operator && t.TokenText == "~");
            Assert.AreEqual("~", tilde.TokenText);
        }

        [TestMethod]
        public void IncrementAndDecrement_AreTokenizedAsOperators()
        {
            const string src = "x++\ny--\n";
            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(src);

            var increment = tokens.First(t => t.Category == TokenCategory.Operator && t.TokenText == "++");
            var decrement = tokens.First(t => t.Category == TokenCategory.Operator && t.TokenText == "--");

            Assert.AreEqual("++", increment.TokenText);
            Assert.AreEqual("--", decrement.TokenText);
        }

        [TestMethod]
        public void RangeOperator_IsTokenizedAsOperator()
        {
            const string src = "value = 1 .. 3\n";
            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(src);

            var range = tokens.First(t => t.Category == TokenCategory.Operator && t.TokenText == "..");
            Assert.AreEqual("..", range.TokenText);
        }

        [TestMethod]
        public void PairColon_IsTokenizedAsPunctuation()
        {
            const string src = "value = 1 : 2\n";
            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(src);

            var colon = tokens.First(t => t.Category == TokenCategory.Punctuation && t.TokenText == ":");
            Assert.AreEqual(":", colon.TokenText);
        }
    }
}
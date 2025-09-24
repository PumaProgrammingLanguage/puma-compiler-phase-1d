using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Puma.Lexer;

namespace Puma.Tests
{
    [TestClass]
    public class LexerTests
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
        public void SectionHeaders_AreTokenizedAsIdentifiersInOrder()
        {
            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(Sample);

            var identifiers = tokens
                .Where(t => t.Category == TokenCategory.Identifier)
                .Select(t => t.TokenText)
                .ToArray();

            var expected = new[]
            {
                "using","module","enums","records","initialize","finalize","functions","end"
            };

            CollectionAssert.AreEqual(expected, identifiers);
        }
    }
}
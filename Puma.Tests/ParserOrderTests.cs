using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Puma.Parser;

namespace Puma.Tests
{
    [TestClass]
    public class ParserOrderTests
    {
        private const string CorrectOrder =
@"using

module

enums

records

initialize

finalize

functions

end
";

        private const string IncorrectOrder =
@"module

using

enums

records

initialize

finalize

functions

end
";

        [TestMethod]
        public void Parse_DoesNotThrow_WhenSectionsAreInCorrectOrder()
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(CorrectOrder);

            // Should not throw
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
        public void Parse_ThrowsFriendlyError_WhenSectionsAreOutOfOrder()
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(IncorrectOrder);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));

            // Optional: check the message gives guidance (parser should provide a friendly message)
            StringAssert.Contains(ex.Message.ToLowerInvariant(), "order");
        }
    }
}
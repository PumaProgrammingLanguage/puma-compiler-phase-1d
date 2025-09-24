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
    }
}
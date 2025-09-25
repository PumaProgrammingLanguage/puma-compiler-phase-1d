using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Puma.Parser;

namespace Puma.Tests
{
    [TestClass]
    public class ParserOptionalSectionsTests
    {
        [TestMethod]
        public void Parse_AllSectionsMissing_YieldsEmptyAst()
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(string.Empty);
            var ast = parser.Parse(tokens);

            Assert.AreEqual(0, ast.Count);
        }

        [TestMethod]
        public void Parse_OnlyEnd_IsAccepted()
        {
            const string src = "end\n";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();
            CollectionAssert.AreEqual(new[] { Section.end }, sections);
        }

        [TestMethod]
        public void Parse_OnlyFunctionsAndEnd_IsAccepted()
        {
            const string src =
@"functions

end
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();
            CollectionAssert.AreEqual(new[] { Section.Functions, Section.end }, sections);
        }

        [TestMethod]
        public void Parse_UsingModuleEnd_SubsetInOrder_IsAccepted()
        {
            const string src =
@"using

module

end
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();
            CollectionAssert.AreEqual(new[] { Section.Using, Section.Module, Section.end }, sections);
        }

        [TestMethod]
        public void Parse_StartWithoutInitialize_IsAccepted()
        {
            const string src =
@"using

module

start

functions

end
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();
            CollectionAssert.AreEqual(
                new[] { Section.Using, Section.Module, Section.Start, Section.Functions, Section.end },
                sections);
        }

        [TestMethod]
        public void Parse_InitializeWithoutStart_IsAccepted()
        {
            const string src =
@"using

module

initialize

functions

end
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();
            CollectionAssert.AreEqual(
                new[] { Section.Using, Section.Module, Section.Initialize, Section.Functions, Section.end },
                sections);
        }

        [TestMethod]
        public void Parse_MissingEnd_IsAccepted()
        {
            const string src =
@"using

module

functions
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();
            CollectionAssert.AreEqual(
                new[] { Section.Using, Section.Module, Section.Functions },
                sections);
        }

        [TestMethod]
        public void Parse_DuplicateSection_ThrowsFriendlyError()
        {
            const string src =
@"using

using

end
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ex = Assert.ThrowsException<System.InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message.ToLowerInvariant(), "duplicate");
        }
    }
}
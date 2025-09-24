using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Puma.Tests
{
    [TestClass]
    public class CodegenTests
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

        private static string Normalize(string s) =>
            s.Replace("\r\n", "\n").Replace("\r", "\n");

        [TestMethod]
        public void Generate_EmitsExpectedCCommentSkeleton()
        {
            var expected =
@"// using

// module

// enums

// records

// initialize

// finalize

// functions

// end
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(Sample);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected), Normalize(c));
        }
    }
}
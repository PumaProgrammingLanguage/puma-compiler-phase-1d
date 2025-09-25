using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Puma.Tests
{
    [TestClass]
    public class CodegenOptionalSectionsTests
    {
        private static string Normalize(string s) =>
            s.Replace("\r\n", "\n").Replace("\r", "\n");

        [TestMethod]
        public void Generate_OnlyFunctionsAndEnd_EmitsExpected()
        {
            const string src =
@"functions

end
";
            var expected =
@"// functions

// end
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected), Normalize(c));
        }
    }
}
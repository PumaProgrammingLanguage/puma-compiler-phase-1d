using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class LexerParserCodeGenTest
    {
        private static string Normalize(string s) =>
            s.Replace("\r\n", "\n").Replace("\r", "\n");

        [TestMethod]
        public void SourceExample_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"start
    a = 1
    b = 2 int
    c = 3 int64
    d = 4 int32
    e = 5 int16
    f = 6 int8
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significant = tokens
                .Where(t => t.Category is not Puma.Lexer.TokenCategory.Whitespace
                    and not Puma.Lexer.TokenCategory.EndOfLine
                    and not Puma.Lexer.TokenCategory.Indent
                    and not Puma.Lexer.TokenCategory.Dedent
                    and not Puma.Lexer.TokenCategory.Comment)
                .Select(t => t.TokenText)
                .ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "start",
                "a", "=", "1",
                "b", "=", "2", "int",
                "c", "=", "3", "int64",
                "d", "=", "4", "int32",
                "e", "=", "5", "int16",
                "f", "=", "6", "int8"
            }, significant);

            var ast = parser.Parse(tokens);
            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToList();
            Assert.AreEqual(6, assignments.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d", "e", "f" }, assignments.Select(a => a.AssignmentLeft).ToArray());
            CollectionAssert.AreEqual(new[] { "1", "2", "3", "4", "5", "6" }, assignments.Select(a => a.AssignmentRightExpression?.Value).ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"int main()
{
    int64_t a = 1;
    int64_t b = 2;
    int64_t c = 3;
    int32_t d = 4;
    int16_t e = 5;
    int8_t f = 6;
    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void SourceExample_UnsignedIntegers_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"start
    b = 2 uint64
    c = 3 uint64
    d = 4 uint32
    e = 5 uint16
    f = 6 uint8
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significant = tokens
                .Where(t => t.Category is not Puma.Lexer.TokenCategory.Whitespace
                    and not Puma.Lexer.TokenCategory.EndOfLine
                    and not Puma.Lexer.TokenCategory.Indent
                    and not Puma.Lexer.TokenCategory.Dedent
                    and not Puma.Lexer.TokenCategory.Comment)
                .Select(t => t.TokenText)
                .ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "start",
                "b", "=", "2", "uint64",
                "c", "=", "3", "uint64",
                "d", "=", "4", "uint32",
                "e", "=", "5", "uint16",
                "f", "=", "6", "uint8"
            }, significant);

            var ast = parser.Parse(tokens);
            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToList();
            Assert.AreEqual(5, assignments.Count);
            CollectionAssert.AreEqual(new[] { "b", "c", "d", "e", "f" }, assignments.Select(a => a.AssignmentLeft).ToArray());
            CollectionAssert.AreEqual(new[] { "2", "3", "4", "5", "6" }, assignments.Select(a => a.AssignmentRightExpression?.Value).ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"int main()
{
    uint64_t b = 2;
    uint64_t c = 3;
    uint32_t d = 4;
    uint16_t e = 5;
    uint8_t f = 6;
    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void SourceExample_Floats_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"start
    a = 1.1
    b = 2.2 flt
    c = 3.3 flt64
    d = 4.4 flt32
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significant = tokens
                .Where(t => t.Category is not Puma.Lexer.TokenCategory.Whitespace
                    and not Puma.Lexer.TokenCategory.EndOfLine
                    and not Puma.Lexer.TokenCategory.Indent
                    and not Puma.Lexer.TokenCategory.Dedent
                    and not Puma.Lexer.TokenCategory.Comment)
                .Select(t => t.TokenText)
                .ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "start",
                "a", "=", "1.1",
                "b", "=", "2.2", "flt",
                "c", "=", "3.3", "flt64",
                "d", "=", "4.4", "flt32"
            }, significant);

            var ast = parser.Parse(tokens);
            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToList();
            Assert.AreEqual(4, assignments.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, assignments.Select(a => a.AssignmentLeft).ToArray());
            CollectionAssert.AreEqual(new[] { "1.1", "2.2", "3.3", "4.4" }, assignments.Select(a => a.AssignmentRightExpression?.Value).ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"int main()
{
    double a = 1.1;
    double b = 2.2;
    double c = 3.3;
    float d = 4.4;
    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void SourceExample_BoolAndString_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"start
    a = false
    b = true
    c = bool
    d = """"
    e = str
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significant = tokens
                .Where(t => t.Category is not Puma.Lexer.TokenCategory.Whitespace
                    and not Puma.Lexer.TokenCategory.EndOfLine
                    and not Puma.Lexer.TokenCategory.Indent
                    and not Puma.Lexer.TokenCategory.Dedent
                    and not Puma.Lexer.TokenCategory.Comment)
                .Select(t => t.TokenText)
                .ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "start",
                "a", "=", "false",
                "b", "=", "true",
                "c", "=", "bool",
                "d", "=", "\"\"",
                "e", "=", "str"
            }, significant);

            var ast = parser.Parse(tokens);
            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToList();
            Assert.AreEqual(5, assignments.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d", "e" }, assignments.Select(a => a.AssignmentLeft).ToArray());
            CollectionAssert.AreEqual(new[] { "false", "true", "bool", "\"\"", "str" }, assignments.Select(a => a.AssignmentRightExpression?.Value).ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdbool.h>

int main()
{
    bool a = false;
    bool b = true;
    bool c = false;
    stdstr d = """";
    stdstr e = """";
    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }
    }
}

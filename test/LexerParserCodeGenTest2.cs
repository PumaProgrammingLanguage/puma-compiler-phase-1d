using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class LexerParserCodeGenTest2
    {
        private static string Normalize(string s) =>
            s.Replace("\r\n", "\n").Replace("\r", "\n");

        private static List<Puma.Lexer.LexerTokens> GetSignificantTokens(List<Puma.Lexer.LexerTokens> tokens)
        {
            return tokens
                .Where(t => t.Category is not Puma.Lexer.TokenCategory.Whitespace
                    and not Puma.Lexer.TokenCategory.EndOfLine
                    and not Puma.Lexer.TokenCategory.Indent
                    and not Puma.Lexer.TokenCategory.Dedent
                    and not Puma.Lexer.TokenCategory.Comment)
                .ToList();
        }

        [TestMethod]
        public void FunctionsExample_HelloAndAdd_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"functions
    Hello()
        s = ""Hello, World!""
        PrintLn(s)

    Add(a int, b int) int
        return a + b
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "functions",
                "Hello", "(", ")",
                "s", "=", "\"Hello, World!\"",
                "PrintLn", "(", "s", ")",
                "Add", "(", "a", "int", ",", "b", "int", ")", "int",
                "return", "a", "+", "b"
            }, significant);

            var ast = parser.Parse(tokens);
            var functions = ast.Where(n => n.Kind == NodeKind.FunctionDeclaration).ToList();
            Assert.AreEqual(2, functions.Count);
            Assert.AreEqual("Hello", functions[0].FunctionDeclarationName);
            Assert.AreEqual("Add", functions[1].FunctionDeclarationName);
            Assert.AreEqual("int", functions[1].FunctionDeclarationReturnType);

            var helloBody = functions[0].FunctionBody;
            Assert.AreEqual(2, helloBody.Count);
            Assert.AreEqual(NodeKind.AssignmentStatement, helloBody[0].Kind);
            Assert.AreEqual(NodeKind.FunctionCall, helloBody[1].Kind);

            var addBody = functions[1].FunctionBody;
            Assert.AreEqual(1, addBody.Count);
            Assert.AreEqual(NodeKind.ReturnStatement, addBody[0].Kind);

            var generated = codegen.Generate(ast);
            var expected =
@"// functions
void Hello(void)
{
    s = ""Hello, World!""s;
    PrintLn(s);
}

int Add(a int, b int)
{
    return a + b;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void FunctionsExample_MissingParameterType_Parser_ThrowsCompilerError()
        {
            const string src =
@"functions
    Add(a, b int) int
        return a + b
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "functions",
                "Add", "(", "a", ",", "b", "int", ")", "int",
                "return", "a", "+", "b"
            }, significant);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "missing the type");
        }

        [TestMethod]
        public void FunctionsExample_MissingParameterName_Parser_ThrowsCompilerError()
        {
            const string src =
@"functions
    Add(int, b int) int
        return b
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "functions",
                "Add", "(", "int", ",", "b", "int", ")", "int",
                "return", "b"
            }, significant);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "missing the name");
        }

        [TestMethod]
        public void FunctionsExample_MissingParameterNameAndType_Parser_ThrowsCompilerError()
        {
            const string src =
@"functions
    Add(a int, , b int) int
        return a + b
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "functions",
                "Add", "(", "a", "int", ",", ",", "b", "int", ")", "int",
                "return", "a", "+", "b"
            }, significant);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "missing the name and type");
        }

        [TestMethod]
        public void FunctionsExample_EmptyParameterList_Parser_Accepts()
        {
            const string src =
@"functions
    Hello() int
        return 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "functions",
                "Hello", "(", ")", "int",
                "return", "1"
            }, significant);

            var ast = parser.Parse(tokens);
            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationName == "Hello");
            Assert.AreEqual(0, function.FunctionParameterList.Count);
        }

        [TestMethod]
        public void FunctionsExample_MissingParameterListAndReturnType_DefaultToVoid()
        {
            const string src =
@"functions
    Hello()
        x = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "functions",
                "Hello", "(", ")",
                "x", "=", "1"
            }, significant);

            var ast = parser.Parse(tokens);
            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationName == "Hello");
            Assert.AreEqual(0, function.FunctionParameterList.Count);
            Assert.IsTrue(string.IsNullOrWhiteSpace(function.FunctionDeclarationReturnType));

            var generated = codegen.Generate(ast);
            var expected =
@"// functions
void Hello(void)
{
    x = 1;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }
    }
}

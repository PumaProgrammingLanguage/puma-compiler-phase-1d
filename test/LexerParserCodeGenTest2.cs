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

        private static void AssertParserError(string src, string expectedMessagePart)
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);
            try
            {
                parser.Parse(tokens);
                Assert.Fail($"Expected parser error containing '{expectedMessagePart}' but no exception was thrown. Source:\n{src}");
            }
            catch (InvalidOperationException ex)
            {
                StringAssert.Contains(ex.Message, expectedMessagePart);
            }
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
        public void ParserErrorMessages_AreCovered()
        {
            var cases = new (string Source, string MessagePart)[]
            {
                ("start\n    a = 1\ninitialize\n    b = 2\n", "Only one of 'start' or 'initialize'"),
                ("module\n    M\ntype\n    T is object\n", "Only one of 'module', 'type', or 'trait'"),
                ("properties\n    a = 1\nproperties\n    b = 2\n", "Duplicate section 'properties'"),
                ("start\n    a = 1\nproperties\n    b = 2\n", "is out of order after 'start'"),
                ("a = 1\nfunctions\n    F()\n", "Sections are not allowed after implicit start statements"),
                ("type\n    MyType is object\nstart\n    a = 1\n", "start section is only allowed in module files"),
                ("start\n    WriteLine \"x\")\n", "Expected '(' after WriteLine."),
                ("start\n    WriteLine(1)\n", "Expected string literal in WriteLine"),
                ("start\n    WriteLine(\"x\"\n", "Expected ')' after WriteLine argument."),
                ("trait\n    MyTrait is object\n", "Unexpected inheritance in trait declaration"),
                ("type\n    MyType object\n", "must include an 'is' base type"),
                ("type\n    MyType is\n", "Missing base type after 'is'."),
                ("type\n    MyType is object has\n", "Missing trait list after 'has'."),
                ("use\n    a/b.h as\n", "Expected alias identifier after 'as'"),
                ("use\n    a/b.h as alias\n", "File path use statements cannot specify an alias"),
                ("start\n    a = b if c\n", "Conditional expressions require an 'else' branch"),
                ("start\n    a = 1 == 2 == 3\n", "Only one consecutive equality expression is allowed"),
                ("start\n    a = 1 < 2 < 3\n", "Only one consecutive relational expression is allowed"),
                ("start\n    a = 1:2:3\n", "Only one consecutive pair or range expression is allowed"),
                ("start\n    a = !!b\n", "Unary operators cannot be repeated consecutively"),
                ("properties\n    a int\n", "Property declarations must use assignment"),
                ("start\n    = 1\n", "Assignment statements require left and right expressions"),
                ("start\n    has\n", "Has statements require a condition"),
                ("start\n    if\n", "If statements require a condition expression"),
                ("start\n    match\n", "Match statements require an expression"),
                ("start\n    when\n", "When statements require a condition"),
                ("start\n    while\n", "While statements require a condition"),
                ("start\n    for in in xs\n", "For statements require the 'in' keyword and a variable name"),
                ("functions\n    Add a int\n", "Function declarations require a parameter list"),
                ("functions\n    (a int) int\n", "Function declarations require a name"),
                ("functions\n    Add(a, b int) int\n", "missing the type"),
                ("functions\n    Add(int, b int) int\n", "missing the name"),
                ("functions\n    Add(a int, , b int) int\n", "missing the name and type"),
                ("start(\n", "Unterminated parameter list in section header")
            };

            foreach (var (source, messagePart) in cases)
            {
                AssertParserError(source, messagePart);
            }
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

// LLVM Compiler for the Puma programming language
//   as defined in the document "The Puma Programming Language Specification"
//   available at https://github.com/ThePumaProgrammingLanguage
//
// Copyright © 2024-2026 by Darryl Anthony Burchfield
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

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

        private static void AssertParserSuccess(string src)
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            Assert.IsTrue(ast.Count > 0, $"Expected non-empty AST. Source:\n{src}");
        }

        private static void AssertLexerHasUnknownToken(string src)
        {
            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(src);
            Assert.IsTrue(tokens.Any(t => t.Category == Puma.Lexer.TokenCategory.Unknown),
                $"Expected at least one Unknown token. Source:\n{src}");
        }

        private static void AssertLexerHasNoUnknownToken(string src)
        {
            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(src);
            Assert.IsFalse(tokens.Any(t => t.Category == Puma.Lexer.TokenCategory.Unknown),
                $"Expected no Unknown tokens. Source:\n{src}");
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
@"#include <stdint>
#include <String.hpp>

// functions
void Hello(void)
{
    auto s = String(""Hello, World!"");
    PrintLn(s);
}

int Add(int64_t a, int64_t b)
{
    return a + b;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void PropertiesFunctions_ArithmeticLogicalBitwise_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    a = -20
    b = 20
    c = true
    d = false

functions
    F()
        x = a + b
        x = a - b
        x = a * b
        x = a / b

        y = c and d
        y = c or d

        z = a & b
        z = a | b
        z = a ^ b
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "-", "20",
                "b", "=", "20",
                "c", "=", "true",
                "d", "=", "false",
                "functions",
                "F", "(", ")",
                "x", "=", "a", "+", "b",
                "x", "=", "a", "-", "b",
                "x", "=", "a", "*", "b",
                "x", "=", "a", "/", "b",
                "y", "=", "c", "and", "d",
                "y", "=", "c", "or", "d",
                "z", "=", "a", "&", "b",
                "z", "=", "a", "|", "b",
                "z", "=", "a", "^", "b"
            }, significant);

            var ast = parser.Parse(tokens);
            var properties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            Assert.AreEqual(4, properties.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, properties.Select(p => p.PropertyName).ToArray());

            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationName == "F");
            Assert.AreEqual(9, function.FunctionBody.Count);
            Assert.IsTrue(function.FunctionBody.All(n => n.Kind == NodeKind.AssignmentStatement));

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>
#include <stdbool>

// properties
auto a = (int64_t)-20;
auto b = (int64_t)20;
auto c = true;
auto d = false;

// functions
void F(void)
{
    x = a + b;
    x = a - b;
    x = a * b;
    x = a / b;
    y = c and d;
    y = c or d;
    z = a & b;
    z = a | b;
    z = a ^ b;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void PropertiesFunctions_ForLoops_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    a = -20
    b = 20
    myList = List()

functions
    F()
        for x in myList
            b = x

    G()
        for i in Range()
            a = i
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "-", "20",
                "b", "=", "20",
                "myList", "=", "List", "(", ")",
                "functions",
                "F", "(", ")",
                "for", "x", "in", "myList",
                "b", "=", "x",
                "G", "(", ")",
                "for", "i", "in", "Range", "(", ")",
                "a", "=", "i"
            }, significant);

            var ast = parser.Parse(tokens);
            var properties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            Assert.AreEqual(3, properties.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "myList" }, properties.Select(p => p.PropertyName).ToArray());

            var functions = ast.Where(n => n.Kind == NodeKind.FunctionDeclaration).ToList();
            Assert.AreEqual(2, functions.Count);
            CollectionAssert.AreEqual(new[] { "F", "G" }, functions.Select(f => f.FunctionDeclarationName).ToArray());
            Assert.AreEqual(NodeKind.ForStatement, functions[0].FunctionBody[0].Kind);
            Assert.AreEqual(NodeKind.ForStatement, functions[1].FunctionBody[0].Kind);

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>

// properties
auto a = (int64_t)-20;
auto b = (int64_t)20;
auto myList = List();

// functions
void F(void)
{
    for (auto x : myList)
    {
        b = x;
    }
}

void G(void)
{
    for (auto i : Range())
    {
        a = i;
    }
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void PropertiesFunctions_MatchWhileRepeat_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    a = 5
    b = 10

functions
    F()
        match a
            when a < b
                a = b
            when a > b
                b = a
            when a == b
                a = b + 1

    G()
        while a < b
            a++

    H()
        repeat
            b--
            if b <= a
                break
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "5",
                "b", "=", "10",
                "functions",
                "F", "(", ")",
                "match", "a",
                "when", "a", "<", "b",
                "a", "=", "b",
                "when", "a", ">", "b",
                "b", "=", "a",
                "when", "a", "==", "b",
                "a", "=", "b", "+", "1",
                "G", "(", ")",
                "while", "a", "<", "b",
                "a", "++",
                "H", "(", ")",
                "repeat",
                "b", "--",
                "if", "b", "<=", "a",
                "break"
            }, significant);

            var ast = parser.Parse(tokens);
            var functions = ast.Where(n => n.Kind == NodeKind.FunctionDeclaration).ToList();
            Assert.AreEqual(3, functions.Count);
            Assert.AreEqual("F", functions[0].FunctionDeclarationName);
            Assert.AreEqual("G", functions[1].FunctionDeclarationName);
            Assert.AreEqual("H", functions[2].FunctionDeclarationName);
            Assert.AreEqual(NodeKind.MatchStatement, functions[0].FunctionBody[0].Kind);
            Assert.AreEqual(NodeKind.WhileStatement, functions[1].FunctionBody[0].Kind);
            Assert.AreEqual(NodeKind.RepeatStatement, functions[2].FunctionBody[0].Kind);

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>
#include <stdbool>

// properties
auto a = (int64_t)5;
auto b = (int64_t)10;

// functions
void F(void)
{
    switch (a)
    {
        case (a < b):
            a = b;
            break;
        case (a > b):
            b = a;
            break;
        case (a == b):
            a = b + 1;
            break;
    }
}

void G(void)
{
    while (a < b)
    {
        a++;
    }
}

void H(void)
{
    do
    {
        b--;
        if (b <= a)
        {
            break;
        }
    } while (true);
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void PropertiesAndFunctions_IfElseExamples_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    a = 1
    b = 2
functions
    F()
        if a != b
            a = b

    G()
        if a == b
            a += 1
        else
            a = b

    H()
        if a == b
            a += 1
        else if a < b
            a = b + 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "1",
                "b", "=", "2",
                "functions",
                "F", "(", ")",
                "if", "a", "!=", "b",
                "a", "=", "b",
                "G", "(", ")",
                "if", "a", "==", "b",
                "a", "+=", "1",
                "else",
                "a", "=", "b",
                "H", "(", ")",
                "if", "a", "==", "b",
                "a", "+=", "1",
                "else", "if", "a", "<", "b",
                "a", "=", "b", "+", "1"
            }, significant);

            var ast = parser.Parse(tokens);
            var properties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            Assert.AreEqual(2, properties.Count);
            CollectionAssert.AreEqual(new[] { "a", "b" }, properties.Select(p => p.PropertyName).ToArray());

            var functions = ast.Where(n => n.Kind == NodeKind.FunctionDeclaration).ToList();
            Assert.AreEqual(3, functions.Count);
            CollectionAssert.AreEqual(new[] { "F", "G", "H" }, functions.Select(f => f.FunctionDeclarationName).ToArray());
            Assert.AreEqual(NodeKind.IfStatement, functions[0].FunctionBody[0].Kind);
            Assert.AreEqual(NodeKind.IfStatement, functions[1].FunctionBody[0].Kind);
            Assert.AreEqual(NodeKind.IfStatement, functions[2].FunctionBody[0].Kind);

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>

// properties
auto a = (int64_t)1;
auto b = (int64_t)2;

// functions
void F(void)
{
    if (a != b)
    {
        a = b;
    }
}

void G(void)
{
    if (a == b)
    {
        a += 1;
    }
    else
    {
        a = b;
    }
}

void H(void)
{
    if (a == b)
    {
        a += 1;
    }
    else
    {
        a = b + 1;
    }
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
    F(int, b int) int
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
                "F", "(", "int", ",", "b", "int", ")", "int",
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
        public void ParserErrorMessages_AreAvoided_WithValidInputs()
        {
            var validCases = new[]
            {
                "start\n    a = 1\n",
                "module\n    M\n",
                "properties\n    a = 1\nstart\n    b = 2\n",
                "functions\n    F()\n",
                "module\n    M\nstart\n    a = 1\n",
                "start\n    WriteLine(\"x\")\n",
                "trait\n    MyTrait\n",
                "type\n    MyType is object\n",
                "type\n    MyType is object has IPrintable\n",
                "use\n    System.IO as IO\n",
                "use\n    a/b.h\n",
                "start\n    a = b if c else d\n",
                "start\n    a = 1 == 2\n",
                "start\n    a = 1 < 2\n",
                "start\n    a = 1:2\n",
                "start\n    a = !b\n",
                "properties\n    a = 1\n",
                "start\n    a = 1\n",
                "start\n    has x\n",
                "start\n    if x\n        a = 1\n",
                "start\n    match x\n        when 1\n            a = 1\n",
                "start\n    when x\n        a = 1\n",
                "start\n    while x\n        a = 1\n",
                "start\n    for i in xs\n        a = i\n",
                "functions\n    Add(a int) int\n        return a\n",
                "functions\n    Add(a int, b int) int\n        return a + b\n",
                "functions\n    Add(a int, c int) int\n        return a + c\n",
                "start()\n    a = 1\n"
            };

            foreach (var source in validCases)
            {
                AssertParserSuccess(source);
            }
        }

        [TestMethod]
        public void FunctionsExample_EmptyParameterList_Parser_Accepts()
        {
            const string src =
@"functions
    F() int
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
                "F", "(", ")", "int",
                "return", "1"
            }, significant);

            var ast = parser.Parse(tokens);
            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationName == "F");
            Assert.AreEqual(0, function.FunctionParameterList.Count);
        }

        [TestMethod]
        public void FunctionsExample_MissingParameterListAndReturnType_DefaultToVoid()
        {
            const string src =
@"functions
    F()
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
                "F", "(", ")",
                "x", "=", "1"
            }, significant);

            var ast = parser.Parse(tokens);
            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationName == "F");
            Assert.AreEqual(0, function.FunctionParameterList.Count);
            Assert.IsTrue(string.IsNullOrWhiteSpace(function.FunctionDeclarationReturnType));

            var generated = codegen.Generate(ast);
            var expected =
@"// functions
void F(void)
{
    x = 1;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void LexerErrorPaths_AreCovered_WithUnknownTokens()
        {
            var invalidCases = new[]
            {
                "start\n    s = \"\\q\"\n",
                "start\n    c = '\\q'\n",
                "start\n    n = 0b102\n",
                "start\n    a = @\n"
            };

            foreach (var source in invalidCases)
            {
                AssertLexerHasUnknownToken(source);
            }
        }

        [TestMethod]
        public void LexerErrorPaths_AreAvoided_WithValidInputs()
        {
            var validCases = new[]
            {
                "start\n    s = \"\\n\"\n",
                "start\n    c = '\\n'\n",
                "start\n    n = 0b1010\n",
                "start\n    a = 1\n"
            };

            foreach (var source in validCases)
            {
                AssertLexerHasNoUnknownToken(source);
            }
        }

        [TestMethod]
        public void CodegenFallbackPaths_AreCovered_WithIncompleteAstData()
        {
            var ast = new List<Node>
            {
                new Node(Puma.Parser.Section.Properties),
                Node.CreatePropertyDeclaration("p", "mysteryType", null),
                new Node(Puma.Parser.Section.Start)
            };

            var codegen = new Puma.Codegen();
            var generated = codegen.Generate(ast);

            StringAssert.Contains(generated, "mysteryType p = {0};");
            StringAssert.Contains(generated, "int main()");
        }

        [TestMethod]
        public void CodegenFallbackPaths_AreAvoided_WithValidInput()
        {
            const string src =
@"properties
    p = 1 int

start
    q = 2 int
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.IsFalse(generated.Contains("{0}", StringComparison.Ordinal));
            Assert.IsFalse(generated.Contains(" = ;", StringComparison.Ordinal));
        }
    }
}

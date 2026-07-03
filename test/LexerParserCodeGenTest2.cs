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

        [TestMethod]
        public void StartExample_CharacterLiterals_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"start
    first = 'A'
    newline = '\n'
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "start",
                "first", "=", "'A'",
                "newline", "=", "'\\n'"
            }, significant);

            Assert.AreEqual(Puma.Lexer.TokenCategory.CharLiteral, significantTokens[3].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.CharLiteral, significantTokens[6].Category);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            var expected =
@"#include <Character.hpp>

// start
int main()
{
    auto first = Character('A');
    auto newline = Character('\n');

    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void FunctionsExample_CharParameter_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"functions
    DoNothing(c char)
        return
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
                "DoNothing", "(", "c", "char", ")",
                "return"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);
            var expected =
@"#include <Character.hpp>

// functions
void DoNothing(Puma::Type::Character c)
{
    return;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Lexer_CharacterEscapes_HexAndUnicode_AreTokenizedWithoutUnknown()
        {
            const string src =
@"start
    a = '\x41'
    b = '\u0041'
    c = '\U00000041'
";

            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);

            Assert.IsFalse(significantTokens.Any(t => t.Category == Puma.Lexer.TokenCategory.Unknown));
            Assert.IsTrue(significantTokens.Any(t => t.TokenText == "'\\x41'" && t.Category == Puma.Lexer.TokenCategory.CharLiteral));
            Assert.IsTrue(significantTokens.Any(t => t.TokenText == "'\\u0041'" && t.Category == Puma.Lexer.TokenCategory.CharLiteral));
            Assert.IsTrue(significantTokens.Any(t => t.TokenText == "'\\U00000041'" && t.Category == Puma.Lexer.TokenCategory.CharLiteral));
        }

        [TestMethod]
        public void Lexer_CharacterEscape_WithInvalidUnicodeHex_IsUnknown()
        {
            const string src =
@"start
    c = '\u00G1'
";

            AssertLexerHasUnknownToken(src);
        }

        [TestMethod]
        public void PropertiesExample_CharacterLiterals_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    first = 'A'
    newline = '\n'
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
                "first", "=", "'A'",
                "newline", "=", "'\\n'"
            }, significant);

            Assert.AreEqual(Puma.Lexer.TokenCategory.CharLiteral, significantTokens[3].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.CharLiteral, significantTokens[6].Category);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);
            var expected =
@"#include <Character.hpp>

// properties
auto first = Character('A');
auto newline = Character('\n');
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void InitializeExample_CharacterAssignment_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    current = 'A'

initialize
    current = '\n'
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
                "current", "=", "'A'",
                "initialize",
                "current", "=", "'\\n'"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);
            var expected =
@"#include <Character.hpp>

// properties
auto current = Character('A');

// initialize
void initialize(void)
{
    current = Character('\n');
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Functions_CharacterConditionalAndAssignmentFlow_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"functions
    Pick(a char, b char) char
        result = a if a == b else b
        mirror = result
        return mirror
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
                "Pick", "(", "a", "char", ",", "b", "char", ")", "char",
                "result", "=", "a", "if", "a", "==", "b", "else", "b",
                "mirror", "=", "result",
                "return", "mirror"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);
            var expected =
@"#include <Character.hpp>

// functions
char Pick(Puma::Type::Character a, Puma::Type::Character b)
{
    auto result = ((a == b) ? a : b);
    auto mirror = result;
    return mirror;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Functions_ConstCharacterParameterMutation_Parser_ThrowsCompilerError()
        {
            const string src =
@"functions
    Mutate(c char const)
        c = 'Z'
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to constant parameter");
            StringAssert.Contains(ex.Message, "c");
        }

        private static void AssertParserError(string src, string expectedMessagePart)
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();
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
            var codegen = new Puma.Codegen();
            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            Assert.IsTrue(ast.Count > 0, $"Expected non-empty AST. Source:\n{src}");

            var generated = codegen.Generate(ast);
            Assert.IsFalse(string.IsNullOrWhiteSpace(generated), $"Expected non-empty generated output. Source:\n{src}");
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
            Assert.AreEqual("Hello", functions[0].FunctionDeclarationNode.FunctionDeclarationName);
            Assert.AreEqual("Add", functions[1].FunctionDeclarationNode.FunctionDeclarationName);
            Assert.AreEqual("int", functions[1].FunctionDeclarationNode.FunctionDeclarationReturnType);

            var helloBody = functions[0].FunctionDeclarationNode.FunctionBody;
            Assert.AreEqual(2, helloBody.Count);
            Assert.AreEqual(NodeKind.AssignmentStatement, helloBody[0].Kind);
            Assert.AreEqual(NodeKind.FunctionCall, helloBody[1].Kind);

            var addBody = functions[1].FunctionDeclarationNode.FunctionBody;
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
        public void Functions_MixedArgumentExpressions_WithTypedParameters_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"functions
    Consume(a int32, b int32, c int32)
    Caller()
        x = 1 int16
        y = 2 int16
        z = 3 int16
        Consume(x + y, x if x > 0 else y, z)
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
                "Consume", "(", "a", "int32", ",", "b", "int32", ",", "c", "int32", ")",
                "Caller", "(", ")",
                "x", "=", "1", "int16",
                "y", "=", "2", "int16",
                "z", "=", "3", "int16",
                "Consume", "(", "x", "+", "y", ",", "x", "if", "x", ">", "0", "else", "y", ",", "z", ")"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>

// functions
void Consume(int32_t a, int32_t b, int32_t c)
{
}

void Caller(void)
{
    x = 1;
    y = 2;
    z = 3;
    Consume((x + y), ((x > 0) ? x : y), z);
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
        y = not c

        x = a << b
        x = a >> b

        z = a & b
        z = a | b
        z = a ^ b
        x = a % b
        x = ~a
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
                "y", "=", "not", "c",
                "x", "=", "a", "<<", "b",
                "x", "=", "a", ">>", "b",
                "z", "=", "a", "&", "b",
                "z", "=", "a", "|", "b",
                "z", "=", "a", "^", "b",
                "x", "=", "a", "%", "b",
                "x", "=", "~", "a"
            }, significant);

            var ast = parser.Parse(tokens);
            var properties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            Assert.AreEqual(4, properties.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, properties.Select(p => p.PropertyDeclarationNode.PropertyName).ToArray());

            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationNode.FunctionDeclarationName == "F");
            Assert.AreEqual(14, function.FunctionDeclarationNode.FunctionBody.Count);
            Assert.IsTrue(function.FunctionDeclarationNode.FunctionBody.All(n => n.Kind == NodeKind.AssignmentStatement));

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
    y = not c;
    x = a << b;
    x = a >> b;
    z = a & b;
    z = a | b;
    z = a ^ b;
    x = a % b;
    x = ~a;
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
            CollectionAssert.AreEqual(new[] { "a", "b", "myList" }, properties.Select(p => p.PropertyDeclarationNode.PropertyName).ToArray());

            var functions = ast.Where(n => n.Kind == NodeKind.FunctionDeclaration).ToList();
            Assert.AreEqual(2, functions.Count);
            CollectionAssert.AreEqual(new[] { "F", "G" }, functions.Select(f => f.FunctionDeclarationNode.FunctionDeclarationName).ToArray());
            Assert.AreEqual(NodeKind.ForStatement, functions[0].FunctionDeclarationNode.FunctionBody[0].Kind);
            Assert.AreEqual(NodeKind.ForStatement, functions[1].FunctionDeclarationNode.FunctionBody[0].Kind);

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

    I()
        repeat
            a++
            if a >= b
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
                "break",
                "I", "(", ")",
                "repeat",
                "a", "++",
                "if", "a", ">=", "b",
                "break"
            }, significant);

            var ast = parser.Parse(tokens);
            var functions = ast.Where(n => n.Kind == NodeKind.FunctionDeclaration).ToList();
            Assert.AreEqual(4, functions.Count);
            Assert.AreEqual("F", functions[0].FunctionDeclarationNode.FunctionDeclarationName);
            Assert.AreEqual("G", functions[1].FunctionDeclarationNode.FunctionDeclarationName);
            Assert.AreEqual("H", functions[2].FunctionDeclarationNode.FunctionDeclarationName);
            Assert.AreEqual("I", functions[3].FunctionDeclarationNode.FunctionDeclarationName);
            Assert.AreEqual(NodeKind.MatchStatement, functions[0].FunctionDeclarationNode.FunctionBody[0].Kind);
            Assert.AreEqual(NodeKind.WhileStatement, functions[1].FunctionDeclarationNode.FunctionBody[0].Kind);
            Assert.AreEqual(NodeKind.RepeatStatement, functions[2].FunctionDeclarationNode.FunctionBody[0].Kind);
            Assert.AreEqual(NodeKind.RepeatStatement, functions[3].FunctionDeclarationNode.FunctionBody[0].Kind);

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

void I(void)
{
    do
    {
        a++;
        if (a >= b)
        {
            break;
        }
    } while (true);
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Functions_MatchWhenErrorCatchYield_Expressions_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    a = 1
    b = 2

functions
    F()
        match b
            when a + 1
                return b
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
                "match", "b",
                "when", "a", "+", "1",
                "return", "b",
            }, significant);

            var ast = parser.Parse(tokens);
            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationNode.FunctionDeclarationName == "F");
            Assert.AreEqual(1, function.FunctionDeclarationNode.FunctionBody.Count);
            Assert.AreEqual(NodeKind.MatchStatement, function.FunctionDeclarationNode.FunctionBody[0].Kind);

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>

// properties
auto a = (int64_t)1;
auto b = (int64_t)2;

// functions
void F(void)
{
    switch (b)
    {
        case (a + 1):
            return b;
            break;
    }
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
            CollectionAssert.AreEqual(new[] { "a", "b" }, properties.Select(p => p.PropertyDeclarationNode.PropertyName).ToArray());

            var functions = ast.Where(n => n.Kind == NodeKind.FunctionDeclaration).ToList();
            Assert.AreEqual(3, functions.Count);
            CollectionAssert.AreEqual(new[] { "F", "G", "H" }, functions.Select(f => f.FunctionDeclarationNode.FunctionDeclarationName).ToArray());
            Assert.AreEqual(NodeKind.IfStatement, functions[0].FunctionDeclarationNode.FunctionBody[0].Kind);
            Assert.AreEqual(NodeKind.IfStatement, functions[1].FunctionDeclarationNode.FunctionBody[0].Kind);
            Assert.AreEqual(NodeKind.IfStatement, functions[2].FunctionDeclarationNode.FunctionBody[0].Kind);

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
            var codegen = new Puma.Codegen();

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
        public void ParserSectionDiagnostics_DuplicateAndOrderMessages_AreStable()
        {
            var cases = new (string Source, string ExpectedMessage)[]
            {
                (
                    "properties\n    a = 1\nproperties\n    b = 2\n",
                    "Duplicate section 'properties'. Remove the extra 'properties' section."),
                (
                    "start\n    a = 1\nproperties\n    b = 2\n",
                    "Section 'properties' is out of order after 'start'. Fix: move 'properties' to match this order (all optional): use, type/trait/module, enums, records, properties, start/initialize, finalize, functions."),
                (
                    "initialize\n    a = 1\nstart\n    b = 2\n",
                    "Only one of 'start' or 'initialize' sections may appear in a file.")
            };

            foreach (var (source, expectedMessage) in cases)
            {
                var lexer = new Puma.Lexer();
                var parser = new Puma.Parser();
                var tokens = lexer.Tokenize(source);

                var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
                Assert.AreEqual(expectedMessage, ex.Message);
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
            var codegen = new Puma.Codegen();

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
            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationNode.FunctionDeclarationName == "F");
            Assert.AreEqual(0, function.FunctionDeclarationNode.FunctionParameterList.Count);

            var generated = codegen.Generate(ast);
            var expected =
@"// functions
int F(void)
{
    return 1;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
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
            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationNode.FunctionDeclarationName == "F");
            Assert.AreEqual(0, function.FunctionDeclarationNode.FunctionParameterList.Count);
            Assert.IsTrue(string.IsNullOrWhiteSpace(function.FunctionDeclarationNode.FunctionDeclarationReturnType));

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
        public void Functions_DefaultParameterValues_AndCallSiteDefaults_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"functions
    LogValues(a int, b int = 10, c int = 20)
    Caller()
        LogValues(1)
        LogValues(1, 2)
        LogValues(1, 2, 3)
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
                "LogValues", "(", "a", "int", ",", "b", "int", "=", "10", ",", "c", "int", "=", "20", ")",
                "Caller", "(", ")",
                "LogValues", "(", "1", ")",
                "LogValues", "(", "1", ",", "2", ")",
                "LogValues", "(", "1", ",", "2", ",", "3", ")"
            }, significant);

            var ast = parser.Parse(tokens);
            var logValues = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationNode.FunctionDeclarationName == "LogValues");
            Assert.AreEqual(3, logValues.FunctionDeclarationNode.FunctionParameterList.Count);
            Assert.AreEqual("10", logValues.FunctionDeclarationNode.FunctionParameterList[1].DefaultValue);
            Assert.AreEqual("20", logValues.FunctionDeclarationNode.FunctionParameterList[2].DefaultValue);

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>

// functions
void LogValues(int64_t a, int64_t b, int64_t c)
{
}

void Caller(void)
{
    LogValues(1, 10, 20);
    LogValues(1, 2, 20);
    LogValues(1, 2, 3);
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Start_NumericLiteralEdgeCases_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"start
    decNeg = -42
    exp64 = 1.25e+3 flt64
    exp32 = 6.5e-1 flt32
    hex32 = 0x1F uint32
    bin8 = 0b1010 uint8
    oct16 = 0o17 uint16
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            Assert.IsFalse(tokens.Any(t => t.Category == Puma.Lexer.TokenCategory.Unknown), "Expected no Unknown tokens for numeric edge cases.");

            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "start",
                "decNeg", "=", "-", "42",
                "exp64", "=", "1.25e+3", "flt64",
                "exp32", "=", "6.5e-1", "flt32",
                "hex32", "=", "0x1F", "uint32",
                "bin8", "=", "0b1010", "uint8",
                "oct16", "=", "0o17", "uint16"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);
            var expected =
@"#include <cstdint>

// start
int main()
{
    auto decNeg = (int64_t)-42;
    auto exp64 = 1.25e+3;
    auto exp32 = 6.5e-1;
    auto hex32 = (uint32_t)0x1F;
    auto bin8 = (uint8_t)0b1010;
    auto oct16 = (uint16_t)0o17;

    return 0;
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

            var expected =
@"mysteryType p = {0};

// start
int main()
{
    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
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

            var expected =
@"#include <cstdint>

// properties
auto p = (int64_t)1;

// start
int main()
{
    auto q = (int64_t)2;
    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
            Assert.IsFalse(generated.Contains("{0}", StringComparison.Ordinal));
            Assert.IsFalse(generated.Contains(" = ;", StringComparison.Ordinal));
        }
    }
}

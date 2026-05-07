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
    public class LexerParserCodeGenTest3
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
        public void Sanity_NewUnitTestFile_IsIncluded()
        {
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void PropertiesFunctions_OperatorPrecedenceMix_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    a = -20
    b = 20
    c = true
    d = false
    e = 40
    f = -40
    g = true
    h = false

functions
    F()
        x = a + b - e
        x = a - b + f
        x = a * b / e
        x = a / b * f

        x = a + b * e
        x = a - b / f
        x = a * b + e
        x = a / b - f

        x = a + b / e
        x = a - b * f
        x = a / b + e
        x = a * b - f
        x = ~a % b
        x = ~(a % b)

        y = c and d or g
        y = c or d and h

        z = a & b | e
        z = a | b & f
        z = a & b ^ e
        z = a ^ b & e
        z = a | b ^ f
        z = a ^ b | f
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            Assert.IsFalse(tokens.Any(t => t.Category == Puma.Lexer.TokenCategory.Unknown), "Expected lexer to tokenize all operators without Unknown tokens.");

            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "-", "20",
                "b", "=", "20",
                "c", "=", "true",
                "d", "=", "false",
                "e", "=", "40",
                "f", "=", "-", "40",
                "g", "=", "true",
                "h", "=", "false",
                "functions",
                "F", "(", ")",
                "x", "=", "a", "+", "b", "-", "e",
                "x", "=", "a", "-", "b", "+", "f",
                "x", "=", "a", "*", "b", "/", "e",
                "x", "=", "a", "/", "b", "*", "f",
                "x", "=", "a", "+", "b", "*", "e",
                "x", "=", "a", "-", "b", "/", "f",
                "x", "=", "a", "*", "b", "+", "e",
                "x", "=", "a", "/", "b", "-", "f",
                "x", "=", "a", "+", "b", "/", "e",
                "x", "=", "a", "-", "b", "*", "f",
                "x", "=", "a", "/", "b", "+", "e",
                "x", "=", "a", "*", "b", "-", "f",
                "x", "=", "~", "a", "%", "b",
                "x", "=", "~", "(", "a", "%", "b", ")",
                "y", "=", "c", "and", "d", "or", "g",
                "y", "=", "c", "or", "d", "and", "h",
                "z", "=", "a", "&", "b", "|", "e",
                "z", "=", "a", "|", "b", "&", "f",
                "z", "=", "a", "&", "b", "^", "e",
                "z", "=", "a", "^", "b", "&", "e",
                "z", "=", "a", "|", "b", "^", "f",
                "z", "=", "a", "^", "b", "|", "f"
            }, significant);

            var ast = parser.Parse(tokens);
            var properties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            Assert.AreEqual(8, properties.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d", "e", "f", "g", "h" }, properties.Select(p => p.PropertyName).ToArray());

            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationName == "F");
            Assert.AreEqual(22, function.FunctionBody.Count);
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
auto e = (int64_t)40;
auto f = (int64_t)-40;
auto g = true;
auto h = false;

// functions
void F(void)
{
    x = (a + b) - e;
    x = (a - b) + f;
    x = (a * b) / e;
    x = (a / b) * f;
    x = a + (b * e);
    x = a - (b / f);
    x = (a * b) + e;
    x = (a / b) - f;
    x = a + (b / e);
    x = a - (b * f);
    x = (a / b) + e;
    x = (a * b) - f;
    x = ~a % b;
    x = ~(a % b);
    y = (c and d) or g;
    y = c or (d and h);
    z = (a & b) | e;
    z = a | (b & f);
    z = (a & b) ^ e;
    z = a ^ (b & e);
    z = a | (b ^ f);
    z = (a ^ b) | f;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void PropertiesStart_ReassignConstant_ThrowsParserError()
        {
            const string src =
@"properties
    PI = 3.14159 constant
    MAX_RADIUS = 5.0 constant

start
    MAX_RADIUS = 10
    PI = 3.14
    r = MAX_RADIUS
    area = 2.0 * PI * (r * r)
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to constant property");
        }

        [TestMethod]
        public void PropertiesStart_ReadwriteByDefault_Reassignment_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    max_length = 100
    max_width = 50.0

start
    max_length = 200
    max_width = 25.0
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "max_length", "=", "100",
                "max_width", "=", "50.0",
                "start",
                "max_length", "=", "200",
                "max_width", "=", "25.0"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);
            var expected =
@"#include <cstdint>

auto max_length = (int64_t)100;
auto max_width = (double)50.0;

// start
int main()
{
    max_length = (int64_t)200;
    max_width = (double)25.0;

    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void TypePropertiesAndFunctions_DefaultVisibility_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"type
    DemoType is object

properties
    value = 1

functions
    GetValue() int
        return value
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "type", "DemoType", "is", "object",
                "properties",
                "value", "=", "1",
                "functions",
                "GetValue", "(", ")", "int",
                "return", "value"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>

class DemoType : public object
{
    // properties
    protected:
    auto value = (int64_t)1;

    // functions
    public:
    int64_t GetValue()
    {
        return value;
    }
};
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }
        [TestMethod]
        public void TypePublicPropertyAndPrivateFunction_VisibilityMapsToCpp_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"type
    DemoType is object

properties
    value = 1 public

functions
    GetValue() private int
        return value
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "type", "DemoType", "is", "object",
                "properties",
                "value", "=", "1", "public",
                "functions",
                "GetValue", "(", ")", "int", "private",
                "return", "value"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>

class DemoType : public object
{
    // properties
    public:
    auto value = (int64_t)1;

    // functions
    protected:
    int64_t GetValue()
    {
        return value;
    }
};
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void PropertiesStart_ConstantsAndAreaExpression_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    PI = 3.14159 constant
    MAX_RADIUS = 5.0 constant

start
    r = MAX_RADIUS
    area = PI * (r * r)
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "PI", "=", "3.14159", "constant",
                "MAX_RADIUS", "=", "5.0", "constant",
                "start",
                "r", "=", "MAX_RADIUS",
                "area", "=", "PI", "*", "(", "r", "*", "r", ")"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            StringAssert.Contains(generated, "const auto PI = (double)3.14159;");
            StringAssert.Contains(generated, "const auto MAX_RADIUS = (double)5.0;");
            StringAssert.Contains(generated, "// start");
            StringAssert.Contains(generated, "int main()");
            StringAssert.Contains(generated, "auto r = MAX_RADIUS;");
            StringAssert.Contains(generated, "auto area =");
            StringAssert.Contains(generated, "PI");
            StringAssert.Contains(generated, "r * r");

            var expected =
@"// properties
const auto PI = (double)3.14159;
const auto MAX_RADIUS = (double)5.0;

// start
int main()
{
    auto r = MAX_RADIUS;
    auto area = PI * (r * r);

    return 0;
}";
            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void PropertiesFunctions_AssignmentOperators_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    a = 64
    b = 8

functions
    F()
        x = a
        x /= b
        x *= b
        x %= b
        x += b
        x -= b
        x <<= 1
        x >>= 1
        x &= b
        x ^= b
        x |= b
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            Assert.IsFalse(tokens.Any(t => t.Category == Puma.Lexer.TokenCategory.Unknown), "Expected lexer to tokenize assignment operators without Unknown tokens.");

            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "64",
                "b", "=", "8",
                "functions",
                "F", "(", ")",
                "x", "=", "a",
                "x", "/=", "b",
                "x", "*=", "b",
                "x", "%=", "b",
                "x", "+=", "b",
                "x", "-=", "b",
                "x", "<<=", "1",
                "x", ">>=", "1",
                "x", "&=", "b",
                "x", "^=", "b",
                "x", "|=", "b"
            }, significant);

            var ast = parser.Parse(tokens);
            var properties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            Assert.AreEqual(2, properties.Count);
            CollectionAssert.AreEqual(new[] { "a", "b" }, properties.Select(p => p.PropertyName).ToArray());

            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationName == "F");
            Assert.AreEqual(11, function.FunctionBody.Count);
            Assert.IsTrue(function.FunctionBody.All(n => n.Kind == NodeKind.AssignmentStatement));

            var generated = codegen.Generate(ast);
            StringAssert.Contains(generated, "x = a;");
            StringAssert.Contains(generated, "x /= b;");
            StringAssert.Contains(generated, "x *= b;");
            StringAssert.Contains(generated, "x %= b;");
            StringAssert.Contains(generated, "x += b;");
            StringAssert.Contains(generated, "x -= b;");
            StringAssert.Contains(generated, "x <<= 1;");
            StringAssert.Contains(generated, "x >>= 1;");
            StringAssert.Contains(generated, "x &= b;");
            StringAssert.Contains(generated, "x ^= b;");
            StringAssert.Contains(generated, "x |= b;");
        }

        [TestMethod]
        public void PropertiesFunctions_PrefixPostfixMemberAccess_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    counter = 0
    obj = state

functions
    F()
        preInc = ++counter
        preDec = --counter
        counter++
        counter--
        memberVal = obj.value
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            Assert.IsFalse(tokens.Any(t => t.Category == Puma.Lexer.TokenCategory.Unknown), "Expected lexer to tokenize increment/decrement/member-access operators without Unknown tokens.");

            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "counter", "=", "0",
                "obj", "=", "state",
                "functions",
                "F", "(", ")",
                "preInc", "=", "++", "counter",
                "preDec", "=", "--", "counter",
                "counter", "++",
                "counter", "--",
                "memberVal", "=", "obj", ".", "value"
            }, significant);

            var ast = parser.Parse(tokens);
            var properties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            Assert.AreEqual(2, properties.Count);
            CollectionAssert.AreEqual(new[] { "counter", "obj" }, properties.Select(p => p.PropertyName).ToArray());

            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationName == "F");
            Assert.AreEqual(5, function.FunctionBody.Count);
            Assert.IsTrue(function.FunctionBody.All(n => n.Kind == NodeKind.AssignmentStatement));

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>

// properties
auto counter = (int64_t)0;
auto obj = state;

// functions
void F(void)
{
    preInc = ++counter;
    preDec = --counter;
    counter++;
    counter--;
    memberVal = obj.value;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void UseSection_NamespaceAndFilePath_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"use
    ""System/IO""
    ""a/b.h""

start
    x = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "use",
                "\"System/IO\"",
                "\"a/b.h\"",
                "start",
                "x", "=", "1"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            StringAssert.Contains(generated, "#include");
            StringAssert.Contains(generated, "int main()");
            StringAssert.Contains(generated, "auto x = (int64_t)1;");
        }

        [TestMethod]
        public void Start_HasTraitStatement_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"start
    has trait Printable obj
        WriteLn(""Obj is printable!"")
        WriteLn(obj.ToString())
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "start",
                "has", "trait", "Printable", "obj",
                "WriteLn", "(", "\"Obj is printable!\"", ")",
                "WriteLn", "(", "obj", ".", "ToString", "(", ")", ")"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            StringAssert.Contains(generated, "if (obj != null && typeof(obj) == typeof(Printable))");
            StringAssert.Contains(generated, "WriteLn");
        }


        [TestMethod]
        public void PropertiesFunctions_IndexExpression_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    arr = Array(items, 2)

functions
    F()
        i = 0
        x = arr[i]
        x = arr[i + 1]
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "arr", "=", "Array", "(", "items", ",", "2", ")",
                "functions",
                "F", "(", ")",
                "i", "=", "0",
                "x", "=", "arr", "[", "i", "]",
                "x", "=", "arr", "[", "i", "+", "1", "]"
            }, significant);

            var ast = parser.Parse(tokens);
            var fn = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationName == "F");
            Assert.AreEqual(3, fn.FunctionBody.Count);

            var generated = codegen.Generate(ast);
            StringAssert.Contains(generated, "arr = Array(items,2)");
            StringAssert.Contains(generated, "x = arr[i];");
            StringAssert.Contains(generated, "x = arr[(i + 1)];");
        }

        [TestMethod]
        public void RecordsExample_BoolAndString_Packed_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"records
    MyRecord packed
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
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "records",
                "MyRecord", "packed",
                "a", "=", "false",
                "b", "=", "true",
                "c", "=", "bool",
                "d", "=", "\"\"",
                "e", "=", "str"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdbool>
#include <String.hpp>

// records
struct MyRecord [[gnu::packed]]
{
    auto a = false;
    auto b = true;
    auto c = false;
    auto d = String("""");
    auto e = String("""");
};
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }
    }
}

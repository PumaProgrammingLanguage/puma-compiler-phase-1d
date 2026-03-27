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
    public class LexerParserCodeGenTest
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
        public void StartExample_Integers_LexerParserCodegen_AreConsistent()
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
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

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

            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[3].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[6].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[10].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[14].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[18].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[22].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.Keyword, significantTokens[7].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.Keyword, significantTokens[11].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.Keyword, significantTokens[15].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.Keyword, significantTokens[19].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.Keyword, significantTokens[23].Category);

            var ast = parser.Parse(tokens);
            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToList();
            Assert.AreEqual(6, assignments.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d", "e", "f" }, assignments.Select(a => a.AssignmentLeft).ToArray());
            CollectionAssert.AreEqual(new[] { "1", "2", "3", "4", "5", "6" }, assignments.Select(a => a.AssignmentRightExpression?.Value).ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"#include <cstdint>

int main()
{
    auto a = (int64_t)1;
    auto b = (int64_t)2;
    auto c = (int64_t)3;
    auto d = (int32_t)4;
    auto e = (int16_t)5;
    auto f = (int8_t)6;
    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void TraitInitializeExample_Floats_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"trait
    MyTrait

initialize
    a = 1.1
    b = 2.2 flt
    c = 3.3 flt64
    d = 4.4 flt32
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "trait", "MyTrait",
                "initialize",
                "a", "=", "1.1",
                "b", "=", "2.2", "flt",
                "c", "=", "3.3", "flt64",
                "d", "=", "4.4", "flt32"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);
            var expected =
@"class MyTrait
{
public:
    MyTrait()
    {
        auto a = (double)1.1;
        auto b = (double)2.2;
        auto c = (double)3.3;
        auto d = (float)4.4;
    }
};
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void ModuleStartExample_Integers_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"module
    MyModule

start
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
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "module", "MyModule",
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

            var generated = codegen.Generate(ast);
            var expected =
@"#include <cstdint>

namespace MyModule
{
    int main()
    {
        auto a = (int64_t)1;
        auto b = (int64_t)2;
        auto c = (int64_t)3;
        auto d = (int32_t)4;
        auto e = (int16_t)5;
        auto f = (int8_t)6;
        return 0;
    }
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void TraitInitializeExample_BoolAndString_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"trait
    MyTrait

initialize
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
                "trait", "MyTrait",
                "initialize",
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
#include <string>

class MyTrait
{
public:
    MyTrait()
    {
        auto a = false;
        auto b = true;
        auto c = false;
        auto c = false;
        auto d = u8""""s;
        auto e = u8""""s;
    }
};
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }


        [TestMethod]
        public void StartExample_UnsignedIntegers_LexerParserCodegen_AreConsistent()
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
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "start",
                "b", "=", "2", "uint64",
                "c", "=", "3", "uint64",
                "d", "=", "4", "uint32",
                "e", "=", "5", "uint16",
                "f", "=", "6", "uint8"
            }, significant);

            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[3].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[7].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[11].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[15].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[19].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.Keyword, significantTokens[4].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.Keyword, significantTokens[8].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.Keyword, significantTokens[12].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.Keyword, significantTokens[16].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.Keyword, significantTokens[20].Category);

            var ast = parser.Parse(tokens);
            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToList();
            Assert.AreEqual(5, assignments.Count);
            CollectionAssert.AreEqual(new[] { "b", "c", "d", "e", "f" }, assignments.Select(a => a.AssignmentLeft).ToArray());
            CollectionAssert.AreEqual(new[] { "2", "3", "4", "5", "6" }, assignments.Select(a => a.AssignmentRightExpression?.Value).ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"#include <cstdint>

int main()
{
    auto b = (uint64_t)2;
    auto c = (uint64_t)3;
    auto d = (uint32_t)4;
    auto e = (uint16_t)5;
    auto f = (uint8_t)6;
    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void ModuleStartExample_UnsignedIntegers_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"module
    MyModule

start
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
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "module", "MyModule",
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

            var generated = codegen.Generate(ast);
            var expected =
@"#include <cstdint>

namespace MyModule
{
    int main()
    {
        auto b = (uint64_t)2;
        auto c = (uint64_t)3;
        auto d = (uint32_t)4;
        auto e = (uint16_t)5;
        auto f = (uint8_t)6;
        return 0;
    }
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void InitializeExample_Floats_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"initialize
    a = 1.1
    b = 2.2 flt
    c = 3.3 flt64
    d = 4.4 flt32
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "initialize",
                "a", "=", "1.1",
                "b", "=", "2.2", "flt",
                "c", "=", "3.3", "flt64",
                "d", "=", "4.4", "flt32"
            }, significant);

            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[3].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[6].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[10].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.NumericLiteral, significantTokens[14].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.Keyword, significantTokens[7].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.Keyword, significantTokens[11].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.Keyword, significantTokens[15].Category);

            var ast = parser.Parse(tokens);
            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToList();
            Assert.AreEqual(4, assignments.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, assignments.Select(a => a.AssignmentLeft).ToArray());
            CollectionAssert.AreEqual(new[] { "1.1", "2.2", "3.3", "4.4" }, assignments.Select(a => a.AssignmentRightExpression?.Value).ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"// initialize
void initialize(void)
{
    auto a = (double)1.1;
    auto b = (double)2.2;
    auto c = (double)3.3;
    auto d = (float)4.4;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void TypeInitializeExample_Floats_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"type
    MyType is object

initialize
    a = 1.1
    b = 2.2 flt
    c = 3.3 flt64
    d = 4.4 flt32
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "type", "MyType", "is", "object",
                "initialize",
                "a", "=", "1.1",
                "b", "=", "2.2", "flt",
                "c", "=", "3.3", "flt64",
                "d", "=", "4.4", "flt32"
            }, significant);

            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);
            var expected =
@"class MyType : public object
{
public:
    MyType()
    {
        auto a = (double)1.1;
        auto b = (double)2.2;
        auto c = (double)3.3;
        auto d = (float)4.4;
    }
};
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void InitializeExample_BoolAndString_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"initialize
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
                "initialize",
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
@"#include <stdbool>
#include <string>

// initialize
void initialize(void)
{
    auto a = false;
    auto b = true;
    auto c = false;
    auto d = u8""""s;
    auto e = u8""""s;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void TypeInitializeExample_BoolAndString_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"type
    MyType is object

initialize
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
                "type", "MyType", "is", "object",
                "initialize",
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
#include <string>

class MyType : public object
{
public:
    MyType()
    {
        auto a = false;
        auto b = true;
        auto c = false;
        auto d = u8""""s;
        auto e = u8""""s;
    }
};
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void PropertiesExample_BoolAndString_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
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
                "properties",
                "a", "=", "false",
                "b", "=", "true",
                "c", "=", "bool",
                "d", "=", "\"\"",
                "e", "=", "str"
            }, significant);

            var ast = parser.Parse(tokens);
            var properties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            Assert.AreEqual(5, properties.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d", "e" }, properties.Select(p => p.PropertyName).ToArray());
            CollectionAssert.AreEqual(new[] { "false", "true", "bool", "\"\"", "str" }, properties.Select(p => p.PropertyValue).ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdbool>
#include <string>

// properties
auto a = false;
auto b = true;
auto c = false;
auto d = u8""""s;
auto e = u8""""s;
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void PropertiesExample_Integers_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
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
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "1",
                "b", "=", "2", "int",
                "c", "=", "3", "int64",
                "d", "=", "4", "int32",
                "e", "=", "5", "int16",
                "f", "=", "6", "int8"
            }, significant);

            var ast = parser.Parse(tokens);
            var properties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            Assert.AreEqual(6, properties.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d", "e", "f" }, properties.Select(p => p.PropertyName).ToArray());
            CollectionAssert.AreEqual(new[] { "1", "2", "3", "4", "5", "6" }, properties.Select(p => p.PropertyValue).ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>

// properties
auto a = (int64_t)1;
auto b = (int64_t)2;
auto c = (int64_t)3;
auto d = (int32_t)4;
auto e = (int16_t)5;
auto f = (int8_t)6;
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void PropertiesExample_UnsignedIntegers_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    b = 2 uint
    c = 3 uint64
    d = 4 uint32
    e = 5 uint16
    f = 6 uint8
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
                "b", "=", "2", "uint",
                "c", "=", "3", "uint64",
                "d", "=", "4", "uint32",
                "e", "=", "5", "uint16",
                "f", "=", "6", "uint8"
            }, significant);

            var ast = parser.Parse(tokens);
            var properties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            Assert.AreEqual(5, properties.Count);
            CollectionAssert.AreEqual(new[] { "b", "c", "d", "e", "f" }, properties.Select(p => p.PropertyName).ToArray());
            CollectionAssert.AreEqual(new[] { "2", "3", "4", "5", "6" }, properties.Select(p => p.PropertyValue).ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>

// properties
auto b = (uint64_t)2;
auto c = (uint64_t)3;
auto d = (uint32_t)4;
auto e = (uint16_t)5;
auto f = (uint8_t)6;
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void RecordsExample_BoolAndString_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"records
    MyRecord
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
                "MyRecord",
                "a", "=", "false",
                "b", "=", "true",
                "c", "=", "bool",
                "d", "=", "\"\"",
                "e", "=", "str"
            }, significant);

            var ast = parser.Parse(tokens);
            var record = ast.Single(n => n.Kind == NodeKind.RecordDeclaration && n.RecordName == "MyRecord");
            CollectionAssert.AreEqual(new[] { "a=false", "b=true", "c=bool", "d=\"\"", "e=str" }, record.RecordMembers.ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdbool>
#include <string>

// records
struct MyRecord
{
    auto a = false;
    auto b = true;
    auto c = false;
    auto d = u8""s;
    auto e = u8""s;
};
";
            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void RecordsExample_Integers_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"records
    MyRecord
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
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "records",
                "MyRecord",
                "a", "=", "1",
                "b", "=", "2", "int",
                "c", "=", "3", "int64",
                "d", "=", "4", "int32",
                "e", "=", "5", "int16",
                "f", "=", "6", "int8"
            }, significant);

            var ast = parser.Parse(tokens);
            var record = ast.Single(n => n.Kind == NodeKind.RecordDeclaration && n.RecordName == "MyRecord");
            CollectionAssert.AreEqual(new[] { "a=1", "b=2", "c=3", "d=4", "e=5", "f=6" }, record.RecordMembers.ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"#include <cstdint>

// records
struct MyRecord
{
    auto a = (int64_t)1;
    auto b = (int64_t)2;
    auto c = (int64_t)3;
    auto d = (int32_t)4;
    auto e = (int16_t)5;
    auto f = (int8_t)6;
};
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void RecordsExample_UnsignedIntegers_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"records
    MyRecord
        b = 2 uint
        c = 3 uint64
        d = 4 uint32
        e = 5 uint16
        f = 6 uint8
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
                "MyRecord",
                "b", "=", "2", "uint",
                "c", "=", "3", "uint64",
                "d", "=", "4", "uint32",
                "e", "=", "5", "uint16",
                "f", "=", "6", "uint8"
            }, significant);

            var ast = parser.Parse(tokens);
            var record = ast.Single(n => n.Kind == NodeKind.RecordDeclaration && n.RecordName == "MyRecord");
            CollectionAssert.AreEqual(new[] { "b=2", "c=3", "d=4", "e=5", "f=6" }, record.RecordMembers.ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"#include <cstdint>

// records
struct MyRecord
{
    auto b = (uint64_t)2;
    auto c = (uint64_t)3;
    auto d = (uint32_t)4;
    auto e = (uint16_t)5;
    auto f = (uint8_t)6;
};
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void EnumsExample_UnassignedMembers_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"enums
    MyEnum
        A
        B
        C
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "enums",
                "MyEnum",
                "A",
                "B",
                "C"
            }, significant);

            var ast = parser.Parse(tokens);
            var enumNode = ast.Single(n => n.Kind == NodeKind.EnumDeclaration && n.EnumName == "MyEnum");
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, enumNode.EnumMembers.ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"// enums
Enums MyEnum
{
    A,
    B,
    C,
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void EnumsExample_AssignedMembers_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"enums
    MyEnum
        A = 1
        B = 3
        C = 5
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var significantTokens = GetSignificantTokens(tokens);
            var significant = significantTokens.Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "enums",
                "MyEnum",
                "A", "=", "1",
                "B", "=", "3",
                "C", "=", "5"
            }, significant);

            var ast = parser.Parse(tokens);
            var enumNode = ast.Single(n => n.Kind == NodeKind.EnumDeclaration && n.EnumName == "MyEnum");
            CollectionAssert.AreEqual(new[] { "A=1", "B=3", "C=5" }, enumNode.EnumMembers.ToArray());

            var generated = codegen.Generate(ast);
            var expected =
@"// enums
Enums MyEnum
{
    A=1,
    B=3,
    C=5,
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void PropertiesAndFinalizeExample_StringLifecycle_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    s = ""Hello, World!\n""
finalize
    s = """"
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
                "s", "=", "\"Hello, World!\\n\"",
                "finalize",
                "s", "=", "\"\""
            }, significant);

            Assert.AreEqual(Puma.Lexer.TokenCategory.StringLiteral, significantTokens[3].Category);
            Assert.AreEqual(Puma.Lexer.TokenCategory.StringLiteral, significantTokens[7].Category);

            var ast = parser.Parse(tokens);
            var properties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            Assert.AreEqual(1, properties.Count);
            Assert.AreEqual("s", properties[0].PropertyName);
            Assert.AreEqual("\"Hello, World!\\n\"", properties[0].PropertyValue);

            var finalizeSection = ast.Single(n => n.Kind == NodeKind.Section && n.Section == Puma.Parser.Section.Finalize);
            Assert.IsNotNull(finalizeSection);
            var finalizeAssignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement && n.AssignmentLeft == "s").ToList();
            Assert.AreEqual(1, finalizeAssignments.Count);
            Assert.AreEqual("\"\"", finalizeAssignments[0].AssignmentRightExpression?.Value);

            var generated = codegen.Generate(ast);
            var expected =
@"#include <string>

// properties
auto s = u8""Hello, World!\n""s;

// finalize
void finalize(void)
{
    s = u8""""s;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }
    }
}

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
            Assert.AreEqual(20, function.FunctionBody.Count);
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
    }
}

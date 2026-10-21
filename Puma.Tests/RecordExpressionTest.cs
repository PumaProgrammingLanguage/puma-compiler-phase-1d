// LLVM Compiler for the Puma programming language
//   as defined in the document "The Puma Programming Language Specification"
//   available at https://github.com/ThePumaProgrammingLanguage
//
// Copyright © 2024-2026 by Darryl Anthony Burchfield
//
//   Licensed under the Apache License, Version 2.0 (the "License") WITH LLVM-exception;
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
    public class RecordExpressionTest
    {
        [TestMethod]
        public void RecordNumericInitializers_PreserveLiteralFormsAndTypes()
        {
            const string source =
@"records
    Numbers
        exponent = 1.25e+3
        negative = -42 int16
        hex = 0xFE
        bits = 0b1010 uint8
        sum = 1 uint8 + 2 int16
";
            const string expected =
@"#include <cstdint>

// records
struct Numbers
{
    auto exponent = (double)1.25e+3;
    auto negative = (int16_t)-42;
    auto hex = (int64_t)0xFE;
    auto bits = (uint8_t)0b1010;
    auto sum = ((uint8_t)1 + (int16_t)2);
};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var result = new Codegen().GenerateResult(ast);

            Assert.AreEqual(expected.Replace("\r\n", "\n"), result.SourceCode.Replace("\r\n", "\n").Trim());
            Assert.AreEqual(0, result.RequiredRuntimeLibraries.Count);
        }

        [TestMethod]
        public void RecordRuntimeInitializers_SelectHeadersFromExpressions()
        {
            const string source =
@"records
    Values
        enabled = bool
        label = ""x=y""
        marker = 'A'
";
            const string expected =
@"#include <stdbool>
#include <PumaType/Character.hpp>
#include <PumaType/String.hpp>

// records
struct Values
{
    auto enabled = false;
    auto label = PumaType::String(""x=y"", (size_t)(sizeof(""x=y"") - 1));
    auto marker = PumaType::Character(reinterpret_cast<const uint8_t*>(""\x41""));
};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var result = new Codegen().GenerateResult(ast);

            Assert.AreEqual(expected.Replace("\r\n", "\n"), result.SourceCode.Replace("\r\n", "\n").Trim());
            CollectionAssert.AreEqual(new[] { "PumaType" }, result.RequiredRuntimeLibraries.ToArray());
        }

        [TestMethod]
        public void RecordInitializerAstChange_UpdatesOutputAndDependencies()
        {
            const string source = "records\n    Value\n        item = 1\n";
            const string replacementSource = "records\n    Replacement\n        item = \"updated\"\n";
            const string expected =
@"#include <PumaType/String.hpp>

// records
struct Value
{
    auto item = PumaType::String(""updated"", (size_t)(sizeof(""updated"") - 1));
};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var replacement = new Parser().Parse(new Lexer().Tokenize(replacementSource));
            ast.OfType<RecordDeclarationAstNode>().Single().MemberDeclarations.Single().ValueExpression =
                replacement.OfType<RecordDeclarationAstNode>().Single().MemberDeclarations.Single().ValueExpression;

            var result = new Codegen().GenerateResult(ast);

            Assert.AreEqual(expected.Replace("\r\n", "\n"), result.SourceCode.Replace("\r\n", "\n").Trim());
            CollectionAssert.AreEqual(new[] { "PumaType" }, result.RequiredRuntimeLibraries.ToArray());
        }

        [TestMethod]
        public void RecordParserReuse_DoesNotRetainPreviousMembers()
        {
            const string firstSource = "records\n    First\n        text = \"discard\"\n";
            const string secondSource = "records\n    Second\n        number = 7 uint8\n";
            const string expected =
@"#include <cstdint>

// records
struct Second
{
    auto number = (uint8_t)7;
};";
            var parser = new Parser();
            parser.Parse(new Lexer().Tokenize(firstSource));
            var ast = parser.Parse(new Lexer().Tokenize(secondSource));
            var record = ast.OfType<RecordDeclarationAstNode>().Single();

            Assert.AreEqual("number", record.MemberDeclarations.Single().Name);
            Assert.AreEqual("uint8", record.MemberDeclarations.Single().ValueExpression?.DeclaredType);
            Assert.AreEqual(expected.Replace("\r\n", "\n"), new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }
    }
}

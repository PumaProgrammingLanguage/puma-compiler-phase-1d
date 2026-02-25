// LLVM Compiler for the Puma programming language
//   as defined in the document "The Puma Programming Language Specification"
//   available at https://github.com/ThePumaProgrammingLanguage
//
// Copyright © 2024-2025 by Darryl Anthony Burchfield
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
using static Puma.Parser;

namespace Puma.Tests
{
    [TestClass]
    public class ParserOrderTests
    {
        private const string CorrectOrder =
@"use

module

enums

records

properties

initialize

finalize

functions
";

        private const string IncorrectOrder =
@"use

enums

module

records

properties

initialize

finalize

functions
";


        private const string IncorrectOrder2 =
@"use

module

enums

functions

records

properties

initialize

finalize
";


        private const string IncorrectOrder3 =
@"enums

module

records

use

properties

initialize

finalize

functions
";

        [TestMethod]
        public void Parse_DoesNotThrow_WhenSectionsAreInCorrectOrder()
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(CorrectOrder);

            // Should not throw
            var ast = parser.Parse(tokens);

            var sections = ast.Where(n => n.Kind == NodeKind.Section).Select(n => n.Section).ToArray();
            var expected = new[]
            {
                Section.Use,
                Section.Module,
                Section.Enums,
                Section.Records,
                Section.Properties,
                Section.Initialize,
                Section.Finalize,
                Section.Functions
            };

            CollectionAssert.AreEqual(expected, sections);
        }

        [TestMethod]
        public void Parse_ThrowsFriendlyError_WhenSectionsAreOutOfOrder()
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(IncorrectOrder);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));

            // Optional: check the message gives guidance (parser should provide a friendly message)
            StringAssert.Contains(ex.Message.ToLowerInvariant(), "order");
        }

        [TestMethod]
        public void Parse_ThrowsFriendlyError_WhenSectionsAreOutOfOrder2()
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(IncorrectOrder2);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));

            // Optional: check the message gives guidance (parser should provide a friendly message)
            StringAssert.Contains(ex.Message.ToLowerInvariant(), "order");
        }

        [TestMethod]
        public void Parse_ThrowsFriendlyError_WhenSectionsAreOutOfOrder3()
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(IncorrectOrder3);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));

            // Optional: check the message gives guidance (parser should provide a friendly message)
            StringAssert.Contains(ex.Message.ToLowerInvariant(), "order");
        }
    }
}

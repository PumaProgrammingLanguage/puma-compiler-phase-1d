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

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Puma.Parser;

namespace Puma.Tests
{
    [TestClass]
    public class ParserOrderTests
    {
        private const string CorrectOrder =
@"using

module

enums

records

initialize

finalize

functions

end
";

        private const string IncorrectOrder =
@"module

using

enums

records

initialize

finalize

functions

end
";

        [TestMethod]
        public void Parse_DoesNotThrow_WhenSectionsAreInCorrectOrder()
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(CorrectOrder);

            // Should not throw
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();
            var expected = new[]
            {
                Section.Using,
                Section.Module,
                Section.Enums,
                Section.Records,
                Section.Initialize,
                Section.Finalize,
                Section.Functions,
                Section.end
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
    }
}
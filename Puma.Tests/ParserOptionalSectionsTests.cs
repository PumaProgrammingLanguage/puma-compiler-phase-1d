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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Puma.Parser;

namespace Puma.Tests
{
    [TestClass]
    public class ParserOptionalSectionsTests
    {
        [TestMethod]
        public void Parse_AllSectionsMissing_YieldsEmptyAst()
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(string.Empty);
            var ast = parser.Parse(tokens);

            Assert.AreEqual(0, ast.Count);
        }

        [TestMethod]
        public void Parse_OnlyEnd_IsAccepted()
        {
            const string src = "end\n";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();
            CollectionAssert.AreEqual(new[] { Section.end }, sections);
        }

        [TestMethod]
        public void Parse_OnlyFunctionsAndEnd_IsAccepted()
        {
            const string src =
@"functions

end
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();
            CollectionAssert.AreEqual(new[] { Section.Functions, Section.end }, sections);
        }

        [TestMethod]
        public void Parse_UsingModuleEnd_SubsetInOrder_IsAccepted()
        {
            const string src =
@"using

module

end
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();
            CollectionAssert.AreEqual(new[] { Section.Using, Section.Module, Section.end }, sections);
        }

        [TestMethod]
        public void Parse_StartWithoutInitialize_IsAccepted()
        {
            const string src =
@"using

module

start

functions

end
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();
            CollectionAssert.AreEqual(
                new[] { Section.Using, Section.Module, Section.Start, Section.Functions, Section.end },
                sections);
        }

        [TestMethod]
        public void Parse_InitializeWithoutStart_IsAccepted()
        {
            const string src =
@"using

module

initialize

functions

end
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();
            CollectionAssert.AreEqual(
                new[] { Section.Using, Section.Module, Section.Initialize, Section.Functions, Section.end },
                sections);
        }

        [TestMethod]
        public void Parse_MissingEnd_IsAccepted()
        {
            const string src =
@"using

module

functions
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();
            CollectionAssert.AreEqual(
                new[] { Section.Using, Section.Module, Section.Functions },
                sections);
        }

        [TestMethod]
        public void Parse_DuplicateSection_ThrowsFriendlyError()
        {
            const string src =
@"using

using

end
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ex = Assert.ThrowsException<System.InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message.ToLowerInvariant(), "duplicate");
        }
    }
}
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

namespace Puma.Tests
{
    [TestClass]
    public class CodegenOptionalSectionsTests
    {
        private static string Normalize(string s) =>
            s.Replace("\r\n", "\n").Replace("\r", "\n");

        [TestMethod]
        public void Generate_OnlyFunctionsAndEnd_EmitsExpected()
        {
            const string src =
@"functions

end
";
            var expected =
@"// functions

// end
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected), Normalize(c));
        }
    }
}
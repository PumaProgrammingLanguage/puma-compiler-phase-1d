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
    public class WriteLineExpressionTest
    {
        [TestMethod]
        public void ProvidedExample_StringVariableWriteLn_EmitsExactCppOutput()
        {
            const string source =
@"start
    z = 5
    s = ""The value of z is 5""
    WriteLn(s)
";
            const string expected =
@"#include <cstdint>
#include <PumaType/String.hpp>
#include <PumaConsole/Console.hpp>

// start
int main()
{
    auto z = (int64_t)5;
    auto s = PumaType::String(""The value of z is 5"", (size_t)(sizeof(""The value of z is 5"") - 1));
    PumaConsole::WriteLn(s);
    return 0;
}";
            var tokens = new Lexer().Tokenize(source);
            var ast = new Parser().Parse(tokens);
            var generated = new Codegen().Generate(ast);

            Assert.AreEqual(expected.Replace("\r\n", "\n"), generated.Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void HelloWorldWithoutSections_EmitsExactCppOutput()
        {
            const string source =
@"// Hello World program in Puma language
PumaConsole.WriteLn(""Hello, World!"");
// Wait for user input before closing the program
PumaConsole.Pause();
";
            const string expected =
@"#include <PumaConsole/Console.hpp>

// start
int main()
{
    PumaConsole::WriteLn(""Hello, World!"");
    PumaConsole::Pause();
    return 0;
}";
            var tokens = new Lexer().Tokenize(source);
            var ast = new Parser().Parse(tokens);
            var generated = new Codegen().Generate(ast);

            Assert.AreEqual(expected.Replace("\r\n", "\n"), generated.Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void HelloWorldWithConsoleImport_EmitsExactCppOutput()
        {
            const string source =
@"// Hello World program in Puma language
use
    PumaConsole

start
    WriteLn(""Hello, World!"");
    // Wait for user input before closing the program
    Pause();
";
            const string expected =
@"#include <PumaConsole/Console.hpp>

// start
int main()
{
    PumaConsole::WriteLn(""Hello, World!"");
    PumaConsole::Pause();
    return 0;
}";
            var tokens = new Lexer().Tokenize(source);
            var ast = new Parser().Parse(tokens);
            var generated = new Codegen().Generate(ast);

            Assert.AreEqual(expected.Replace("\r\n", "\n"), generated.Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow("\"Hello\"")]
        [DataRow("\"\"")]
        [DataRow("\"a, (b)\"")]
        [DataRow("\"Hello\\nworld\"")]
        public void StringArgument_EmitsExactOutputAndRetainsSourceSpan(string literal)
        {
            var source = $"start\n    WriteLn({literal})\n";
            var expected = $"#include <PumaConsole/Console.hpp>\n\n// start\nint main()\n{{\n    PumaConsole::WriteLn({literal});\n    return 0;\n}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var argument = ast.OfType<WriteLineAstNode>().Single().ArgumentExpression;
            Assert.IsNotNull(argument);
            Assert.AreEqual(ExpressionKind.Literal, argument.Kind);
            Assert.AreEqual(literal, argument.Value);
            Assert.AreEqual(new SourceSpan(2, 13, 2, 13 + literal.Length), argument.SourceSpan);
            var result = new Codegen().GenerateResult(ast);
            Assert.AreEqual(expected, result.SourceCode.Replace("\r\n", "\n").Trim());
            CollectionAssert.AreEqual(new[] { "PumaConsole", "PumaType" }, result.RequiredRuntimeLibraries.ToArray());
        }

        [DataTestMethod]
        [DataRow("s", "")]
        [DataRow("message", ";")]
        public void IdentifierArgument_EmitsExactOutputAndRetainsSourceSpan(string identifier, string terminator)
        {
            var source = $@"start
    {identifier} = ""Hello""
    WriteLn({identifier}){terminator}
";
            var expected = $@"#include <PumaType/String.hpp>
#include <PumaConsole/Console.hpp>

// start
int main()
{{
    auto {identifier} = PumaType::String(""Hello"", (size_t)(sizeof(""Hello"") - 1));
    PumaConsole::WriteLn({identifier});
    return 0;
}}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var argument = ast.OfType<WriteLineAstNode>().Single().ArgumentExpression;
            Assert.IsNotNull(argument);
            Assert.AreEqual(ExpressionKind.Identifier, argument.Kind);
            Assert.AreEqual(identifier, argument.Value);
            Assert.AreEqual(new SourceSpan(3, 13, 3, 13 + identifier.Length), argument.SourceSpan);
            var result = new Codegen().GenerateResult(ast);
            Assert.AreEqual(expected.Replace("\r\n", "\n"), result.SourceCode.Replace("\r\n", "\n").Trim());
            CollectionAssert.AreEqual(new[] { "PumaConsole", "PumaType" }, result.RequiredRuntimeLibraries.ToArray());
        }

        [TestMethod]
        public void ArgumentAstReplacement_UpdatesOutputWithoutStringRuntimeDependency()
        {
            const string source = "start\n    WriteLn(\"original\")\n";
            const string replacementSource = "start\n    WriteLn(\"replacement\")\n";
            const string expected = "#include <PumaConsole/Console.hpp>\n\n// start\nint main()\n{\n    PumaConsole::WriteLn(\"replacement\");\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var replacement = new Parser().Parse(new Lexer().Tokenize(replacementSource));
            ast.OfType<WriteLineAstNode>().Single().ArgumentExpression =
                replacement.OfType<WriteLineAstNode>().Single().ArgumentExpression;
            var result = new Codegen().GenerateResult(ast);
            Assert.AreEqual(expected, result.SourceCode.Replace("\r\n", "\n").Trim());
            CollectionAssert.AreEqual(new[] { "PumaConsole", "PumaType" }, result.RequiredRuntimeLibraries.ToArray());
        }

        [TestMethod]
        public void ArgumentAstReplacement_TraversesTypedExpressionDependencies()
        {
            const string source = "start\n    WriteLn(\"original\")\n";
            const string replacementSource = "start\n    value = 1 int16 + 2 uint8\n";
            const string expected = "#include <cstdint>\n#include <PumaConsole/Console.hpp>\n\n// start\nint main()\n{\n    PumaConsole::WriteLn(((int16_t)1 + (uint8_t)2));\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            var replacement = new Parser().Parse(new Lexer().Tokenize(replacementSource));
            ast.OfType<WriteLineAstNode>().Single().ArgumentExpression =
                replacement.OfType<AssignmentStatementAstNode>().Single().AssignmentRightExpression;
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void MissingArgumentAst_IsRejectedRatherThanOmitted()
        {
            const string source = "start\n    WriteLn(\"Hello\")\n";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            ast.OfType<WriteLineAstNode>().Single().ArgumentExpression = null;
            var error = Assert.ThrowsException<InvalidOperationException>(() => new Codegen().Generate(ast));
            Assert.AreEqual("Expression AST node is required for code generation.", error.Message);
        }

        [TestMethod]
        public void ParserReuse_DoesNotRetainPreviousArgument()
        {
            const string firstSource = "start\n    WriteLn(\"first\")\n";
            const string secondSource = "start\n    WriteLn(\"second\")\n";
            const string expected = "#include <PumaConsole/Console.hpp>\n\n// start\nint main()\n{\n    PumaConsole::WriteLn(\"second\");\n    return 0;\n}";
            var parser = new Parser();
            parser.Parse(new Lexer().Tokenize(firstSource));
            var ast = parser.Parse(new Lexer().Tokenize(secondSource));
            Assert.AreEqual(1, ast.OfType<WriteLineAstNode>().Count());
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        // Insertion point for identifier-argument parser regression coverage.
        [DataTestMethod]
        [DataRow("WriteLn \"x\")", "Expected '(' after WriteLn.")]
        [DataRow("WriteLn(1)", "Expected string literal in WriteLn(...)")]
        [DataRow("WriteLn()", "Expected string literal in WriteLn(...)")]
        [DataRow("WriteLn(\"x\"", "Expected ')' after WriteLn argument.")]
        [DataRow("WriteLn(\"x\", \"y\")", "Expected ')' after WriteLn argument.")]
        [DataRow("WriteLn(s", "Expected ')' after WriteLn argument.")]
        [DataRow("WriteLn(s, sizeof(s) - 1)", "Expected ')' after WriteLn argument.")]
        public void InvalidArgument_PreservesParserDiagnostic(string statement, string expected)
        {
            var source = $"start\n    {statement}\n";
            var error = Assert.ThrowsException<InvalidOperationException>(() => new Parser().Parse(new Lexer().Tokenize(source)));
            Assert.AreEqual(expected, error.Message);
        }
    }
}

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
            const string src =
@"start
    a = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            var expected =
@"#include <cstdint>

// start
int main()
{
    auto a = (int64_t)1;
    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
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
    PI = 3.14159 const
    MAX_RADIUS = 5.0 const

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
        public void PropertiesStart_ConstantMutationDiagnostic_InStart_IsConsistent()
        {
            const string src =
@"properties
    MAX_RADIUS = 5.0 const

start
    MAX_RADIUS = 10
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to constant property");
            StringAssert.Contains(ex.Message, "MAX_RADIUS");
        }

        [TestMethod]
        public void PropertiesInitialize_ConstantMutationDiagnostic_InInitialize_IsConsistent()
        {
            const string src =
@"properties
    MAX_RADIUS = 5.0 const

initialize
    MAX_RADIUS = 10
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to constant property");
            StringAssert.Contains(ex.Message, "MAX_RADIUS");
        }

        [TestMethod]
        public void PropertiesFunctions_ConstantMutationDiagnostic_InFunctions_IsConsistent()
        {
            const string src =
@"properties
    MAX_RADIUS = 5.0 const

functions
    Update()
        MAX_RADIUS = 10
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to constant property");
            StringAssert.Contains(ex.Message, "MAX_RADIUS");
        }

        [TestMethod]
        public void PropertiesStart_ReadonlyMutationDiagnostic_InStart_IsConsistent()
        {
            const string src =
@"properties
    profile = Shape() readonly

start
    profile = Shape()
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly property");
            StringAssert.Contains(ex.Message, "profile");
        }

        [TestMethod]
        public void PropertiesInitialize_ReadonlyMutationDiagnostic_InInitialize_IsConsistent()
        {
            const string src =
@"properties
    profile = Shape() readonly

initialize
    profile = Shape()
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly property");
            StringAssert.Contains(ex.Message, "profile");
        }

        [TestMethod]
        public void PropertiesFunctions_ReadonlyMutationDiagnostic_InFunctions_IsConsistent()
        {
            const string src =
@"properties
    profile = Shape() readonly

functions
    Update()
        profile = Shape()
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly property");
            StringAssert.Contains(ex.Message, "profile");
        }

        [TestMethod]
        public void Start_ReadwriteLocalVariable_Reassignment_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"start
    count = 1 readwrite
    count = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            var expected =
@"#include <cstdint>

// start
int main()
{
    auto count = (int64_t)1;
    count = 2;
    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void PropertiesStart_ReadwriteProperty_ReassignmentAndModifierParsed_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    total = 1 readwrite

start
    total = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var property = ast.Single(n => n.Kind == NodeKind.PropertyDeclaration && n.PropertyName == "total");
            CollectionAssert.Contains(property.PropertyModifiers, "readwrite");

            var generated = codegen.Generate(ast);
            var expected =
@"#include <cstdint>

auto total = (int64_t)1;

// start
int main()
{
    total = (int64_t)2;

    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Functions_ReadwriteParameter_MutationAndModifierParsed_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"functions
    Increment(value int readwrite) int
        value = value + 1
        return value
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationName == "Increment");
            CollectionAssert.Contains(function.FunctionParameterList[0].Modifiers, "readwrite");

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>

// functions
int Increment(int64_t value)
{
    value = value + 1;
    return value;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Functions_ConstParameter_ModifierParsed_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"functions
    Echo(value int const) int
        return value
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationName == "Echo");
            CollectionAssert.Contains(function.FunctionParameterList[0].Modifiers, "const");

            var generated = codegen.Generate(ast);
            var expected =
@"#include <stdint>

// functions
int Echo(int64_t value)
{
    return value;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Functions_ConstParameter_Mutation_ThrowsParserError()
        {
            const string src =
@"functions
    Increment(value int const) int
        value = value + 1
        return value
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to constant parameter");
            StringAssert.Contains(ex.Message, "value");
        }

        [TestMethod]
        public void Start_ReadonlyLocalMutationDiagnostic_IsConsistent()
        {
            const string src =
@"start
    value = 1 readonly
    value = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly local variable");
            StringAssert.Contains(ex.Message, "value");
        }

        [TestMethod]
        public void Initialize_ReadonlyLocalMutationDiagnostic_IsConsistent()
        {
            const string src =
@"initialize
    value = 1 readonly
    value = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly local variable");
            StringAssert.Contains(ex.Message, "value");
        }

        [TestMethod]
        public void Functions_ReadonlyLocalMutationDiagnostic_IsConsistent()
        {
            const string src =
@"functions
    Update()
        value = 1 readonly
        value = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly local variable");
            StringAssert.Contains(ex.Message, "value");
        }

        [TestMethod]
        public void Functions_ReadonlyParameterMutationDiagnostic_IsConsistent()
        {
            const string src =
@"functions
    Update(value int readonly)
        value = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly parameter");
            StringAssert.Contains(ex.Message, "value");
        }

        [TestMethod]
        public void Functions_ReadwriteParameterMutation_IsAllowed()
        {
            const string src =
@"functions
    Update(value int readwrite) int
        value = 2
        return value
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ast = parser.Parse(tokens);
            var function = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration && n.FunctionDeclarationName == "Update");
            CollectionAssert.Contains(function.FunctionParameterList[0].Modifiers, "readwrite");
        }

        [TestMethod]
        public void Functions_VarDefaultParameterRebinding_IsAllowed()
        {
            const string src =
@"functions
    Update(value int) int
        value = 2
        return value
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            var expected =
@"#include <stdint>

// functions
int Update(int64_t value)
{
    value = 2;
    return value;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Start_VarDefaultLocalRebinding_IsAllowed()
        {
            const string src =
@"start
    count = 1
    count = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            var expected =
@"#include <cstdint>

// start
int main()
{
    auto count = (int64_t)1;
    count = 2;
    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void PropertiesStart_ConstPropertyRebinding_IsRejected()
        {
            const string src =
@"properties
    max = 1 const

start
    max = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to constant property");
            StringAssert.Contains(ex.Message, "max");
        }

        [TestMethod]
        public void PropertiesStart_ConstSourceToVarLocal_RebindingIsAllowed_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"properties
    max = 1 const

start
    local = max
    local = 2
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
const auto max = (int64_t)1;

// start
int main()
{
    auto local = max;
    local = 2;

    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Start_ReadonlySourceAssignment_PropagatesReadonlyAndRejectsMutation()
        {
            const string src =
@"start
    source = 1 readonly
    target = source
    target = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly local variable");
            StringAssert.Contains(ex.Message, "target");
        }

        [TestMethod]
        public void Start_ReadwriteSourceAssignment_PropagatesReadonlyAndRejectsMutation()
        {
            const string src =
@"start
    source = 1 readwrite
    target = source
    target = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly local variable");
            StringAssert.Contains(ex.Message, "target");
        }

        [TestMethod]
        public void Start_ReadwriteSourceAssignment_WithReadwriteCastAllowsMutation()
        {
            const string src =
@"start
    source = 1 readwrite
    target = source readwrite
    target = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            var expected =
@"#include <cstdint>

// start
int main()
{
    auto source = (int64_t)1;
    auto target = source;
    target = 2;

    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Functions_ReadonlyParameterSourceAssignment_PropagatesReadonlyAndRejectsMutation()
        {
            const string src =
@"functions
    Update(source int readonly)
        target = source
        target = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly local variable");
            StringAssert.Contains(ex.Message, "target");
        }

        [TestMethod]
        public void PropertiesStart_ReadonlyPropertySourceAssignment_PropagatesReadonlyAndRejectsMutation()
        {
            const string src =
@"properties
    source = 1 readonly

start
    target = source
    target = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly local variable");
            StringAssert.Contains(ex.Message, "target");
        }

        [TestMethod]
        public void PropertiesStart_ReadwritePropertySourceAssignment_WithReadwriteCastAllowsMutation()
        {
            const string src =
@"properties
    source = 1 readwrite

start
    target = source readwrite
    target = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            var expected =
@"#include <cstdint>

auto source = (int64_t)1;

// start
int main()
{
    auto target = source;
    target = 2;

    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Start_ReadonlyPropagationChain_SecondTargetMutationRejected()
        {
            const string src =
@"start
    a = 1 readonly
    b = a
    c = b
    c = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly local variable");
            StringAssert.Contains(ex.Message, "c");
        }

        [TestMethod]
        public void Start_ReadwriteSource_ReadonlyCastStillRejectsMutation()
        {
            const string src =
@"start
    source = 1 readwrite
    target = source readonly
    target = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly local variable");
            StringAssert.Contains(ex.Message, "target");
        }

        [TestMethod]
        public void Start_ReadwriteCastThenReadonlyCast_SecondTargetMutationRejected()
        {
            const string src =
@"start
    source = 1 readwrite
    rw = source readwrite
    ro = rw readonly
    ro = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var tokens = lexer.Tokenize(src);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign to readonly local variable");
            StringAssert.Contains(ex.Message, "ro");
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
    PI = 3.14159 const
    MAX_RADIUS = 5.0 const

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
                "PI", "=", "3.14159", "const",
                "MAX_RADIUS", "=", "5.0", "const",
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
            var expected =
@"#include <stdint>

// properties
auto a = (int64_t)64;
auto b = (int64_t)8;

// functions
void F(void)
{
    x = a;
    x /= b;
    x *= b;
    x %= b;
    x += b;
    x -= b;
    x <<= 1;
    x >>= 1;
    x &= b;
    x ^= b;
    x |= b;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
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
            var expected =
@"#include <cstdint>
#include <""System/IO"">
#include <""a/b/h"">

// start
int main()
{
    auto x = (int64_t)1;
    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void TypeShape_ObjectDefinition_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"type
    Shape is object

properties
    x = 1
    y = 2

functions
    GetX() int
        return x

    GetY() int
        return y
";

            var expected =
@"#include <stdint>

class Shape : public object
{
    // properties
    protected:
    auto x = (int64_t)1;
    auto y = (int64_t)2;

    // functions
    public:
    int64_t GetX()
    {
        return x;
    }
    int64_t GetY()
    {
        return y;
    }
};
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void ObjectReference_NonOptionalUsage_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"use
    PumaObjectSourceFile.puma

properties
    a = Shape()
    b = Shape()

start
    m = a.GetX()
    n = b.GetY()
";

            var expected =
@"#include ""PumaObjectSourceFile.h""

auto a = new Shape();
auto b = new Shape();

// start
int main()
{
    auto m = a.GetX();
    auto n = b.GetY();

    delete a;
    delete b;
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void ObjectReference_OptionalAndNonOptionalUsage_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"use
    PumaObjectSourceFile.puma

properties
    a = Shape()
    b = Shape() optional

start
    m = a.GetX()
    n = b.GetY()

    b = none
";

            var expected =
@"#include ""PumaObjectSourceFile.h""

auto a = new Shape();
auto b = new Shape();

// start
int main()
{
    auto m = a.GetX();
    auto n = b.GetY();
    b = null;

    delete a;
    delete n;
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void ObjectReference_NonOptionalPropertyAssignedNone_ParserError()
        {
            const string src =
@"use
    PumaObjectSourceFile.puma

properties
    a = Shape()
    b = Shape() optional

start
    m = a.GetX()
    n = b.GetY()

    a = none
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
            StringAssert.Contains(ex.Message, "Cannot assign none to non-optional property");
            StringAssert.Contains(ex.Message, "a");
        }

        [TestMethod]
        public void ObjectReference_CoOwnerReassignmentToNone_LastCoOwnerDeleted_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"use
    PumaObjectSourceFile.puma

properties
    b = Shape() optional

start
    n = b.GetY()
    b = none
";

            var expected =
@"#include ""PumaObjectSourceFile.h""

auto b = new Shape();

// start
int main()
{
    auto n = b.GetY();
    b = null;

    delete n;
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void ObjectReference_BorrowerOnlyInnerScope_NotDeleted_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"use
    PumaObjectSourceFile.puma

properties
    a = Shape()

start
    m = a.GetX()
";

            var expected =
@"#include ""PumaObjectSourceFile.h""

auto a = new Shape();

// start
int main()
{
    auto m = a.GetX();

    delete a;
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void ObjectReference_OwnKeywordTransfer_ParsesAndDeletesNewOwner_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"use
    PumaObjectSourceFile.puma

properties
    b = Shape() optional

start
    n = own b
    b = none
";

            var expected =
@"#include ""PumaObjectSourceFile.h""

auto b = new Shape();

// start
int main()
{
    auto n = b;
    b = null;

    delete n;
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void ObjectReference_ReturnOwnershipHandoff_OuterVariableDeleted_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"use
    PumaObjectSourceFile.puma

start
    a = MakeShape()

functions
    MakeShape() Shape own
        return Shape()
";

            var expected =
@"#include ""PumaObjectSourceFile.h""

// functions
Shape MakeShape(void)
{
    return Shape();
}

// start
int main()
{
    auto a = MakeShape();

    delete a;
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void ObjectReference_OptionalLocal_AssignAndNone_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"use
    PumaObjectSourceFile.puma

start
    b = Shape() optional
    b = none
";

            var expected =
@"#include ""PumaObjectSourceFile.h""

// start
int main()
{
    auto b = Shape();
    b = null;

    delete b;
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void OptionalNullCheck_HasStatement_UsesNullLiteralConsistency_LexerParserCodegen_AreConsistent()
        {
            const string src =
@"use
    PumaObjectSourceFile.puma

properties
    b = Shape() optional

start
    has b
        b.GetY()
";

            var expected =
@"#include ""PumaObjectSourceFile.h""

auto b = new Shape();

// start
int main()
{
    if (b != null)
    {
        b.GetY();
    }
    delete b;
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
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
            var expected =
@"// start
int main()
{
    if (obj != null && typeof(obj) == typeof(Printable))
    {
        WriteLn(""Obj is printable!"");
        WriteLn(obj.ToString());
    }
    return 0;
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
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
            var expected =
@"// properties
auto arr = Array(items,2);

// functions
void F(void)
{
    i = 0;
    x = arr[i];
    x = arr[(i + 1)];
}
";

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
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

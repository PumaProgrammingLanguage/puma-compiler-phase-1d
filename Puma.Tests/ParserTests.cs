using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Puma.Parser;

namespace Puma.Tests
{
    [TestClass]
    public class ParserTests
    {
        private const string Sample =
@"use

module

enums

records

properties

initialize

finalize

functions
";

        [TestMethod]
        public void Sections_AreParsedIntoAstInOrder()
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(Sample);
            var ast = parser.Parse(tokens);

            var sections = ast.Select(n => n.Section).ToArray();

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
        public void UseSection_ParsesNamespaceAndAlias()
        {
            const string src =
@"use System.Console as Console

module
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var useNode = ast.Single(n => n.Kind == NodeKind.UseStatement);
            Assert.AreEqual("System.Console", useNode.UseTarget);
            Assert.AreEqual("Console", useNode.UseAlias);
            Assert.IsFalse(useNode.UseIsFilePath);
        }

        [TestMethod]
        public void TypeSection_ParsesTypeWithBaseAndTraits()
        {
            const string src =
@"use

type Sample.Type is object has Alpha, Beta
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var typeNode = ast.Single(n => n.Kind == NodeKind.TypeDeclaration);
            Assert.AreEqual("type", typeNode.DeclarationKind);
            Assert.AreEqual("Sample.Type", typeNode.DeclarationName);
            Assert.AreEqual("object", typeNode.BaseTypeName);
            CollectionAssert.AreEqual(new[] { "Alpha", "Beta" }, typeNode.TraitNames);
        }

        [TestMethod]
        public void EnumsAndRecords_AreParsedWithMembers()
        {
            const string src =
@"use

module

enums
    StatusSetting
        Active
        Inactive

records
    UserRecord pack 4
        Name str
        Age int
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var enumNode = ast.Single(n => n.Kind == NodeKind.EnumDeclaration);
            Assert.AreEqual("StatusSetting", enumNode.EnumName);
            CollectionAssert.AreEqual(new[] { "Active", "Inactive" }, enumNode.EnumMembers);

            var recordNode = ast.Single(n => n.Kind == NodeKind.RecordDeclaration);
            Assert.AreEqual("UserRecord", recordNode.RecordName);
            Assert.AreEqual(4, recordNode.RecordPackSize);
            CollectionAssert.AreEqual(new[] { "Name", "Age" }, recordNode.RecordMembers);
        }

        [TestMethod]
        public void PropertiesSection_ParsesAssignments()
        {
            const string src =
@"use

module

properties
    Status = Active
    User = UserRecord
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var properties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            Assert.AreEqual(2, properties.Count);

            Assert.AreEqual("Status", properties[0].PropertyName);
            Assert.AreEqual("Active", properties[0].PropertyValue);
            Assert.IsNull(properties[0].PropertyType);

            Assert.AreEqual("User", properties[1].PropertyName);
            Assert.AreEqual("UserRecord", properties[1].PropertyValue);
            Assert.IsNull(properties[1].PropertyType);
        }

        [TestMethod]
        public void PropertiesSection_ParsesModifiers()
        {
            const string src =
@"use

module

properties
    Name = value public readonly
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var property = ast.Single(n => n.Kind == NodeKind.PropertyDeclaration);
            CollectionAssert.AreEqual(new[] { "public", "readonly" }, property.PropertyModifiers);
        }

        [TestMethod]
        public void StartAndInitialize_ParseAssignmentStatements()
        {
            const string src =
@"use

module

properties
    Counter = 0

initialize
    Counter = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToList();
            Assert.AreEqual(1, assignments.Count);
            Assert.AreEqual("Counter", assignments[0].AssignmentLeft);
            Assert.AreEqual("1", assignments[0].AssignmentRight);
            Assert.AreEqual("=", assignments[0].AssignmentOperator);
        }

        [TestMethod]
        public void AssignmentOperators_ApplyExpectedValues_PerOperator()
        {
            const string src =
@"use

module

start
    Eq = 5
    Div /= 4
    Mul *= 4
    Mod %= 3
    Add += 5
    Sub -= 4
    Shl <<= 2
    Shr >>= 2
    And &= 11
    Xor ^= 10
    Or |= 10
";

            var values = new Dictionary<string, long>
            {
                ["Eq"] = 0,
                ["Div"] = 20,
                ["Mul"] = 3,
                ["Mod"] = 10,
                ["Add"] = 7,
                ["Sub"] = 9,
                ["Shl"] = 3,
                ["Shr"] = 16,
                ["And"] = 14,
                ["Xor"] = 12,
                ["Or"] = 12
            };

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToList();
            Assert.AreEqual(11, assignments.Count);

            foreach (var assignment in assignments)
            {
                var rhs = EvaluateExpression(assignment.AssignmentRightExpression);
                values[assignment.AssignmentLeft!] = ApplyAssignment(values[assignment.AssignmentLeft!], assignment.AssignmentOperator!, rhs);
            }

            Assert.AreEqual(5L, values["Eq"]);
            Assert.AreEqual(5L, values["Div"]);
            Assert.AreEqual(12L, values["Mul"]);
            Assert.AreEqual(1L, values["Mod"]);
            Assert.AreEqual(12L, values["Add"]);
            Assert.AreEqual(5L, values["Sub"]);
            Assert.AreEqual(12L, values["Shl"]);
            Assert.AreEqual(4L, values["Shr"]);
            Assert.AreEqual(10L, values["And"]);
            Assert.AreEqual(6L, values["Xor"]);
            Assert.AreEqual(14L, values["Or"]);
        }

        [TestMethod]
        public void AssignmentExpressions_FollowOperatorPrecedence_WhenHigherComesAfterLower()
        {
            const string src =
@"use

module

start
    A = 2 + 3 * 4
    B = 20 - 8 / 2
    C = 1 == 1 or 2 == 3 and 4 == 4
    D = 1 | 2 ^ 6
    E = 2 ^ 1 & 3
    F = 1 + 2 << 3
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            Assert.AreEqual(14L, EvaluateExpression(assignments["A"].AssignmentRightExpression));
            Assert.AreEqual(16L, EvaluateExpression(assignments["B"].AssignmentRightExpression));
            Assert.AreEqual(1L, EvaluateExpression(assignments["C"].AssignmentRightExpression));
            Assert.AreEqual(5L, EvaluateExpression(assignments["D"].AssignmentRightExpression));
            Assert.AreEqual(3L, EvaluateExpression(assignments["E"].AssignmentRightExpression));
            Assert.AreEqual(24L, EvaluateExpression(assignments["F"].AssignmentRightExpression));
        }

        [TestMethod]
        public void AssignmentExpressions_ParseUnaryOperators()
        {
            const string src =
@"use

module

start
    A = !0
    B = ~1
    C = -5 + +2
    D = not false
    E = ++1
    F = --1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            Assert.AreEqual(1L, EvaluateExpression(assignments["A"].AssignmentRightExpression));
            Assert.AreEqual(~1L, EvaluateExpression(assignments["B"].AssignmentRightExpression));
            Assert.AreEqual(-3L, EvaluateExpression(assignments["C"].AssignmentRightExpression));
            Assert.AreEqual(1L, EvaluateExpression(assignments["D"].AssignmentRightExpression));
            Assert.AreEqual(2L, EvaluateExpression(assignments["E"].AssignmentRightExpression));
            Assert.AreEqual(0L, EvaluateExpression(assignments["F"].AssignmentRightExpression));
        }

        [TestMethod]
        public void AssignmentExpressions_ParseCast_WithExpectedPrecedence()
        {
            const string src =
@"use

module

start
    A = (int32)1 + 2
    B = (int32)(1 + 2) * 3
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            var a = assignments["A"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, a.Kind);
            Assert.AreEqual("+", a.Value);
            Assert.AreEqual(ExpressionKind.Cast, a.Left!.Kind);
            Assert.AreEqual("int32", a.Left!.Value);
            Assert.AreEqual(3L, EvaluateExpression(a));

            var b = assignments["B"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, b.Kind);
            Assert.AreEqual("*", b.Value);
            Assert.AreEqual(ExpressionKind.Cast, b.Left!.Kind);
            Assert.AreEqual("int32", b.Left!.Value);
            Assert.AreEqual(9L, EvaluateExpression(b));
        }

        [TestMethod]
        public void AssignmentExpressions_FollowLeftToRightAssociativity_Sample()
        {
            const string src =
@"use

module

start
    A = 16 >> 2 >> 1
    B = 1 | 2 | 4
    C = 8 - 3 - 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            Assert.AreEqual(2L, EvaluateExpression(assignments["A"].AssignmentRightExpression));
            Assert.AreEqual(7L, EvaluateExpression(assignments["B"].AssignmentRightExpression));
            Assert.AreEqual(3L, EvaluateExpression(assignments["C"].AssignmentRightExpression));
        }

        [TestMethod]
        public void AssignmentExpressions_FollowShiftBitwisePrecedence_MatrixSample()
        {
            const string src =
@"use

module

start
    A = 1 << 2 & 7
    B = 1 | 4 >> 1
    C = 8 >> 1 | 1
    D = -2 * 3 + 10
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            Assert.AreEqual(4L, EvaluateExpression(assignments["A"].AssignmentRightExpression));
            Assert.AreEqual(3L, EvaluateExpression(assignments["B"].AssignmentRightExpression));
            Assert.AreEqual(5L, EvaluateExpression(assignments["C"].AssignmentRightExpression));
            Assert.AreEqual(4L, EvaluateExpression(assignments["D"].AssignmentRightExpression));
        }

        [TestMethod]
        public void AssignmentExpressions_PreserveExpectedAstShape_ForPrecedenceBoundaries()
        {
            const string src =
@"use

module

start
    A = 1 | 2 ^ 3
    B = 1 << 2 + 3
    C = 1 + 2 << 3
    D = not true and false
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            var a = assignments["A"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, a.Kind);
            Assert.AreEqual("|", a.Value);
            Assert.AreEqual(ExpressionKind.Binary, a.Right!.Kind);
            Assert.AreEqual("^", a.Right!.Value);

            var b = assignments["B"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, b.Kind);
            Assert.AreEqual("<<", b.Value);
            Assert.AreEqual(ExpressionKind.Binary, b.Right!.Kind);
            Assert.AreEqual("+", b.Right!.Value);

            var c = assignments["C"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, c.Kind);
            Assert.AreEqual("<<", c.Value);
            Assert.AreEqual(ExpressionKind.Binary, c.Left!.Kind);
            Assert.AreEqual("+", c.Left!.Value);

            var d = assignments["D"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, d.Kind);
            Assert.AreEqual("and", d.Value);
            Assert.AreEqual(ExpressionKind.Unary, d.Left!.Kind);
            Assert.AreEqual("not", d.Left!.Value);
        }

        [TestMethod]
        public void AssignmentExpressions_ParsePairRange_WithExpectedPrecedence()
        {
            const string src =
@"use

module

start
    A = 1 .. 2 * 3
    B = 1 : 2 + 3
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            var a = assignments["A"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, a.Kind);
            Assert.AreEqual("*", a.Value);
            Assert.AreEqual(ExpressionKind.Binary, a.Left!.Kind);
            Assert.AreEqual("..", a.Left!.Value);

            var b = assignments["B"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, b.Kind);
            Assert.AreEqual("+", b.Value);
            Assert.AreEqual(ExpressionKind.Binary, b.Left!.Kind);
            Assert.AreEqual(":", b.Left!.Value);
        }

        [TestMethod]
        public void AssignmentExpressions_PairRange_DisallowConsecutiveChain()
        {
            const string src =
@"use

module

start
    A = 1 .. 2 .. 3
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(lexer.Tokenize(src)));
        }

        [TestMethod]
        public void AssignmentExpressions_PreserveAstShape_ForBitwiseVsRelationalAndLogical()
        {
            const string src =
@"use

module

start
    A = 1 | 2 < 4
    B = 1 < 2 and 3 | 0
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            var a = assignments["A"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, a.Kind);
            Assert.AreEqual("<", a.Value);
            Assert.AreEqual(ExpressionKind.Binary, a.Left!.Kind);
            Assert.AreEqual("|", a.Left!.Value);

            var b = assignments["B"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, b.Kind);
            Assert.AreEqual("and", b.Value);
            Assert.AreEqual(ExpressionKind.Binary, b.Right!.Kind);
            Assert.AreEqual("|", b.Right!.Value);
        }

        [TestMethod]
        public void AssignmentExpressions_LogicalOr_IsLeftAssociative()
        {
            const string src =
@"use

module

start
    A = true or false or false
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignment = ast.Single(n => n.Kind == NodeKind.AssignmentStatement && n.AssignmentLeft == "A");
            var expr = assignment.AssignmentRightExpression!;

            Assert.AreEqual(ExpressionKind.Binary, expr.Kind);
            Assert.AreEqual("or", expr.Value);
            Assert.AreEqual(ExpressionKind.Binary, expr.Left!.Kind);
            Assert.AreEqual("or", expr.Left!.Value);
            Assert.AreEqual(1L, EvaluateExpression(expr));
        }

        [TestMethod]
        public void AssignmentExpressions_Shift_IsLeftAssociative()
        {
            const string src =
@"use

module

start
    A = 16 >> 2 >> 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignment = ast.Single(n => n.Kind == NodeKind.AssignmentStatement && n.AssignmentLeft == "A");
            var expr = assignment.AssignmentRightExpression!;

            Assert.AreEqual(ExpressionKind.Binary, expr.Kind);
            Assert.AreEqual(">>", expr.Value);
            Assert.AreEqual(ExpressionKind.Binary, expr.Left!.Kind);
            Assert.AreEqual(">>", expr.Left!.Value);
            Assert.AreEqual(2L, EvaluateExpression(expr));
        }

        [TestMethod]
        public void AssignmentExpressions_BitwiseXor_IsLeftAssociative()
        {
            const string src =
@"use

module

start
    A = 1 ^ 2 ^ 3
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignment = ast.Single(n => n.Kind == NodeKind.AssignmentStatement && n.AssignmentLeft == "A");
            var expr = assignment.AssignmentRightExpression!;

            Assert.AreEqual(ExpressionKind.Binary, expr.Kind);
            Assert.AreEqual("^", expr.Value);
            Assert.AreEqual(ExpressionKind.Binary, expr.Left!.Kind);
            Assert.AreEqual("^", expr.Left!.Value);
            Assert.AreEqual(0L, EvaluateExpression(expr));
        }

        [TestMethod]
        public void AssignmentExpressions_CommaMultiExpression_ParsesWithLowestPrecedence()
        {
            const string src =
@"use

module

start
    A = 1, 2
    B = 1 + 2, 3 * 4
    C = (1, 2) + 3
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            var a = assignments["A"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, a.Kind);
            Assert.AreEqual(",", a.Value);
            Assert.AreEqual(2L, EvaluateExpression(a));

            var b = assignments["B"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, b.Kind);
            Assert.AreEqual(",", b.Value);
            Assert.AreEqual(12L, EvaluateExpression(b));

            var c = assignments["C"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, c.Kind);
            Assert.AreEqual("+", c.Value);
            Assert.AreEqual(5L, EvaluateExpression(c));
        }

        [TestMethod]
        public void AssignmentExpressions_ConditionalIfElse_ParsesAndEvaluates()
        {
            const string src =
@"use

module

start
    A = 1 if true else 2
    B = 1 if false else 2
    C = 1 + 2 if false or true else 4
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            var a = assignments["A"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Conditional, a.Kind);
            Assert.AreEqual(1L, EvaluateExpression(a));

            var b = assignments["B"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Conditional, b.Kind);
            Assert.AreEqual(2L, EvaluateExpression(b));

            var c = assignments["C"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Conditional, c.Kind);
            Assert.AreEqual(3L, EvaluateExpression(c));
        }

        [TestMethod]
        public void AssignmentExpressions_ConditionalAndComma_PrecedenceMatrix()
        {
            const string src =
@"use

module

start
    A = 1 if true else 2, 3
    B = 1, 2 if true else 3
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            var a = assignments["A"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, a.Kind);
            Assert.AreEqual(",", a.Value);
            Assert.AreEqual(ExpressionKind.Conditional, a.Left!.Kind);
            Assert.AreEqual(3L, EvaluateExpression(a));

            var b = assignments["B"].AssignmentRightExpression!;
            Assert.AreEqual(ExpressionKind.Binary, b.Kind);
            Assert.AreEqual(",", b.Value);
            Assert.AreEqual(ExpressionKind.Conditional, b.Right!.Kind);
            Assert.AreEqual(2L, EvaluateExpression(b));
        }

        [TestMethod]
        public void AssignmentExpressions_Conditional_IsLeftAssociative()
        {
            const string src =
@"use

module

start
    A = 1 if true else 2 if false else 3
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignment = ast.Single(n => n.Kind == NodeKind.AssignmentStatement && n.AssignmentLeft == "A");
            var conditional = assignment.AssignmentRightExpression!;

            Assert.AreEqual(ExpressionKind.Conditional, conditional.Kind);
            Assert.AreEqual(ExpressionKind.Conditional, conditional.Right!.Kind);
            Assert.AreEqual(3L, EvaluateExpression(conditional));
        }

        [TestMethod]
        public void AssignmentExpressions_FunctionCallArguments_CommaStillSeparatesArguments()
        {
            const string src =
@"use

module

start
    A = func(1, 2 if true else 3)
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignment = ast.Single(n => n.Kind == NodeKind.AssignmentStatement && n.AssignmentLeft == "A");
            var call = assignment.AssignmentRightExpression!;

            Assert.AreEqual(ExpressionKind.Call, call.Kind);
            Assert.AreEqual(2, call.Arguments.Count);
            Assert.AreEqual(ExpressionKind.Literal, call.Arguments[0].Kind);
            Assert.AreEqual("1", call.Arguments[0].Value);
            Assert.AreEqual(ExpressionKind.Conditional, call.Arguments[1].Kind);
        }

        [TestMethod]
        public void AssignmentExpressions_FunctionCallArgument_AllowsParenthesizedCommaExpression()
        {
            const string src =
@"use

module

start
    A = func((1, 2), 3)
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignment = ast.Single(n => n.Kind == NodeKind.AssignmentStatement && n.AssignmentLeft == "A");
            var call = assignment.AssignmentRightExpression!;

            Assert.AreEqual(ExpressionKind.Call, call.Kind);
            Assert.AreEqual(2, call.Arguments.Count);
            Assert.AreEqual(ExpressionKind.Binary, call.Arguments[0].Kind);
            Assert.AreEqual(",", call.Arguments[0].Value);
            Assert.AreEqual(ExpressionKind.Literal, call.Arguments[1].Kind);
            Assert.AreEqual("3", call.Arguments[1].Value);
            Assert.AreEqual(2L, EvaluateExpression(call.Arguments[0]));
        }

        [TestMethod]
        public void StartAndInitialize_ParseFunctionCallStatement_WithExpressionTree()
        {
            const string src =
@"use

module

start
    Print((1, 2), 3 if true else 4)
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var callNode = ast.Single(n => n.Kind == NodeKind.FunctionCall);
            Assert.IsNotNull(callNode.StatementExpression);
            Assert.AreEqual(ExpressionKind.Call, callNode.StatementExpression!.Kind);
            Assert.AreEqual(2, callNode.StatementExpression.Arguments.Count);

            var arg0 = callNode.StatementExpression.Arguments[0];
            Assert.AreEqual(ExpressionKind.Binary, arg0.Kind);
            Assert.AreEqual(",", arg0.Value);

            var arg1 = callNode.StatementExpression.Arguments[1];
            Assert.AreEqual(ExpressionKind.Conditional, arg1.Kind);
            Assert.AreEqual(3L, EvaluateExpression(arg1));
        }

        [TestMethod]
        public void AssignmentExpressions_Pair_DisallowConsecutiveChain()
        {
            const string src =
@"use

module

start
    A = 1 : 2 : 3
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(lexer.Tokenize(src)));
        }

        [TestMethod]
        public void AssignmentExpressions_MixedPairRange_DisallowConsecutiveChain()
        {
            const string src =
@"use

module

start
    A = 1 : 2 .. 3
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(lexer.Tokenize(src)));
        }

        [TestMethod]
        public void AssignmentExpressions_Unary_DisallowConsecutiveChain()
        {
            const string src =
@"use

module

start
    A = !!1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(lexer.Tokenize(src)));
        }

        [TestMethod]
        public void AssignmentExpressions_Equality_DisallowConsecutiveChain()
        {
            const string src =
@"use

module

start
    A = 1 == 1 == 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(lexer.Tokenize(src)));
        }

        [TestMethod]
        public void AssignmentExpressions_Relational_DisallowConsecutiveChain()
        {
            const string src =
@"use

module

start
    A = 1 < 2 < 3
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(lexer.Tokenize(src)));
        }

        [TestMethod]
        public void AssignmentExpressions_FollowLogicalNotAndOrPrecedence_MatrixSample()
        {
            const string src =
@"use

module

start
    A = not false and false
    B = true or false and false
    C = not (true and false)
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            Assert.AreEqual(0L, EvaluateExpression(assignments["A"].AssignmentRightExpression));
            Assert.AreEqual(1L, EvaluateExpression(assignments["B"].AssignmentRightExpression));
            Assert.AreEqual(1L, EvaluateExpression(assignments["C"].AssignmentRightExpression));
        }

        [TestMethod]
        public void AssignmentExpressions_FollowRelationalThenEqualityPrecedence_MatrixSample()
        {
            const string src =
@"use

module

start
    A = 3 < 4 == true
    B = 5 >= 5 != false
    C = 2 > 3 == false
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            Assert.AreEqual(1L, EvaluateExpression(assignments["A"].AssignmentRightExpression));
            Assert.AreEqual(1L, EvaluateExpression(assignments["B"].AssignmentRightExpression));
            Assert.AreEqual(1L, EvaluateExpression(assignments["C"].AssignmentRightExpression));
        }

        [TestMethod]
        public void AssignmentExpressions_FollowRelationalEqualityLogicalPrecedence()
        {
            const string src =
@"use

module

start
    A = 1 < 2 == 1
    B = 2 > 1 and 3 == 3
    C = 1 == 0 or 2 > 1 and 3 < 4
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            Assert.AreEqual(1L, EvaluateExpression(assignments["A"].AssignmentRightExpression));
            Assert.AreEqual(1L, EvaluateExpression(assignments["B"].AssignmentRightExpression));
            Assert.AreEqual(1L, EvaluateExpression(assignments["C"].AssignmentRightExpression));
        }

        [TestMethod]
        public void AssignmentExpressions_MemberCallIndex_BindTighterThanAdditive()
        {
            const string src =
@"use

module

start
    A = obj.value + 1
    B = list[2] * 3
    C = func(4) + 5
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            Assert.AreEqual(ExpressionKind.Binary, assignments["A"].AssignmentRightExpression!.Kind);
            Assert.AreEqual("+", assignments["A"].AssignmentRightExpression!.Value);
            Assert.AreEqual(ExpressionKind.MemberAccess, assignments["A"].AssignmentRightExpression!.Left!.Kind);

            Assert.AreEqual(ExpressionKind.Binary, assignments["B"].AssignmentRightExpression!.Kind);
            Assert.AreEqual("*", assignments["B"].AssignmentRightExpression!.Value);
            Assert.AreEqual(ExpressionKind.Index, assignments["B"].AssignmentRightExpression!.Left!.Kind);

            Assert.AreEqual(ExpressionKind.Binary, assignments["C"].AssignmentRightExpression!.Kind);
            Assert.AreEqual("+", assignments["C"].AssignmentRightExpression!.Value);
            Assert.AreEqual(ExpressionKind.Call, assignments["C"].AssignmentRightExpression!.Left!.Kind);
        }

        [TestMethod]
        public void PostfixIncrementDecrement_AreLoweredToCompoundAssignments()
        {
            const string src =
@"use

module

start
    x++
    y--
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToList();
            Assert.AreEqual(2, assignments.Count);

            Assert.AreEqual("x", assignments[0].AssignmentLeft);
            Assert.AreEqual("+=", assignments[0].AssignmentOperator);
            Assert.AreEqual("1", assignments[0].AssignmentRight);

            Assert.AreEqual("y", assignments[1].AssignmentLeft);
            Assert.AreEqual("-=", assignments[1].AssignmentOperator);
            Assert.AreEqual("1", assignments[1].AssignmentRight);
        }

        [TestMethod]
        public void AssignmentExpressions_Grouping_OverridesDefaultPrecedence()
        {
            const string src =
@"use

module

start
    A = (2 + 3) * 4
    B = 8 >> (1 + 1)
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            Assert.AreEqual(20L, EvaluateExpression(assignments["A"].AssignmentRightExpression));
            Assert.AreEqual(2L, EvaluateExpression(assignments["B"].AssignmentRightExpression));
        }

        [TestMethod]
        public void AssignmentExpressions_FollowAdjacentPrecedenceLevels_MatrixSample()
        {
            const string src =
@"use

module

start
    A = 2 * 3 + 4
    B = 2 + 3 << 1
    C = 1 | 2 == 3
    D = 1 == 1 | 0
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var ast = parser.Parse(lexer.Tokenize(src));

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToDictionary(n => n.AssignmentLeft!);

            Assert.AreEqual(10L, EvaluateExpression(assignments["A"].AssignmentRightExpression));
            Assert.AreEqual(10L, EvaluateExpression(assignments["B"].AssignmentRightExpression));
            Assert.AreEqual(1L, EvaluateExpression(assignments["C"].AssignmentRightExpression));
            Assert.AreEqual(1L, EvaluateExpression(assignments["D"].AssignmentRightExpression));
        }

        [TestMethod]
        public void StartAndInitialize_ParseFunctionCalls()
        {
            const string src =
@"use

module

properties
    Counter = 0

start
    Console.WriteLine(""Hi"")
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var calls = ast.Where(n => n.Kind == NodeKind.FunctionCall).ToList();
            Assert.AreEqual(1, calls.Count);
            Assert.AreEqual("Console.WriteLine", calls[0].FunctionName);
            Assert.AreEqual("\"Hi\"", calls[0].FunctionArguments);
        }

        [TestMethod]
        public void StartAndInitialize_ParseIfStatements()
        {
            const string src =
@"use

module

properties
    Flag = false
    Count = 0

start
    if Flag
        Count = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var ifStatements = ast.Where(n => n.Kind == NodeKind.IfStatement).ToList();
            Assert.AreEqual(1, ifStatements.Count);
            Assert.AreEqual("Flag", ifStatements[0].IfCondition);
        }


        [TestMethod]
        public void StartAndInitialize_ParseMatchStatements()
        {
            const string src =
@"use

module

properties
    value = 0
    Count = 0

start
    match value
        when 1
            Count = 1
        when 2
            Count = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var matchNode = ast.Single(n => n.Kind == NodeKind.MatchStatement);
            Assert.AreEqual("value", matchNode.MatchExpression);

            var whenNodes = matchNode.StatementBody.Where(n => n.Kind == NodeKind.WhenStatement).ToList();
            Assert.AreEqual(2, whenNodes.Count);
        }

        [TestMethod]
        public void StartSection_ParsesIfBlockStatements()
        {
            const string src =
@"use

module

properties
    Count = 0
    ready = false

start
    if ready
        Count = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var ifNode = ast.Single(n => n.Kind == NodeKind.IfStatement);
            Assert.AreEqual(1, ifNode.StatementBody.Count);
            Assert.AreEqual(NodeKind.AssignmentStatement, ifNode.StatementBody[0].Kind);
        }

        [TestMethod]
        public void StartSection_ParsesElseBlockStatements()
        {
            const string src =
@"use

module

properties
    Count = 0
    ready = false

start
    if ready
        Count = 1
    else
        Count = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var ifNode = ast.Single(n => n.Kind == NodeKind.IfStatement);
            Assert.AreEqual(1, ifNode.ElseBody.Count);
            Assert.AreEqual(NodeKind.AssignmentStatement, ifNode.ElseBody[0].Kind);
        }

        [TestMethod]
        public void StartSection_ParsesIfStatementBodyAssignments()
        {
            const string src =
@"use

module

properties
    Count = 0
    Flag = false

start
    if Flag
        Count = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var ifNode = ast.Single(n => n.Kind == NodeKind.IfStatement);
            Assert.AreEqual(1, ifNode.StatementBody.Count);
            var assignment = ifNode.StatementBody[0];
            Assert.AreEqual(NodeKind.AssignmentStatement, assignment.Kind);
            Assert.AreEqual("Count", assignment.AssignmentLeft);
            Assert.AreEqual("1", assignment.AssignmentRight);
        }

        [TestMethod]
        public void StartSection_ParsesIfElseStatementBodyAssignments()
        {
            const string src =
@"use

module

properties
    Count = 0
    Flag = false

start
    if Flag
        Count = 1
    else
        Count = 2
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var ifNode = ast.Single(n => n.Kind == NodeKind.IfStatement);
            Assert.AreEqual(1, ifNode.StatementBody.Count);
            Assert.AreEqual(1, ifNode.ElseBody.Count);
            Assert.AreEqual(NodeKind.AssignmentStatement, ifNode.StatementBody[0].Kind);
            Assert.AreEqual(NodeKind.AssignmentStatement, ifNode.ElseBody[0].Kind);
        }

        [TestMethod]
        public void StartSection_ParsesHasStatementBodyAssignments()
        {
            const string src =
@"use

module

properties
    Count = 0
    item = 0

start
    has item
        Count = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var hasNode = ast.Single(n => n.Kind == NodeKind.HasStatement);
            Assert.AreEqual(1, hasNode.StatementBody.Count);
            Assert.AreEqual(NodeKind.AssignmentStatement, hasNode.StatementBody[0].Kind);
        }

        [TestMethod]
        public void StartSection_ParsesWhileStatementBodyAssignments()
        {
            const string src =
@"use

module

properties
    Count = 0
    running = true

start
    while running
        Count = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var whileNode = ast.Single(n => n.Kind == NodeKind.WhileStatement);
            Assert.AreEqual(1, whileNode.StatementBody.Count);
            Assert.AreEqual(NodeKind.AssignmentStatement, whileNode.StatementBody[0].Kind);
        }

        [TestMethod]
        public void StartSection_ParsesForStatementBodyAssignments()
        {
            const string src =
@"use

module

properties
    Count = 0
    items = 0

start
    for entry in items
        Count = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var forNode = ast.Single(n => n.Kind == NodeKind.ForStatement);
            Assert.AreEqual(1, forNode.StatementBody.Count);
            Assert.AreEqual(NodeKind.AssignmentStatement, forNode.StatementBody[0].Kind);
            Assert.AreEqual("entry", forNode.ForVariable);
            Assert.AreEqual("items", forNode.ForContainer);
        }

        [TestMethod]
        public void StartSection_ParsesRepeatStatementBodyAssignments()
        {
            const string src =
@"use

module

properties
    Count = 0

start
    repeat Count
        Count = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var repeatNode = ast.Single(n => n.Kind == NodeKind.RepeatStatement);
            Assert.AreEqual(1, repeatNode.StatementBody.Count);
            Assert.AreEqual(NodeKind.AssignmentStatement, repeatNode.StatementBody[0].Kind);
            Assert.AreEqual("Count", repeatNode.RepeatExpression);
        }
        [TestMethod]
        public void StartAndInitialize_ParseWhileStatements()
        {
            const string src =
@"use

module

properties
    running = true
    Count = 0

initialize
    while running
        Count = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var whileNodes = ast.Where(n => n.Kind == NodeKind.WhileStatement).ToList();
            Assert.AreEqual(1, whileNodes.Count);
            Assert.AreEqual("running", whileNodes[0].WhileCondition);
        }

        [TestMethod]
        public void StartAndInitialize_ParseForStatements()
        {
            const string src =
@"use

module

properties
    items = 0
    Count = 0

start
    forall entry in items
        Count = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var forAllNode = ast.Single(n => n.Kind == NodeKind.ForAllStatement);
            Assert.AreEqual("entry", forAllNode.ForVariable);
            Assert.AreEqual("items", forAllNode.ForContainer);
        }

        [TestMethod]
        public void StartAndInitialize_ParseRepeatStatements()
        {
            const string src =
@"use

module

properties
    dummy = 0
    Count = 0

initialize
    repeat
        Count = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var repeats = ast.Where(n => n.Kind == NodeKind.RepeatStatement).ToList();
            Assert.AreEqual(1, repeats.Count);
            Assert.AreEqual(string.Empty, repeats[0].RepeatExpression);
        }

        [TestMethod]
        public void StartAndInitialize_ParseHasStatements()
        {
            const string src =
@"use

module

properties
    optionalValue = 0
    item = 0
    Count = 0

start
    has item
        Count = 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var hasStatements = ast.Where(n => n.Kind == NodeKind.HasStatement).ToList();
            Assert.AreEqual(1, hasStatements.Count);
            Assert.AreEqual("item", hasStatements[0].HasCondition);
        }

        [TestMethod]
        public void StartAndInitialize_ParseHasTraitStatements()
        {
            const string src =
@"use

module

properties
    item = 0
    Count = 0

initialize
    has Alpha item
        Count = 1
        item.y++
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var hasTraits = ast.Where(n => n.Kind == NodeKind.HasTraitStatement).ToList();
            Assert.AreEqual(1, hasTraits.Count);
            Assert.AreEqual("Alphaitem", hasTraits[0].HasTraitCondition);
            Assert.AreEqual("Alpha", hasTraits[0].HasTraitTypeName);
            Assert.AreEqual("item", hasTraits[0].HasTraitVariableName);
            Assert.AreEqual(2, hasTraits[0].StatementBody.Count);
            Assert.AreEqual(NodeKind.AssignmentStatement, hasTraits[0].StatementBody[1].Kind);
            Assert.AreEqual("item.y", hasTraits[0].StatementBody[1].AssignmentLeft);
            Assert.AreEqual("+=", hasTraits[0].StatementBody[1].AssignmentOperator);
        }

        [TestMethod]
        public void StartInitializeFinalize_ParseAssignmentsAndParameters()
        {
            const string src =
@"use

module

properties
    Count = 0

initialize(value int32)
    Count += value

finalize
    Count -= 1
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var initializeNode = ast.Single(n => n.Kind == NodeKind.Section && n.Section == Section.Initialize);
            Assert.AreEqual("valueint32", initializeNode.SectionParameters);

            var assignments = ast.Where(n => n.Kind == NodeKind.AssignmentStatement).ToList();
            Assert.AreEqual(2, assignments.Count);
            Assert.AreEqual("+=", assignments[0].AssignmentOperator);
            Assert.AreEqual("-=", assignments[1].AssignmentOperator);
        }

        [TestMethod]
        public void StartSection_ParsesParameters()
        {
            const string src =
@"use

module

start(args str)
    Count = 0
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var startNode = ast.Single(n => n.Kind == NodeKind.Section && n.Section == Section.Start);
            Assert.AreEqual("argsstr", startNode.SectionParameters);
            Assert.AreEqual(1, startNode.SectionParameterList.Count);
            Assert.AreEqual("args", startNode.SectionParameterList[0].Name);
            Assert.AreEqual("str", startNode.SectionParameterList[0].Type);
        }

        [TestMethod]
        public void FunctionsSection_ParsesParameterDefaults()
        {
            const string src =
@"use

module

properties
    Count = 0 int32

functions
    Configure(value int32 = 3) int32
        return Count
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var functionNode = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration);
            Assert.AreEqual(1, functionNode.FunctionParameterList.Count);
            Assert.AreEqual("value", functionNode.FunctionParameterList[0].Name);
            Assert.AreEqual("int32", functionNode.FunctionParameterList[0].Type);
            Assert.AreEqual("3", functionNode.FunctionParameterList[0].DefaultValue);
        }

        [TestMethod]
        public void FunctionsSection_ParsesParameterModifiers()
        {
            const string src =
@"use

module

functions
    Update(count int32 readonly) int32
        return count
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var functionNode = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration);
            Assert.IsTrue(functionNode.FunctionParameterList[0].Modifiers.Contains("readonly"));
        }


        [TestMethod]
        public void FunctionsSection_ParsesDelegateDeclarations()
        {
            const string src =
@"use

module

functions
    OnUpdate(value int32) delegate
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var delegateNode = ast.Single(n => n.Kind == NodeKind.DelegateDeclaration);
            Assert.AreEqual("OnUpdate", delegateNode.DelegateName);
            Assert.AreEqual(1, delegateNode.DelegateParameterList.Count);
            Assert.AreEqual("value", delegateNode.DelegateParameterList[0].Name);
            Assert.AreEqual("int32", delegateNode.DelegateParameterList[0].Type);
        }

        [TestMethod]
        public void FunctionsSection_ParsesFunctionDeclarationsAndBodies()
        {
            const string src =
@"use

module

properties
    Total = 0

functions
    Add(a int32, b int32) int32
        Result = a
        Total += b
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var functionNode = ast.Single(n => n.Kind == NodeKind.FunctionDeclaration);
            Assert.AreEqual("Add", functionNode.FunctionDeclarationName);
            Assert.AreEqual("aint32,bint32", functionNode.FunctionDeclarationParameters);
            Assert.AreEqual("int32", functionNode.FunctionDeclarationReturnType);

            var bodyAssignments = functionNode.FunctionBody
                .Where(n => n.Kind == NodeKind.AssignmentStatement)
                .ToList();
            Assert.AreEqual(2, bodyAssignments.Count);
            Assert.AreEqual("Result", bodyAssignments[0].AssignmentLeft);
            Assert.AreEqual("a", bodyAssignments[0].AssignmentRight);
            Assert.AreEqual("Total", bodyAssignments[1].AssignmentLeft);
            Assert.AreEqual("b", bodyAssignments[1].AssignmentRight);
        }

        [TestMethod]
        public void TypeSection_ParsesPropertyAndFunctionMembers()
        {
            const string src =
@"use

type Sample.Type is object has Alpha

properties
    Count = 1

functions
    Add(a int32) int32
        return a
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var typeNode = ast.Single(n => n.Kind == NodeKind.TypeDeclaration);
            Assert.AreEqual(1, typeNode.TypeProperties.Count);
            Assert.AreEqual("Count", typeNode.TypeProperties[0].PropertyName);
            Assert.AreEqual(1, typeNode.TypeFunctions.Count);
            Assert.AreEqual("Add", typeNode.TypeFunctions[0].FunctionDeclarationName);
        }

        [TestMethod]
        public void TraitSection_ParsesPropertyAndFunctionMembers()
        {
            const string src =
@"use

trait Alpha

properties
    Value = 0

functions
    Get() int32
        return Value
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var traitNode = ast.Single(n => n.Kind == NodeKind.TypeDeclaration);
            Assert.AreEqual(1, traitNode.TypeProperties.Count);
            Assert.AreEqual("Value", traitNode.TypeProperties[0].PropertyName);
            Assert.AreEqual(1, traitNode.TypeFunctions.Count);
            Assert.AreEqual("Get", traitNode.TypeFunctions[0].FunctionDeclarationName);
        }

        private static long EvaluateExpression(ExpressionNode? node)
        {
            if (node == null)
            {
                return 0;
            }

            return node.Kind switch
            {
                ExpressionKind.Literal => ParseLiteral(node.Value),
                ExpressionKind.Identifier => ParseLiteral(node.Value),
                ExpressionKind.Unary => EvaluateUnary(node.Value, EvaluateExpression(node.Left)),
                ExpressionKind.Cast => EvaluateExpression(node.Left),
                ExpressionKind.Conditional => EvaluateExpression(node.Left) != 0
                    ? EvaluateExpression(node.Right)
                    : EvaluateExpression(node.Arguments.FirstOrDefault()),
                ExpressionKind.Binary => EvaluateBinary(node.Value, EvaluateExpression(node.Left), EvaluateExpression(node.Right)),
                _ => 0
            };
        }

        private static long ParseLiteral(string? value)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return long.TryParse(value, out var number) ? number : 0;
        }

        private static long EvaluateUnary(string? op, long operand) => op switch
        {
            "++" => operand + 1,
            "--" => operand - 1,
            "-" => -operand,
            "+" => operand,
            "!" => operand == 0 ? 1 : 0,
            "~" => ~operand,
            "not" => operand == 0 ? 1 : 0,
            _ => operand
        };

        private static long EvaluateBinary(string? op, long left, long right) => op switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => right == 0 ? 0 : left / right,
            "%" => right == 0 ? 0 : left % right,
            "<<" => left << (int)right,
            ">>" => left >> (int)right,
            "&" => left & right,
            "^" => left ^ right,
            "|" => left | right,
            "==" => left == right ? 1 : 0,
            "!=" => left != right ? 1 : 0,
            "<" => left < right ? 1 : 0,
            ">" => left > right ? 1 : 0,
            "<=" => left <= right ? 1 : 0,
            ">=" => left >= right ? 1 : 0,
            "and" => (left != 0 && right != 0) ? 1 : 0,
            "or" => (left != 0 || right != 0) ? 1 : 0,
            "," => right,
            _ => 0
        };

        private static long ApplyAssignment(long current, string assignmentOperator, long value) => assignmentOperator switch
        {
            "=" => value,
            "/=" => value == 0 ? 0 : current / value,
            "*=" => current * value,
            "%=" => value == 0 ? 0 : current % value,
            "+=" => current + value,
            "-=" => current - value,
            "<<=" => current << (int)value,
            ">>=" => current >> (int)value,
            "&=" => current & value,
            "^=" => current ^ value,
            "|=" => current | value,
            _ => current
        };
    }
}
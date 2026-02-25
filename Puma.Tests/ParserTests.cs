using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Puma.Parser;

namespace Puma.Tests
{
    [TestClass]
    public class ParserTests
    {
        private const string Sample =
@"using

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

start
    if status
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var ifStatements = ast.Where(n => n.Kind == NodeKind.IfStatement).ToList();
            Assert.AreEqual(1, ifStatements.Count);
            Assert.AreEqual("status", ifStatements[0].IfCondition);
        }


        [TestMethod]
        public void StartAndInitialize_ParseMatchStatements()
        {
            const string src =
@"use

module

properties
    value = 0

start
    match value
        when 1
        when 2
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
        public void StartAndInitialize_ParseWhileStatements()
        {
            const string src =
@"use

module

properties
    running = true

initialize
    while isReady
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var whileNodes = ast.Where(n => n.Kind == NodeKind.WhileStatement).ToList();
            Assert.AreEqual(1, whileNodes.Count);
            Assert.AreEqual("isReady", whileNodes[0].WhileCondition);
        }

        [TestMethod]
        public void StartAndInitialize_ParseForStatements()
        {
            const string src =
@"use

module

properties
    items = 0

start
    forall entry in records
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var forAllNode = ast.Single(n => n.Kind == NodeKind.ForAllStatement);
            Assert.AreEqual("entry", forAllNode.ForVariable);
            Assert.AreEqual("records", forAllNode.ForContainer);
        }

        [TestMethod]
        public void StartAndInitialize_ParseRepeatStatements()
        {
            const string src =
@"use

module

properties
    dummy = 0

initialize
    repeat
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

start
    has item
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

initialize
    has trait item
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);

            var hasTraits = ast.Where(n => n.Kind == NodeKind.HasTraitStatement).ToList();
            Assert.AreEqual(1, hasTraits.Count);
            Assert.AreEqual("item", hasTraits[0].HasTraitCondition);
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

functions
    Configure(value int32 = 3)
        return value
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
    Update(count int32 readonly)
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

functions
    Add(a int32, b int32) int32
        Result = a
        .Total += b
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
            Assert.AreEqual(".Total", bodyAssignments[1].AssignmentLeft);
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
    }
}
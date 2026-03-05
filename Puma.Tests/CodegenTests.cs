using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Puma.Tests
{
    [TestClass]
    public class CodegenTests
    {
        private const string Sample =
@"use

module

properties
    Count = 1

functions
    Add(a int32, b int32) int32
        Result = a
";

        private static string Normalize(string s) =>
            s.Replace("\r\n", "\n").Replace("\r", "\n");

        private static string GenerateCode(string src)
        {
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            return codegen.Generate(ast);
        }

        [TestMethod]
        public void Generate_EmitsExpectedCOutput()
        {
            var expected =
@"int64_t Count = 1;

int32_t Add(int32_t a, int32_t b)
{
    Result = a;
}
";
            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(Sample);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsHasTraitStatementAsTypeGuard()
        {
            const string src =
@"use

module

properties
    x = refValue

start
    has TraitName x
        z = (TraitName)x
        z.f()
        z.y++
";

            var expected =
@"int64_t x = refValue;

int main()
{
    if (x != null && typeof(x) == TraitNameType)
    {
        z = (TraitName)x;
        z.f();
        z.y += 1;
    }
    return 0;
}
";

            var c = GenerateCode(src);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsHasStatementAsNullCheckWithBody()
        {
            const string src =
@"use

module

properties
    x = refValue

start
    has x
        x.f()
";

            var expected =
@"int64_t x = refValue;

int main()
{
    if (x != nullptr)
    {
        x.f();
    }
    return 0;
}
";

            var c = GenerateCode(src);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsTypeMembers()
        {
            const string src =
@"use

type Sample.Type is object

properties
    Count = 1

functions
    Add(a int32) int32
        return a
";

            var expected =
@"class Sample::Type : public object
{
public:
    int64_t Count = 1;
    int32_t Add(int32_t a)
    {
        return a;
    }
};
";

            var c = GenerateCode(src);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsIncludesForUseAndBool()
        {
            const string src =
@"use System.Console as Console

module

properties
    Flag = true

start
    WriteLine(""Hi"")
";

            var expected =
@"#include <System/Console>
#include <stdio.h>

bool_t Flag = true;

int main()
{
    puts(""Hi"");
    return 0;
}
";

            var c = GenerateCode(src);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsTraitMembers()
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

            var expected =
@"class Alpha
{
public:
    int64_t Value = 0;
    int32_t Get()
    {
        return Value;
    }
};
";

            var c = GenerateCode(src);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsEnumsAndRecords()
        {
            const string src =
@"use

module

enums
    Status
        Active
        Inactive

records
    UserRecord pack 4
        Name str
        Age int
";

            var expected =
@"typedef enum Status {
    Active,
    Inactive
} Status;

typedef struct UserRecord {
    int Name;
    int Age;
} UserRecord;
";

            var c = GenerateCode(src);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsFinalizeCall()
        {
            const string src =
@"use

module

properties
    Count = 0

start
    Count = 3

finalize
    Count = 2
";

            var expected =
@"int64_t Count = 0;

void finalize()
{
    Count = 2;
}

int main()
{
    Count = 3;
    finalize();
    return 0;
}
";

            var c = GenerateCode(src);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsControlFlowStatements()
        {
            const string src =
@"use

module

properties
    Count = 0

start
    if Count
        Count = 1
    else
        Count = 2
    while Count
        Count = 3
";

            var expected =
@"int64_t Count = 0;

int main()
{
    if (Count)
    {
        Count = 1;
    }
    else
    {
        Count = 2;
    }
    while (Count)
    {
        Count = 3;
    }
    return 0;
}
";

            var c = GenerateCode(src);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsTypeSkeleton()
        {
            const string src =
@"use

type Sample.Type is object has Alpha, Beta
";

            var expected =
@"class Sample::Type : public object, public Alpha, public Beta
{
public:
};
";

            var c = GenerateCode(src);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsTraitSkeleton()
        {
            const string src =
@"use

trait Alpha
";

            var expected =
@"class Alpha
{
public:
};
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_MapsFunctionTypes_ToConfiguredCppTypes()
        {
            const string src =
@"use

module

functions
    Convert(i int, u uint, f flt, b bool, c char, s str, x fix32) uint64
        return 0
";

            var expected =
@"uint64_t Convert(int64_t i, uint64_t u, double f, bool_t b, uint8[4] c, stdstr s, int32_t x)
{
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_InfersStringProperty_AsStdStr()
        {
            const string src =
@"use

module

properties
    Name = ""Puma""
";

            var expected =
@"stdstr Name = ""Puma"";
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsConditionalExpressionInAssignment()
        {
            const string src =
@"use

module

properties
    A = 0

start
    A = 1 if true else 2
";

            var expected =
@"int64_t A = 0;

int main()
{
    A = (true ? 1 : 2);
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsCommaMultiExpressionInAssignment()
        {
            const string src =
@"use

module

properties
    A = 0

start
    A = 1 + 2, 3 * 4
";

            var expected =
@"int64_t A = 0;

int main()
{
    A = ((1 + 2) , (3 * 4));
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsNestedConditionalAndCommaExpression()
        {
            const string src =
@"use

module

properties
    A = 0

start
    A = (1 if true else 2), (3 if false else 4)
";

            var expected =
@"int64_t A = 0;

int main()
{
    A = ((true ? 1 : 2) , (false ? 3 : 4));
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsConditionalWithBitwiseAndAdditiveSubexpressions()
        {
            const string src =
@"use

module

properties
    A = 0

start
    A = 1 + 2 if 1 | 2 < 4 else 5 * 6
";

            var expected =
@"int64_t A = 0;

int main()
{
    A = (((1 | 2) < 4) ? (1 + 2) : (5 * 6));
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsConditionalExpressionInsideFunctionArguments()
        {
            const string src =
@"use

module

properties
    A = 0

start
    A = Print(1 if true else 2, 3)
";

            var expected =
@"int64_t A = 0;

int main()
{
    A = Print((true ? 1 : 2), 3);
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsLeftAssociativeConditionalChainShape()
        {
            const string src =
@"use

module

properties
    A = 0

start
    A = 1 if true else 2 if false else 3
";

            var expected =
@"int64_t A = 0;

int main()
{
    A = (false ? (true ? 1 : 2) : 3);
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsConditionalExpressionInsideStatementLevelFunctionArguments()
        {
            const string src =
@"use

module

start
    Print(1 if true else 2, 3)
";

            var expected =
@"int main()
{
    Print((true ? 1 : 2), 3);
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsParenthesizedCommaExpressionAsSingleFunctionArgument()
        {
            const string src =
@"use

module

start
    Print((1, 2), 3)
";

            var expected =
@"int main()
{
    Print((1 , 2), 3);
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsMatchWhenAsSwitchCases()
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

            var expected =
@"int64_t value = 0;
int64_t Count = 0;

int main()
{
    switch (value)
    {
        case 1:
            Count = 1;
            break;
        case 2:
            Count = 2;
            break;
    }
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsForAndRepeatStatements()
        {
            const string src =
@"use

module

properties
    items = 0
    Count = 0

start
    for entry in items
        Count = 1
    repeat Count
        Count -= 1
";

            var expected =
@"int64_t items = 0;
int64_t Count = 0;

int main()
{
    for (auto entry : items)
    {
        Count = 1;
    }
    do
    {
        Count -= 1;
    } while (Count);
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsInitializeSectionFunctionWithTypedParameters()
        {
            const string src =
@"use

module

properties
    Count = 0

initialize(value int32)
    Count += value
";

            var expected =
@"int64_t Count = 0;

void initialize(int32_t value)
{
    Count += value;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsBreakContinueYieldErrorCatchStatements()
        {
            const string src =
@"use

module

properties
    Count = 1

start
    while Count
        continue
        break
    yield Count
    error Count
    catch Count
";

            var expected =
@"int64_t Count = 1;

int main()
{
    while (Count)
    {
        continue;
        break;
    }
    /* yield Count */
    /* error Count */
    /* catch Count */
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }

        [TestMethod]
        public void Generate_EmitsForAllStatements()
        {
            const string src =
@"use

module

properties
    items = 0
    Count = 0

start
    forall entry in items
        Count += 1
";

            var expected =
@"int64_t items = 0;
int64_t Count = 0;

int main()
{
    for (auto entry : items)
    {
        Count += 1;
    }
    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(c).Trim());
        }
    }
}
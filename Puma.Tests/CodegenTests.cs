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

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

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

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

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

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

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

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

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

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

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

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

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

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(src);
            var ast = parser.Parse(tokens);
            var c = codegen.Generate(ast);

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
    }
}
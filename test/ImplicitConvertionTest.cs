using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class ImplicitConvertionTest
    {
        private static string Normalize(string s) =>
            s.Replace("\r\n", "\n").Replace("\r", "\n");

        [TestMethod]
        public void Convertion_ImplicitExample_UInt8_OutputText_AreConsistent()
        {
            const string src =
@"properties
    a = 0 uint8
    b = 1 uint8
    c = 2 uint8
    d = 3 uint8
    e = 4 uint8
    f = 5 uint8
    g = 6 uint8
    h = 7 uint8
    i = 8 uint8

start
    m = 0 uint8
    n = 0 uint16
    o = 0 uint32
    p = 0 uint64
    q = 0 int16
    r = 0 int32
    s = 0 int64
    t = 0 flt32
    u = 0 flt64
    m = a
    n = b
    o = c
    p = d
    q = e
    r = f
    s = g
    t = h
    u = i
";

            var expected =
@"#include <cstdint>

// properties
auto a = (uint8_t)0;
auto b = (uint8_t)1;
auto c = (uint8_t)2;
auto d = (uint8_t)3;
auto e = (uint8_t)4;
auto f = (uint8_t)5;
auto g = (uint8_t)6;
auto h = (uint8_t)7;
auto i = (uint8_t)8;

// start
int main()
{
    auto m = (uint8_t)0;
    auto n = (uint16_t)0;
    auto o = (uint32_t)0;
    auto p = (uint64_t)0;
    auto q = (int16_t)0;
    auto r = (int32_t)0;
    auto s = (int64_t)0;
    auto t = (float)0;
    auto u = (double)0;
    m = a;
    n = b;
    o = c;
    p = d;
    q = e;
    r = f;
    s = g;
    t = h;
    u = i;
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
        public void Convertion_ImplicitExample_Int8_OutputText_AreConsistent()
        {
            const string src =
@"properties
    a = 0 int8
    b = 1 int8
    c = 2 int8
    d = 3 int8
    e = 4 int8
    f = 5 int8

start
    m = 0 int8
    n = 0 int16
    o = 0 int32
    p = 0 int64
    q = 0 flt32
    r = 0 flt64
    m = a
    n = b
    o = c
    p = d
    q = e
    r = f
";

            var expected =
@"#include <cstdint>

// properties
auto a = (int8_t)0;
auto b = (int8_t)1;
auto c = (int8_t)2;
auto d = (int8_t)3;
auto e = (int8_t)4;
auto f = (int8_t)5;

// start
int main()
{
    auto m = (int8_t)0;
    auto n = (int16_t)0;
    auto o = (int32_t)0;
    auto p = (int64_t)0;
    auto q = (float)0;
    auto r = (double)0;
    m = a;
    n = b;
    o = c;
    p = d;
    q = e;
    r = f;
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
        public void Convertion_ImplicitExample_UInt16_OutputText_AreConsistent()
        {
            const string src =
@"properties
    a = 0 uint16
    b = 1 uint16
    c = 2 uint16
    d = 3 uint16
    e = 4 uint16
    f = 5 uint16
    g = 6 uint16

start
    m = 0 uint16
    n = 0 uint32
    o = 0 uint64
    p = 0 int32
    q = 0 int64
    r = 0 flt32
    s = 0 flt64
    m = a
    n = b
    o = c
    p = d
    q = e
    r = f
    s = g
";

            var expected =
@"#include <cstdint>

// properties
auto a = (uint16_t)0;
auto b = (uint16_t)1;
auto c = (uint16_t)2;
auto d = (uint16_t)3;
auto e = (uint16_t)4;
auto f = (uint16_t)5;
auto g = (uint16_t)6;

// start
int main()
{
    auto m = (uint16_t)0;
    auto n = (uint32_t)0;
    auto o = (uint64_t)0;
    auto p = (int32_t)0;
    auto q = (int64_t)0;
    auto r = (float)0;
    auto s = (double)0;
    m = a;
    n = b;
    o = c;
    p = d;
    q = e;
    r = f;
    s = g;
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
        public void Convertion_ImplicitExample_Int16_OutputText_AreConsistent()
        {
            const string src =
@"properties
    a = 0 int16
    b = 1 int16
    c = 2 int16
    d = 3 int16
    e = 4 int16

start
    m = 0 int16
    n = 0 int32
    o = 0 int64
    p = 0 flt32
    q = 0 flt64
    m = a
    n = b
    o = c
    p = d
    q = e
";

            var expected =
@"#include <cstdint>

// properties
auto a = (int16_t)0;
auto b = (int16_t)1;
auto c = (int16_t)2;
auto d = (int16_t)3;
auto e = (int16_t)4;

// start
int main()
{
    auto m = (int16_t)0;
    auto n = (int32_t)0;
    auto o = (int64_t)0;
    auto p = (float)0;
    auto q = (double)0;
    m = a;
    n = b;
    o = c;
    p = d;
    q = e;
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
        public void Convertion_ImplicitExample_UInt32_OutputText_AreConsistent()
        {
            const string src =
@"properties
    a = 0 uint32
    b = 1 uint32
    c = 2 uint32
    d = 3 uint32

start
    m = 0 uint32
    n = 0 uint64
    o = 0 int64
    p = 0 flt64
    m = a
    n = b
    o = c
    p = d
";

            var expected =
@"#include <cstdint>

// properties
auto a = (uint32_t)0;
auto b = (uint32_t)1;
auto c = (uint32_t)2;
auto d = (uint32_t)3;

// start
int main()
{
    auto m = (uint32_t)0;
    auto n = (uint64_t)0;
    auto o = (int64_t)0;
    auto p = (double)0;
    m = a;
    n = b;
    o = c;
    p = d;
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
        public void Convertion_ImplicitExample_Int32_OutputText_AreConsistent()
        {
            const string src =
@"properties
    a = 0 int32
    b = 1 int32
    c = 2 int32

start
    m = 0 int32
    n = 0 int64
    o = 0 flt64
    m = a
    n = b
    o = c
";

            var expected =
@"#include <cstdint>

// properties
auto a = (int32_t)0;
auto b = (int32_t)1;
auto c = (int32_t)2;

// start
int main()
{
    auto m = (int32_t)0;
    auto n = (int64_t)0;
    auto o = (double)0;
    m = a;
    n = b;
    o = c;
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
        public void Convertion_ImplicitExample_UInt64_OutputText_AreConsistent()
        {
            const string src =
@"properties
    a = 0 uint64
    b = 1 uint64

start
    m = 0 uint64
    n = 0 uint64
    m = a
    n = b
";

            var expected =
@"#include <cstdint>

// properties
auto a = (uint64_t)0;
auto b = (uint64_t)1;

// start
int main()
{
    auto m = (uint64_t)0;
    auto n = (uint64_t)0;
    m = a;
    n = b;
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
        public void Convertion_ImplicitExample_Int64_OutputText_AreConsistent()
        {
            const string src =
@"properties
    a = 0 int64
    b = 1 int64

start
    m = 0 int64
    n = 0 int64
    m = a
    n = b
";

            var expected =
@"#include <cstdint>

// properties
auto a = (int64_t)0;
auto b = (int64_t)1;

// start
int main()
{
    auto m = (int64_t)0;
    auto n = (int64_t)0;
    m = a;
    n = b;
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
        public void Convertion_ImplicitExample_Flt32_OutputText_AreConsistent()
        {
            const string src =
@"properties
    a = 0 flt32
    b = 1 flt32

start
    m = 0 flt32
    n = 0 flt64
    m = a
    n = b
";

            var expected =
@"// properties
auto a = (float)0;
auto b = (float)1;

// start
int main()
{
    auto m = (float)0;
    auto n = (double)0;
    m = a;
    n = b;
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
        public void Convertion_ImplicitExample_Flt64_OutputText_AreConsistent()
        {
            const string src =
@"properties
    a = 0 flt64

start
    m = 0 flt64
    m = a
";

            var expected =
@"// properties
auto a = (double)0;

// start
int main()
{
    auto m = (double)0;
    m = a;
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
    }
}
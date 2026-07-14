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
        public void Convertion_ImplicitBoundary_IntegerWidths_OutputText_AreConsistent()
        {
            const string src =
@"start
    u8Min = 0 uint8
    u8Max = 255 uint8
    u16Min = 0 uint16
    u16Max = 65535 uint16
    u32Min = 0 uint32
    u32Max = 4294967295 uint32
    u64Min = 0 uint64
    u64Max = 18446744073709551615 uint64
    i8Min = 0 int8
    i8Max = 127 int8
    i16Min = 0 int16
    i16Max = 32767 int16
    i32Min = 0 int32
    i32Max = 2147483647 int32
    i64Min = 0 int64
    i64Max = 9223372036854775807 int64
    i8Min = -128
    i16Min = -32768
    i32Min = -2147483648
    i64Min = -9223372036854775808
";

            var expected =
@"#include <cstdint>

// start
int main()
{
    auto u8Min = (uint8_t)0;
    auto u8Max = (uint8_t)255;
    auto u16Min = (uint16_t)0;
    auto u16Max = (uint16_t)65535;
    auto u32Min = (uint32_t)0;
    auto u32Max = (uint32_t)4294967295;
    auto u64Min = (uint64_t)0;
    auto u64Max = (uint64_t)18446744073709551615;
    auto i8Min = (int8_t)0;
    auto i8Max = (int8_t)127;
    auto i16Min = (int16_t)0;
    auto i16Max = (int16_t)32767;
    auto i32Min = (int32_t)0;
    auto i32Max = (int32_t)2147483647;
    auto i64Min = (int64_t)0;
    auto i64Max = (int64_t)9223372036854775807;
    i8Min = -128;
    i16Min = -32768;
    i32Min = -2147483648;
    i64Min = -9223372036854775808;
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

        [TestMethod]
        public void Convertion_ImplicitBoundary_FloatingWidths_OutputText_AreConsistent()
        {
            const string src =
@"start
    f32Min = 1.17549435e-38 flt32
    f32Max = 3.4028235e+38 flt32
    f64Min = 2.2250738585072014e-308 flt64
    f64Max = 1.7976931348623157e+308 flt64
    g32 = 0 flt32
    g64 = 0 flt64
    g32 = f32Max
    g64 = f64Max
";

            var expected =
@"// start
int main()
{
    auto f32Min = 1.17549435e-38;
    auto f32Max = 3.4028235e+38;
    auto f64Min = 2.2250738585072014e-308;
    auto f64Max = 1.7976931348623157e+308;
    auto g32 = (float)0;
    auto g64 = (double)0;
    g32 = f32Max;
    g64 = f64Max;

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
        public void Convertion_MixedExpression_NumericTypes_OutputText_AreConsistent()
        {
            const string src =
@"start
    u16 = 10 uint16
    i32 = 20 int32
    f64 = 30 flt64
    sum1 = 0 int32
    sum2 = 0 flt64
    sum3 = 0 flt64
    sum1 = u16 + i32
    sum2 = u16 + f64
    sum3 = i32 + f64
";

            var expected =
@"#include <cstdint>

// start
int main()
{
    auto u16 = (uint16_t)10;
    auto i32 = (int32_t)20;
    auto f64 = (double)30;
    auto sum1 = (int32_t)0;
    auto sum2 = (double)0;
    auto sum3 = (double)0;
    sum1 = u16 + i32;
    sum2 = u16 + f64;
    sum3 = i32 + f64;
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
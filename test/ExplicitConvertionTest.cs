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
    public class ExplicitConvertionTest
    {
        private static string Normalize(string s) =>
            s.Replace("\r\n", "\n").Replace("\r", "\n");

        private static void EnsureImplicit(Convertion.Type fromType, Convertion.Type toType)
        {
            if (!Convertion.IsImplicit(fromType, toType))
            {
                throw new InvalidOperationException($"Implicit conversion is not valid: {fromType} -> {toType}");
            }
        }

        [TestMethod]
        public void ConvertionTable_AllImplicitEntries_AreImplicit()
        {
            var max = Enum.GetValues<Convertion.Type>().Length;
            for (var from = 0; from < max; from++)
            {
                for (var to = 0; to < max; to++)
                {
                    var fromType = (Convertion.Type)from;
                    var toType = (Convertion.Type)to;
                    if (Convertion.GetConversionType(fromType, toType) == 'I')
                    {
                        Assert.IsTrue(
                            Convertion.IsImplicit(fromType, toType),
                            $"Expected implicit conversion for {fromType} -> {toType}.");
                    }
                }
            }
        }

        [TestMethod]
        public void Convertion_ExplicitExample_OutputText_AreConsistent()
        {
            const string pumaSource =
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
    j = 9 uint8

start
    m = a uint8
    n = b uint16
    o = c uint32
    p = d uint64
    q = e int8
    r = f int16
    s = g int32
    t = h int64
    u = i flt32
    v = j flt64
";

            var expected =
@"// properties
auto a = (uint8_t)0;
auto b = (uint8_t)1;
auto c = (uint8_t)2;
auto d = (uint8_t)3;
auto e = (uint8_t)4;
auto f = (uint8_t)5;
auto g = (uint8_t)6;
auto h = (uint8_t)7;
auto i = (uint8_t)8;
auto j = (uint8_t)9;

// start
int main()
{
    auto m = (uint8_t) a;
    auto n = (uint16_t) b;
    auto o = (uint32_t) c;
    auto p = (uint64_t) d;
    auto q = (int8_t) e;
    auto r = (int16_t) f;
    auto s = (int32_t) g;
    auto t = (int64_t) h;
    auto u = (float) i;
    auto v = (double) j;

    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(pumaSource);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Convertion_ExplicitExample_Int32_OutputText_AreConsistent()
        {
            const string pumaSource =
@"properties
    a = 0 int32
    b = 1 int32
    c = 2 int32
    d = 3 int32
    e = 4 int32
    f = 5 int32
    g = 6 int32
    h = 7 int32
    i = 8 int32
    j = 9 int32


start
    m = a uint8
    n = b uint16
    o = c uint32
    p = d uint64
    q = e int8
    r = f int16
    s = g int32
    t = h int64
    u = i flt32
    v = j flt64
";

            var expected =
@"// properties
auto a = (int32_t)0;
auto b = (int32_t)1;
auto c = (int32_t)2;
auto d = (int32_t)3;
auto e = (int32_t)4;
auto f = (int32_t)5;
auto g = (int32_t)6;
auto h = (int32_t)7;
auto i = (int32_t)8;
auto j = (int32_t)9;


// start
int main()
{
    auto m = (uint8_t) a;
    auto n = (uint16_t) b;
    auto o = (uint32_t) c;
    auto p = (uint64_t) d;
    auto q = (int8_t) e;
    auto r = (int16_t) f;
    auto s = (int32_t) g;
    auto t = (int64_t) h;
    auto u = (float) i;
    auto v = (double) j;

    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(pumaSource);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Convertion_ExplicitExample_Int8_OutputText_AreConsistent()
        {
            const string pumaSource =
@"properties
    a = 0 int8
    b = 1 int8
    c = 2 int8
    d = 3 int8
    e = 4 int8
    f = 5 int8
    g = 6 int8
    h = 7 int8
    i = 8 int8
    j = 9 int8


start
    m = a uint8
    n = b uint16
    o = c uint32
    p = d uint64
    q = e int8
    r = f int16
    s = g int32
    t = h int64
    u = i flt32
    v = j flt64
";

            var expected =
@"// properties
auto a = (int8_t)0;
auto b = (int8_t)1;
auto c = (int8_t)2;
auto d = (int8_t)3;
auto e = (int8_t)4;
auto f = (int8_t)5;
auto g = (int8_t)6;
auto h = (int8_t)7;
auto i = (int8_t)8;
auto j = (int8_t)9;


// start
int main()
{
    auto m = (uint8_t) a;
    auto n = (uint16_t) b;
    auto o = (uint32_t) c;
    auto p = (uint64_t) d;
    auto q = (int8_t) e;
    auto r = (int16_t) f;
    auto s = (int32_t) g;
    auto t = (int64_t) h;
    auto u = (float) i;
    auto v = (double) j;

    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(pumaSource);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Convertion_ExplicitExample_UInt64_OutputText_AreConsistent()
        {
            const string pumaSource =
@"properties
    a = 0 uint64
    b = 1 uint64
    c = 2 uint64
    d = 3 uint64
    e = 4 uint64
    f = 5 uint64
    g = 6 uint64
    h = 7 uint64
    i = 8 uint64
    j = 9 uint64


start
    m = a uint8
    n = b uint16
    o = c uint32
    p = d uint64
    q = e int8
    r = f int16
    s = g int32
    t = h int64
    u = i flt32
    v = j flt64
";

            var expected =
@"// properties
auto a = (uint64_t)0;
auto b = (uint64_t)1;
auto c = (uint64_t)2;
auto d = (uint64_t)3;
auto e = (uint64_t)4;
auto f = (uint64_t)5;
auto g = (uint64_t)6;
auto h = (uint64_t)7;
auto i = (uint64_t)8;
auto j = (uint64_t)9;


// start
int main()
{
    auto m = (uint8_t) a;
    auto n = (uint16_t) b;
    auto o = (uint32_t) c;
    auto p = (uint64_t) d;
    auto q = (int8_t) e;
    auto r = (int16_t) f;
    auto s = (int32_t) g;
    auto t = (int64_t) h;
    auto u = (float) i;
    auto v = (double) j;

    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(pumaSource);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Convertion_ExplicitExample_UInt32_OutputText_AreConsistent()
        {
            const string pumaSource =
@"properties
    a = 0 uint32
    b = 1 uint32
    c = 2 uint32
    d = 3 uint32
    e = 4 uint32
    f = 5 uint32
    g = 6 uint32
    h = 7 uint32
    i = 8 uint32
    j = 9 uint32


start
    m = a uint8
    n = b uint16
    o = c uint32
    p = d uint64
    q = e int8
    r = f int16
    s = g int32
    t = h int64
    u = i flt32
    v = j flt64
";

            var expected =
@"// properties
auto a = (uint32_t)0;
auto b = (uint32_t)1;
auto c = (uint32_t)2;
auto d = (uint32_t)3;
auto e = (uint32_t)4;
auto f = (uint32_t)5;
auto g = (uint32_t)6;
auto h = (uint32_t)7;
auto i = (uint32_t)8;
auto j = (uint32_t)9;


// start
int main()
{
    auto m = (uint8_t) a;
    auto n = (uint16_t) b;
    auto o = (uint32_t) c;
    auto p = (uint64_t) d;
    auto q = (int8_t) e;
    auto r = (int16_t) f;
    auto s = (int32_t) g;
    auto t = (int64_t) h;
    auto u = (float) i;
    auto v = (double) j;

    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(pumaSource);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Convertion_ExplicitExample_Flt64_OutputText_AreConsistent()
        {
            const string pumaSource =
@"properties
    a = 0 flt64
    b = 1 flt64
    c = 2 flt64
    d = 3 flt64
    e = 4 flt64
    f = 5 flt64
    g = 6 flt64
    h = 7 flt64
    i = 8 flt64
    j = 9 flt64


start
    m = a uint8
    n = b uint16
    o = c uint32
    p = d uint64
    q = e int8
    r = f int16
    s = g int32
    t = h int64
    u = i flt32
    v = j flt64
";

            var expected =
@"// properties
auto a = (double)0;
auto b = (double)1;
auto c = (double)2;
auto d = (double)3;
auto e = (double)4;
auto f = (double)5;
auto g = (double)6;
auto h = (double)7;
auto i = (double)8;
auto j = (double)9;


// start
int main()
{
    auto m = (uint8_t) a;
    auto n = (uint16_t) b;
    auto o = (uint32_t) c;
    auto p = (uint64_t) d;
    auto q = (int8_t) e;
    auto r = (int16_t) f;
    auto s = (int32_t) g;
    auto t = (int64_t) h;
    auto u = (float) i;
    auto v = (double) j;

    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(pumaSource);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Convertion_ExplicitExample_Flt32_OutputText_AreConsistent()
        {
            const string pumaSource =
@"properties
    a = 0 flt32
    b = 1 flt32
    c = 2 flt32
    d = 3 flt32
    e = 4 flt32
    f = 5 flt32
    g = 6 flt32
    h = 7 flt32
    i = 8 flt32
    j = 9 flt32


start
    m = a uint8
    n = b uint16
    o = c uint32
    p = d uint64
    q = e int8
    r = f int16
    s = g int32
    t = h int64
    u = i flt32
    v = j flt64
";

            var expected =
@"// properties
auto a = (float)0;
auto b = (float)1;
auto c = (float)2;
auto d = (float)3;
auto e = (float)4;
auto f = (float)5;
auto g = (float)6;
auto h = (float)7;
auto i = (float)8;
auto j = (float)9;


// start
int main()
{
    auto m = (uint8_t) a;
    auto n = (uint16_t) b;
    auto o = (uint32_t) c;
    auto p = (uint64_t) d;
    auto q = (int8_t) e;
    auto r = (int16_t) f;
    auto s = (int32_t) g;
    auto t = (int64_t) h;
    auto u = (float) i;
    auto v = (double) j;

    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(pumaSource);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Convertion_ExplicitExample_Int64_OutputText_AreConsistent()
        {
            const string pumaSource =
@"properties
    a = 0 int64
    b = 1 int64
    c = 2 int64
    d = 3 int64
    e = 4 int64
    f = 5 int64
    g = 6 int64
    h = 7 int64
    i = 8 int64
    j = 9 int64

start
    m = a uint8
    n = b uint16
    o = c uint32
    p = d uint64
    q = e int8
    r = f int16
    s = g int32
    t = h int64
    u = i flt32
    v = j flt64
";

            var expected =
@"// properties
auto a = (int64_t)0;
auto b = (int64_t)1;
auto c = (int64_t)2;
auto d = (int64_t)3;
auto e = (int64_t)4;
auto f = (int64_t)5;
auto g = (int64_t)6;
auto h = (int64_t)7;
auto i = (int64_t)8;
auto j = (int64_t)9;

// start
int main()
{
    auto m = (uint8_t) a;
    auto n = (uint16_t) b;
    auto o = (uint32_t) c;
    auto p = (uint64_t) d;
    auto q = (int8_t) e;
    auto r = (int16_t) f;
    auto s = (int32_t) g;
    auto t = (int64_t) h;
    auto u = (float) i;
    auto v = (double) j;

    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(pumaSource);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Convertion_ExplicitExample_Int16_OutputText_AreConsistent()
        {
            const string pumaSource =
@"properties
    a = 0 int16
    b = 1 int16
    c = 2 int16
    d = 3 int16
    e = 4 int16
    f = 5 int16
    g = 6 int16
    h = 7 int16
    i = 8 int16
    j = 9 int16

start
    m = a uint8
    n = b uint16
    o = c uint32
    p = d uint64
    q = e int8
    r = f int16
    s = g int32
    t = h int64
    u = i flt32
    v = j flt64
";

            var expected =
@"// properties
auto a = (int16_t)0;
auto b = (int16_t)1;
auto c = (int16_t)2;
auto d = (int16_t)3;
auto e = (int16_t)4;
auto f = (int16_t)5;
auto g = (int16_t)6;
auto h = (int16_t)7;
auto i = (int16_t)8;
auto j = (int16_t)9;

// start
int main()
{
    auto m = (uint8_t) a;
    auto n = (uint16_t) b;
    auto o = (uint32_t) c;
    auto p = (uint64_t) d;
    auto q = (int8_t) e;
    auto r = (int16_t) f;
    auto s = (int32_t) g;
    auto t = (int64_t) h;
    auto u = (float) i;
    auto v = (double) j;

    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(pumaSource);
            var ast = parser.Parse(tokens);
            var generated = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generated).Trim());
        }

        [TestMethod]
        public void Convertion_ExplicitExample_UInt16_OutputText_AreConsistent()
        {
            const string pumaSource =
@"properties
    a = 0 uint16
    b = 1 uint16
    c = 2 uint16
    d = 3 uint16
    e = 4 uint16
    f = 5 uint16
    g = 6 uint16
    h = 7 uint16
    i = 8 uint16
    j = 9 uint16

start
    m = a uint8
    n = b uint16
    o = c uint32
    p = d uint64
    q = e int8
    r = f int16
    s = g int32
    t = h int64
    u = i flt32
    v = j flt64
";

            var expected =
@"// properties
auto a = (uint16_t)0;
auto b = (uint16_t)1;
auto c = (uint16_t)2;
auto d = (uint16_t)3;
auto e = (uint16_t)4;
auto f = (uint16_t)5;
auto g = (uint16_t)6;
auto h = (uint16_t)7;
auto i = (uint16_t)8;
auto j = (uint16_t)9;

// start
int main()
{
    auto m = (uint8_t) a;
    auto n = (uint16_t) b;
    auto o = (uint32_t) c;
    auto p = (uint64_t) d;
    auto q = (int8_t) e;
    auto r = (int16_t) f;
    auto s = (int32_t) g;
    auto t = (int64_t) h;
    auto u = (float) i;
    auto v = (double) j;

    return 0;
}
";

            var lexer = new Puma.Lexer();
            var parser = new Puma.Parser();
            var codegen = new Puma.Codegen();

            var tokens = lexer.Tokenize(pumaSource);
            var ast = parser.Parse(tokens);
            var generatedOutput = codegen.Generate(ast);

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(generatedOutput).Trim());
        }

        [TestMethod]
        public void Convertion_InvalidImplicitConversion_ReturnsErrorMessage()
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                EnsureImplicit(Convertion.Type.FLT64, Convertion.Type.INT32));

            StringAssert.Contains(ex.Message, "Implicit conversion is not valid");
            StringAssert.Contains(ex.Message, "FLT64 -> INT32");
        }

        [TestMethod]
        public void ConvertionTable_AllExplicitEntries_AreExplicit()
        {
            var max = Enum.GetValues<Convertion.Type>().Length;
            for (var from = 0; from < max; from++)
            {
                for (var to = 0; to < max; to++)
                {
                    var fromType = (Convertion.Type)from;
                    var toType = (Convertion.Type)to;
                    if (Convertion.GetConversionType(fromType, toType) == 'E')
                    {
                        Assert.IsFalse(
                            Convertion.IsImplicit(fromType, toType),
                            $"Expected explicit conversion for {fromType} -> {toType}.");
                    }
                }
            }
        }
    }
}

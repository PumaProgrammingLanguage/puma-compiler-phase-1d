using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class ImplicitConvertionTest
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

            EnsureImplicit(Convertion.Type.UINT8, Convertion.Type.UINT8);
            EnsureImplicit(Convertion.Type.UINT8, Convertion.Type.UINT16);
            EnsureImplicit(Convertion.Type.UINT8, Convertion.Type.UINT32);
            EnsureImplicit(Convertion.Type.UINT8, Convertion.Type.UINT64);
            EnsureImplicit(Convertion.Type.UINT8, Convertion.Type.INT16);
            EnsureImplicit(Convertion.Type.UINT8, Convertion.Type.INT32);
            EnsureImplicit(Convertion.Type.UINT8, Convertion.Type.INT64);
            EnsureImplicit(Convertion.Type.UINT8, Convertion.Type.FLT32);
            EnsureImplicit(Convertion.Type.UINT8, Convertion.Type.FLT64);

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

            EnsureImplicit(Convertion.Type.INT8, Convertion.Type.INT8);
            EnsureImplicit(Convertion.Type.INT8, Convertion.Type.INT16);
            EnsureImplicit(Convertion.Type.INT8, Convertion.Type.INT32);
            EnsureImplicit(Convertion.Type.INT8, Convertion.Type.INT64);
            EnsureImplicit(Convertion.Type.INT8, Convertion.Type.FLT32);
            EnsureImplicit(Convertion.Type.INT8, Convertion.Type.FLT64);

            var expected =
@"// properties
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
    }
}
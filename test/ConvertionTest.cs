using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;
using System.Text;

namespace test
{
    [TestClass]
    public class ConvertionTest
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

        private static string ToPumaTypeKeyword(Convertion.Type type) => type switch
        {
            Convertion.Type.UINT8 => "uint8_t",
            Convertion.Type.UINT16 => "uint16_t",
            Convertion.Type.UINT32 => "uint32_t",
            Convertion.Type.UINT64 => "uint64_t",
            Convertion.Type.INT8 => "int8_t",
            Convertion.Type.INT16 => "int16_t",
            Convertion.Type.INT32 => "int32_t",
            Convertion.Type.INT64 => "int64_t",
            Convertion.Type.FLT32 => "float",
            Convertion.Type.FLT64 => "double",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported conversion type")
        };

        private static string BuildConvertedAssignment(Convertion.Type fromType, Convertion.Type toType, string sourceName)
        {
            _ = fromType;
            return $"({ToPumaTypeKeyword(toType)}) {sourceName}";
        }

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

            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(pumaSource);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "0", "uint8",
                "b", "=", "1", "uint8",
                "c", "=", "2", "uint8",
                "d", "=", "3", "uint8",
                "e", "=", "4", "uint8",
                "f", "=", "5", "uint8",
                "g", "=", "6", "uint8",
                "h", "=", "7", "uint8",
                "i", "=", "8", "uint8",
                "j", "=", "9", "uint8",
                "start",
                "m", "=", "a", "uint8",
                "n", "=", "b", "uint16",
                "o", "=", "c", "uint32",
                "p", "=", "d", "uint64",
                "q", "=", "e", "int8",
                "r", "=", "f", "int16",
                "s", "=", "g", "int32",
                "t", "=", "h", "int64",
                "u", "=", "i", "flt32",
                "v", "=", "j", "flt64",
            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4), ("f", 5),
                ("g", 6), ("h", 7), ("i", 8), ("j", 9)
            };

            var startAssignments = new (string Target, string Source, Convertion.Type ToType)[]
            {
                ("m", "a", Convertion.Type.UINT8),
                ("n", "b", Convertion.Type.UINT16),
                ("o", "c", Convertion.Type.UINT32),
                ("p", "d", Convertion.Type.UINT64),
                ("q", "e", Convertion.Type.INT8),
                ("r", "f", Convertion.Type.INT16),
                ("s", "g", Convertion.Type.INT32),
                ("t", "h", Convertion.Type.INT64),
                ("u", "i", Convertion.Type.FLT32),
                ("v", "j", Convertion.Type.FLT64)
            };

            var sb = new StringBuilder();
            sb.AppendLine("// properties");
            foreach (var (name, value) in fromProperties)
            {
                sb.AppendLine($"    auto {name} = {BuildConvertedAssignment(Convertion.Type.UINT8, Convertion.Type.UINT8, value.ToString())};");
            }

            sb.AppendLine();
            sb.AppendLine("// start");
            sb.AppendLine("int main(void)");
            sb.AppendLine("{");
            foreach (var (target, source, toType) in startAssignments)
            {
                var expr = BuildConvertedAssignment(Convertion.Type.UINT8, toType, source);
                sb.AppendLine($"    auto {target} = {expr};");
            }
            sb.AppendLine();
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");

            var expected =
@"// properties
    auto a = (uint8_t) 0;
    auto b = (uint8_t) 1;
    auto c = (uint8_t) 2;
    auto d = (uint8_t) 3;
    auto e = (uint8_t) 4;
    auto f = (uint8_t) 5;
    auto g = (uint8_t) 6;
    auto h = (uint8_t) 7;
    auto i = (uint8_t) 8;
    auto j = (uint8_t) 9;

// start
int main(void)
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

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(sb.ToString()).Trim());
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

            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(pumaSource);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "0", "int32",
                "b", "=", "1", "int32",
                "c", "=", "2", "int32",
                "d", "=", "3", "int32",
                "e", "=", "4", "int32",
                "f", "=", "5", "int32",
                "g", "=", "6", "int32",
                "h", "=", "7", "int32",
                "i", "=", "8", "int32",
                "j", "=", "9", "int32",
                "start",
                "m", "=", "a", "uint8",
                "n", "=", "b", "uint16",
                "o", "=", "c", "uint32",
                "p", "=", "d", "uint64",
                "q", "=", "e", "int8",
                "r", "=", "f", "int16",
                "s", "=", "g", "int32",
                "t", "=", "h", "int64",
                "u", "=", "i", "flt32",
                "v", "=", "j", "flt64"
            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4),
                ("f", 5), ("g", 6), ("h", 7), ("i", 8), ("j", 9)
            };

            var startAssignments = new (string Target, string Source, Convertion.Type ToType)[]
            {
                ("m", "a", Convertion.Type.UINT8),
                ("n", "b", Convertion.Type.UINT16),
                ("o", "c", Convertion.Type.UINT32),
                ("p", "d", Convertion.Type.UINT64),
                ("q", "e", Convertion.Type.INT8),
                ("r", "f", Convertion.Type.INT16),
                ("s", "g", Convertion.Type.INT32),
                ("t", "h", Convertion.Type.INT64),
                ("u", "i", Convertion.Type.FLT32),
                ("v", "j", Convertion.Type.FLT64)
            };

            var sb = new StringBuilder();
            sb.AppendLine("// properties");
            foreach (var (name, value) in fromProperties)
            {
                sb.AppendLine($"    auto {name} = {BuildConvertedAssignment(Convertion.Type.INT32, Convertion.Type.INT32, value.ToString())};");
            }

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("// start");
            sb.AppendLine("int main(void)");
            sb.AppendLine("{");
            foreach (var (target, source, toType) in startAssignments)
            {
                var expr = BuildConvertedAssignment(Convertion.Type.INT32, toType, source);
                sb.AppendLine($"    auto {target} = {expr};");
            }
            sb.AppendLine();
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");

            var expected =
@"// properties
    auto a = (int32_t) 0;
    auto b = (int32_t) 1;
    auto c = (int32_t) 2;
    auto d = (int32_t) 3;
    auto e = (int32_t) 4;
    auto f = (int32_t) 5;
    auto g = (int32_t) 6;
    auto h = (int32_t) 7;
    auto i = (int32_t) 8;
    auto j = (int32_t) 9;


// start
int main(void)
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

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(sb.ToString()).Trim());
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

            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(pumaSource);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "0", "int8",
                "b", "=", "1", "int8",
                "c", "=", "2", "int8",
                "d", "=", "3", "int8",
                "e", "=", "4", "int8",
                "f", "=", "5", "int8",
                "g", "=", "6", "int8",
                "h", "=", "7", "int8",
                "i", "=", "8", "int8",
                "j", "=", "9", "int8",
                "start",
                "m", "=", "a", "uint8",
                "n", "=", "b", "uint16",
                "o", "=", "c", "uint32",
                "p", "=", "d", "uint64",
                "q", "=", "e", "int8",
                "r", "=", "f", "int16",
                "s", "=", "g", "int32",
                "t", "=", "h", "int64",
                "u", "=", "i", "flt32",
                "v", "=", "j", "flt64"
            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4),
                ("f", 5), ("g", 6), ("h", 7), ("i", 8), ("j", 9)
            };

            var startAssignments = new (string Target, string Source, Convertion.Type ToType)[]
            {
                ("m", "a", Convertion.Type.UINT8),
                ("n", "b", Convertion.Type.UINT16),
                ("o", "c", Convertion.Type.UINT32),
                ("p", "d", Convertion.Type.UINT64),
                ("q", "e", Convertion.Type.INT8),
                ("r", "f", Convertion.Type.INT16),
                ("s", "g", Convertion.Type.INT32),
                ("t", "h", Convertion.Type.INT64),
                ("u", "i", Convertion.Type.FLT32),
                ("v", "j", Convertion.Type.FLT64)
            };

            var sb = new StringBuilder();
            sb.AppendLine("// properties");
            foreach (var (name, value) in fromProperties)
            {
                sb.AppendLine($"    auto {name} = {BuildConvertedAssignment(Convertion.Type.INT8, Convertion.Type.INT8, value.ToString())};");
            }

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("// start");
            sb.AppendLine("int main(void)");
            sb.AppendLine("{");
            foreach (var (target, source, toType) in startAssignments)
            {
                var expr = BuildConvertedAssignment(Convertion.Type.INT8, toType, source);
                sb.AppendLine($"    auto {target} = {expr};");
            }
            sb.AppendLine();
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");

            var expected =
@"// properties
    auto a = (int8_t) 0;
    auto b = (int8_t) 1;
    auto c = (int8_t) 2;
    auto d = (int8_t) 3;
    auto e = (int8_t) 4;
    auto f = (int8_t) 5;
    auto g = (int8_t) 6;
    auto h = (int8_t) 7;
    auto i = (int8_t) 8;
    auto j = (int8_t) 9;


// start
int main(void)
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

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(sb.ToString()).Trim());
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

            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(pumaSource);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "0", "uint64",
                "b", "=", "1", "uint64",
                "c", "=", "2", "uint64",
                "d", "=", "3", "uint64",
                "e", "=", "4", "uint64",
                "f", "=", "5", "uint64",
                "g", "=", "6", "uint64",
                "h", "=", "7", "uint64",
                "i", "=", "8", "uint64",
                "j", "=", "9", "uint64",
                "start",
                "m", "=", "a", "uint8",
                "n", "=", "b", "uint16",
                "o", "=", "c", "uint32",
                "p", "=", "d", "uint64",
                "q", "=", "e", "int8",
                "r", "=", "f", "int16",
                "s", "=", "g", "int32",
                "t", "=", "h", "int64",
                "u", "=", "i", "flt32",
                "v", "=", "j", "flt64"
            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4),
                ("f", 5), ("g", 6), ("h", 7), ("i", 8), ("j", 9)
            };

            var startAssignments = new (string Target, string Source, Convertion.Type ToType)[]
            {
                ("m", "a", Convertion.Type.UINT8),
                ("n", "b", Convertion.Type.UINT16),
                ("o", "c", Convertion.Type.UINT32),
                ("p", "d", Convertion.Type.UINT64),
                ("q", "e", Convertion.Type.INT8),
                ("r", "f", Convertion.Type.INT16),
                ("s", "g", Convertion.Type.INT32),
                ("t", "h", Convertion.Type.INT64),
                ("u", "i", Convertion.Type.FLT32),
                ("v", "j", Convertion.Type.FLT64)
            };

            var sb = new StringBuilder();
            sb.AppendLine("// properties");
            foreach (var (name, value) in fromProperties)
            {
                sb.AppendLine($"    auto {name} = {BuildConvertedAssignment(Convertion.Type.UINT64, Convertion.Type.UINT64, value.ToString())};");
            }

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("// start");
            sb.AppendLine("int main(void)");
            sb.AppendLine("{");
            foreach (var (target, source, toType) in startAssignments)
            {
                var expr = BuildConvertedAssignment(Convertion.Type.UINT64, toType, source);
                sb.AppendLine($"    auto {target} = {expr};");
            }
            sb.AppendLine();
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");

            var expected =
@"// properties
    auto a = (uint64_t) 0;
    auto b = (uint64_t) 1;
    auto c = (uint64_t) 2;
    auto d = (uint64_t) 3;
    auto e = (uint64_t) 4;
    auto f = (uint64_t) 5;
    auto g = (uint64_t) 6;
    auto h = (uint64_t) 7;
    auto i = (uint64_t) 8;
    auto j = (uint64_t) 9;


// start
int main(void)
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

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(sb.ToString()).Trim());
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

            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(pumaSource);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "0", "uint32",
                "b", "=", "1", "uint32",
                "c", "=", "2", "uint32",
                "d", "=", "3", "uint32",
                "e", "=", "4", "uint32",
                "f", "=", "5", "uint32",
                "g", "=", "6", "uint32",
                "h", "=", "7", "uint32",
                "i", "=", "8", "uint32",
                "j", "=", "9", "uint32",
                "start",
                "m", "=", "a", "uint8",
                "n", "=", "b", "uint16",
                "o", "=", "c", "uint32",
                "p", "=", "d", "uint64",
                "q", "=", "e", "int8",
                "r", "=", "f", "int16",
                "s", "=", "g", "int32",
                "t", "=", "h", "int64",
                "u", "=", "i", "flt32",
                "v", "=", "j", "flt64"
            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4),
                ("f", 5), ("g", 6), ("h", 7), ("i", 8), ("j", 9)
            };

            var startAssignments = new (string Target, string Source, Convertion.Type ToType)[]
            {
                ("m", "a", Convertion.Type.UINT8),
                ("n", "b", Convertion.Type.UINT16),
                ("o", "c", Convertion.Type.UINT32),
                ("p", "d", Convertion.Type.UINT64),
                ("q", "e", Convertion.Type.INT8),
                ("r", "f", Convertion.Type.INT16),
                ("s", "g", Convertion.Type.INT32),
                ("t", "h", Convertion.Type.INT64),
                ("u", "i", Convertion.Type.FLT32),
                ("v", "j", Convertion.Type.FLT64)
            };

            var sb = new StringBuilder();
            sb.AppendLine("// properties");
            foreach (var (name, value) in fromProperties)
            {
                sb.AppendLine($"    auto {name} = {BuildConvertedAssignment(Convertion.Type.UINT32, Convertion.Type.UINT32, value.ToString())};");
            }

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("// start");
            sb.AppendLine("int main(void)");
            sb.AppendLine("{");
            foreach (var (target, source, toType) in startAssignments)
            {
                var expr = BuildConvertedAssignment(Convertion.Type.UINT32, toType, source);
                sb.AppendLine($"    auto {target} = {expr};");
            }
            sb.AppendLine();
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");

            var expected =
@"// properties
    auto a = (uint32_t) 0;
    auto b = (uint32_t) 1;
    auto c = (uint32_t) 2;
    auto d = (uint32_t) 3;
    auto e = (uint32_t) 4;
    auto f = (uint32_t) 5;
    auto g = (uint32_t) 6;
    auto h = (uint32_t) 7;
    auto i = (uint32_t) 8;
    auto j = (uint32_t) 9;


// start
int main(void)
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

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(sb.ToString()).Trim());
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

            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(pumaSource);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "0", "flt64",
                "b", "=", "1", "flt64",
                "c", "=", "2", "flt64",
                "d", "=", "3", "flt64",
                "e", "=", "4", "flt64",
                "f", "=", "5", "flt64",
                "g", "=", "6", "flt64",
                "h", "=", "7", "flt64",
                "i", "=", "8", "flt64",
                "j", "=", "9", "flt64",
                "start",
                "m", "=", "a", "uint8",
                "n", "=", "b", "uint16",
                "o", "=", "c", "uint32",
                "p", "=", "d", "uint64",
                "q", "=", "e", "int8",
                "r", "=", "f", "int16",
                "s", "=", "g", "int32",
                "t", "=", "h", "int64",
                "u", "=", "i", "flt32",
                "v", "=", "j", "flt64"
            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4),
                ("f", 5), ("g", 6), ("h", 7), ("i", 8), ("j", 9)
            };

            var startAssignments = new (string Target, string Source, Convertion.Type ToType)[]
            {
                ("m", "a", Convertion.Type.UINT8),
                ("n", "b", Convertion.Type.UINT16),
                ("o", "c", Convertion.Type.UINT32),
                ("p", "d", Convertion.Type.UINT64),
                ("q", "e", Convertion.Type.INT8),
                ("r", "f", Convertion.Type.INT16),
                ("s", "g", Convertion.Type.INT32),
                ("t", "h", Convertion.Type.INT64),
                ("u", "i", Convertion.Type.FLT32),
                ("v", "j", Convertion.Type.FLT64)
            };

            var sb = new StringBuilder();
            sb.AppendLine("// properties");
            foreach (var (name, value) in fromProperties)
            {
                sb.AppendLine($"    auto {name} = {BuildConvertedAssignment(Convertion.Type.FLT64, Convertion.Type.FLT64, value.ToString())};");
            }

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("// start");
            sb.AppendLine("int main(void)");
            sb.AppendLine("{");
            foreach (var (target, source, toType) in startAssignments)
            {
                var expr = BuildConvertedAssignment(Convertion.Type.FLT64, toType, source);
                sb.AppendLine($"    auto {target} = {expr};");
            }
            sb.AppendLine();
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");

            var expected =
@"// properties
    auto a = (double) 0;
    auto b = (double) 1;
    auto c = (double) 2;
    auto d = (double) 3;
    auto e = (double) 4;
    auto f = (double) 5;
    auto g = (double) 6;
    auto h = (double) 7;
    auto i = (double) 8;
    auto j = (double) 9;


// start
int main(void)
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

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(sb.ToString()).Trim());
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

            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(pumaSource);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "0", "flt32",
                "b", "=", "1", "flt32",
                "c", "=", "2", "flt32",
                "d", "=", "3", "flt32",
                "e", "=", "4", "flt32",
                "f", "=", "5", "flt32",
                "g", "=", "6", "flt32",
                "h", "=", "7", "flt32",
                "i", "=", "8", "flt32",
                "j", "=", "9", "flt32",
                "start",
                "m", "=", "a", "uint8",
                "n", "=", "b", "uint16",
                "o", "=", "c", "uint32",
                "p", "=", "d", "uint64",
                "q", "=", "e", "int8",
                "r", "=", "f", "int16",
                "s", "=", "g", "int32",
                "t", "=", "h", "int64",
                "u", "=", "i", "flt32",
                "v", "=", "j", "flt64"
            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4),
                ("f", 5), ("g", 6), ("h", 7), ("i", 8), ("j", 9)
            };

            var startAssignments = new (string Target, string Source, Convertion.Type ToType)[]
            {
                ("m", "a", Convertion.Type.UINT8),
                ("n", "b", Convertion.Type.UINT16),
                ("o", "c", Convertion.Type.UINT32),
                ("p", "d", Convertion.Type.UINT64),
                ("q", "e", Convertion.Type.INT8),
                ("r", "f", Convertion.Type.INT16),
                ("s", "g", Convertion.Type.INT32),
                ("t", "h", Convertion.Type.INT64),
                ("u", "i", Convertion.Type.FLT32),
                ("v", "j", Convertion.Type.FLT64)
            };

            var sb = new StringBuilder();
            sb.AppendLine("// properties");
            foreach (var (name, value) in fromProperties)
            {
                sb.AppendLine($"    auto {name} = {BuildConvertedAssignment(Convertion.Type.FLT32, Convertion.Type.FLT32, value.ToString())};");
            }

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("// start");
            sb.AppendLine("int main(void)");
            sb.AppendLine("{");
            foreach (var (target, source, toType) in startAssignments)
            {
                var expr = BuildConvertedAssignment(Convertion.Type.FLT32, toType, source);
                sb.AppendLine($"    auto {target} = {expr};");
            }
            sb.AppendLine();
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");

            var expected =
@"// properties
    auto a = (float) 0;
    auto b = (float) 1;
    auto c = (float) 2;
    auto d = (float) 3;
    auto e = (float) 4;
    auto f = (float) 5;
    auto g = (float) 6;
    auto h = (float) 7;
    auto i = (float) 8;
    auto j = (float) 9;


// start
int main(void)
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

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(sb.ToString()).Trim());
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

            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(pumaSource);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "0", "int64",
                "b", "=", "1", "int64",
                "c", "=", "2", "int64",
                "d", "=", "3", "int64",
                "e", "=", "4", "int64",
                "f", "=", "5", "int64",
                "g", "=", "6", "int64",
                "h", "=", "7", "int64",
                "i", "=", "8", "int64",
                "j", "=", "9", "int64",
                "start",
                "m", "=", "a", "uint8",
                "n", "=", "b", "uint16",
                "o", "=", "c", "uint32",
                "p", "=", "d", "uint64",
                "q", "=", "e", "int8",
                "r", "=", "f", "int16",
                "s", "=", "g", "int32",
                "t", "=", "h", "int64",
                "u", "=", "i", "flt32",
                "v", "=", "j", "flt64"
            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4), ("f", 5),
                ("g", 6), ("h", 7), ("i", 8), ("j", 9)
            };

            var startAssignments = new (string Target, string Source, Convertion.Type ToType)[]
            {
                ("m", "a", Convertion.Type.UINT8),
                ("n", "b", Convertion.Type.UINT16),
                ("o", "c", Convertion.Type.UINT32),
                ("p", "d", Convertion.Type.UINT64),
                ("q", "e", Convertion.Type.INT8),
                ("r", "f", Convertion.Type.INT16),
                ("s", "g", Convertion.Type.INT32),
                ("t", "h", Convertion.Type.INT64),
                ("u", "i", Convertion.Type.FLT32),
                ("v", "j", Convertion.Type.FLT64)
            };

            var sb = new StringBuilder();
            sb.AppendLine("// properties");
            foreach (var (name, value) in fromProperties)
            {
                sb.AppendLine($"    auto {name} = {BuildConvertedAssignment(Convertion.Type.INT64, Convertion.Type.INT64, value.ToString())};");
            }

            sb.AppendLine();
            sb.AppendLine("// start");
            sb.AppendLine("int main(void)");
            sb.AppendLine("{");
            foreach (var (target, source, toType) in startAssignments)
            {
                var expr = BuildConvertedAssignment(Convertion.Type.INT64, toType, source);
                sb.AppendLine($"    auto {target} = {expr};");
            }
            sb.AppendLine();
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");

            var expected =
@"// properties
    auto a = (int64_t) 0;
    auto b = (int64_t) 1;
    auto c = (int64_t) 2;
    auto d = (int64_t) 3;
    auto e = (int64_t) 4;
    auto f = (int64_t) 5;
    auto g = (int64_t) 6;
    auto h = (int64_t) 7;
    auto i = (int64_t) 8;
    auto j = (int64_t) 9;

// start
int main(void)
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

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(sb.ToString()).Trim());
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

            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(pumaSource);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "0", "int16",
                "b", "=", "1", "int16",
                "c", "=", "2", "int16",
                "d", "=", "3", "int16",
                "e", "=", "4", "int16",
                "f", "=", "5", "int16",
                "g", "=", "6", "int16",
                "h", "=", "7", "int16",
                "i", "=", "8", "int16",
                "j", "=", "9", "int16",
                "start",
                "m", "=", "a", "uint8",
                "n", "=", "b", "uint16",
                "o", "=", "c", "uint32",
                "p", "=", "d", "uint64",
                "q", "=", "e", "int8",
                "r", "=", "f", "int16",
                "s", "=", "g", "int32",
                "t", "=", "h", "int64",
                "u", "=", "i", "flt32",
                "v", "=", "j", "flt64",
            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4), ("f", 5),
                ("g", 6), ("h", 7), ("i", 8), ("j", 9)
            };

            var startAssignments = new (string Target, string Source, Convertion.Type ToType)[]
            {
                ("m", "a", Convertion.Type.UINT8),
                ("n", "b", Convertion.Type.UINT16),
                ("o", "c", Convertion.Type.UINT32),
                ("p", "d", Convertion.Type.UINT64),
                ("q", "e", Convertion.Type.INT8),
                ("r", "f", Convertion.Type.INT16),
                ("s", "g", Convertion.Type.INT32),
                ("t", "h", Convertion.Type.INT64),
                ("u", "i", Convertion.Type.FLT32),
                ("v", "j", Convertion.Type.FLT64)
            };

            var sb = new StringBuilder();
            sb.AppendLine("// properties");
            foreach (var (name, value) in fromProperties)
            {
                sb.AppendLine($"    auto {name} = {BuildConvertedAssignment(Convertion.Type.INT16, Convertion.Type.INT16, value.ToString())};");
            }

            sb.AppendLine();
            sb.AppendLine("// start");
            sb.AppendLine("int main(void)");
            sb.AppendLine("{");
            foreach (var (target, source, toType) in startAssignments)
            {
                var expr = BuildConvertedAssignment(Convertion.Type.INT16, toType, source);
                sb.AppendLine($"    auto {target} = {expr};");
            }
            sb.AppendLine();
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");

            var expected =
@"// properties
    auto a = (int16_t) 0;
    auto b = (int16_t) 1;
    auto c = (int16_t) 2;
    auto d = (int16_t) 3;
    auto e = (int16_t) 4;
    auto f = (int16_t) 5;
    auto g = (int16_t) 6;
    auto h = (int16_t) 7;
    auto i = (int16_t) 8;
    auto j = (int16_t) 9;

// start
int main(void)
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

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(sb.ToString()).Trim());
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

            var lexer = new Puma.Lexer();
            var tokens = lexer.Tokenize(pumaSource);
            var significant = GetSignificantTokens(tokens).Select(t => t.TokenText).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "properties",
                "a", "=", "0", "uint16",
                "b", "=", "1", "uint16",
                "c", "=", "2", "uint16",
                "d", "=", "3", "uint16",
                "e", "=", "4", "uint16",
                "f", "=", "5", "uint16",
                "g", "=", "6", "uint16",
                "h", "=", "7", "uint16",
                "i", "=", "8", "uint16",
                "j", "=", "9", "uint16",
                "start",
                "m", "=", "a", "uint8",
                "n", "=", "b", "uint16",
                "o", "=", "c", "uint32",
                "p", "=", "d", "uint64",
                "q", "=", "e", "int8",
                "r", "=", "f", "int16",
                "s", "=", "g", "int32",
                "t", "=", "h", "int64",
                "u", "=", "i", "flt32",
                "v", "=", "j", "flt64",

            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4), ("f", 5),
                ("g", 6), ("h", 7), ("i", 8), ("j", 9)
            };

            var startAssignments = new (string Target, string Source, Convertion.Type ToType)[]
            {
                ("m", "a", Convertion.Type.UINT8),
                ("n", "b", Convertion.Type.UINT16),
                ("o", "c", Convertion.Type.UINT32),
                ("p", "d", Convertion.Type.UINT64),
                ("q", "e", Convertion.Type.INT8),
                ("r", "f", Convertion.Type.INT16),
                ("s", "g", Convertion.Type.INT32),
                ("t", "h", Convertion.Type.INT64),
                ("u", "i", Convertion.Type.FLT32),
                ("v", "j", Convertion.Type.FLT64),
            };

            var sb = new StringBuilder();
            sb.AppendLine("// properties");
            foreach (var (name, value) in fromProperties)
            {
                sb.AppendLine($"    auto {name} = {BuildConvertedAssignment(Convertion.Type.UINT16, Convertion.Type.UINT16, value.ToString())};");
            }

            sb.AppendLine();
            sb.AppendLine("// start");
            sb.AppendLine("int main(void)");
            sb.AppendLine("{");
            foreach (var (target, source, toType) in startAssignments)
            {
                var expr = BuildConvertedAssignment(Convertion.Type.UINT16, toType, source);
                sb.AppendLine($"    auto {target} = {expr};");
            }
            sb.AppendLine();
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");

            var expected =
@"// properties
    auto a = (uint16_t) 0;
    auto b = (uint16_t) 1;
    auto c = (uint16_t) 2;
    auto d = (uint16_t) 3;
    auto e = (uint16_t) 4;
    auto f = (uint16_t) 5;
    auto g = (uint16_t) 6;
    auto h = (uint16_t) 7;
    auto i = (uint16_t) 8;
    auto j = (uint16_t) 9;

// start
int main(void)
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

            Assert.AreEqual(Normalize(expected).Trim(), Normalize(sb.ToString()).Trim());
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

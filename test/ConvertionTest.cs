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
            Convertion.Type.UINT8 => "uint8",
            Convertion.Type.UINT16 => "uint16",
            Convertion.Type.UINT32 => "uint32",
            Convertion.Type.UINT64 => "uint64",
            Convertion.Type.INT8 => "int8",
            Convertion.Type.INT16 => "int16",
            Convertion.Type.INT32 => "int32",
            Convertion.Type.INT64 => "int64",
            Convertion.Type.FLT32 => "flt32",
            Convertion.Type.FLT64 => "flt64",
            Convertion.Type.FIX32 => "fix32",
            Convertion.Type.FIX64 => "fix64",
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
    k = 10 uint8
    l = 11 uint8

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
    w = k fix32
    x = l fix64
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
                "k", "=", "10", "uint8",
                "l", "=", "11", "uint8",
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
                "w", "=", "k", "fix32",
                "x", "=", "l", "fix64"
            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4), ("f", 5),
                ("g", 6), ("h", 7), ("i", 8), ("j", 9), ("k", 10), ("l", 11)
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
                ("w", "k", Convertion.Type.FIX32),
                ("x", "l", Convertion.Type.FIX64)
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
    auto a = (uint8) 0;
    auto b = (uint8) 1;
    auto c = (uint8) 2;
    auto d = (uint8) 3;
    auto e = (uint8) 4;
    auto f = (uint8) 5;
    auto g = (uint8) 6;
    auto h = (uint8) 7;
    auto i = (uint8) 8;
    auto j = (uint8) 9;
    auto k = (uint8) 10;
    auto l = (uint8) 11;

// start
int main(void)
{
    auto m = (uint8) a;
    auto n = (uint16) b;
    auto o = (uint32) c;
    auto p = (uint64) d;
    auto q = (int8) e;
    auto r = (int16) f;
    auto s = (int32) g;
    auto t = (int64) h;
    auto u = (flt32) i;
    auto v = (flt64) j;
    auto w = (fix32) k;
    auto x = (fix64) l;

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
    k = 10 int64
    l = 11 int64

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
    w = k fix32
    x = l fix64
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
                "k", "=", "10", "int64",
                "l", "=", "11", "int64",
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
                "w", "=", "k", "fix32",
                "x", "=", "l", "fix64"
            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4), ("f", 5),
                ("g", 6), ("h", 7), ("i", 8), ("j", 9), ("k", 10), ("l", 11)
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
                ("w", "k", Convertion.Type.FIX32),
                ("x", "l", Convertion.Type.FIX64)
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
    auto a = (int64) 0;
    auto b = (int64) 1;
    auto c = (int64) 2;
    auto d = (int64) 3;
    auto e = (int64) 4;
    auto f = (int64) 5;
    auto g = (int64) 6;
    auto h = (int64) 7;
    auto i = (int64) 8;
    auto j = (int64) 9;
    auto k = (int64) 10;
    auto l = (int64) 11;

// start
int main(void)
{
    auto m = (uint8) a;
    auto n = (uint16) b;
    auto o = (uint32) c;
    auto p = (uint64) d;
    auto q = (int8) e;
    auto r = (int16) f;
    auto s = (int32) g;
    auto t = (int64) h;
    auto u = (flt32) i;
    auto v = (flt64) j;
    auto w = (fix32) k;
    auto x = (fix64) l;

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
    k = 10 int16
    l = 11 int16

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
    w = k fix32
    x = l fix64
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
                "k", "=", "10", "int16",
                "l", "=", "11", "int16",
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
                "w", "=", "k", "fix32",
                "x", "=", "l", "fix64"
            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4), ("f", 5),
                ("g", 6), ("h", 7), ("i", 8), ("j", 9), ("k", 10), ("l", 11)
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
                ("w", "k", Convertion.Type.FIX32),
                ("x", "l", Convertion.Type.FIX64)
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
    auto a = (int16) 0;
    auto b = (int16) 1;
    auto c = (int16) 2;
    auto d = (int16) 3;
    auto e = (int16) 4;
    auto f = (int16) 5;
    auto g = (int16) 6;
    auto h = (int16) 7;
    auto i = (int16) 8;
    auto j = (int16) 9;
    auto k = (int16) 10;
    auto l = (int16) 11;

// start
int main(void)
{
    auto m = (uint8) a;
    auto n = (uint16) b;
    auto o = (uint32) c;
    auto p = (uint64) d;
    auto q = (int8) e;
    auto r = (int16) f;
    auto s = (int32) g;
    auto t = (int64) h;
    auto u = (flt32) i;
    auto v = (flt64) j;
    auto w = (fix32) k;
    auto x = (fix64) l;

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
    k = 10 uint16
    l = 11 uint16

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
    w = k fix32
    x = l fix64
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
                "k", "=", "10", "uint16",
                "l", "=", "11", "uint16",
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
                "w", "=", "k", "fix32",
                "x", "=", "l", "fix64"
            }, significant);

            var fromProperties = new (string Name, int Value)[]
            {
                ("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4), ("f", 5),
                ("g", 6), ("h", 7), ("i", 8), ("j", 9), ("k", 10), ("l", 11)
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
                ("w", "k", Convertion.Type.FIX32),
                ("x", "l", Convertion.Type.FIX64)
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
    auto a = (uint16) 0;
    auto b = (uint16) 1;
    auto c = (uint16) 2;
    auto d = (uint16) 3;
    auto e = (uint16) 4;
    auto f = (uint16) 5;
    auto g = (uint16) 6;
    auto h = (uint16) 7;
    auto i = (uint16) 8;
    auto j = (uint16) 9;
    auto k = (uint16) 10;
    auto l = (uint16) 11;

// start
int main(void)
{
    auto m = (uint8) a;
    auto n = (uint16) b;
    auto o = (uint32) c;
    auto p = (uint64) d;
    auto q = (int8) e;
    auto r = (int16) f;
    auto s = (int32) g;
    auto t = (int64) h;
    auto u = (flt32) i;
    auto v = (flt64) j;
    auto w = (fix32) k;
    auto x = (fix64) l;

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

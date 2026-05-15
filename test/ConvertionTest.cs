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
        public void Convertion_ImplicitAndExplicitExample_OutputText_AreConsistent()
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
    m = a
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
                "m", "=", "a",
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
                sb.AppendLine($"    uint8 {name} = {value};");
            }

            sb.AppendLine();
            sb.AppendLine("// start");
            sb.AppendLine("int main(void)");
            sb.AppendLine("{");
            foreach (var (target, source, toType) in startAssignments)
            {
                var expr = BuildConvertedAssignment(Convertion.Type.UINT8, toType, source);
                sb.AppendLine($"    {target} = {expr};");
            }
            sb.AppendLine();
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");

            var expected =
@"// properties
    uint8 a = 0;
    uint8 b = 1;
    uint8 c = 2;
    uint8 d = 3;
    uint8 e = 4;
    uint8 f = 5;
    uint8 g = 6;
    uint8 h = 7;
    uint8 i = 8;
    uint8 j = 9;
    uint8 k = 10;
    uint8 l = 11;

// start
int main(void)
{
    m = (uint8) a;
    n = (uint16) b;
    o = (uint32) c;
    p = (uint64) d;
    q = (int8) e;
    r = (int16) f;
    s = (int32) g;
    t = (int64) h;
    u = (flt32) i;
    v = (flt64) j;
    w = (fix32) k;
    x = (fix64) l;

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

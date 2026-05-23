using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class InvalidImplicitConvertionTest
    {
        private static string ToPumaType(Convertion.Type type) => type switch
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
            _ => throw new InvalidOperationException($"Unsupported conversion type '{type}'.")
        };

        private static string BuildInvalidImplicitAssignmentSource(Convertion.Type sourceType, Convertion.Type targetType)
        {
            var sourceTypeName = ToPumaType(sourceType);
            var targetTypeName = ToPumaType(targetType);
            return
$@"properties
    source = 0 {sourceTypeName}
    target = 0 {targetTypeName}

start
    target = source
";
        }

        [TestMethod]
        public void Convertion_InvalidImplicitAssignments_FromSource_AreRejected()
        {
            var cases = new List<(string Source, string SourceType, string TargetType)>();
            var max = Enum.GetValues<Convertion.Type>().Length;
            for (var from = 0; from < max; from++)
            {
                for (var to = 0; to < max; to++)
                {
                    var fromType = (Convertion.Type)from;
                    var toType = (Convertion.Type)to;
                    if (Convertion.GetConversionType(fromType, toType) != 'E')
                    {
                        continue;
                    }

                    cases.Add((
                        BuildInvalidImplicitAssignmentSource(fromType, toType),
                        ToPumaType(fromType),
                        ToPumaType(toType)));
                }
            }

            foreach (var (src, sourceType, targetType) in cases)
            {
                var lexer = new Puma.Lexer();
                var parser = new Puma.Parser();

                var tokens = lexer.Tokenize(src);
                var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
                StringAssert.Contains(ex.Message, "Implicit conversion is not valid");

                if (TryMapConvertionType(sourceType, out var fromType)
                    && TryMapConvertionType(targetType, out var toType))
                {
                    StringAssert.Contains(ex.Message, $"{fromType} -> {toType}");
                }
            }
        }

        private static bool TryMapConvertionType(string? typeText, out Convertion.Type type)
        {
            type = default;
            if (string.IsNullOrWhiteSpace(typeText))
            {
                return false;
            }

            switch (typeText.Trim())
            {
                case "uint8":
                    type = Convertion.Type.UINT8;
                    return true;
                case "uint16":
                    type = Convertion.Type.UINT16;
                    return true;
                case "uint32":
                    type = Convertion.Type.UINT32;
                    return true;
                case "uint":
                case "uint64":
                    type = Convertion.Type.UINT64;
                    return true;
                case "int8":
                    type = Convertion.Type.INT8;
                    return true;
                case "int16":
                    type = Convertion.Type.INT16;
                    return true;
                case "int":
                case "int32":
                    type = Convertion.Type.INT32;
                    return true;
                case "int64":
                    type = Convertion.Type.INT64;
                    return true;
                case "flt32":
                    type = Convertion.Type.FLT32;
                    return true;
                case "flt":
                case "flt64":
                    type = Convertion.Type.FLT64;
                    return true;
                case "fix32":
                    type = Convertion.Type.FIX32;
                    return true;
                case "fix":
                case "fix64":
                    type = Convertion.Type.FIX64;
                    return true;
                default:
                    return false;
            }
        }
    }
}

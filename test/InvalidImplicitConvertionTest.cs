using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class InvalidImplicitConvertionTest
    {
        private static void EnsureImplicit(Convertion.Type fromType, Convertion.Type toType)
        {
            if (!Convertion.IsImplicit(fromType, toType))
            {
                throw new InvalidOperationException($"Implicit conversion is not valid: {fromType} -> {toType}");
            }
        }

        private static Convertion.Type MapPumaType(string pumaType) => pumaType switch
        {
            "uint8" => Convertion.Type.UINT8,
            "uint16" => Convertion.Type.UINT16,
            "uint32" => Convertion.Type.UINT32,
            "uint64" or "uint" => Convertion.Type.UINT64,
            "int8" => Convertion.Type.INT8,
            "int16" => Convertion.Type.INT16,
            "int32" => Convertion.Type.INT32,
            "int64" or "int" => Convertion.Type.INT64,
            "flt32" => Convertion.Type.FLT32,
            "flt64" or "flt" => Convertion.Type.FLT64,
            "fix32" => Convertion.Type.FIX32,
            "fix64" or "fix" => Convertion.Type.FIX64,
            _ => throw new InvalidOperationException($"Unsupported Puma type '{pumaType}'.")
        };

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

        private static string BuildInvalidImplicitAssignmentSource(Convertion.Type fromType, Convertion.Type toType)
        {
            var sourceType = ToPumaType(fromType);
            var targetType = ToPumaType(toType);
            return
$@"properties
    source = 0 {sourceType}
    target = 0 {targetType}

start
    target = source
";
        }

        private static void ValidateImplicitAssignments(List<Node> ast)
        {
            var declaredTypes = ast
                .Where(n => n.Kind == NodeKind.PropertyDeclaration
                    && !string.IsNullOrWhiteSpace(n.PropertyName)
                    && !string.IsNullOrWhiteSpace(n.PropertyType))
                .ToDictionary(n => n.PropertyName!, n => MapPumaType(n.PropertyType!), StringComparer.Ordinal);

            foreach (var assignment in ast.Where(n => n.Kind == NodeKind.AssignmentStatement && n.AssignmentOperator == "="))
            {
                if (string.IsNullOrWhiteSpace(assignment.AssignmentLeft)
                    || string.IsNullOrWhiteSpace(assignment.AssignmentRight))
                {
                    continue;
                }

                if (!declaredTypes.TryGetValue(assignment.AssignmentLeft!, out var toType))
                {
                    continue;
                }

                if (!declaredTypes.TryGetValue(assignment.AssignmentRight!, out var fromType))
                {
                    continue;
                }

                EnsureImplicit(fromType, toType);
            }
        }

        [TestMethod]
        public void Convertion_InvalidImplicitAssignments_FromSource_AreRejected()
        {
            var cases = new List<(string Source, string MessagePart)>();
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
                        $"{fromType} -> {toType}"));
                }
            }

            foreach (var (src, messagePart) in cases)
            {
                var lexer = new Puma.Lexer();
                var parser = new Puma.Parser();
                var codegen = new Puma.Codegen();

                var tokens = lexer.Tokenize(src);
                var ast = parser.Parse(tokens);
                var generated = codegen.Generate(ast);

                Assert.IsFalse(string.IsNullOrWhiteSpace(generated));

                var ex = Assert.ThrowsException<InvalidOperationException>(() => ValidateImplicitAssignments(ast));
                StringAssert.Contains(ex.Message, "Implicit conversion is not valid");
                StringAssert.Contains(ex.Message, messagePart);
            }
        }
    }
}

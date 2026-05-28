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

        private static string BuildInvalidImplicitAssignmentSourcePropertyProperty(Convertion.Type sourceType, Convertion.Type targetType)
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

        private static string BuildInvalidImplicitReturnSource(Convertion.Type sourceType, Convertion.Type targetType)
        {
            var sourceTypeName = ToPumaType(sourceType);
            var targetTypeName = ToPumaType(targetType);
            return
$@"functions
    F() {targetTypeName}
        source = 0 {sourceTypeName}
        return source
";
        }

        private static string BuildInvalidImplicitFunctionCallArgumentSource(Convertion.Type sourceType, Convertion.Type targetType)
        {
            var sourceTypeName = ToPumaType(sourceType);
            var targetTypeName = ToPumaType(targetType);
            return
$@"functions
    Consume(value {targetTypeName})
    Caller()
        source = 0 {sourceTypeName}
        Consume(source)
";
        }

        private static string BuildInvalidImplicitConditionalReturnSource(Convertion.Type sourceType, Convertion.Type targetType)
        {
            var sourceTypeName = ToPumaType(sourceType);
            var targetTypeName = ToPumaType(targetType);
            return
$@"functions
    F() {targetTypeName}
        source = 1 {sourceTypeName}
        target = 0 {targetTypeName}
        return source if source > 0 else target
";
        }

        private static string BuildInvalidImplicitAssignmentSourcePropertyLocal(Convertion.Type sourceType, Convertion.Type targetType)
        {
            var sourceTypeName = ToPumaType(sourceType);
            var targetTypeName = ToPumaType(targetType);
            return
$@"properties
    source = 0 {sourceTypeName}

start
    target = 0 {targetTypeName}
    target = source
";
        }

        private static string BuildInvalidImplicitAssignmentSourceLocalProperty(Convertion.Type sourceType, Convertion.Type targetType)
        {
            var sourceTypeName = ToPumaType(sourceType);
            var targetTypeName = ToPumaType(targetType);
            return
$@"properties
    target = 0 {targetTypeName}

start
    source = 0 {sourceTypeName}
    target = source
";
        }

        private static string BuildInvalidImplicitAssignmentSourceLocalLocal(Convertion.Type sourceType, Convertion.Type targetType)
        {
            var sourceTypeName = ToPumaType(sourceType);
            var targetTypeName = ToPumaType(targetType);
            return
$@"start
    source = 0 {sourceTypeName}
    target = 0 {targetTypeName}
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
                        BuildInvalidImplicitAssignmentSourcePropertyProperty(fromType, toType),
                        ToPumaType(fromType),
                        ToPumaType(toType)));

                    cases.Add((
                        BuildInvalidImplicitAssignmentSourcePropertyLocal(fromType, toType),
                        ToPumaType(fromType),
                        ToPumaType(toType)));

                    cases.Add((
                        BuildInvalidImplicitAssignmentSourceLocalProperty(fromType, toType),
                        ToPumaType(fromType),
                        ToPumaType(toType)));

                    cases.Add((
                        BuildInvalidImplicitAssignmentSourceLocalLocal(fromType, toType),
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

        [TestMethod]
        public void Convertion_InvalidImplicitConversions_InNonAssignmentContexts_AreRejected()
        {
            var sourceType = Convertion.Type.UINT16;
            var targetType = Convertion.Type.INT16;
            Assert.AreEqual('E', Convertion.GetConversionType(sourceType, targetType));

            var cases = new[]
            {
                BuildInvalidImplicitReturnSource(sourceType, targetType),
                BuildInvalidImplicitFunctionCallArgumentSource(sourceType, targetType),
                BuildInvalidImplicitConditionalReturnSource(sourceType, targetType)
            };

            foreach (var src in cases)
            {
                var lexer = new Puma.Lexer();
                var parser = new Puma.Parser();
                var tokens = lexer.Tokenize(src);

                var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
                StringAssert.Contains(ex.Message, "Implicit conversion is not valid");
            }
        }

        [TestMethod]
        public void Convertion_InvalidImplicitAssignments_ErrorMessage_IsConsistent_AcrossSections()
        {
            const string startSource =
@"properties
    source = 0 uint16
    target = 0 int16

start
    target = source
";

            const string initializeSource =
@"properties
    source = 0 uint16
    target = 0 int16

initialize
    target = source
";

            const string functionsSource =
@"properties
    source = 0 uint16
    target = 0 int16

functions
    F()
        target = source
";

            var sectionCases = new[]
            {
                (Section: "start", Source: startSource),
                (Section: "initialize", Source: initializeSource),
                (Section: "functions", Source: functionsSource)
            };

            var expectedMessage = "Implicit conversion is not valid: UINT16 -> INT16";

            foreach (var (section, src) in sectionCases)
            {
                var lexer = new Puma.Lexer();
                var parser = new Puma.Parser();
                var tokens = lexer.Tokenize(src);

                var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
                Assert.AreEqual(expectedMessage, ex.Message, $"Unexpected message in '{section}' section.");
            }
        }

        [TestMethod]
        public void Convertion_InvalidImplicitReturnConversions_AreRejected()
        {
            const string literalSource =
@"functions
    F() int32
        return 1.5 flt64
";

            const string identifierSource =
@"functions
    F() int32
        source = 1.5 flt64
        return source
";

            const string conditionalSource =
@"functions
    F() int32
        source = 1.5 flt64
        target = 1 int32
        return source if source > 0 else target
";

            var cases = new[]
            {
                literalSource,
                identifierSource,
                conditionalSource
            };

            foreach (var src in cases)
            {
                var lexer = new Puma.Lexer();
                var parser = new Puma.Parser();
                var tokens = lexer.Tokenize(src);

                var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
                StringAssert.Contains(ex.Message, "Implicit conversion is not valid");
                StringAssert.Contains(ex.Message, "FLT64 -> INT32");
            }
        }

        [TestMethod]
        public void Convertion_InvalidImplicitFunctionCallArguments_AreRejected()
        {
            const string literalSource =
@"functions
    Consume(value int32)
    Caller()
        Consume(1.5 flt64)
";

            const string identifierSource =
@"functions
    Consume(value int32)
    Caller()
        source = 1.5 flt64
        Consume(source)
";

            const string conditionalSource =
@"functions
    Consume(value int32)
    Caller()
        source = 1.5 flt64
        target = 1 int32
        Consume(source if source > 0 else target)
";

            var cases = new[]
            {
                literalSource,
                identifierSource,
                conditionalSource
            };

            foreach (var src in cases)
            {
                var lexer = new Puma.Lexer();
                var parser = new Puma.Parser();
                var tokens = lexer.Tokenize(src);

                var ex = Assert.ThrowsException<InvalidOperationException>(() => parser.Parse(tokens));
                StringAssert.Contains(ex.Message, "Implicit conversion is not valid");
                StringAssert.Contains(ex.Message, "FLT64 -> INT32");
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

using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class PumaImportExpressionTest
    {
        private const string CountCpp = "#include \"library.h\"\n\nauto value = Count();\n\n// start\nint main()\n{\n    return 0;\n}";

        [DataTestMethod]
        [DataRow("functions\n    Count() double\n        return 1.25\n", "Count", 1, CountCpp)]
        [DataRow("type\n    widget is object\n", "widget", 0, "#include \"library.h\"\n\nauto value = new widget();\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}")]
        [DataRow("type\n    Number is value\n", "Number", 2, "#include \"library.h\"\n\nauto value = Number();\n\n// start\nint main()\n{\n    return 0;\n}")]
        public void ImportedDeclaration_ControlsExactOutput(string librarySource, string name, int kind, string expected)
        {
            InDirectory(directory =>
            {
                File.WriteAllText(Path.Combine(directory, "library.puma"), librarySource);
                var source = $"use\n    library.puma\nproperties\n    value = {name}()\nstart\n";
                var ast = Parse(source);
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual(name, symbols.Single().Name);
                Assert.AreEqual((ExternalSymbolKind)kind, symbols.Single().Kind);
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, symbols)));
            });
        }

        [DataTestMethod]
        [DataRow(" own", true, "#include \"library.h\"\n\nauto value = Fetch();\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}")]
        [DataRow("", false, "#include \"library.h\"\n\nauto value = Fetch();\n\n// start\nint main()\n{\n    return 0;\n}")]
        public void ImportedFunctionOwnership_UsesExplicitModifierNotBodyInference(string modifier, bool owned, string expected)
        {
            InDirectory(directory =>
            {
                File.WriteAllText(Path.Combine(directory, "library.puma"), $"functions\n    Fetch() Shape{modifier}\n        return Shape()\n");
                var ast = Parse("use\n    library.puma\nproperties\n    value = Fetch()\nstart\n");
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual(owned, symbols.Single().ReturnsOwnedObject);
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, symbols)));
            });
        }

        [TestMethod]
        public void FileImports_ResolveRelativeToSourceFileIncludingSubdirectories()
        {
            InDirectory(directory =>
            {
                Directory.CreateDirectory(Path.Combine(directory, "sub"));
                File.WriteAllText(Path.Combine(directory, "sub", "library.puma"), "functions\n    Count() double\n        return 1.25\n");
                var ast = Parse("use\n    sub/library.puma\nproperties\n    value = Count()\nstart\n");
                const string expected = "#include \"sub/library.h\"\n\nauto value = Count();\n\n// start\nint main()\n{\n    return 0;\n}";
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, symbols)));
            });
        }

        [TestMethod]
        public void NormalizedDuplicateImports_DoNotDuplicateSymbols()
        {
            InDirectory(directory =>
            {
                File.WriteAllText(Path.Combine(directory, "library.puma"), "functions\n    Count() double\n        return 1.25\n");
                var ast = Parse("use\n    library.puma\n    ./library.puma\nproperties\n    value = Count()\nstart\n");
                const string expected = "#include \"./library.h\"\n#include \"library.h\"\n\nauto value = Count();\n\n// start\nint main()\n{\n    return 0;\n}";
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual(1, symbols.Count);
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, symbols)));
            });
        }

        [TestMethod]
        public void PrivateFunctions_AreNotExportedAsBareSymbols()
        {
            InDirectory(directory =>
            {
                File.WriteAllText(Path.Combine(directory, "library.puma"), "functions\n    private Hidden() double\n        return 1.25\n    Count() double\n        return 1.25\n");
                var ast = Parse("use\n    library.puma\nproperties\n    value = Count()\nstart\n");
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual("Count", symbols.Single().Name);
                Assert.AreEqual(CountCpp, Normalize(new Codegen().Generate(ast, symbols)));
            });
        }

        [DataTestMethod]
        [DataRow("type", "widget is object", 0)]
        [DataRow("trait", "widget", 3)]
        public void TypeAndTraitMethods_AreNotExportedAsBareFunctions(string kind, string declaration, int expectedKind)
        {
            InDirectory(directory =>
            {
                File.WriteAllText(Path.Combine(directory, "library.puma"), $"{kind}\n    {declaration}\nfunctions\n    Count() double\n        return 1.25\n");
                var ast = Parse("use\n    library.puma\nstart\n");
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual("widget", symbols.Single().Name);
                Assert.AreEqual((ExternalSymbolKind)expectedKind, symbols.Single().Kind);
                const string expected = "#include \"library.h\"\n\n// start\nint main()\n{\n    return 0;\n}";
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, symbols)));
            });
        }

        [DataTestMethod]
        [DataRow("trait", 3, "Line 4, column 13: Cannot instantiate imported trait 'Feature'.")]
        [DataRow("module", 4, "Line 4, column 13: Cannot instantiate imported module 'Feature'.")]
        public void NonInstantiableImport_ReportsCallSiteDiagnostic(string kind, int expectedKind, string message)
        {
            InDirectory(directory =>
            {
                File.WriteAllText(Path.Combine(directory, "library.puma"), $"{kind}\n    Feature\n");
                var ast = Parse("use\n    library.puma\nstart\n    value = Feature()\n");
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual((ExternalSymbolKind)expectedKind, symbols.Single().Kind);
                var exception = Assert.ThrowsException<InvalidOperationException>(() => new Codegen().Generate(ast, symbols));
                Assert.AreEqual(message, exception.Message);
            });
        }

        [TestMethod]
        public void LocalDeclaration_ShadowingImportedModule_IsNotRejected()
        {
            InDirectory(directory =>
            {
                File.WriteAllText(Path.Combine(directory, "library.puma"), "module\n    Feature\n");
                var ast = Parse("use\n    library.puma\nstart\n    value = Feature()\nfunctions\n    Feature() double\n        return 1.25\n");
                const string expected = "#include \"library.h\"\n\n// functions\ndouble Feature(void)\n{\n    return 1.25;\n}\n\n// start\nint main()\n{\n    auto value = Feature();\n\n    return 0;\n}";
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, symbols)));
            });
        }

        [TestMethod]
        public void MissingImport_ReportsExactUseLocation()
        {
            InDirectory(directory =>
            {
                var ast = Parse("use\n    missing.puma\nstart\n");
                var exception = Assert.ThrowsException<InvalidOperationException>(() => new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma")));
                Assert.AreEqual("Line 2, column 5: Puma import 'missing.puma' was not found.", exception.Message);
                Assert.IsInstanceOfType(exception.InnerException, typeof(FileNotFoundException));
            });
        }

        [TestMethod]
        public void InvalidImportedSource_ReportsImportAndParserDiagnostic()
        {
            InDirectory(directory =>
            {
                File.WriteAllText(Path.Combine(directory, "library.puma"), "unexpected\nproperties\n    value = 1\n");
                var ast = Parse("use\n    library.puma\nstart\n");
                var exception = Assert.ThrowsException<InvalidOperationException>(() => new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma")));
                Assert.AreEqual("Line 2, column 5: Unable to parse Puma import 'library.puma': Line 2, column 1: Sections are not allowed after implicit start statements.", exception.Message);
            });
        }

        [TestMethod]
        public void AmbiguousDirectExports_ReportBothFiles()
        {
            InDirectory(directory =>
            {
                File.WriteAllText(Path.Combine(directory, "first.puma"), "functions\n    Count() double\n        return 1.25\n");
                File.WriteAllText(Path.Combine(directory, "second.puma"), "functions\n    Count() double\n        return 1.25\n");
                var ast = Parse("use\n    first.puma\n    second.puma\nstart\n");
                var exception = Assert.ThrowsException<InvalidOperationException>(() => new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma")));
                Assert.AreEqual("Line 3, column 5: Ambiguous imported symbol 'Count' from 'first.puma' and 'second.puma'.", exception.Message);
            });
        }

        [TestMethod]
        public void NativeAndNamespaceImports_DoNotTriggerPumaFileReads()
        {
            InDirectory(directory =>
            {
                var ast = Parse("use\n    native.h\n    Missing.Namespace as N\nstart\n");
                const string expected = "#include \"native.h\"\n#include <Missing/Namespace>\n\n// start\nint main()\n{\n    return 0;\n}";
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual(0, symbols.Count);
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, symbols)));
            });
        }

        [TestMethod]
        public void DirectImports_DoNotSilentlyReexportDependencies()
        {
            InDirectory(directory =>
            {
                File.WriteAllText(Path.Combine(directory, "library.puma"), "use\n    other.puma\nfunctions\n    Count() double\n        return 1.25\n");
                File.WriteAllText(Path.Combine(directory, "other.puma"), "type\n    widget is object\n");
                var ast = Parse("use\n    library.puma\nproperties\n    value = Count()\nstart\n");
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual("Count", symbols.Single().Name);
                Assert.AreEqual(CountCpp, Normalize(new Codegen().Generate(ast, symbols)));
            });
        }

        [TestMethod]
        public void ImportTargetAstReplacement_UsesCurrentTargetAndFreshMetadata()
        {
            InDirectory(directory =>
            {
                File.WriteAllText(Path.Combine(directory, "library.puma"), "functions\n    Count() double\n        return 1.25\n");
                File.WriteAllText(Path.Combine(directory, "types.puma"), "type\n    widget is object\n");
                var ast = Parse("use\n    library.puma\nproperties\n    value = Count()\nstart\n");
                var resolver = new PumaImportResolver();
                var codegen = new Codegen();
                var path = Path.Combine(directory, "main.puma");
                Assert.AreEqual(CountCpp, Normalize(codegen.Generate(ast, resolver.Resolve(ast, path))));
                ast.OfType<UseStatementAstNode>().Single().Target = "types.puma";
                ast.OfType<PropertyDeclarationAstNode>().Single().PropertyValueExpression!.Left!.Value = "widget";
                const string expected = "#include \"types.h\"\n\nauto value = new widget();\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}";
                Assert.AreEqual(expected, Normalize(codegen.Generate(ast, resolver.Resolve(ast, path))));
            });
        }

        [TestMethod]
        public void ResolverReuse_RereadsFilesAfterFailureAndSuccessfulResolution()
        {
            InDirectory(directory =>
            {
                var ast = Parse("use\n    library.puma\nproperties\n    value = Count()\nstart\n");
                var resolver = new PumaImportResolver();
                var path = Path.Combine(directory, "main.puma");
                Assert.ThrowsException<InvalidOperationException>(() => resolver.Resolve(ast, path));
                File.WriteAllText(Path.Combine(directory, "library.puma"), "functions\n    Count() double\n        return 1.25\n");
                Assert.AreEqual(CountCpp, Normalize(new Codegen().Generate(ast, resolver.Resolve(ast, path))));
                File.WriteAllText(Path.Combine(directory, "library.puma"), "type\n    Count is object\n");
                const string expected = "#include \"library.h\"\n\nauto value = new Count();\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}";
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, resolver.Resolve(ast, path))));
            });
        }

        [TestMethod]
        public void UseSourceSpans_IncludeTargetAndAliasWithoutChangingOutput()
        {
            var ast = Parse("use\n    library.puma\n    Tools as T\nstart\n");
            var imports = ast.OfType<UseStatementAstNode>().ToArray();
            Assert.AreEqual(new SourceSpan(2, 5, 2, 17), imports[0].SourceSpan);
            Assert.AreEqual(new SourceSpan(3, 5, 3, 15), imports[1].SourceSpan);
            const string expected = "#include \"library.h\"\n#include <Tools>\n\n// start\nint main()\n{\n    return 0;\n}";
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast)));
        }

        [TestMethod]
        public void CliEmitC_UsesImportedMetadataFromSourceDirectory()
        {
            InDirectory(directory =>
            {
                File.WriteAllText(Path.Combine(directory, "library.puma"), "functions\n    Count() double\n        return 1.25\n");
                var sourcePath = Path.Combine(directory, "main.puma");
                File.WriteAllText(sourcePath, "use\n    library.puma\nproperties\n    value = Count()\nstart\n");
                var result = RunCli(sourcePath);
                Assert.AreEqual(0, result.ExitCode, result.Error);
                Assert.AreEqual(CountCpp, Normalize(File.ReadAllText(Path.ChangeExtension(sourcePath, ".cpp"))));
            });
        }

        [TestMethod]
        public void CliMissingImport_ReturnsBuildErrorWithoutGeneratedOutput()
        {
            InDirectory(directory =>
            {
                var sourcePath = Path.Combine(directory, "main.puma");
                File.WriteAllText(sourcePath, "use\n    missing.puma\nstart\n");
                var result = RunCli(sourcePath);
                Assert.AreEqual(1, result.ExitCode);
                Assert.AreEqual("Puma build error: Line 2, column 5: Puma import 'missing.puma' was not found.", result.Error.Trim());
                Assert.IsFalse(File.Exists(Path.ChangeExtension(sourcePath, ".cpp")));
            });
        }

        private static (int ExitCode, string Error) RunCli(string sourcePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(AppContext.BaseDirectory, "Puma.exe"),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = AppContext.BaseDirectory,
                ArgumentList = { "-emit-c", sourcePath }
            };
            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process);
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            output.GetAwaiter().GetResult();
            return (process.ExitCode, error.GetAwaiter().GetResult());
        }

        private static List<Node> Parse(string source) => new Parser().Parse(new Lexer().Tokenize(source));

        private static string Normalize(string source) => source.Replace("\r\n", "\n").Trim();

        private static void InDirectory(Action<string> action)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"PumaImportTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                action(directory);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

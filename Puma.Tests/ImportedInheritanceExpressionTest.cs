using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class ImportedInheritanceExpressionTest
    {
        private const string Source = "use\n    child.puma\nproperties\n    value = Child()\nstart\n";
        private const string ValueCpp = "#include \"child.h\"\n\nauto value = Child();\n\n// start\nint main()\n{\n    return 0;\n}";
        private const string ObjectCpp = "#include \"child.h\"\n\nauto value = new Child();\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}";
        private const string ChildSource = "use\n    base.puma\ntype\n    Child is Base\n";
        private const string FileCycleMessage = "Line 2, column 5: Unable to resolve Puma import 'a.puma': Line 2, column 5: Unable to resolve Puma import 'b.puma': Line 2, column 5: Cyclic Puma import: a.puma -> b.puma -> a.puma.";

        [DataTestMethod]
        [DataRow("value", 2, ValueCpp)]
        [DataRow("object", 0, ObjectCpp)]
        public void ImportedDerivedType_InheritsBaseKindWithoutReexport(string baseKind, int expectedKind, string expected)
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "base.puma", $"type\n    Base is {baseKind}\n");
                WriteFile(directory, "child.puma", ChildSource);
                var result = Generate(directory, Source);
                Assert.AreEqual("Child", result.Symbols.Single().Name);
                Assert.AreEqual((ExternalSymbolKind)expectedKind, result.Symbols.Single().Kind);
                Assert.AreEqual(expected, result.Code);
            });
        }

        [TestMethod]
        public void MultiLevelValueInheritance_TraversesDependenciesOnlyForClassification()
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "value.puma", "type\n    ValueBase is value\n");
                WriteFile(directory, "base.puma", "use\n    value.puma\ntype\n    Base is ValueBase\n");
                WriteFile(directory, "child.puma", ChildSource);
                var result = Generate(directory, Source);
                Assert.AreEqual(ExternalSymbolKind.ValueType, result.Symbols.Single().Kind);
                Assert.AreEqual(ValueCpp, result.Code);
            });
        }

        [DataTestMethod]
        [DataRow("P")]
        [DataRow("Acme.Parent")]
        public void NamespaceBase_ResolvesAliasAndCanonicalName(string baseName)
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "Acme/Parent.puma", "type\n    Acme.Parent is value\n");
                WriteFile(directory, "Child.puma", $"use\n    Acme.Parent as P\ntype\n    Child is {baseName}\n");
                var result = Generate(directory, "use\n    Child as C\nproperties\n    value = C()\nstart\n");
                const string expected = "#include \"Child.h\"\n\nauto value = Child();\n\n// start\nint main()\n{\n    return 0;\n}";
                Assert.AreEqual(2, result.Symbols.Count);
                Assert.IsTrue(result.Symbols.All(symbol => symbol.Kind == ExternalSymbolKind.ValueType));
                CollectionAssert.AreEquivalent(new[] { "Child", "C" }, result.Symbols.Select(symbol => symbol.Name).ToArray());
                Assert.AreEqual(expected, result.Code);
            });
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SharedDependencyDiamond_IsNotACycleAndIsOrderIndependent(bool reverse)
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "base.puma", "type\n    Base is value\n");
                WriteFile(directory, "left.puma", "use\n    base.puma\ntype\n    Left is Base\n");
                WriteFile(directory, "right.puma", "use\n    base.puma\ntype\n    Right is Base\n");
                var imports = reverse ? "    right.puma\n    left.puma\n" : "    left.puma\n    right.puma\n";
                var result = Generate(directory, $"use\n{imports}properties\n    first = Left()\n    second = Right()\nstart\n");
                const string expected = "#include \"left.h\"\n#include \"right.h\"\n\nauto first = Left();\nauto second = Right();\n\n// start\nint main()\n{\n    return 0;\n}";
                Assert.AreEqual(2, result.Symbols.Count);
                Assert.IsTrue(result.Symbols.All(symbol => symbol.Kind == ExternalSymbolKind.ValueType));
                Assert.AreEqual(expected, result.Code);
            });
        }

        [TestMethod]
        public void DependencyPathsAndScopes_AreRelativeAndIsolatedPerFile()
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "first/base.puma", "type\n    Base is value\n");
                WriteFile(directory, "second/base.puma", "type\n    Base is object\n");
                WriteFile(directory, "first/child.puma", "use\n    base.puma\ntype\n    First is Base\n");
                WriteFile(directory, "second/child.puma", "use\n    base.puma\ntype\n    Second is Base\n");
                var result = Generate(directory, "use\n    first/child.puma\n    second/child.puma\nproperties\n    first = First()\n    second = Second()\nstart\n");
                const string expected = "#include \"first/child.h\"\n#include \"second/child.h\"\n\nauto first = First();\nauto second = new Second();\n\n// start\nint main()\n{\n    delete second;\n    return 0;\n}";
                Assert.AreEqual(2, result.Symbols.Count);
                Assert.AreEqual(ExternalSymbolKind.ValueType, result.Symbols.Single(symbol => symbol.Name == "First").Kind);
                Assert.AreEqual(ExternalSymbolKind.Type, result.Symbols.Single(symbol => symbol.Name == "Second").Kind);
                Assert.AreEqual(expected, result.Code);
            });
        }

        [TestMethod]
        public void FileImportCycle_ReportsNormalizedChainAndUseContext()
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "a.puma", "use\n    b.puma\nfunctions\n    A()\n");
                WriteFile(directory, "b.puma", "use\n    a.puma\nfunctions\n    B()\n");
                Assert.AreEqual(FileCycleMessage, ResolveError(directory, "use\n    a.puma\nstart\n"));
            });
        }

        [TestMethod]
        public void NamespaceImportCycle_ReportsCanonicalFileChain()
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "A.puma", "use\n    B\nmodule\n    A\n");
                WriteFile(directory, "B.puma", "use\n    A\nmodule\n    B\n");
                const string expected = "Line 2, column 5: Unable to resolve Puma import 'A': Line 2, column 5: Unable to resolve Puma import 'B': Line 2, column 5: Cyclic Puma import: A.puma -> B.puma -> A.puma.";
                Assert.AreEqual(expected, ResolveError(directory, "use\n    A as Alias\nstart\n"));
            });
        }

        [TestMethod]
        public void RootSelfImport_UsesCurrentAstAndRejectsCycleBeforeReadingRoot()
        {
            InDirectory(directory =>
            {
                Assert.AreEqual("Line 2, column 5: Cyclic Puma import: main.puma -> main.puma.", ResolveError(directory, "use\n    main.puma\nstart\n"));
            });
        }

        [TestMethod]
        public void NormalizedSelfImport_IsDetectedAsSameFile()
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "a.puma", "use\n    ./a.puma\nmodule\n    A\n");
                const string expected = "Line 2, column 5: Unable to resolve Puma import 'a.puma': Line 2, column 5: Cyclic Puma import: a.puma -> a.puma.";
                Assert.AreEqual(expected, ResolveError(directory, "use\n    a.puma\nstart\n"));
            });
        }

        [TestMethod]
        public void MissingDependency_ReportsBothImportLevels()
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "child.puma", "use\n    missing.puma\ntype\n    Child is object\n");
                const string expected = "Line 2, column 5: Unable to resolve Puma import 'child.puma': Line 2, column 5: Puma import 'missing.puma' was not found.";
                Assert.AreEqual(expected, ResolveError(directory, Source));
            });
        }

        [TestMethod]
        public void MalformedDependency_PreservesParserAndImportLocations()
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "child.puma", ChildSource);
                WriteFile(directory, "base.puma", "unexpected\nproperties\n    value = 1\n");
                const string expected = "Line 2, column 5: Unable to resolve Puma import 'child.puma': Line 2, column 5: Unable to parse Puma import 'base.puma': Line 2, column 1: Sections are not allowed after implicit start statements.";
                Assert.AreEqual(expected, ResolveError(directory, Source));
            });
        }

        [TestMethod]
        public void UnknownBase_IsNotGuessedToBeAnObjectType()
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "child.puma", "type\n    Child is Missing\n");
                const string expected = "Line 2, column 5: Unable to resolve Puma import 'child.puma': Unable to resolve base type 'Missing' of imported type 'Child'.";
                Assert.AreEqual(expected, ResolveError(directory, Source));
            });
        }

        [DataTestMethod]
        [DataRow("trait\n    Base\n")]
        [DataRow("module\n    Base\n")]
        [DataRow("functions\n    Base()\n")]
        public void NonTypeBase_ReportsInvalidInheritance(string baseSource)
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "base.puma", baseSource);
                WriteFile(directory, "child.puma", ChildSource);
                const string expected = "Line 2, column 5: Unable to resolve Puma import 'child.puma': Base 'Base' of imported type 'Child' is not a type.";
                Assert.AreEqual(expected, ResolveError(directory, Source));
            });
        }

        [TestMethod]
        public void SelfInheritance_ReportsTypeCycle()
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "child.puma", "type\n    Child is Child\n");
                const string expected = "Line 2, column 5: Unable to resolve Puma import 'child.puma': Cyclic Puma inheritance: Child -> Child.";
                Assert.AreEqual(expected, ResolveError(directory, Source));
            });
        }

        [TestMethod]
        public void TransitiveBase_NotDirectlyImported_IsNotVisible()
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "base.puma", "type\n    Base is value\n");
                WriteFile(directory, "middle.puma", "use\n    base.puma\nfunctions\n    Count()\n");
                WriteFile(directory, "child.puma", "use\n    middle.puma\ntype\n    Child is Base\n");
                const string expected = "Line 2, column 5: Unable to resolve Puma import 'child.puma': Unable to resolve base type 'Base' of imported type 'Child'.";
                Assert.AreEqual(expected, ResolveError(directory, Source));
            });
        }

        [TestMethod]
        public void ResolverReuse_RereadsDependenciesAndRecoversAfterCycleFailure()
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "base.puma", "type\n    Base is value\n");
                WriteFile(directory, "child.puma", ChildSource);
                var ast = Parse(Source);
                var resolver = new PumaImportResolver();
                var codegen = new Codegen();
                var path = Path.Combine(directory, "main.puma");
                Assert.AreEqual(ValueCpp, Normalize(codegen.Generate(ast, resolver.Resolve(ast, path))));
                WriteFile(directory, "base.puma", "use\n    child.puma\ntype\n    Base is object\n");
                Assert.ThrowsException<InvalidOperationException>(() => resolver.Resolve(ast, path));
                WriteFile(directory, "base.puma", "type\n    Base is object\n");
                Assert.AreEqual(ObjectCpp, Normalize(codegen.Generate(ast, resolver.Resolve(ast, path))));
            });
        }

        [TestMethod]
        public void NativeHeaderOnlyDependency_RemainsCompatible()
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "child.puma", "use\n    native.h\n    Missing.Namespace\ntype\n    Child is object\n");
                var result = Generate(directory, Source);
                Assert.AreEqual("Child", result.Symbols.Single().Name);
                Assert.AreEqual(ObjectCpp, result.Code);
            });
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void CliInheritedValueType_EmitsExactOutputAndSupportsNativeExecution(bool emitOnly)
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "base.puma", "type\n    Base is value\n");
                WriteFile(directory, "child.puma", ChildSource);
                WriteFile(directory, "child.h", "struct Base {};\nstruct Child : Base {};\n");
                WriteFile(directory, "main.puma", Source);
                var result = RunCli(directory, emitOnly);
                Assert.AreEqual(0, result.ExitCode, result.Error);
                Assert.AreEqual(ValueCpp, Normalize(File.ReadAllText(Path.Combine(directory, "main.cpp"))));
                if (!emitOnly)
                {
                    using var executable = Process.Start(new ProcessStartInfo { FileName = Path.Combine(directory, "main.exe"), UseShellExecute = false });
                    Assert.IsNotNull(executable);
                    executable.WaitForExit();
                    Assert.AreEqual(0, executable.ExitCode);
                }
            });
        }

        [TestMethod]
        public void CliImportCycle_ReturnsBuildErrorWithoutGeneratedOutput()
        {
            InDirectory(directory =>
            {
                WriteFile(directory, "a.puma", "use\n    b.puma\nfunctions\n    A()\n");
                WriteFile(directory, "b.puma", "use\n    a.puma\nfunctions\n    B()\n");
                WriteFile(directory, "main.puma", "use\n    a.puma\nstart\n");
                var result = RunCli(directory, true);
                Assert.AreEqual(1, result.ExitCode);
                Assert.AreEqual("Puma build error: " + FileCycleMessage, result.Error.Trim());
                Assert.IsFalse(File.Exists(Path.Combine(directory, "main.cpp")));
            });
        }

        private static (IReadOnlyList<ExternalSymbol> Symbols, string Code) Generate(string directory, string source)
        {
            var ast = Parse(source);
            var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
            return (symbols, Normalize(new Codegen().Generate(ast, symbols)));
        }

        private static string ResolveError(string directory, string source)
        {
            var error = Assert.ThrowsException<InvalidOperationException>(() => new PumaImportResolver().Resolve(Parse(source), Path.Combine(directory, "main.puma")));
            return error.Message;
        }

        private static (int ExitCode, string Error) RunCli(string directory, bool emitOnly)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(AppContext.BaseDirectory, "Puma.exe"),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = AppContext.BaseDirectory
            };
            if (emitOnly) startInfo.ArgumentList.Add("-emit-c");
            startInfo.ArgumentList.Add(Path.Combine(directory, "main.puma"));
            if (!emitOnly)
            {
                startInfo.ArgumentList.Add("-std=c++20");
                startInfo.ArgumentList.Add("-o");
                startInfo.ArgumentList.Add(Path.Combine(directory, "main.exe"));
            }
            var libraryPaths = (Environment.GetEnvironmentVariable("LIB") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => path.Replace("\\x86", "\\x64", StringComparison.OrdinalIgnoreCase))
                .Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (libraryPaths.Length > 0) startInfo.Environment["LIB"] = string.Join(Path.PathSeparator, libraryPaths);
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

        private static void WriteFile(string directory, string relativePath, string content)
        {
            var path = Path.Combine(directory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        private static void InDirectory(Action<string> action)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"PumaInheritanceTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try { action(directory); }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }
}

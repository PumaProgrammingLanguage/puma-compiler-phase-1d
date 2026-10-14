using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class UnresolvedCallExpressionTest
    {
        [DataTestMethod]
        [DataRow("Fetch")]
        [DataRow("fetch")]
        public void GlobalUnresolvedCall_DoesNotAllocateOrOwn(string name)
        {
            var source = $"properties\n    value = {name}()\nstart\n";
            var expected = $"auto value = {name}();\n\n// start\nint main()\n{{\n    return 0;\n}}";
            var ast = Parse(source);
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast)));
            Assert.IsNull(ast.OfType<PropertyDeclarationAstNode>().Single().PropertyValueExpression?.Left?.ResolvedExternalSymbol);
        }

        [DataTestMethod]
        [DataRow("Fetch")]
        [DataRow("fetch")]
        public void LocalUnresolvedCall_DoesNotAllocateOrOwn(string name)
        {
            var source = $"start\n    value = {name}()\n";
            var expected = $"// start\nint main()\n{{\n    auto value = {name}();\n\n    return 0;\n}}";
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(Parse(source))));
        }

        [DataTestMethod]
        [DataRow("Fetch")]
        [DataRow("fetch")]
        public void RecordUnresolvedInitializer_RemainsOrdinaryCall(string name)
        {
            var source = $"records\n    Values\n        value = {name}()\n";
            var expected = $"// records\nstruct Values\n{{\n    auto value = {name}();\n}};";
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(Parse(source))));
        }

        [DataTestMethod]
        [DataRow("Fetch")]
        [DataRow("fetch")]
        public void TypeUnresolvedInitializer_RemainsOrdinaryCall(string name)
        {
            var source = $"type\n    Values is object\nproperties\n    value = {name}()\n";
            var expected = $"class Values : public object\n{{\n    // properties\n    protected:\n    auto value = {name}();\n}};";
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(Parse(source))));
        }

        [DataTestMethod]
        [DataRow("Fetch")]
        [DataRow("fetch")]
        public void UnresolvedReturn_DoesNotInferFactoryOwnership(string name)
        {
            var source = $"properties\n    value = Read()\nstart\nfunctions\n    Read() double\n        return {name}()\n";
            var expected = $"auto value = Read();\n\n// functions\ndouble Read(void)\n{{\n    return {name}();\n}}\n\n// start\nint main()\n{{\n    return 0;\n}}";
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(Parse(source))));
        }

        [TestMethod]
        public void CalleeAstCaseReplacement_DoesNotChangeAllocationOrCleanup()
        {
            const string source = "properties\n    value = Fetch()\nstart\n";
            const string expected = "auto value = fetch();\n\n// start\nint main()\n{\n    return 0;\n}";
            var ast = Parse(source);
            var codegen = new Codegen();
            codegen.Generate(ast);
            ast.OfType<PropertyDeclarationAstNode>().Single().PropertyValueExpression!.Left!.Value = "fetch";
            Assert.AreEqual(expected, Normalize(codegen.Generate(ast)));
        }

        [TestMethod]
        public void SameAst_MetadataControlsConstructionAndOwnershipThenOmissionClearsBoth()
        {
            const string source = "properties\n    value = fetch()\nstart\n";
            const string ordinary = "auto value = fetch();\n\n// start\nint main()\n{\n    return 0;\n}";
            const string constructor = "auto value = new fetch();\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}";
            const string factory = "auto value = fetch();\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}";
            var ast = Parse(source);
            var codegen = new Codegen();
            Assert.AreEqual(ordinary, Normalize(codegen.Generate(ast)));
            Assert.AreEqual(constructor, Normalize(codegen.Generate(ast, new[] { new ExternalSymbol("fetch", ExternalSymbolKind.Type) })));
            Assert.AreEqual(factory, Normalize(codegen.Generate(ast, new[] { new ExternalSymbol("fetch", ExternalSymbolKind.Function, true) })));
            Assert.AreEqual(ordinary, Normalize(codegen.Generate(ast)));
            Assert.IsNull(ast.OfType<PropertyDeclarationAstNode>().Single().PropertyValueExpression?.Left?.ResolvedExternalSymbol);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void CliNativeHeaderOnly_UnresolvedUpperAndLowerCallsRemainOrdinary(bool emitOnly)
        {
            const string source = "use\n    Native\nproperties\n    upper = Fetch()\n    lower = fetch()\nstart\n    Verify()\n";
            const string expected = "#include <Native>\n\nauto upper = Fetch();\nauto lower = fetch();\n\n// start\nint main()\n{\n    Verify();\n    return 0;\n}";
            var directory = Path.Combine(Path.GetTempPath(), $"PumaUnresolvedTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, "Native"), "#include <cstdlib>\ninline int calls = 0;\ninline int Fetch() { ++calls; return 7; }\ninline int fetch() { ++calls; return 9; }\ninline void Verify() { if (calls != 2) std::abort(); }\n");
                var sourcePath = Path.Combine(directory, "main.puma");
                File.WriteAllText(sourcePath, source);
                var executablePath = Path.Combine(directory, "main.exe");
                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(AppContext.BaseDirectory, "Puma.exe"),
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    WorkingDirectory = AppContext.BaseDirectory
                };
                if (emitOnly) startInfo.ArgumentList.Add("-emit-c");
                startInfo.ArgumentList.Add(sourcePath);
                if (!emitOnly)
                {
                    startInfo.ArgumentList.Add("-std=c++20");
                    startInfo.ArgumentList.Add("-I" + directory);
                    startInfo.ArgumentList.Add("-o");
                    startInfo.ArgumentList.Add(executablePath);
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
                Assert.AreEqual(0, process.ExitCode, error.GetAwaiter().GetResult());
                Assert.AreEqual(expected, Normalize(File.ReadAllText(Path.ChangeExtension(sourcePath, ".cpp"))));
                if (!emitOnly)
                {
                    using var executable = Process.Start(new ProcessStartInfo { FileName = executablePath, UseShellExecute = false });
                    Assert.IsNotNull(executable);
                    executable.WaitForExit();
                    Assert.AreEqual(0, executable.ExitCode);
                }
            }
            finally { Directory.Delete(directory, recursive: true); }
        }

        private static List<Node> Parse(string source) => new Parser().Parse(new Lexer().Tokenize(source));
        private static string Normalize(string source) => source.Replace("\r\n", "\n").Trim();
    }
}

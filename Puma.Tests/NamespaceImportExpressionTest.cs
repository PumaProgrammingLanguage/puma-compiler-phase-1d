using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class NamespaceImportExpressionTest
    {
        private const string ModuleSource = "module\n    Acme.Tools\nfunctions\n    Count() int32\n        return 1 int32\n";
        private const string CountCpp = "#include \"Acme/Tools.h\"\n\nauto value = Acme::Tools::Count();\n\n// start\nint main()\n{\n    return 0;\n}";
        private const string ModuleCpp = "#include <cstdint>\n\nnamespace Acme::Tools\n{\n    // functions\n    int32_t Count(void)\n    {\n        return (int32_t)1;\n    }\n}";

        [DataTestMethod]
        [DataRow("Acme.Tools", "Acme.Tools.Count")]
        [DataRow("Acme.Tools as T", "T.Count")]
        public void NamespaceAndAlias_EmitCanonicalCallAndHeader(string import, string callee)
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.Tools", ModuleSource);
                var ast = Parse($"use\n    {import}\nproperties\n    value = {callee}()\nstart\n");
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual(CountCpp, Normalize(new Codegen().Generate(ast, symbols)));
                Assert.AreEqual("Acme::Tools::Count", ast.OfType<PropertyDeclarationAstNode>().Single().PropertyValueExpression?.Left?.ResolvedExternalSymbol?.CppName);
                Assert.AreEqual("Acme/Tools.h", ast.OfType<UseStatementAstNode>().Single().ResolvedImport?.Header);
            });
        }

        [TestMethod]
        public void DottedModuleNamespace_EmitsStandardCppNamespace()
        {
            Assert.AreEqual(ModuleCpp, Normalize(new Codegen().Generate(Parse(ModuleSource))));
        }

        [TestMethod]
        public void MultipleAliasesOfSameSource_KeepBothBindingsAndOneHeader()
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.Tools", ModuleSource);
                var ast = Parse("use\n    Acme.Tools as T\n    Acme.Tools as U\nproperties\n    first = T.Count()\n    second = U.Count()\nstart\n");
                const string expected = "#include \"Acme/Tools.h\"\n\nauto first = Acme::Tools::Count();\nauto second = Acme::Tools::Count();\n\n// start\nint main()\n{\n    return 0;\n}";
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual(6, symbols.Count);
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, symbols)));
            });
        }

        [TestMethod]
        public void FileAndNamespaceImportOfSameModule_DoNotDuplicateSymbols()
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.Tools", ModuleSource);
                var ast = Parse("use\n    Acme/Tools.puma\n    Acme.Tools as T\nproperties\n    value = T.Count()\nstart\n");
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual(4, symbols.Count);
                Assert.AreEqual(CountCpp, Normalize(new Codegen().Generate(ast, symbols)));
            });
        }

        [DataTestMethod]
        [DataRow("Acme.widget", "Acme.widget")]
        [DataRow("Acme.widget as W", "W")]
        public void QualifiedObjectConstructor_UsesCanonicalTypeAndOwnerCleanup(string import, string callee)
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.widget", "type\n    Acme.widget is object\n");
                var ast = Parse($"use\n    {import}\nproperties\n    value = {callee}()\nstart\n");
                const string expected = "#include \"Acme/widget.h\"\n\nauto value = new Acme::widget();\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}";
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, symbols)));
            });
        }

        [TestMethod]
        public void QualifiedLocalConstructor_IsAllocatedBeforeCleanup()
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.widget", "type\n    Acme.widget is object\n");
                var ast = Parse("use\n    Acme.widget as W\nstart\n    value = W()\n");
                const string expected = "#include \"Acme/widget.h\"\n\n// start\nint main()\n{\n    auto value = new Acme::widget();\n\n    delete value;\n    return 0;\n}";
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma")))));
            });
        }

        [DataTestMethod]
        [DataRow(" own", "    delete value;\n")]
        [DataRow("", "")]
        public void QualifiedFunctionLocal_DeletesOnlyExplicitOwnedResult(string modifier, string cleanup)
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.Tools", $"module\n    Acme.Tools\nfunctions\n    Fetch() Shape{modifier}\n        return Shape()\n");
                var ast = Parse("use\n    Acme.Tools as T\nstart\n    value = T.Fetch()\n");
                var expected = $"#include \"Acme/Tools.h\"\n\n// start\nint main()\n{{\n    auto value = Acme::Tools::Fetch();\n\n{cleanup}    return 0;\n}}";
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma")))));
            });
        }

        [TestMethod]
        public void ParameterShadowingAlias_PreservesObjectDotAndOtherFunctionNamespace()
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.Tools", ModuleSource);
                var ast = Parse("use\n    Acme.Tools as T\nfunctions\n    ObjectCall(T Widget)\n        T.Count()\n    NamespaceCall()\n        T.Count()\n");
                const string expected = "#include \"Acme/Tools.h\"\n\n// functions\nvoid ObjectCall(Widget T)\n{\n    T.Count();\n}\n\nvoid NamespaceCall(void)\n{\n    Acme::Tools::Count();\n}";
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma")))));
            });
        }

        [TestMethod]
        public void LocalShadowingAlias_PreservesObjectMemberCall()
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.Tools", ModuleSource);
                var ast = Parse("use\n    Acme.Tools as T\nstart\n    T = receiver\n    T.Count()\n");
                const string expected = "#include \"Acme/Tools.h\"\n\n// start\nint main()\n{\n    auto T = receiver;\n    T.Count();\n\n    return 0;\n}";
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma")))));
            });
        }

        [TestMethod]
        public void BranchLocalShadow_DoesNotLeakOutsideBranch()
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.Tools", ModuleSource);
                var ast = Parse("use\n    Acme.Tools as T\nfunctions\n    Calls()\n        if flag\n            T = receiver\n            T.Count()\n        T.Count()\n");
                const string expected = "#include \"Acme/Tools.h\"\n\n// functions\nvoid Calls(void)\n{\n    if (flag)\n    {\n        T = receiver;\n        T.Count();\n    }\n    Acme::Tools::Count();\n}";
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma")))));
            });
        }

        [TestMethod]
        public void QualifiedAstAndAliasReplacement_RebindWithoutStaleNames()
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.Tools", ModuleSource);
                var ast = Parse("use\n    Acme.Tools as T\nproperties\n    value = T.Count()\nstart\n");
                var resolver = new PumaImportResolver();
                var codegen = new Codegen();
                var path = Path.Combine(directory, "main.puma");
                Assert.AreEqual(CountCpp, Normalize(codegen.Generate(ast, resolver.Resolve(ast, path))));
                ast.OfType<UseStatementAstNode>().Single().Alias = "U";
                ast.OfType<PropertyDeclarationAstNode>().Single().PropertyValueExpression!.Left!.Left!.Value = "U";
                Assert.AreEqual(CountCpp, Normalize(codegen.Generate(ast, resolver.Resolve(ast, path))));
                Assert.AreEqual("U.Count", ast.OfType<PropertyDeclarationAstNode>().Single().PropertyValueExpression?.Left?.ResolvedExternalSymbol?.Name);
            });
        }

        [TestMethod]
        public void ResolverReuse_WhenSourceDisappears_ClearsResolvedHeader()
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.Tools", ModuleSource);
                var ast = Parse("use\n    Acme.Tools as T\nstart\n");
                var resolver = new PumaImportResolver();
                var path = Path.Combine(directory, "main.puma");
                resolver.Resolve(ast, path);
                File.Delete(Path.Combine(directory, "Acme", "Tools.puma"));
                var symbols = resolver.Resolve(ast, path);
                Assert.AreEqual(0, symbols.Count);
                Assert.IsNull(ast.OfType<UseStatementAstNode>().Single().ResolvedImport);
                const string expected = "#include <Acme/Tools>\n\n// start\nint main()\n{\n    return 0;\n}";
                Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, symbols)));
            });
        }

        [TestMethod]
        public void NamespaceDeclarationMismatch_ReportsUseLocation()
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.Tools", "module\n    Wrong\n");
                var ast = Parse("use\n    Acme.Tools as T\nstart\n");
                var error = Assert.ThrowsException<InvalidOperationException>(() => new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma")));
                Assert.AreEqual("Line 2, column 5: Puma namespace import 'Acme.Tools' does not declare 'Acme.Tools'.", error.Message);
            });
        }

        [TestMethod]
        public void ConflictingAliases_ReportBothNamespaces()
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.Tools", ModuleSource);
                WriteModule(directory, "Acme.Other", "module\n    Acme.Other\n");
                var ast = Parse("use\n    Acme.Tools as T\n    Acme.Other as T\nstart\n");
                var error = Assert.ThrowsException<InvalidOperationException>(() => new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma")));
                Assert.AreEqual("Line 3, column 5: Conflicting Puma import alias 'T' for 'Acme.Tools' and 'Acme.Other'.", error.Message);
            });
        }

        [DataTestMethod]
        [DataRow("Missing")]
        [DataRow("Hidden")]
        public void MissingOrPrivateModuleCallable_ReportsCallLocation(string member)
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.Tools", ModuleSource + "    private Hidden() int32\n        return 1 int32\n");
                var ast = Parse($"use\n    Acme.Tools as T\nstart\n    T.{member}()\n");
                var symbols = new PumaImportResolver().Resolve(ast, Path.Combine(directory, "main.puma"));
                var error = Assert.ThrowsException<InvalidOperationException>(() => new Codegen().Generate(ast, symbols));
                Assert.AreEqual($"Line 4, column 5: Unknown callable 'T.{member}' in imported Puma module.", error.Message);
            });
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void CliNamespaceImport_EmitsExactOutputAndCompilesNativeCall(bool emitOnly)
        {
            InDirectory(directory =>
            {
                WriteModule(directory, "Acme.Tools", ModuleSource);
                var header = new Codegen().Generate(Parse(ModuleSource));
                Assert.AreEqual(ModuleCpp, Normalize(header));
                File.WriteAllText(Path.Combine(directory, "Acme", "Tools.h"), header);
                var sourcePath = Path.Combine(directory, "main.puma");
                File.WriteAllText(sourcePath, "use\n    Acme.Tools as T\nproperties\n    value = T.Count()\nstart\n");
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
                Assert.AreEqual(CountCpp, Normalize(File.ReadAllText(Path.ChangeExtension(sourcePath, ".cpp"))));
                if (!emitOnly)
                {
                    using var executable = Process.Start(new ProcessStartInfo { FileName = executablePath, UseShellExecute = false });
                    Assert.IsNotNull(executable);
                    executable.WaitForExit();
                    Assert.AreEqual(0, executable.ExitCode);
                }
            });
        }

        private static List<Node> Parse(string source) => new Parser().Parse(new Lexer().Tokenize(source));
        private static string Normalize(string source) => source.Replace("\r\n", "\n").Trim();

        private static void WriteModule(string directory, string name, string source)
        {
            var path = Path.Combine(directory, name.Replace('.', Path.DirectorySeparatorChar) + ".puma");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, source);
        }

        private static void InDirectory(Action<string> action)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"PumaNamespaceTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try { action(directory); }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }
}

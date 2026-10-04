using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class ExternalSymbolExpressionTest
    {
        private const string FunctionCpp = "auto value = Fetch();\n\n// start\nint main()\n{\n    return 0;\n}";
        private const string OwnedFunctionCpp = "auto value = Fetch();\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}";
        private const string ConstructorCpp = "auto value = new Fetch();\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}";

        [DataTestMethod]
        [DataRow((int)ExternalSymbolKind.Function, false, FunctionCpp)]
        [DataRow((int)ExternalSymbolKind.Function, true, OwnedFunctionCpp)]
        [DataRow((int)ExternalSymbolKind.Type, false, ConstructorCpp)]
        public void GlobalInitializer_UsesExternalKindAndOwnership(int kind, bool owned, string expected)
        {
            const string source = "properties\n    value = Fetch()\nstart\n";
            var ast = Parse(source);
            var symbol = new ExternalSymbol("Fetch", (ExternalSymbolKind)kind, owned);
            var generated = new Codegen().GenerateResult(ast, new[] { symbol });
            Assert.AreEqual(expected, Normalize(generated.SourceCode));
            Assert.AreEqual(0, generated.RequiredRuntimeLibraries.Count);
            Assert.AreSame(symbol, ast.OfType<PropertyDeclarationAstNode>().Single().PropertyValueExpression?.Left?.ResolvedExternalSymbol);
        }

        [TestMethod]
        public void LowercaseExternalType_IsAllocatedAndDeleted()
        {
            const string source = "properties\n    value = widget()\nstart\n";
            const string expected = "auto value = new widget();\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}";
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(Parse(source), new[] { new ExternalSymbol("widget", ExternalSymbolKind.Type) })));
        }

        [DataTestMethod]
        [DataRow(false, "// start\nint main()\n{\n    auto value = Fetch();\n\n    return 0;\n}")]
        [DataRow(true, "// start\nint main()\n{\n    auto value = Fetch();\n\n    delete value;\n    return 0;\n}")]
        public void ExternalFunctionLocal_DeletesOnlyOwnedResults(bool owned, string expected)
        {
            const string source = "start\n    value = Fetch()\n";
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(Parse(source), new[] { new ExternalSymbol("Fetch", ExternalSymbolKind.Function, owned) })));
        }

        [DataTestMethod]
        [DataRow((int)ExternalSymbolKind.Function, "// records\nstruct Values\n{\n    auto value = Fetch();\n};")]
        [DataRow((int)ExternalSymbolKind.Type, "// records\nstruct Values\n{\n    auto value = new Fetch();\n};")]
        public void RecordInitializer_UsesExternalMetadata(int kind, string expected)
        {
            const string source = "records\n    Values\n        value = Fetch()\n";
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(Parse(source), new[] { new ExternalSymbol("Fetch", (ExternalSymbolKind)kind) })));
        }

        [DataTestMethod]
        [DataRow("type", " : public object")]
        [DataRow("trait", "")]
        public void OwnerProperty_UsesMetadataWithoutTopLevelPropertyNode(string ownerKind, string inheritance)
        {
            var source = $"{ownerKind}\n    Widget{(ownerKind == "type" ? " is object" : "")}\nproperties\n    value = Fetch()\n";
            var expected = $"class Widget{inheritance}\n{{\n    // properties\n    protected:\n    auto value = Fetch();\n}};";
            var ast = Parse(source);
            ast.RemoveAll(node => node.Kind == NodeKind.PropertyDeclaration);
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, new[] { new ExternalSymbol("Fetch", ExternalSymbolKind.Function) })));
        }

        [DataTestMethod]
        [DataRow((int)ExternalSymbolKind.Type, false)]
        [DataRow((int)ExternalSymbolKind.Function, true)]
        public void LocalFunctionDeclaration_OverridesExternalAllocationAndOwnership(int kind, bool owned)
        {
            const string source = "start\n    value = Fetch()\nfunctions\n    Fetch() double\n        return 1.25\n";
            const string expected = "// functions\ndouble Fetch(void)\n{\n    return 1.25;\n}\n\n// start\nint main()\n{\n    auto value = Fetch();\n\n    return 0;\n}";
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(Parse(source), new[] { new ExternalSymbol("Fetch", (ExternalSymbolKind)kind, owned) })));
        }

        [TestMethod]
        public void LocalTypeDeclaration_OverridesExternalFunctionMetadata()
        {
            const string source = "type\n    item is object\nproperties\n    value = item()\n";
            const string expected = "class item : public object\n{\n    // properties\n    protected:\n    auto value = new item();\n};";
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(Parse(source), new[] { new ExternalSymbol("item", ExternalSymbolKind.Function) })));
        }

        [TestMethod]
        public void NestedFactoryReturns_PropagateExternalOwnedResultsIndependentOfDeclarationOrder()
        {
            const string source = "properties\n    value = Wrap()\nstart\nfunctions\n    Wrap() Shape own\n        return Forward()\n    Forward() Shape own\n        if flag\n            return acquire()\n";
            const string expected = "auto value = Wrap();\n\n// functions\nShape Wrap(void)\n{\n    return Forward();\n}\n\nShape Forward(void)\n{\n    if (flag)\n    {\n        return acquire();\n    }\n}\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}";
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(Parse(source), new[] { new ExternalSymbol("acquire", ExternalSymbolKind.Function, true) })));
        }

        [TestMethod]
        public void ExternalBorrowedReturn_DoesNotCreateFactoryOwner()
        {
            const string source = "properties\n    value = Wrap()\nstart\nfunctions\n    Wrap() Shape\n        return Fetch()\n";
            const string expected = "auto value = Wrap();\n\n// functions\nShape Wrap(void)\n{\n    return Fetch();\n}\n\n// start\nint main()\n{\n    return 0;\n}";
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(Parse(source), new[] { new ExternalSymbol("Fetch", ExternalSymbolKind.Function) })));
        }

        [TestMethod]
        public void MetadataReplacementAndOmission_RebindSameAstWithoutStaleOwnership()
        {
            const string source = "properties\n    value = Fetch()\nstart\n";
            var ast = Parse(source);
            var codegen = new Codegen();
            Assert.AreEqual(OwnedFunctionCpp, Normalize(codegen.Generate(ast, new[] { new ExternalSymbol("Fetch", ExternalSymbolKind.Function, true) })));
            Assert.AreEqual(FunctionCpp, Normalize(codegen.Generate(ast, new[] { new ExternalSymbol("Fetch", ExternalSymbolKind.Function) })));
            Assert.AreEqual(ConstructorCpp, Normalize(codegen.Generate(ast)));
            Assert.IsNull(ast.OfType<PropertyDeclarationAstNode>().Single().PropertyValueExpression?.Left?.ResolvedExternalSymbol);
            Assert.AreEqual(FunctionCpp, Normalize(codegen.Generate(ast, new[] { new ExternalSymbol("Fetch", ExternalSymbolKind.Function) })));
        }

        [TestMethod]
        public void CalleeAstReplacement_UsesCurrentIdentifierAndMetadata()
        {
            const string source = "properties\n    value = Fetch()\nstart\n";
            const string expected = "auto value = new widget();\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}";
            var ast = Parse(source);
            var symbols = new[] { new ExternalSymbol("Fetch", ExternalSymbolKind.Function), new ExternalSymbol("widget", ExternalSymbolKind.Type) };
            var codegen = new Codegen();
            Assert.AreEqual(FunctionCpp, Normalize(codegen.Generate(ast, symbols)));
            ast.OfType<PropertyDeclarationAstNode>().Single().PropertyValueExpression!.Left!.Value = "widget";
            Assert.AreEqual(expected, Normalize(codegen.Generate(ast, symbols)));
            Assert.AreSame(symbols[1], ast.OfType<PropertyDeclarationAstNode>().Single().PropertyValueExpression?.Left?.ResolvedExternalSymbol);
        }

        [TestMethod]
        public void CodegenReuse_DoesNotLeakMetadataIntoDifferentAst()
        {
            const string source = "properties\n    value = Fetch()\nstart\n";
            var codegen = new Codegen();
            Assert.AreEqual(FunctionCpp, Normalize(codegen.Generate(Parse(source), new[] { new ExternalSymbol("Fetch", ExternalSymbolKind.Function) })));
            Assert.AreEqual(ConstructorCpp, Normalize(codegen.Generate(Parse(source))));
        }

        [TestMethod]
        public void MetadataNames_AreCaseSensitive()
        {
            const string source = "properties\n    value = Fetch()\nstart\n";
            Assert.AreEqual(ConstructorCpp, Normalize(new Codegen().Generate(Parse(source), new[] { new ExternalSymbol("fetch", ExternalSymbolKind.Function) })));
        }

        [TestMethod]
        public void DefaultExpression_BindsExternalCalleeMetadata()
        {
            const string source = "functions\n    F(value double = Fetch())\n    Caller()\n        F()\n";
            const string expected = "// functions\nvoid F(double value)\n{\n}\n\nvoid Caller(void)\n{\n    F(Fetch());\n}";
            var ast = Parse(source);
            var symbol = new ExternalSymbol("Fetch", ExternalSymbolKind.Function);
            Assert.AreEqual(expected, Normalize(new Codegen().Generate(ast, new[] { symbol })));
            Assert.AreSame(symbol, ast.OfType<FunctionDeclarationAstNode>().First().FunctionParameterList.Single().DefaultExpression?.Left?.ResolvedExternalSymbol);
        }

        [TestMethod]
        public void DuplicateMetadata_RaisesExactCompilerDiagnostic()
        {
            const string source = "start\n";
            var exception = Assert.ThrowsException<InvalidOperationException>(() => new Codegen().Generate(Parse(source), new[]
            {
                new ExternalSymbol("Fetch", ExternalSymbolKind.Function),
                new ExternalSymbol("Fetch", ExternalSymbolKind.Type)
            }));
            Assert.AreEqual("Duplicate external symbol metadata for 'Fetch'.", exception.Message);
        }

        [DataTestMethod]
        [DataRow("", (int)ExternalSymbolKind.Function, false, "External symbol names must not be empty.")]
        [DataRow("Fetch", 99, false, "Invalid metadata for external symbol 'Fetch'.")]
        [DataRow("widget", (int)ExternalSymbolKind.Type, true, "Invalid metadata for external symbol 'widget'.")]
        public void InvalidMetadata_RaisesExactCompilerDiagnostic(string name, int kind, bool owned, string message)
        {
            const string source = "start\n";
            var exception = Assert.ThrowsException<InvalidOperationException>(() => new Codegen().Generate(Parse(source), new[] { new ExternalSymbol(name, (ExternalSymbolKind)kind, owned) }));
            Assert.AreEqual(message, exception.Message);
        }

        private static List<Node> Parse(string source) => new Parser().Parse(new Lexer().Tokenize(source));

        private static string Normalize(string source) => source.Replace("\r\n", "\n").Trim();
    }
}

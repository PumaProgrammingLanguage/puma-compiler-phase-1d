using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class ConstructorExpressionTest
    {
        [TestMethod]
        public void DeclaredFunctionProperty_IsNotAllocatedOrDeletedAsConstructor()
        {
            const string source = "properties\n    value = Count()\nstart\nfunctions\n    Count() double\n        return 1.25\n";
            const string expected = "auto value = Count();\n\n// functions\ndouble Count(void)\n{\n    return 1.25;\n}\n\n// start\nint main()\n{\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void DeclaredFunctionLocal_IsNotDeletedAsConstructor()
        {
            const string source = "start\n    value = Count()\nfunctions\n    Count() double\n        return 1.25\n";
            const string expected = "// functions\ndouble Count(void)\n{\n    return 1.25;\n}\n\n// start\nint main()\n{\n    auto value = Count();\n\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void RecordInitializer_ResolvesDeclaredFunction()
        {
            const string source = "records\n    Values\n        value = Count()\nfunctions\n    Count() double\n        return 1.25\n";
            const string expected = "// records\nstruct Values\n{\n    auto value = Count();\n};\n\n// functions\ndouble Count(void)\n{\n    return 1.25;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [DataTestMethod]
        [DataRow("type", " : public object")]
        [DataRow("trait", "")]
        public void OwnerProperty_ResolvesItsStructuredMethods(string kind, string inheritance)
        {
            var source = $"{kind}\n    Widget{(kind == "type" ? " is object" : "")}\nproperties\n    value = Count()\nfunctions\n    Count() double\n        return 1.25\n";
            var expected = $"class Widget{inheritance}\n{{\n    // properties\n    protected:\n    auto value = Count();\n\n    // functions\n    public:\n    double Count()\n    {{\n        return 1.25;\n    }}\n}};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            ast.RemoveAll(node => node.Kind == NodeKind.FunctionDeclaration);
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void DeclaredLowercaseType_IsRecognizedAsConstructor()
        {
            const string source = "type\n    item is object\nproperties\n    value = item()\n";
            const string expected = "class item : public object\n{\n    // properties\n    protected:\n    auto value = new item();\n};";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void NestedConstructedReturn_PreservesFactoryOwnerCleanup()
        {
            const string source = "start\n    value = makeShape()\nfunctions\n    makeShape() Shape own\n        if flag\n            return Shape()\n";
            const string expected = "// functions\nShape makeShape(void)\n{\n    if (flag)\n    {\n        return Shape();\n    }\n}\n\n// start\nint main()\n{\n    auto value = makeShape();\n\n    delete value;\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast, new[] { new ExternalSymbol("Shape", ExternalSymbolKind.Type) }).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void ReturnOfDeclaredNumericFunction_DoesNotCreateFactoryOwner()
        {
            const string source = "start\n    value = Read()\nfunctions\n    Count() double\n        return 1.25\n    Read() double\n        return Count()\n";
            const string expected = "// functions\ndouble Count(void)\n{\n    return 1.25;\n}\n\ndouble Read(void)\n{\n    return Count();\n}\n\n// start\nint main()\n{\n    auto value = Read();\n\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void GlobalFactoryResult_RemainsOwnerWithoutConstructorWrapping()
        {
            const string source = "properties\n    value = makeShape()\nstart\nfunctions\n    makeShape() Shape own\n        return Shape()\n";
            const string expected = "auto value = makeShape();\n\n// functions\nShape makeShape(void)\n{\n    return Shape();\n}\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            Assert.AreEqual(expected, new Codegen().Generate(ast, new[] { new ExternalSymbol("Shape", ExternalSymbolKind.Type) }).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void FunctionDeclarationAstChange_UpdatesAllocationAndCleanup()
        {
            const string source = "properties\n    value = Shape()\nstart\nfunctions\n    Read() double\n        return 1.25\n";
            const string expected = "auto value = Shape();\n\n// functions\ndouble Shape(void)\n{\n    return 1.25;\n}\n\n// start\nint main()\n{\n    return 0;\n}";
            var ast = new Parser().Parse(new Lexer().Tokenize(source));
            ast.OfType<FunctionDeclarationAstNode>().Single().FunctionDeclarationName = "Shape";
            Assert.AreEqual(expected, new Codegen().Generate(ast).Replace("\r\n", "\n").Trim());
        }

        [TestMethod]
        public void CodegenReuse_DoesNotRetainPreviousFunctionDeclarations()
        {
            const string firstSource = "properties\n    value = Shape()\nstart\nfunctions\n    Shape() double\n        return 1.25\n";
            const string secondSource = "properties\n    value = Shape()\nstart\n";
            const string expected = "auto value = new Shape();\n\n// start\nint main()\n{\n    delete value;\n    return 0;\n}";
            var codegen = new Codegen();
            var symbols = new[] { new ExternalSymbol("Shape", ExternalSymbolKind.Type) };
            codegen.Generate(new Parser().Parse(new Lexer().Tokenize(firstSource)), symbols);
            var ast = new Parser().Parse(new Lexer().Tokenize(secondSource));
            Assert.AreEqual(expected, codegen.Generate(ast, symbols).Replace("\r\n", "\n").Trim());
        }
    }
}

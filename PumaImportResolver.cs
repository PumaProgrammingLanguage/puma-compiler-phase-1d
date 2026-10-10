namespace Puma
{
    internal sealed class PumaImportResolver
    {
        internal IReadOnlyList<ExternalSymbol> Resolve(List<Node> ast, string sourceFilePath)
        {
            var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceFilePath))!;
            var paths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            var symbols = new Dictionary<string, ExternalSymbol>(StringComparer.Ordinal);
            var origins = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var import in ast.OfType<UseStatementAstNode>())
            {
                if (!import.IsFilePath || import.Target is not { } target
                    || !target.EndsWith(".puma", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (import.Alias != null)
                {
                    throw ImportError(import, "File path use statements cannot specify an alias.");
                }

                string path;
                try
                {
                    path = Path.GetFullPath(target, sourceDirectory);
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    throw ImportError(import, $"Invalid Puma import path '{target}'.", exception);
                }

                if (!paths.Add(path))
                {
                    continue;
                }

                string source;
                try
                {
                    source = File.ReadAllText(path);
                }
                catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
                {
                    throw ImportError(import, $"Puma import '{target}' was not found.", exception);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    throw ImportError(import, $"Unable to read Puma import '{target}': {exception.Message}", exception);
                }

                List<Node> importedAst;
                try
                {
                    importedAst = new Parser().Parse(new Lexer().Tokenize(source));
                }
                catch (InvalidOperationException exception)
                {
                    throw ImportError(import, $"Unable to parse Puma import '{target}': {exception.Message}", exception);
                }

                foreach (var symbol in GetExports(importedAst))
                {
                    if (!symbols.TryAdd(symbol.Name, symbol))
                    {
                        throw ImportError(import, $"Ambiguous imported symbol '{symbol.Name}' from '{origins[symbol.Name]}' and '{target}'.");
                    }

                    origins.Add(symbol.Name, target);
                }
            }

            return symbols.Values.ToArray();
        }

        private static IEnumerable<ExternalSymbol> GetExports(List<Node> ast)
        {
            var declarations = ast.OfType<TypeDeclarationAstNode>().ToList();
            foreach (var declaration in declarations)
            {
                if (string.IsNullOrWhiteSpace(declaration.DeclarationName))
                {
                    continue;
                }

                var kind = declaration.DeclarationKind switch
                {
                    "type" when declaration.BaseTypeName == "value" => ExternalSymbolKind.ValueType,
                    "type" => ExternalSymbolKind.Type,
                    "trait" => ExternalSymbolKind.Trait,
                    "module" => ExternalSymbolKind.Module,
                    _ => (ExternalSymbolKind?)null
                };
                if (kind != null)
                {
                    yield return new ExternalSymbol(declaration.DeclarationName, kind.Value);
                }
            }

            if (declarations.Any(declaration => declaration.DeclarationKind is "type" or "trait"))
            {
                yield break;
            }

            foreach (var function in ast.OfType<FunctionDeclarationAstNode>())
            {
                if (!string.IsNullOrWhiteSpace(function.FunctionDeclarationName)
                    && !function.FunctionModifiers.Contains("private"))
                {
                    yield return new ExternalSymbol(function.FunctionDeclarationName, ExternalSymbolKind.Function,
                        function.FunctionModifiers.Contains("own"));
                }
            }
        }

        private static InvalidOperationException ImportError(UseStatementAstNode import, string message, Exception? innerException = null)
        {
            var location = import.SourceSpan is { } span ? $"Line {span.StartLine}, column {span.StartColumn}: " : string.Empty;
            return new InvalidOperationException(location + message, innerException);
        }
    }
}

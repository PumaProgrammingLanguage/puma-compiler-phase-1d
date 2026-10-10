namespace Puma
{
    internal sealed class PumaImportResolver
    {
        internal IReadOnlyList<ExternalSymbol> Resolve(List<Node> ast, string sourceFilePath)
        {
            var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceFilePath))!;
            var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var files = new Dictionary<string, List<Node>>(pathComparer);
            var symbols = new Dictionary<string, ExternalSymbol>(StringComparer.Ordinal);
            var origins = new Dictionary<string, (string Path, string Target)>(StringComparer.Ordinal);
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var import in ast.OfType<UseStatementAstNode>())
            {
                import.ResolvedImport = null;
                if (string.IsNullOrWhiteSpace(import.Target)
                    || (import.IsFilePath && !import.Target.EndsWith(".puma", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var target = import.Target;
                if (import.IsFilePath && import.Alias != null)
                {
                    throw ImportError(import, "File path use statements cannot specify an alias.");
                }

                var relativePath = import.IsFilePath ? target : target.Replace('.', '/') + ".puma";
                string path;
                try
                {
                    path = Path.GetFullPath(relativePath, sourceDirectory);
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    throw ImportError(import, $"Invalid Puma import path '{target}'.", exception);
                }

                if (!files.TryGetValue(path, out var importedAst))
                {
                    string source;
                    try
                    {
                        source = File.ReadAllText(path);
                    }
                    catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
                    {
                        if (!import.IsFilePath) continue;
                        throw ImportError(import, $"Puma import '{target}' was not found.", exception);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        throw ImportError(import, $"Unable to read Puma import '{target}': {exception.Message}", exception);
                    }

                    try
                    {
                        importedAst = new Parser().Parse(new Lexer().Tokenize(source));
                    }
                    catch (InvalidOperationException exception)
                    {
                        throw ImportError(import, $"Unable to parse Puma import '{target}': {exception.Message}", exception);
                    }
                    files.Add(path, importedAst);
                }

                if (!import.IsFilePath && !importedAst.OfType<TypeDeclarationAstNode>().Any(declaration => declaration.DeclarationName == target))
                {
                    throw ImportError(import, $"Puma namespace import '{target}' does not declare '{target}'.");
                }
                if (import.Alias is { } alias)
                {
                    if (aliases.TryGetValue(alias, out var previous) && previous != target)
                    {
                        throw ImportError(import, $"Conflicting Puma import alias '{alias}' for '{previous}' and '{target}'.");
                    }
                    aliases[alias] = target;
                }

                import.ResolvedImport = new ResolvedPumaImport(target, import.Alias, relativePath[..^5] + ".h");
                foreach (var symbol in GetExports(importedAst))
                {
                    Register(symbol);
                    if (import.Alias is { } lookupAlias && (symbol.Name == target || symbol.Name.StartsWith(target + ".", StringComparison.Ordinal)))
                    {
                        Register(symbol with { Name = lookupAlias + symbol.Name[target.Length..] });
                    }

                    void Register(ExternalSymbol entry)
                    {
                        if (symbols.TryGetValue(entry.Name, out var existing))
                        {
                            if (pathComparer.Equals(origins[entry.Name].Path, path) && existing == entry) return;
                            throw ImportError(import, $"Ambiguous imported symbol '{entry.Name}' from '{origins[entry.Name].Target}' and '{target}'.");
                        }
                        symbols.Add(entry.Name, entry);
                        origins.Add(entry.Name, (path, target));
                    }
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
                    yield return new ExternalSymbol(declaration.DeclarationName, kind.Value,
                        CppName: declaration.DeclarationName.Replace(".", "::", StringComparison.Ordinal));
                }
            }

            if (declarations.Any(declaration => declaration.DeclarationKind is "type" or "trait"))
            {
                yield break;
            }

            var moduleName = declarations.FirstOrDefault(declaration => declaration.DeclarationKind == "module")?.DeclarationName;
            foreach (var function in ast.OfType<FunctionDeclarationAstNode>())
            {
                if (!string.IsNullOrWhiteSpace(function.FunctionDeclarationName)
                    && !function.FunctionModifiers.Contains("private"))
                {
                    var name = moduleName != null && !function.FunctionDeclarationName.StartsWith(moduleName + ".", StringComparison.Ordinal)
                        ? moduleName + "." + function.FunctionDeclarationName
                        : function.FunctionDeclarationName;
                    yield return new ExternalSymbol(name, ExternalSymbolKind.Function,
                        function.FunctionModifiers.Contains("own"), name.Replace(".", "::", StringComparison.Ordinal));
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

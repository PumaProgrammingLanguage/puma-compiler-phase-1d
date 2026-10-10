namespace Puma
{
    internal sealed class PumaImportResolver
    {
        internal IReadOnlyList<ExternalSymbol> Resolve(List<Node> ast, string sourceFilePath)
        {
            var sourcePath = Path.GetFullPath(sourceFilePath);
            return new ResolutionSession(sourcePath).ResolveImports(ast, sourcePath).Values.ToArray();
        }

        private sealed class ResolutionSession
        {
            private sealed record ImportedFile(List<Node> Ast, IReadOnlyList<ExternalSymbol> Exports);

            private readonly StringComparer _pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            private readonly Dictionary<string, ImportedFile> _files;
            private readonly List<string> _activePaths = new();
            private readonly string _rootDirectory;

            internal ResolutionSession(string sourcePath)
            {
                _files = new Dictionary<string, ImportedFile>(_pathComparer);
                _rootDirectory = Path.GetDirectoryName(sourcePath)!;
                _activePaths.Add(sourcePath);
            }

            internal Dictionary<string, ExternalSymbol> ResolveImports(List<Node> ast, string sourcePath)
            {
                var sourceDirectory = Path.GetDirectoryName(sourcePath)!;
                var symbols = new Dictionary<string, ExternalSymbol>(StringComparer.Ordinal);
                var origins = new Dictionary<string, (string Path, string Target)>(StringComparer.Ordinal);
                var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
                var imports = ast.OfType<UseStatementAstNode>().ToList();
                foreach (var import in imports) import.ResolvedImport = null;

                foreach (var import in imports)
                {
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

                    var file = LoadFile(path, import);
                    if (file == null) continue;
                    if (!import.IsFilePath && !file.Ast.OfType<TypeDeclarationAstNode>().Any(declaration => declaration.DeclarationName == target))
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
                    foreach (var symbol in file.Exports)
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
                                if (_pathComparer.Equals(origins[entry.Name].Path, path) && existing == entry) return;
                                throw ImportError(import, $"Ambiguous imported symbol '{entry.Name}' from '{origins[entry.Name].Target}' and '{target}'.");
                            }
                            symbols.Add(entry.Name, entry);
                            origins.Add(entry.Name, (path, target));
                        }
                    }
                }

                return symbols;
            }

            private ImportedFile? LoadFile(string path, UseStatementAstNode import)
            {
                var cycleIndex = _activePaths.FindIndex(active => _pathComparer.Equals(active, path));
                if (cycleIndex >= 0)
                {
                    var chain = _activePaths.Skip(cycleIndex).Append(path)
                        .Select(file => Path.GetRelativePath(_rootDirectory, file).Replace('\\', '/'));
                    throw ImportError(import, $"Cyclic Puma import: {string.Join(" -> ", chain)}.");
                }
                if (_files.TryGetValue(path, out var cached)) return cached;

                string source;
                try
                {
                    source = File.ReadAllText(path);
                }
                catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
                {
                    if (!import.IsFilePath) return null;
                    throw ImportError(import, $"Puma import '{import.Target}' was not found.", exception);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    throw ImportError(import, $"Unable to read Puma import '{import.Target}': {exception.Message}", exception);
                }

                List<Node> importedAst;
                try
                {
                    importedAst = new Parser().Parse(new Lexer().Tokenize(source));
                }
                catch (InvalidOperationException exception)
                {
                    throw ImportError(import, $"Unable to parse Puma import '{import.Target}': {exception.Message}", exception);
                }

                _activePaths.Add(path);
                try
                {
                    var dependencies = ResolveImports(importedAst, path);
                    var file = new ImportedFile(importedAst, GetExports(importedAst, dependencies).ToArray());
                    _files.Add(path, file);
                    return file;
                }
                catch (InvalidOperationException exception)
                {
                    throw ImportError(import, $"Unable to resolve Puma import '{import.Target}': {exception.Message}", exception);
                }
                finally
                {
                    _activePaths.RemoveAt(_activePaths.Count - 1);
                }
            }
        }

        private static IEnumerable<ExternalSymbol> GetExports(List<Node> ast, IReadOnlyDictionary<string, ExternalSymbol> dependencies)
        {
            var declarations = ast.OfType<TypeDeclarationAstNode>().ToList();
            var classifications = new Dictionary<string, ExternalSymbolKind>(StringComparer.Ordinal);
            var activeTypes = new List<string>();
            foreach (var declaration in declarations)
            {
                if (string.IsNullOrWhiteSpace(declaration.DeclarationName))
                {
                    continue;
                }

                var kind = declaration.DeclarationKind switch
                {
                    "type" => ClassifyType(declaration),
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

            ExternalSymbolKind ClassifyType(TypeDeclarationAstNode declaration)
            {
                var name = declaration.DeclarationName!;
                if (classifications.TryGetValue(name, out var cached)) return cached;
                var cycleIndex = activeTypes.IndexOf(name);
                if (cycleIndex >= 0)
                {
                    throw new InvalidOperationException($"Cyclic Puma inheritance: {string.Join(" -> ", activeTypes.Skip(cycleIndex).Append(name))}.");
                }

                activeTypes.Add(name);
                try
                {
                    ExternalSymbolKind kind;
                    if (declaration.BaseTypeName is "object" or "value")
                    {
                        kind = declaration.BaseTypeName == "value" ? ExternalSymbolKind.ValueType : ExternalSymbolKind.Type;
                    }
                    else
                    {
                        var localBase = declarations.FirstOrDefault(candidate => candidate.DeclarationKind == "type"
                            && candidate.DeclarationName == declaration.BaseTypeName);
                        if (localBase != null)
                        {
                            kind = ClassifyType(localBase);
                        }
                        else if (declaration.BaseTypeName is { } baseName && dependencies.TryGetValue(baseName, out var symbol))
                        {
                            if (symbol.Kind is not (ExternalSymbolKind.Type or ExternalSymbolKind.ValueType))
                            {
                                throw new InvalidOperationException($"Base '{baseName}' of imported type '{name}' is not a type.");
                            }
                            kind = symbol.Kind;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unable to resolve base type '{declaration.BaseTypeName}' of imported type '{name}'.");
                        }
                    }

                    classifications.Add(name, kind);
                    return kind;
                }
                finally
                {
                    activeTypes.RemoveAt(activeTypes.Count - 1);
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

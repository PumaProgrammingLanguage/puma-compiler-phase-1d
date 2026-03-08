// LLVM Compiler for the Puma programming language
//   as defined in the document "The Puma Programming Language Specification"
//   available at https://github.com/ThePumaProgrammingLanguage
//
// Copyright © 2024-2026 by Darryl Anthony Burchfield
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using static Puma.Lexer;

namespace Puma
{
    internal partial class Parser
    {
        private enum FileDeclarationKind
        {
            None,
            Module,
            Type,
            Trait
        }

        readonly string[] Sections =
        [
            // use
            "use",
            // type (currently recognized but not enforced in the order below)
            "type",
            "trait",
            "module",
            // enums
            "enums",
            // records
            "records",
            // properties
            "properties",
            // initialize
            "initialize",
            "start",
            // finalize
            "finalize",
            // functions
            "functions",
        ];

        public enum Section
        {
            None,
            Use,
            Module,
            Type,
            Trait,
            Enums,
            Records,
            Properties,
            Start,
            Initialize,
            Finalize,
            Functions,
            Invalid,
        }

        private readonly Dictionary<string, Section> _sectionMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["use"] = Section.Use,
            ["module"] = Section.Module,
            ["type"] = Section.Type,
            ["trait"] = Section.Trait,
            ["enums"] = Section.Enums,
            ["records"] = Section.Records,
            ["properties"] = Section.Properties,
            ["start"] = Section.Start,
            ["initialize"] = Section.Initialize,
            ["finalize"] = Section.Finalize,
            ["functions"] = Section.Functions
        };

        // Enforce order: sections are optional, but if present they must follow this sequence.
        // type/trait/module share the same position, and start/initialize share the same position.
        private static readonly Dictionary<Section, int> SectionRank = new()
        {
            [Section.Use] = 10,
            [Section.Type] = 20,
            [Section.Trait] = 20,
            [Section.Module] = 20,
            [Section.Enums] = 30,
            [Section.Records] = 40,
            [Section.Properties] = 50,
            [Section.Start] = 60,
            [Section.Initialize] = 60,
            [Section.Finalize] = 70,
            [Section.Functions] = 80
        };

        private const string ExpectedOrderText =
            "use, type/trait/module, enums, records, properties, start/initialize, finalize, functions";

        private int _lastRank = int.MinValue;
        private Section _lastSection = Section.None;
        private readonly HashSet<Section> _seen = new();
        private bool _typeHeaderParsed;
        private bool _traitHeaderParsed;
        private bool _moduleHeaderParsed;
        private bool _startHeaderParsed;
        private bool _initializeHeaderParsed;
        private bool _implicitStartSection;
        private bool _hasExplicitSections;
        private int _currentIndentLevel;
        private int? _enumSectionIndent;
        private string? _currentEnumName;
        private List<string> _currentEnumMembers = new();
        private int? _recordSectionIndent;
        private string? _currentRecordName;
        private int? _currentRecordPackSize;
        private List<string> _currentRecordMembers = new();
        private Dictionary<string, string> _currentRecordMemberTypes = new(StringComparer.Ordinal);
        private int? _propertiesSectionIndent;
        private FileDeclarationKind _currentFileKind;
        private Node? _currentSectionNode;
        private int? _functionsSectionIndent;
        private Node? _currentFunctionNode;
        private List<Node> _currentFunctionBody = new();
        private bool _currentFunctionIsDelegate;
        private readonly Stack<List<Node>> _statementTargetStack = new();
        private List<Node>? _pendingBlockTarget;
        private Node? _typeOrTraitNode;
        private static readonly HashSet<string> AssignmentOperators = new(StringComparer.Ordinal)
        {
            "=", "/=", "*=", "%=", "+=", "-=", "<<=", ">>=", "&=", "^=", "|="
        };
        private static readonly HashSet<string> AccessModifiers = new(StringComparer.Ordinal)
        {
            "public", "private", "internal"
        };
        private static readonly HashSet<string> PropertyModifiers = new(StringComparer.Ordinal)
        {
            "public", "private", "internal", "readonly", "readwrite", "constant", "optional"
        };
        private static readonly HashSet<string> ParameterModifiers = new(StringComparer.Ordinal)
        {
            "readonly", "readwrite", "constant"
        };
        private static readonly HashSet<string> NumericCastSuffixes = new(StringComparer.Ordinal)
        {
            "int", "int64", "int32", "int16", "int8",
            "uint", "uint64", "uint32", "uint16", "uint8",
            "flt", "flt64", "flt32",
            "fix", "fix64", "fix32"
        };

        private Section CurrentSection = Section.None;
        private LexerTokens? token = new LexerTokens();
        private Node? rootNode = new Node();
        private Node? currentNode = new Node();
        private Node? previousNode = new Node();
        private List<Node> ast = new List<Node>();
        private int index = 0;

        // Keep a reference to tokens to allow sub-parsers to read ahead
        private List<LexerTokens> _tokens = new();

        public Parser()
        {
            index = 0;
            currentNode = rootNode;
        }

        private LexerTokens? GetNextToken(List<LexerTokens> tokens)
        {
            if (index < tokens.Count)
            {
                return tokens[index++];
            }
            else
            {
                return null;
            }
        }

        public List<Node> Parse(List<LexerTokens> tokens)
        {
            _tokens = tokens;
            // reset parser state for each Parse call
            index = 0;
            _lastRank = int.MinValue;
            _lastSection = Section.None;
            _seen.Clear();
            _typeHeaderParsed = false;
            _traitHeaderParsed = false;
            _moduleHeaderParsed = false;
            _startHeaderParsed = false;
            _initializeHeaderParsed = false;
            _implicitStartSection = false;
            _hasExplicitSections = false;
            _currentIndentLevel = 0;
            _enumSectionIndent = null;
            _currentEnumName = null;
            _currentEnumMembers = new List<string>();
            _recordSectionIndent = null;
            _currentRecordName = null;
            _currentRecordPackSize = null;
            _currentRecordMembers = new List<string>();
            _currentRecordMemberTypes = new Dictionary<string, string>(StringComparer.Ordinal);
            _propertiesSectionIndent = null;
            _currentFileKind = FileDeclarationKind.None;
            _currentSectionNode = null;
            _functionsSectionIndent = null;
            _currentFunctionNode = null;
            _currentFunctionBody = new List<Node>();
            _currentFunctionIsDelegate = false;
            _statementTargetStack.Clear();
            _pendingBlockTarget = null;
            _typeOrTraitNode = null;
            CurrentSection = Section.None;
            ast = new List<Node>();

            while (true)
            {
                token = GetNextToken(_tokens);
                if (token == null)
                {
                    break;
                }

                switch (CurrentSection)
                {
                    case Section.None:
                        ParseFile(token);
                        break;

                    case Section.Use:
                        ParseUsing(token);
                        break;

                    case Section.Module:
                        ParseModule(token);
                        break;

                    case Section.Type:
                        ParseType(token);
                        break;

                    case Section.Trait:
                        ParseTrait(token);
                        break;

                    case Section.Enums:
                        ParseEnums(token);
                        break;

                    case Section.Records:
                        ParseRecords(token);
                        break;

                    case Section.Properties:
                        ParseProperties(token);
                        break;

                    case Section.Start:
                        ParseStart(token);
                        break;

                    case Section.Initialize:
                        ParseInitialize(token);
                        break;

                    case Section.Finalize:
                        ParseFinalize(token);
                        break;

                    case Section.Functions:
                        ParseFunctions(token);
                        break;

                    case Section.Invalid:
                    default:
                        break;
                }
            }

            FinalizeEnum();
            FinalizeRecord();
            FinalizeFunction();

            if (_currentFileKind is FileDeclarationKind.Type or FileDeclarationKind.Trait)
            {
                var typeNode = ast.FirstOrDefault(n => n.Kind == NodeKind.TypeDeclaration &&
                    (n.DeclarationKind == "type" || n.DeclarationKind == "trait"));
                if (typeNode != null)
                {
                    if (typeNode.TypeProperties.Count == 0)
                    {
                        typeNode.TypeProperties.AddRange(ast.Where(n => n.Kind == NodeKind.PropertyDeclaration));
                    }

                    if (typeNode.TypeFunctions.Count == 0)
                    {
                        typeNode.TypeFunctions.AddRange(ast.Where(n => n.Kind == NodeKind.FunctionDeclaration));
                    }
                }
            }

            return ast;
        }

        private bool TrySwitchSection(LexerTokens? tok)
        {
            if (tok is LexerTokens t && (t.Category == TokenCategory.Identifier || t.Category == TokenCategory.Keyword))
            {
                var text = t.TokenText;
                if (_sectionMap.TryGetValue(text, out var next))
                {
                    if (_implicitStartSection)
                    {
                        throw new InvalidOperationException("Sections are not allowed after implicit start statements.");
                    }

                    if ((next == Section.Start && _seen.Contains(Section.Initialize))
                        || (next == Section.Initialize && _seen.Contains(Section.Start)))
                    {
                        throw new InvalidOperationException("Only one of 'start' or 'initialize' sections may appear in a file.");
                    }

                    if ((next == Section.Module || next == Section.Type || next == Section.Trait)
                        && (_seen.Contains(Section.Module) || _seen.Contains(Section.Type) || _seen.Contains(Section.Trait)))
                    {
                        throw new InvalidOperationException("Only one of 'module', 'type', or 'trait' sections may appear in a file.");
                    }

                    // Duplicate check
                    if (_seen.Contains(next))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate section '{DisplayName(next)}'. Remove the extra '{DisplayName(next)}' section.");
                    }

                    // Order check
                    if (!SectionRank.TryGetValue(next, out var rank))
                    {
                        throw new InvalidOperationException(
                            $"Section '{DisplayName(next)}' is not allowed here. Sections must appear in this order (all optional): {ExpectedOrderText}.");
                    }

                    if (rank < _lastRank)
                    {
                        throw new InvalidOperationException(
                            $"Section '{DisplayName(next)}' is out of order after '{DisplayName(_lastSection)}'. " +
                            $"Fix: move '{DisplayName(next)}' to match this order (all optional): {ExpectedOrderText}.");
                    }

                    _lastRank = rank;
                    _lastSection = next;
                    _seen.Add(next);
                    _hasExplicitSections = true;

                    CurrentSection = next;
                    if (_currentFileKind == FileDeclarationKind.None && next is Section.Enums or Section.Records or Section.Properties
                        or Section.Start or Section.Initialize or Section.Finalize or Section.Functions)
                    {
                        _currentFileKind = FileDeclarationKind.Module;
                    }
                    else
                    {
                        _currentFileKind = next switch
                        {
                            Section.Module => FileDeclarationKind.Module,
                            Section.Type => FileDeclarationKind.Type,
                            Section.Trait => FileDeclarationKind.Trait,
                            _ => _currentFileKind
                        };
                    }
                    _currentSectionNode = new Node(next);
                    ast.Add(_currentSectionNode);
                    return true;
                }
            }
            return false;
        }

        private static (string Value, List<string> Modifiers) SplitTrailingModifiers(List<LexerTokens> tokens, HashSet<string> allowedModifiers)
        {
            var modifiers = new List<string>();
            if (tokens.Count == 0)
            {
                return (string.Empty, modifiers);
            }

            var index = tokens.Count - 1;
            while (index >= 0)
            {
                var token = tokens[index];
                if ((token.Category == TokenCategory.Keyword || token.Category == TokenCategory.Identifier)
                    && allowedModifiers.Contains(token.TokenText))
                {
                    modifiers.Insert(0, token.TokenText);
                    index--;
                    continue;
                }

                break;
            }

            var valueTokens = tokens.Take(index + 1).ToList();
            var value = BuildQualifiedName(valueTokens);
            return (value, modifiers);
        }

        private static List<Node.ParameterInfo> ParseParameterList(List<LexerTokens> tokens)
        {
            var parameters = new List<Node.ParameterInfo>();
            if (tokens.Count == 0)
            {
                return parameters;
            }

            var current = new List<LexerTokens>();
            foreach (var token in tokens)
            {
                if (token.Category == TokenCategory.Punctuation && token.TokenText == ",")
                {
                    AddParameterFromTokens(parameters, current);
                    current.Clear();
                    continue;
                }

                current.Add(token);
            }

            AddParameterFromTokens(parameters, current);
            return parameters;
        }

        private static void AddParameterFromTokens(List<Node.ParameterInfo> parameters, List<LexerTokens> tokens)
        {
            if (tokens.Count == 0)
            {
                return;
            }

            var equalsIndex = tokens.FindIndex(t => t.Category == TokenCategory.Operator && t.TokenText == "=");
            var nameAndTypeTokens = equalsIndex >= 0 ? tokens.Take(equalsIndex).ToList() : tokens.ToList();
            var defaultTokens = equalsIndex >= 0 ? tokens.Skip(equalsIndex + 1).ToList() : new List<LexerTokens>();

            if (nameAndTypeTokens.Count == 0)
            {
                return;
            }

            var name = nameAndTypeTokens[0].TokenText;
            var typeTokens = nameAndTypeTokens.Skip(1).ToList();
            var (type, modifiers) = SplitTrailingModifiers(typeTokens, ParameterModifiers);
            var defaultValue = defaultTokens.Count > 0 ? BuildQualifiedName(defaultTokens) : null;

            parameters.Add(new Node.ParameterInfo
            {
                Name = name,
                Type = type,
                DefaultValue = defaultValue
            });

            if (modifiers.Count > 0)
            {
                parameters[^1].Modifiers.AddRange(modifiers);
            }
        }

        private void ParseFunctionDeclaration(LexerTokens firstToken)
        {
            var tokens = ReadTokensUntilEol(firstToken);
            if (tokens.Count == 0)
            {
                return;
            }

            var tokenIndex = 0;
            if (tokens.Count > 0 && tokens[0].Category == TokenCategory.Keyword && AccessModifiers.Contains(tokens[0].TokenText))
            {
                tokenIndex++;
            }

            var openIndex = tokens.FindIndex(t => t.Category == TokenCategory.Delimiter && t.TokenText == "(");
            var closeIndex = tokens.FindIndex(t => t.Category == TokenCategory.Delimiter && t.TokenText == ")");
            if (openIndex < 0 || closeIndex < openIndex)
            {
                throw new InvalidOperationException("Function declarations require a parameter list.");
            }

            var nameTokens = tokens.Skip(tokenIndex).Take(openIndex - tokenIndex).ToList();
            var name = BuildQualifiedName(nameTokens);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Function declarations require a name.");
            }

            var parameterTokens = tokens.Skip(openIndex + 1).Take(closeIndex - openIndex - 1).ToList();
            var parameters = BuildQualifiedName(parameterTokens);
            var parameterList = ParseParameterList(parameterTokens);

            var returnTokens = tokens.Skip(closeIndex + 1).ToList();
            var returnType = returnTokens.Count > 0 ? BuildQualifiedName(returnTokens) : null;

            var isDelegate = returnTokens.Any(t => t.Category == TokenCategory.Keyword && t.TokenText == "delegate")
                || string.Equals(returnType, "delegate", StringComparison.OrdinalIgnoreCase);

            if (isDelegate)
            {
                ast.Add(Node.CreateDelegateDeclaration(name, parameterList));
                _currentFunctionNode = null;
                _currentFunctionBody.Clear();
                _currentFunctionIsDelegate = true;
                return;
            }

            _currentFunctionNode = Node.CreateFunctionDeclaration(name, parameters, returnType, Array.Empty<Node>(), parameterList);
            _currentFunctionBody = new List<Node>();
            _currentFunctionIsDelegate = false;
        }

        private void FinalizeFunction()
        {
            if (_currentFunctionNode == null)
            {
                return;
            }

            _currentFunctionNode.FunctionBody.AddRange(_currentFunctionBody);
            ast.Add(_currentFunctionNode);
            _currentFunctionNode = null;
            _currentFunctionBody = new List<Node>();
        }

        private static string DisplayName(Section s) => s switch
        {
            Section.Start => "start",
            Section.Initialize => "initialize",
            Section.Finalize => "finalize",
            Section.Functions => "functions",
            Section.Use => "use",
            Section.Module => "module",
            Section.Enums => "enums",
            Section.Records => "records",
            Section.Type => "type",
            Section.Trait => "trait",
            Section.Properties => "properties",
            _ => s.ToString()
        };

        private void ParseFile(LexerTokens? token)
        {
            // Look for the first section header
            if (TrySwitchSection(token))
            {
                return;
            }

            if (token == null || IsIgnorable(token.Value))
            {
                return;
            }

            if (_hasExplicitSections)
            {
                throw new InvalidOperationException("Code is not allowed outside of sections.");
            }

            _implicitStartSection = true;
            CurrentSection = Section.Start;
            _currentFileKind = FileDeclarationKind.Module;
            _currentSectionNode = new Node(Section.Start);
            ast.Add(_currentSectionNode);
            ParseStart(token);
        }

        private void ParseUsing(LexerTokens? token)
        {
            if (TrySwitchSection(token))
            {
                return;
            }

            if (_currentFileKind is FileDeclarationKind.Type or FileDeclarationKind.Trait && _typeOrTraitNode == null)
            {
                _typeOrTraitNode = GetTypeOrTraitOwner();
            }

            if (token == null)
            {
                return;
            }

            if (token.Value.Category is TokenCategory.EndOfLine or TokenCategory.Indent or TokenCategory.Dedent
                or TokenCategory.Whitespace or TokenCategory.Comment)
            {
                return;
            }

            ParseUseStatement(token.Value);
        }
        private void ParseType(LexerTokens? token)
        {
            if (TrySwitchSection(token))
            {
                return;
            }

            if (_typeHeaderParsed || token == null)
            {
                return;
            }

            if (IsIgnorable(token.Value))
            {
                return;
            }

            ParseTypeDeclaration(token.Value, "type");
            _typeHeaderParsed = true;
            _currentFileKind = FileDeclarationKind.Type;
            _typeOrTraitNode = ast.LastOrDefault(n => n.Kind == NodeKind.TypeDeclaration && n.DeclarationKind == "type");
        }

        private void ParseTrait(LexerTokens? token)
        {
            if (TrySwitchSection(token))
            {
                return;
            }

            if (_traitHeaderParsed || token == null)
            {
                return;
            }

            if (IsIgnorable(token.Value))
            {
                return;
            }

            ParseSimpleDeclaration(token.Value, "trait");
            _traitHeaderParsed = true;
            _currentFileKind = FileDeclarationKind.Trait;
            _typeOrTraitNode = ast.LastOrDefault(n => n.Kind == NodeKind.TypeDeclaration && n.DeclarationKind == "trait");
        }

        private void ParseModule(LexerTokens? token)
        {
            if (TrySwitchSection(token))
            {
                return;
            }

            if (_moduleHeaderParsed || token == null)
            {
                return;
            }

            if (IsIgnorable(token.Value))
            {
                return;
            }

            ParseSimpleDeclaration(token.Value, "module");
            _moduleHeaderParsed = true;
            _currentFileKind = FileDeclarationKind.Module;
        }
        private void ParseEnums(LexerTokens? token)
        {
            if (TrySwitchSection(token))
            {
                FinalizeEnum();
                _enumSectionIndent = null;
                return;
            }

            if (token == null)
            {
                return;
            }

            if (UpdateIndentation(token.Value))
            {
                return;
            }

            if (IsIgnorable(token.Value))
            {
                return;
            }

            if (_enumSectionIndent == null)
            {
                _enumSectionIndent = _currentIndentLevel;
            }

            if (_currentIndentLevel == _enumSectionIndent)
            {
                FinalizeEnum();
                var headerTokens = ReadTokensUntilEol(token.Value);
                var name = BuildQualifiedName(headerTokens);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _currentEnumName = name;
                    _currentEnumMembers = new List<string>();
                }
                return;
            }

            if (_currentIndentLevel > _enumSectionIndent)
            {
                if (_currentEnumName == null)
                {
                    throw new InvalidOperationException("Enum member declared without an enum name.");
                }

                var memberTokens = ReadTokensUntilEol(token.Value);
                var memberName = BuildQualifiedName(memberTokens);
                if (!string.IsNullOrWhiteSpace(memberName))
                {
                    _currentEnumMembers.Add(memberName);
                }
                return;
            }

            FinalizeEnum();
        }

        private void ParseRecords(LexerTokens? token)
        {
            if (TrySwitchSection(token))
            {
                FinalizeRecord();
                _recordSectionIndent = null;
                return;
            }

            if (token == null)
            {
                return;
            }

            if (UpdateIndentation(token.Value))
            {
                return;
            }

            if (IsIgnorable(token.Value))
            {
                return;
            }

            if (_recordSectionIndent == null)
            {
                _recordSectionIndent = _currentIndentLevel;
            }

            if (_currentIndentLevel == _recordSectionIndent)
            {
                FinalizeRecord();
                var headerTokens = ReadTokensUntilEol(token.Value);
                var packIndex = headerTokens.FindIndex(t => t.Category == TokenCategory.Keyword && t.TokenText == "pack");
                var nameTokens = packIndex >= 0 ? headerTokens.Take(packIndex).ToList() : headerTokens;
                var name = BuildQualifiedName(nameTokens);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _currentRecordName = name;
                    _currentRecordMembers = new List<string>();
                    _currentRecordMemberTypes = new Dictionary<string, string>(StringComparer.Ordinal);
                    _currentRecordPackSize = null;

                    if (packIndex >= 0 && packIndex + 1 < headerTokens.Count)
                    {
                        if (int.TryParse(headerTokens[packIndex + 1].TokenText, out var packSize))
                        {
                            _currentRecordPackSize = packSize;
                        }
                    }
                }
                return;
            }

            if (_currentIndentLevel > _recordSectionIndent)
            {
                if (_currentRecordName == null)
                {
                    throw new InvalidOperationException("Record member declared without a record name.");
                }

                var memberTokens = ReadTokensUntilEol(token.Value);
                if (memberTokens.Count == 0)
                {
                    return;
                }

                var equalsIndex = memberTokens.FindIndex(t => t.Category == TokenCategory.Operator && t.TokenText == "=");
                string memberName;
                if (equalsIndex >= 0)
                {
                    var left = BuildQualifiedName(memberTokens.Take(equalsIndex));
                    var rightTokens = memberTokens.Skip(equalsIndex + 1).ToList();
                    var right = NormalizeAssignedValueTokens(rightTokens);
                    if (TryExtractNumericLiteralWithSuffix(rightTokens, out _, out var memberType)
                        && !string.IsNullOrWhiteSpace(left))
                    {
                        _currentRecordMemberTypes[left] = memberType;
                    }
                    memberName = $"{left}={right}";
                }
                else
                {
                    memberName = BuildQualifiedName(memberTokens);
                }

                if (!string.IsNullOrWhiteSpace(memberName))
                {
                    _currentRecordMembers.Add(memberName);
                }
                return;
            }

            FinalizeRecord();
        }
        private void ParseProperties(LexerTokens? token)
        {
            if (TrySwitchSection(token))
            {
                _propertiesSectionIndent = null;
                return;
            }

            if (token == null)
            {
                return;
            }

            if (UpdateIndentation(token.Value))
            {
                return;
            }

            if (IsIgnorable(token.Value))
            {
                return;
            }

            if (_propertiesSectionIndent == null)
            {
                _propertiesSectionIndent = _currentIndentLevel;
            }

            if (_currentIndentLevel >= _propertiesSectionIndent)
            {
                var propertyNode = ParsePropertyDeclaration(token.Value);
                if (propertyNode != null && _currentFileKind is FileDeclarationKind.Type or FileDeclarationKind.Trait)
                {
                    var owner = _typeOrTraitNode ?? GetTypeOrTraitOwner();
                    owner?.TypeProperties.Add(propertyNode);
                    return;
                }
            }
        }

        private void ParseStart(LexerTokens? token)
        {
            if (_currentFileKind != FileDeclarationKind.Module)
            {
                throw new InvalidOperationException("The start section is only allowed in module files.");
            }

            // First, check if the token starts a new section
            if (TrySwitchSection(token))
                return;

            if (!_startHeaderParsed && TryParseSectionParameters(token, out var parameters))
            {
                _currentSectionNode!.SectionParameters = parameters;
                _startHeaderParsed = true;
                return;
            }

            // Recognize built-in WriteLine in start section: WriteLine("...") [EOL]
            if (token is LexerTokens t && t.Category == TokenCategory.Identifier && t.TokenText == "WriteLine")
            {
                ParseWriteLineCall();
                return;
            }

            if (token != null && UpdateIndentation(token.Value))
            {
                return;
            }

            if (token != null && !IsIgnorable(token.Value))
            {
                ParseStatement(token.Value, GetStatementTarget(ast));
            }
        }

        private void ParseInitialize(LexerTokens? token)
        {
            if (_currentFileKind == FileDeclarationKind.None)
            {
                throw new InvalidOperationException("The initialize section is only allowed in module, type, or trait files.");
            }

            // For now, just watch for next section
            if (TrySwitchSection(token))
            {
                return;
            }

            if (!_initializeHeaderParsed && TryParseSectionParameters(token, out var parameters))
            {
                _currentSectionNode!.SectionParameters = parameters;
                _initializeHeaderParsed = true;
                return;
            }

            if (token != null && UpdateIndentation(token.Value))
            {
                return;
            }

            if (token != null && !IsIgnorable(token.Value))
            {
                ParseStatement(token.Value, GetStatementTarget(ast));
            }
        }

        private void ParseFinalize(LexerTokens? token)
        {
            if (TrySwitchSection(token))
            {
                return;
            }

            if (token != null && UpdateIndentation(token.Value))
            {
                return;
            }

            if (token != null && !IsIgnorable(token.Value))
            {
                ParseStatement(token.Value, GetStatementTarget(ast));
            }
        }
        private void ParseFunctions(LexerTokens? token)
        {
            if (TrySwitchSection(token))
            {
                FinalizeFunction();
                _functionsSectionIndent = null;
                return;
            }

            if (token == null)
            {
                return;
            }

            if (_currentFileKind is FileDeclarationKind.Type or FileDeclarationKind.Trait && _typeOrTraitNode == null)
            {
                _typeOrTraitNode = ast.LastOrDefault(n => n.Kind == NodeKind.TypeDeclaration &&
                    (n.DeclarationKind == "type" || n.DeclarationKind == "trait"));
            }

            if (UpdateIndentation(token.Value))
            {
                if (_functionsSectionIndent != null && _currentIndentLevel <= _functionsSectionIndent)
                {
                    FinalizeFunction();
                }
                return;
            }

            if (IsIgnorable(token.Value))
            {
                return;
            }

            if (_functionsSectionIndent == null)
            {
                _functionsSectionIndent = _currentIndentLevel;
            }

            if (_currentIndentLevel == _functionsSectionIndent)
            {
                FinalizeFunction();
                ParseFunctionDeclaration(token.Value);
                if (_currentFunctionIsDelegate)
                {
                    _currentFunctionIsDelegate = false;
                }
                return;
            }

            if (_currentIndentLevel > _functionsSectionIndent && _currentFunctionNode != null)
            {
                ParseStatement(token.Value, GetStatementTarget(_currentFunctionBody));
            }
        }

        private void ParseWriteLineCall()
        {
            // Expect '(' StringLiteral ')'
            var open = GetNextToken(_tokens);
            if (open == null || open.Value.Category != TokenCategory.Delimiter || open.Value.TokenText != "(")
            {
                throw new InvalidOperationException("Expected '(' after WriteLine.");
            }

            var textTok = GetNextToken(_tokens);
            if (textTok == null || textTok.Value.Category != TokenCategory.StringLiteral)
            {
                throw new InvalidOperationException("Expected string literal in WriteLine(...)");
            }
            var literal = textTok.Value.TokenText; // keep quotes

            var close = GetNextToken(_tokens);
            if (close == null || close.Value.Category != TokenCategory.Delimiter || close.Value.TokenText != ")")
            {
                throw new InvalidOperationException("Expected ')' after WriteLine argument.");
            }

            ast.Add(Node.CreateWriteLine(literal));
        }

        private static bool IsIgnorable(LexerTokens token)
        {
            return token.Category is TokenCategory.EndOfLine or TokenCategory.Indent or TokenCategory.Dedent
                or TokenCategory.Whitespace or TokenCategory.Comment;
        }

        private void ParseSimpleDeclaration(LexerTokens firstToken, string declarationKind)
        {
            var tokens = ReadTokensUntilEol(firstToken);
            if (tokens.Count == 0)
            {
                return;
            }

            if (tokens.Any(t => t.Category == TokenCategory.Keyword && (t.TokenText == "is" || t.TokenText == "has")))
            {
                throw new InvalidOperationException($"Unexpected inheritance in {declarationKind} declaration.");
            }

            var name = BuildQualifiedName(tokens);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException($"Missing {declarationKind} name.");
            }

            ast.Add(Node.CreateTypeDeclaration(declarationKind, name, null));
        }

        private void ParseTypeDeclaration(LexerTokens firstToken, string declarationKind)
        {
            var tokens = ReadTokensUntilEol(firstToken);
            if (tokens.Count == 0)
            {
                return;
            }

            var isIndex = tokens.FindIndex(t => t.Category == TokenCategory.Keyword && t.TokenText == "is");
            if (isIndex < 0)
            {
                throw new InvalidOperationException("Type declarations must include an 'is' base type.");
            }

            var nameTokens = tokens.Take(isIndex).ToList();
            if (nameTokens.Count == 0)
            {
                throw new InvalidOperationException("Missing type name.");
            }

            var hasIndex = tokens.FindIndex(t => t.Category == TokenCategory.Keyword && t.TokenText == "has");
            var baseTokens = hasIndex >= 0
                ? tokens.Skip(isIndex + 1).Take(hasIndex - isIndex - 1).ToList()
                : tokens.Skip(isIndex + 1).ToList();

            if (baseTokens.Count == 0)
            {
                throw new InvalidOperationException("Missing base type after 'is'.");
            }

            var name = BuildQualifiedName(nameTokens);
            var baseType = BuildQualifiedName(baseTokens);

            var traits = new List<string>();
            if (hasIndex >= 0)
            {
                var traitTokens = tokens.Skip(hasIndex + 1).ToList();
                var current = new List<LexerTokens>();

                foreach (var token in traitTokens)
                {
                    if (token.Category == TokenCategory.Punctuation && token.TokenText == ",")
                    {
                        var traitName = BuildQualifiedName(current);
                        if (!string.IsNullOrWhiteSpace(traitName))
                        {
                            traits.Add(traitName);
                        }
                        current.Clear();
                        continue;
                    }

                    current.Add(token);
                }

                var lastTrait = BuildQualifiedName(current);
                if (!string.IsNullOrWhiteSpace(lastTrait))
                {
                    traits.Add(lastTrait);
                }

                if (traits.Count == 0)
                {
                    throw new InvalidOperationException("Missing trait list after 'has'.");
                }
            }

            ast.Add(Node.CreateTypeDeclaration(declarationKind, name, baseType, traits));
        }

        private List<LexerTokens> ReadTokensUntilEol(LexerTokens firstToken)
        {
            var parts = new List<LexerTokens>();

            if (!IsIgnorable(firstToken))
            {
                parts.Add(firstToken);
            }

            while (true)
            {
                var next = GetNextToken(_tokens);
                if (next == null)
                {
                    break;
                }

                if (next.Value.Category == TokenCategory.EndOfLine)
                {
                    break;
                }

                if (IsIgnorable(next.Value))
                {
                    continue;
                }

                parts.Add(next.Value);
            }

            return parts;
        }

        private static string BuildQualifiedName(IEnumerable<LexerTokens> tokens)
        {
            return string.Concat(tokens.Select(t => t.TokenText));
        }

        private static string NormalizeAssignedValueTokens(List<LexerTokens> tokens)
        {
            if (tokens.Count == 0)
            {
                return string.Empty;
            }

            if (TryExtractNumericLiteralWithSuffix(tokens, out var literal, out _))
            {
                return literal;
            }

            return BuildQualifiedName(tokens);
        }

        private static bool TryExtractNumericLiteralWithSuffix(List<LexerTokens> tokens, out string literal, out string suffix)
        {
            literal = string.Empty;
            suffix = string.Empty;

            if (tokens.Count != 2)
            {
                return false;
            }

            if (tokens[0].Category != TokenCategory.NumericLiteral)
            {
                return false;
            }

            if (tokens[1].Category is not (TokenCategory.Keyword or TokenCategory.Identifier))
            {
                return false;
            }

            if (!NumericCastSuffixes.Contains(tokens[1].TokenText))
            {
                return false;
            }

            literal = tokens[0].TokenText;
            suffix = tokens[1].TokenText;
            return true;
        }

        private void ParseUseStatement(LexerTokens firstToken)
        {
            var parts = new List<LexerTokens> { firstToken };

            while (true)
            {
                var next = GetNextToken(_tokens);
                if (next == null)
                {
                    break;
                }

                if (next.Value.Category == TokenCategory.EndOfLine)
                {
                    break;
                }

                if (next.Value.Category is TokenCategory.Indent or TokenCategory.Dedent
                    or TokenCategory.Whitespace or TokenCategory.Comment)
                {
                    continue;
                }

                parts.Add(next.Value);
            }

            if (parts.Count == 0)
            {
                return;
            }

            var aliasIndex = parts.FindIndex(t => t.Category == TokenCategory.Keyword && t.TokenText == "as");
            string? alias = null;
            if (aliasIndex >= 0)
            {
                if (aliasIndex + 1 >= parts.Count)
                {
                    throw new InvalidOperationException("Expected alias identifier after 'as' in use statement.");
                }

                alias = parts[aliasIndex + 1].TokenText;
                parts = parts.Take(aliasIndex).ToList();
            }

            var target = string.Concat(parts.Select(p => p.TokenText));
            var isFilePath = parts.Any(p => p.Category == TokenCategory.Operator && p.TokenText == "/")
                || LooksLikeFilePath(parts);

            if (isFilePath && alias != null)
            {
                throw new InvalidOperationException("File path use statements cannot specify an alias.");
            }

            ast.Add(Node.CreateUseStatement(target, alias, isFilePath));
        }

        private bool UpdateIndentation(LexerTokens token)
        {
            if (token.Category == TokenCategory.Indent || token.Category == TokenCategory.Dedent)
            {
                if (int.TryParse(token.TokenText, out var indent))
                {
                    _currentIndentLevel = indent;
                }

                if (token.Category == TokenCategory.Indent && _pendingBlockTarget != null)
                {
                    _statementTargetStack.Push(_pendingBlockTarget);
                    _pendingBlockTarget = null;
                }

                if (token.Category == TokenCategory.Dedent && _statementTargetStack.Count > 0)
                {
                    _statementTargetStack.Pop();
                }

                return true;
            }

            return false;
        }

        private List<Node> GetStatementTarget(List<Node> fallback)
        {
            return _statementTargetStack.Count > 0 ? _statementTargetStack.Peek() : fallback;
        }

        private void SetPendingBlockTarget(List<Node> target, int startCount)
        {
            if (target.Count <= startCount)
            {
                return;
            }

            var node = target[startCount];
            if (!SupportsStatementBlock(node.Kind))
            {
                return;
            }

            _pendingBlockTarget = node.StatementBody;
        }

        private static bool SupportsStatementBlock(NodeKind kind)
        {
            return kind is NodeKind.IfStatement or NodeKind.MatchStatement or NodeKind.WhenStatement
                or NodeKind.WhileStatement or NodeKind.ForStatement or NodeKind.ForAllStatement
                or NodeKind.RepeatStatement or NodeKind.HasStatement or NodeKind.HasTraitStatement
                or NodeKind.ErrorStatement or NodeKind.CatchStatement or NodeKind.ElseStatement;
        }

        private static ExpressionNode? ParseExpression(List<LexerTokens> tokens)
        {
            if (tokens.Count == 0)
            {
                return null;
            }

            var parser = new ExpressionParser(tokens);
            var expression = parser.ParseExpression();
            if (parser.HasRemainingTokens())
            {
                throw new InvalidOperationException("Unable to parse full expression.");
            }

            return expression;
        }

        private sealed class ExpressionParser
        {
            private readonly List<LexerTokens> _tokens;
            private int _index;

            public ExpressionParser(List<LexerTokens> tokens)
            {
                _tokens = tokens;
            }

            public ExpressionNode? ParseExpression() => ParseMultiExpression();

            private ExpressionNode? ParseMultiExpression()
            {
                var left = ParseConditional();
                while (MatchPunctuation(","))
                {
                    var right = ParseConditional();
                    left = new ExpressionNode { Kind = ExpressionKind.Binary, Value = ",", Left = left, Right = right };
                }

                return left;
            }

            private ExpressionNode? ParseConditional()
            {
                var expression = ParseOr();
                while (MatchKeyword("if"))
                {
                    var condition = ParseOr();
                    if (!MatchKeyword("else"))
                    {
                        throw new InvalidOperationException("Conditional expressions require an 'else' branch.");
                    }

                    var whenFalse = ParseOr();
                    var conditional = new ExpressionNode
                    {
                        Kind = ExpressionKind.Conditional,
                        Left = condition,
                        Right = expression
                    };

                    if (whenFalse != null)
                    {
                        conditional.Arguments.Add(whenFalse);
                    }

                    expression = conditional;
                }

                return expression;
            }

            public bool HasRemainingTokens() => _index < _tokens.Count;

            private ExpressionNode? ParseOr()
            {
                var left = ParseAnd();
                while (MatchKeyword("or"))
                {
                    var op = _tokens[_index - 1].TokenText;
                    var right = ParseAnd();
                    left = new ExpressionNode { Kind = ExpressionKind.Binary, Value = op, Left = left, Right = right };
                }
                return left;
            }

            private ExpressionNode? ParseAnd()
            {
                var left = ParseEquality();
                while (MatchKeyword("and"))
                {
                    var op = _tokens[_index - 1].TokenText;
                    var right = ParseEquality();
                    left = new ExpressionNode { Kind = ExpressionKind.Binary, Value = op, Left = left, Right = right };
                }
                return left;
            }

            private ExpressionNode? ParseEquality()
            {
                var left = ParseComparison();
                var matched = false;
                while (MatchOperator("==") || MatchOperator("!="))
                {
                    if (matched)
                    {
                        throw new InvalidOperationException("Only one consecutive equality expression is allowed.");
                    }

                    matched = true;
                    var op = _tokens[_index - 1].TokenText;
                    var right = ParseComparison();
                    left = new ExpressionNode { Kind = ExpressionKind.Binary, Value = op, Left = left, Right = right };
                }
                return left;
            }

            private ExpressionNode? ParseComparison()
            {
                var left = ParseBitwiseOr();
                var matched = false;
                while (MatchOperator("<") || MatchOperator(">") || MatchOperator("<=") || MatchOperator(">="))
                {
                    if (matched)
                    {
                        throw new InvalidOperationException("Only one consecutive relational expression is allowed.");
                    }

                    matched = true;
                    var op = _tokens[_index - 1].TokenText;
                    var right = ParseBitwiseOr();
                    left = new ExpressionNode { Kind = ExpressionKind.Binary, Value = op, Left = left, Right = right };
                }
                return left;
            }

            private ExpressionNode? ParseBitwiseOr()
            {
                var left = ParseBitwiseXor();
                while (MatchOperator("|"))
                {
                    var op = _tokens[_index - 1].TokenText;
                    var right = ParseBitwiseXor();
                    left = new ExpressionNode { Kind = ExpressionKind.Binary, Value = op, Left = left, Right = right };
                }
                return left;
            }

            private ExpressionNode? ParseBitwiseXor()
            {
                var left = ParseBitwiseAnd();
                while (MatchOperator("^"))
                {
                    var op = _tokens[_index - 1].TokenText;
                    var right = ParseBitwiseAnd();
                    left = new ExpressionNode { Kind = ExpressionKind.Binary, Value = op, Left = left, Right = right };
                }
                return left;
            }

            private ExpressionNode? ParseBitwiseAnd()
            {
                var left = ParseShift();
                while (MatchOperator("&"))
                {
                    var op = _tokens[_index - 1].TokenText;
                    var right = ParseShift();
                    left = new ExpressionNode { Kind = ExpressionKind.Binary, Value = op, Left = left, Right = right };
                }
                return left;
            }

            private ExpressionNode? ParseShift()
            {
                var left = ParseTerm();
                while (MatchOperator("<<") || MatchOperator(">>"))
                {
                    var op = _tokens[_index - 1].TokenText;
                    var right = ParseTerm();
                    left = new ExpressionNode { Kind = ExpressionKind.Binary, Value = op, Left = left, Right = right };
                }
                return left;
            }

            private ExpressionNode? ParseTerm()
            {
                var left = ParseFactor();
                while (MatchOperator("+") || MatchOperator("-"))
                {
                    var op = _tokens[_index - 1].TokenText;
                    var right = ParseFactor();
                    left = new ExpressionNode { Kind = ExpressionKind.Binary, Value = op, Left = left, Right = right };
                }
                return left;
            }

            private ExpressionNode? ParseFactor()
            {
                var left = ParsePairRange();
                while (MatchOperator("*") || MatchOperator("/") || MatchOperator("%"))
                {
                    var op = _tokens[_index - 1].TokenText;
                    var right = ParsePairRange();
                    left = new ExpressionNode { Kind = ExpressionKind.Binary, Value = op, Left = left, Right = right };
                }
                return left;
            }

            private ExpressionNode? ParsePairRange()
            {
                var left = ParseUnary();
                var seenPairOrRange = false;
                while (MatchPunctuation(":") || MatchOperator(".."))
                {
                    if (seenPairOrRange)
                    {
                        throw new InvalidOperationException("Only one consecutive pair or range expression is allowed.");
                    }

                    seenPairOrRange = true;
                    var op = _tokens[_index - 1].TokenText;
                    var right = ParseUnary();
                    left = new ExpressionNode { Kind = ExpressionKind.Binary, Value = op, Left = left, Right = right };
                }

                return left;
            }

            private ExpressionNode? ParseUnary()
            {
                var castStart = _index;
                if (MatchDelimiter("(") && MatchIdentifier(out var castType) && MatchDelimiter(")"))
                {
                    var castOperand = ParseUnary();
                    if (castOperand != null)
                    {
                        return new ExpressionNode { Kind = ExpressionKind.Cast, Value = castType, Left = castOperand };
                    }
                }
                _index = castStart;

                if (MatchOperator("++") || MatchOperator("--") || MatchOperator("-") || MatchOperator("+") || MatchOperator("!") || MatchOperator("~") || MatchKeyword("not"))
                {
                    var op = _tokens[_index - 1].TokenText;
                    var operand = ParseUnary();
                    if (operand?.Kind == ExpressionKind.Unary)
                    {
                        throw new InvalidOperationException("Unary operators cannot be repeated consecutively.");
                    }
                    return new ExpressionNode { Kind = ExpressionKind.Unary, Value = op, Left = operand };
                }

                return ParsePostfix();
            }

            private ExpressionNode? ParsePostfix()
            {
                var expr = ParsePrimary();
                while (true)
                {
                    if (MatchPunctuation("."))
                    {
                        if (MatchIdentifier(out var member))
                        {
                            expr = new ExpressionNode { Kind = ExpressionKind.MemberAccess, Value = member, Left = expr };
                            continue;
                        }
                    }

                    if (MatchDelimiter("["))
                    {
                        var indexExpr = ParseExpression();
                        MatchDelimiter("]");
                        expr = new ExpressionNode { Kind = ExpressionKind.Index, Left = expr, Right = indexExpr };
                        continue;
                    }

                    if (MatchDelimiter("("))
                    {
                        var call = new ExpressionNode { Kind = ExpressionKind.Call, Left = expr };
                        if (!MatchDelimiter(")"))
                        {
                            do
                            {
                                var arg = ParseConditional();
                                if (arg != null)
                                {
                                    call.Arguments.Add(arg);
                                }
                            } while (MatchPunctuation(","));

                            MatchDelimiter(")");
                        }

                        expr = call;
                        continue;
                    }

                    break;
                }

                return expr;
            }

            private ExpressionNode? ParsePrimary()
            {
                if (MatchDelimiter("("))
                {
                    var expr = ParseExpression();
                    MatchDelimiter(")");
                    return expr;
                }

                if (_index >= _tokens.Count)
                {
                    return null;
                }

                var token = _tokens[_index++];
                if (token.Category is TokenCategory.Identifier or TokenCategory.Keyword)
                {
                    return new ExpressionNode { Kind = ExpressionKind.Identifier, Value = token.TokenText };
                }

                if (token.Category is TokenCategory.StringLiteral or TokenCategory.NumericLiteral or TokenCategory.CharLiteral)
                {
                    if (token.Category == TokenCategory.NumericLiteral
                        && _index < _tokens.Count
                        && (_tokens[_index].Category is TokenCategory.Identifier or TokenCategory.Keyword)
                        && IsNumericTypeSuffix(_tokens[_index].TokenText))
                    {
                        _index++;
                    }

                    return new ExpressionNode { Kind = ExpressionKind.Literal, Value = token.TokenText };
                }

                return new ExpressionNode { Kind = ExpressionKind.Literal, Value = token.TokenText };
            }

            private bool MatchOperator(string op)
            {
                if (_index < _tokens.Count && _tokens[_index].Category == TokenCategory.Operator && _tokens[_index].TokenText == op)
                {
                    _index++;
                    return true;
                }

                return false;
            }

            private bool MatchKeyword(string keyword)
            {
                if (_index < _tokens.Count && _tokens[_index].Category == TokenCategory.Keyword && _tokens[_index].TokenText == keyword)
                {
                    _index++;
                    return true;
                }

                return false;
            }

            private bool MatchPunctuation(string punctuation)
            {
                if (_index < _tokens.Count && _tokens[_index].Category == TokenCategory.Punctuation && _tokens[_index].TokenText == punctuation)
                {
                    _index++;
                    return true;
                }

                return false;
            }

            private bool MatchDelimiter(string delimiter)
            {
                if (_index < _tokens.Count && _tokens[_index].Category == TokenCategory.Delimiter && _tokens[_index].TokenText == delimiter)
                {
                    _index++;
                    return true;
                }

                return false;
            }

            private bool MatchIdentifier(out string value)
            {
                if (_index < _tokens.Count && (_tokens[_index].Category is TokenCategory.Identifier or TokenCategory.Keyword))
                {
                    value = _tokens[_index].TokenText;
                    _index++;
                    return true;
                }

                value = string.Empty;
                return false;
            }

            private static bool IsNumericTypeSuffix(string tokenText)
            {
                return tokenText is "int" or "int64" or "int32" or "int16" or "int8"
                    or "uint" or "uint64" or "uint32" or "uint16" or "uint8"
                    or "fix" or "fix64" or "fix32"
                    or "flt" or "flt64" or "flt32";
            }
        }

        private void FinalizeEnum()
        {
            if (_currentEnumName == null)
            {
                return;
            }

            ast.Add(Node.CreateEnumDeclaration(_currentEnumName, _currentEnumMembers));
            _currentEnumName = null;
            _currentEnumMembers = new List<string>();
        }

        private void FinalizeRecord()
        {
            if (_currentRecordName == null)
            {
                return;
            }

            var node = Node.CreateRecordDeclaration(_currentRecordName, _currentRecordPackSize, _currentRecordMembers);
            foreach (var pair in _currentRecordMemberTypes)
            {
                node.RecordMemberTypes[pair.Key] = pair.Value;
            }

            ast.Add(node);
            _currentRecordName = null;
            _currentRecordPackSize = null;
            _currentRecordMembers = new List<string>();
            _currentRecordMemberTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private Node? ParsePropertyDeclaration(LexerTokens firstToken)
        {
            var tokens = ReadTokensUntilEol(firstToken);
            if (tokens.Count == 0)
            {
                return null;
            }

            var equalsIndex = tokens.FindIndex(t => t.Category == TokenCategory.Operator && t.TokenText == "=");
            if (equalsIndex >= 0)
            {
                var nameTokens = tokens.Take(equalsIndex).ToList();
                var valueTokens = tokens.Skip(equalsIndex + 1).ToList();
                var name = BuildQualifiedName(nameTokens);

                var modifiers = new List<string>();
                var valueEnd = valueTokens.Count - 1;
                while (valueEnd >= 0)
                {
                    var trailing = valueTokens[valueEnd];
                    if ((trailing.Category == TokenCategory.Keyword || trailing.Category == TokenCategory.Identifier)
                        && PropertyModifiers.Contains(trailing.TokenText))
                    {
                        modifiers.Insert(0, trailing.TokenText);
                        valueEnd--;
                        continue;
                    }

                    break;
                }

                var coreValueTokens = valueEnd >= 0 ? valueTokens.Take(valueEnd + 1).ToList() : new List<LexerTokens>();
                string? propertyType = null;
                if (TryExtractNumericLiteralWithSuffix(coreValueTokens, out _, out var suffixType))
                {
                    propertyType = suffixType;
                }
                var value = NormalizeAssignedValueTokens(coreValueTokens);

                if (!string.IsNullOrWhiteSpace(name))
                {
                    var node = Node.CreatePropertyDeclaration(name, value, propertyType, modifiers);
                    ast.Add(node);
                    return node;
                }

                return null;
            }
            var fallbackName = BuildQualifiedName(tokens);
            if (!string.IsNullOrWhiteSpace(fallbackName))
            {
                throw new InvalidOperationException("Property declarations must use assignment (name = value).");
            }

            return null;
        }

        private Node? GetTypeOrTraitOwner()
        {
            return ast.FirstOrDefault(n => n.Kind == NodeKind.TypeDeclaration &&
                (n.DeclarationKind == "type" || n.DeclarationKind == "trait"));
        }

        private void ParseStatement(LexerTokens firstToken) => ParseStatement(firstToken, ast);

        private void ParseStatement(LexerTokens firstToken, List<Node> target)
        {
            var startCount = target.Count;
            var tokens = ReadTokensUntilEol(firstToken);
            while (tokens.Count > 0
                && (tokens[^1].Category == TokenCategory.Punctuation
                    || tokens[^1].Category == TokenCategory.Delimiter
                    || tokens[^1].Category == TokenCategory.Unknown)
                && tokens[^1].TokenText == ";")
            {
                tokens.RemoveAt(tokens.Count - 1);
            }
            if (tokens.Count == 0)
            {
                return;
            }

            if (TryParseMatchStatement(tokens, target))
            {
                SetPendingBlockTarget(target, startCount);
                return;
            }

            if (TryParseWhenStatement(tokens, target))
            {
                SetPendingBlockTarget(target, startCount);
                return;
            }

            if (TryParseIfStatement(tokens, target))
            {
                SetPendingBlockTarget(target, startCount);
                return;
            }

            if (TryParseElseStatement(tokens, target))
            {
                SetPendingBlockTarget(target, startCount);
                AttachElseBody(target, startCount);
                return;
            }

            if (TryParseWhileStatement(tokens, target))
            {
                SetPendingBlockTarget(target, startCount);
                return;
            }

            if (TryParseForStatement(tokens, target))
            {
                SetPendingBlockTarget(target, startCount);
                return;
            }

            if (TryParseRepeatStatement(tokens, target))
            {
                SetPendingBlockTarget(target, startCount);
                return;
            }

            if (TryParseHasStatement(tokens, target))
            {
                SetPendingBlockTarget(target, startCount);
                return;
            }

            if (TryParseHasTraitStatement(tokens, target))
            {
                SetPendingBlockTarget(target, startCount);
                return;
            }

            if (TryParseAssignmentStatement(tokens, target))
            {
                return;
            }

            if (TryParseIncrementDecrementStatement(tokens, target))
            {
                return;
            }

            if (TryParseReturnStatement(tokens, target))
            {
                return;
            }

            if (TryParseYieldStatement(tokens, target))
            {
                return;
            }

            if (TryParseBreakContinueStatement(tokens, target))
            {
                return;
            }

            if (TryParseErrorCatchStatement(tokens, target))
            {
                SetPendingBlockTarget(target, startCount);
                return;
            }

            TryParseFunctionCall(tokens, target);
        }

        private bool TryParseAssignmentStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count == 0)
            {
                return false;
            }
            var assignmentIndex = tokens.FindIndex(t => t.Category == TokenCategory.Operator && AssignmentOperators.Contains(t.TokenText));
            if (assignmentIndex < 0)
            {
                return false;
            }

            var leftTokens = tokens.Take(assignmentIndex).ToList();
            var rightTokens = tokens.Skip(assignmentIndex + 1).ToList();
            var assignmentOperator = tokens[assignmentIndex].TokenText;
            var left = BuildQualifiedName(leftTokens);
            var right = BuildQualifiedName(rightTokens);

            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                throw new InvalidOperationException("Assignment statements require left and right expressions.");
            }

            var node = Node.CreateAssignmentStatement(left, right, assignmentOperator);
            node.AssignmentLeftExpression = ParseExpression(leftTokens);
            node.AssignmentRightExpression = ParseExpression(rightTokens);
            target.Add(node);
            return true;
        }

        private bool TryParseIncrementDecrementStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count < 2)
            {
                return false;
            }

            var last = tokens[^1];
            if (last.Category != TokenCategory.Operator || (last.TokenText != "++" && last.TokenText != "--"))
            {
                return false;
            }

            var leftTokens = tokens.Take(tokens.Count - 1).ToList();
            var left = BuildQualifiedName(leftTokens);
            if (string.IsNullOrWhiteSpace(left))
            {
                return false;
            }

            var assignmentOperator = last.TokenText == "++" ? "+=" : "-=";
            var node = Node.CreateAssignmentStatement(left, "1", assignmentOperator);
            node.AssignmentLeftExpression = ParseExpression(leftTokens);
            node.AssignmentRightExpression = new ExpressionNode { Kind = ExpressionKind.Literal, Value = "1" };
            node.IsLoweredPostfixMutation = true;
            target.Add(node);
            return true;
        }

        private void AttachBlockIfPresent(List<Node> target, int startCount)
        {
            if (target.Count <= startCount)
            {
                return;
            }

            var node = target[startCount];
            if (!SupportsStatementBlock(node.Kind))
            {
                return;
            }

            if (!TryConsumeIndent(out _))
            {
                if (index > 0 && _tokens[index - 1].Category == TokenCategory.Indent)
                {
                    ParseIndentedBlock(node.StatementBody);
                    return;
                }

                if (node.Kind == NodeKind.ElseStatement)
                {
                    var next = GetNextToken(_tokens);
                    if (next != null && !IsIgnorable(next.Value))
                    {
                        ParseStatement(next.Value, node.StatementBody);
                    }
                }
                return;
            }

            // ParseIndentedBlock(node.StatementBody);
        }

        private void ParseIndentedBlock(List<Node> target)
        {
            var depth = 1;
            while (true)
            {
                var next = GetNextToken(_tokens);
                if (next == null)
                {
                    break;
                }

                if (next.Value.Category == TokenCategory.Indent)
                {
                    depth++;
                    continue;
                }

                if (next.Value.Category == TokenCategory.Dedent)
                {
                    depth--;
                    if (depth <= 0)
                    {
                        break;
                    }
                    continue;
                }

                if (IsIgnorable(next.Value))
                {
                    continue;
                }

                ParseStatement(next.Value, target);
            }
        }

        private bool TryConsumeIndent(out LexerTokens token)
        {
            token = default;
            if (index >= _tokens.Count)
            {
                return false;
            }

            while (index < _tokens.Count)
            {
                var ignorable = _tokens[index];
                if (ignorable.Category is TokenCategory.EndOfLine or TokenCategory.Whitespace or TokenCategory.Comment)
                {
                    index++;
                    continue;
                }

                break;
            }

            if (index >= _tokens.Count)
            {
                return false;
            }

            var peek = _tokens[index];
            if (peek.Category != TokenCategory.Indent)
            {
                return false;
            }

            token = peek;
            index++;
            return true;
        }


        private bool TryParseRepeatStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count == 0)
            {
                return false;
            }

            if (tokens[0].Category != TokenCategory.Keyword || tokens[0].TokenText != "repeat")
            {
                return false;
            }

            var expressionTokens = tokens.Skip(1).ToList();
            var expression = BuildQualifiedName(expressionTokens);

            var node = Node.CreateRepeatStatement(expression);
            node.RepeatExpressionNode = ParseExpression(expressionTokens);
            target.Add(node);
            return true;
        }

        private bool TryParseHasStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count == 0)
            {
                return false;
            }

            if (tokens[0].Category != TokenCategory.Keyword || tokens[0].TokenText != "has")
            {
                return false;
            }

            if (tokens.Count > 1 && tokens[1].Category == TokenCategory.Keyword && tokens[1].TokenText == "trait")
            {
                return false;
            }

            if (tokens.Count >= 3
                && tokens[1].Category is TokenCategory.Identifier or TokenCategory.Keyword
                && tokens[2].Category is TokenCategory.Identifier or TokenCategory.Keyword)
            {
                return false;
            }

            var conditionTokens = tokens.Skip(1).ToList();
            var condition = BuildQualifiedName(conditionTokens);
            if (string.IsNullOrWhiteSpace(condition))
            {
                throw new InvalidOperationException("Has statements require a condition.");
            }

            var node = Node.CreateHasStatement(condition);
            node.HasExpression = ParseExpression(conditionTokens);
            target.Add(node);
            return true;
        }

        private bool TryParseHasTraitStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count < 3)
            {
                return false;
            }

            if (tokens[0].Category != TokenCategory.Keyword || tokens[0].TokenText != "has")
            {
                return false;
            }

            var offset = 1;
            if (tokens[1].Category == TokenCategory.Keyword && tokens[1].TokenText == "trait")
            {
                offset = 2;
                if (tokens.Count < 4)
                {
                    return false;
                }
            }

            if (tokens.Count <= offset + 1)
            {
                return false;
            }

            var traitTypeTokens = new List<LexerTokens> { tokens[offset] };
            var variableTokens = tokens.Skip(offset + 1).ToList();
            var traitType = BuildQualifiedName(traitTypeTokens);
            var traitVariable = BuildQualifiedName(variableTokens);
            var conditionTokens = tokens.Skip(offset).ToList();
            var condition = BuildQualifiedName(conditionTokens);
            if (string.IsNullOrWhiteSpace(condition) || string.IsNullOrWhiteSpace(traitType) || string.IsNullOrWhiteSpace(traitVariable))
            {
                throw new InvalidOperationException("Has trait statements require a condition.");
            }

            var node = Node.CreateHasTraitStatement(condition, traitType, traitVariable);
            node.HasTraitExpression = ParseExpression(variableTokens);
            target.Add(node);
            return true;
        }

        private bool TryParseFunctionCall(List<LexerTokens> tokens, List<Node> target)
        {
            var openIndex = tokens.FindIndex(t => t.Category == TokenCategory.Delimiter && t.TokenText == "(");
            if (openIndex <= 0)
            {
                return false;
            }

            var closeIndex = tokens.FindLastIndex(t => t.Category == TokenCategory.Delimiter && t.TokenText == ")");
            if (closeIndex < 0 || closeIndex < openIndex)
            {
                return false;
            }

            var nameTokens = tokens.Take(openIndex).ToList();
            var argsTokens = tokens.Skip(openIndex + 1).Take(closeIndex - openIndex - 1).ToList();
            var name = BuildQualifiedName(nameTokens);
            var args = BuildQualifiedName(argsTokens);

            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var callExpression = ParseExpression(tokens);
            var callNode = Node.CreateFunctionCall(name, args, callExpression);
            callNode.StatementExpression = callExpression;
            target.Add(callNode);
            return true;
        }

        private bool TryParseIfStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count == 0)
            {
                return false;
            }

            if (tokens[0].Category != TokenCategory.Keyword || tokens[0].TokenText != "if")
            {
                return false;
            }

            var conditionTokens = tokens.Skip(1).ToList();
            var condition = BuildQualifiedName(conditionTokens);
            if (string.IsNullOrWhiteSpace(condition))
            {
                throw new InvalidOperationException("If statements require a condition expression.");
            }

            var node = Node.CreateIfStatement(condition);
            node.ConditionExpression = ParseExpression(conditionTokens);
            target.Add(node);
            return true;
        }

        private bool TryParseElseStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count == 0)
            {
                return false;
            }

            if (tokens[0].Category != TokenCategory.Keyword || tokens[0].TokenText != "else")
            {
                return false;
            }

            var valueTokens = tokens.Skip(1).ToList();
            var value = valueTokens.Count > 0 ? BuildQualifiedName(valueTokens) : null;
            target.Add(Node.CreateStatement(NodeKind.ElseStatement, value));
            return true;
        }

        private void AttachElseBody(List<Node> target, int startCount)
        {
            if (startCount == 0)
            {
                return;
            }

            var previous = target[startCount - 1];
            var elseNode = target[startCount];
            if (previous.Kind != NodeKind.IfStatement)
            {
                return;
            }

            previous.ElseBody.AddRange(elseNode.StatementBody);
            target.RemoveAt(startCount);
            if (_pendingBlockTarget == elseNode.StatementBody)
            {
                _pendingBlockTarget = previous.ElseBody;
            }
        }

        private bool TryParseMatchStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count == 0)
            {
                return false;
            }

            if (tokens[0].Category != TokenCategory.Keyword || tokens[0].TokenText != "match")
            {
                return false;
            }

            var expressionTokens = tokens.Skip(1).ToList();
            var expression = BuildQualifiedName(expressionTokens);
            if (string.IsNullOrWhiteSpace(expression))
            {
                throw new InvalidOperationException("Match statements require an expression.");
            }

            var node = Node.CreateMatchStatement(expression);
            node.MatchExpressionNode = ParseExpression(expressionTokens);
            target.Add(node);
            return true;
        }

        private bool TryParseWhenStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count == 0)
            {
                return false;
            }

            if (tokens[0].Category != TokenCategory.Keyword || tokens[0].TokenText != "when")
            {
                return false;
            }

            var conditionTokens = tokens.Skip(1).ToList();
            var condition = BuildQualifiedName(conditionTokens);
            if (string.IsNullOrWhiteSpace(condition))
            {
                throw new InvalidOperationException("When statements require a condition.");
            }

            var node = Node.CreateWhenStatement(condition);
            node.WhenExpression = ParseExpression(conditionTokens);
            target.Add(node);
            return true;
        }

        private bool TryParseWhileStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count == 0)
            {
                return false;
            }

            if (tokens[0].Category != TokenCategory.Keyword || tokens[0].TokenText != "while")
            {
                return false;
            }

            var conditionTokens = tokens.Skip(1).ToList();
            var condition = BuildQualifiedName(conditionTokens);
            if (string.IsNullOrWhiteSpace(condition))
            {
                throw new InvalidOperationException("While statements require a condition.");
            }

            var node = Node.CreateWhileStatement(condition);
            node.WhileExpression = ParseExpression(conditionTokens);
            target.Add(node);
            return true;
        }

        private bool TryParseForStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count < 4)
            {
                return false;
            }

            if (tokens[0].Category != TokenCategory.Keyword || (tokens[0].TokenText != "for" && tokens[0].TokenText != "forall"))
            {
                return false;
            }

            var inIndex = tokens.FindIndex(t => t.Category == TokenCategory.Keyword && t.TokenText == "in");
            if (inIndex <= 1)
            {
                throw new InvalidOperationException("For statements require the 'in' keyword and a variable name.");
            }

            var variable = BuildQualifiedName(tokens.Skip(1).Take(inIndex - 1));
            var container = BuildQualifiedName(tokens.Skip(inIndex + 1));
            if (string.IsNullOrWhiteSpace(variable) || string.IsNullOrWhiteSpace(container))
            {
                throw new InvalidOperationException("For statements require a variable and container expression.");
            }

            if (tokens[0].TokenText == "for")
            {
                var node = Node.CreateForStatement(variable, container);
                node.ForContainerExpression = ParseExpression(tokens.Skip(inIndex + 1).ToList());
                target.Add(node);
            }
            else
            {
                var node = Node.CreateForAllStatement(variable, container);
                node.ForContainerExpression = ParseExpression(tokens.Skip(inIndex + 1).ToList());
                target.Add(node);
            }

            return true;
        }

        private static bool LooksLikeFilePath(List<LexerTokens> tokens)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "puma", "c", "h", "lib", "a"
            };

            for (var i = 0; i < tokens.Count - 1; i++)
            {
                if (tokens[i].Category == TokenCategory.Punctuation && tokens[i].TokenText == ".")
                {
                    var ext = tokens[i + 1].TokenText;
                    if (extensions.Contains(ext))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryParseSectionParameters(LexerTokens? token, out string parameters)
        {
            parameters = string.Empty;

            if (token == null)
            {
                return false;
            }

            if (IsIgnorable(token.Value))
            {
                return false;
            }

            if (token.Value.Category != TokenCategory.Delimiter || token.Value.TokenText != "(")
            {
                return false;
            }

            var parameterTokens = new List<LexerTokens>();

            while (true)
            {
                var next = GetNextToken(_tokens);
                if (next == null)
                {
                    throw new InvalidOperationException("Unterminated parameter list in section header.");
                }

                if (next.Value.Category == TokenCategory.Delimiter && next.Value.TokenText == ")")
                {
                    break;
                }

                if (IsIgnorable(next.Value))
                {
                    continue;
                }

                parameterTokens.Add(next.Value);
            }

            parameters = BuildQualifiedName(parameterTokens);
            if (_currentSectionNode != null)
            {
                _currentSectionNode.SectionParameterList.Clear();
                _currentSectionNode.SectionParameterList.AddRange(ParseParameterList(parameterTokens));
            }
            return true;
        }

        private bool TryParseReturnStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count == 0 || tokens[0].Category != TokenCategory.Keyword || tokens[0].TokenText != "return")
            {
                return false;
            }

            var valueTokens = tokens.Skip(1).ToList();
            var value = valueTokens.Count > 0 ? BuildQualifiedName(valueTokens) : null;
            var node = Node.CreateStatement(NodeKind.ReturnStatement, value);
            node.StatementExpression = ParseExpression(valueTokens);
            target.Add(node);
            return true;
        }

        private bool TryParseYieldStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count == 0 || tokens[0].Category != TokenCategory.Keyword || tokens[0].TokenText != "yield")
            {
                return false;
            }

            var valueTokens = tokens.Skip(1).ToList();
            var value = valueTokens.Count > 0 ? BuildQualifiedName(valueTokens) : null;
            var node = Node.CreateStatement(NodeKind.YieldStatement, value);
            node.StatementExpression = ParseExpression(valueTokens);
            target.Add(node);
            return true;
        }

        private bool TryParseBreakContinueStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count == 0)
            {
                return false;
            }

            if (tokens[0].Category == TokenCategory.Keyword && (tokens[0].TokenText == "break" || tokens[0].TokenText == "continue"))
            {
                var valueTokens = tokens.Skip(1).ToList();
                var value = valueTokens.Count > 0 ? BuildQualifiedName(valueTokens) : null;
                var kind = tokens[0].TokenText == "break" ? NodeKind.BreakStatement : NodeKind.ContinueStatement;
                var node = Node.CreateStatement(kind, value);
                node.StatementExpression = ParseExpression(valueTokens);
                target.Add(node);
                return true;
            }

            return false;
        }

        private bool TryParseErrorCatchStatement(List<LexerTokens> tokens, List<Node> target)
        {
            if (tokens.Count == 0)
            {
                return false;
            }

            if (tokens[0].Category == TokenCategory.Keyword && tokens[0].TokenText == "error")
            {
                var valueTokens = tokens.Skip(1).ToList();
                var value = valueTokens.Count > 0 ? BuildQualifiedName(valueTokens) : null;
                var node = Node.CreateStatement(NodeKind.ErrorStatement, value);
                node.StatementExpression = ParseExpression(valueTokens);
                target.Add(node);
                return true;
            }

            if (tokens[0].Category == TokenCategory.Keyword && tokens[0].TokenText == "catch")
            {
                var valueTokens = tokens.Skip(1).ToList();
                var value = valueTokens.Count > 0 ? BuildQualifiedName(valueTokens) : null;
                var node = Node.CreateStatement(NodeKind.CatchStatement, value);
                node.StatementExpression = ParseExpression(valueTokens);
                target.Add(node);
                return true;
            }

            return false;
        }
    }
}
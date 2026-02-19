// LLVM Compiler for the Puma programming language
//   as defined in the document "The Puma Programming Language Specification"
//   available at https://github.com/ThePumaProgrammingLanguage
//
// Copyright © 2024-2025 by Darryl Anthony Burchfield
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

using System.Runtime.CompilerServices;
using System.Xml.Linq;
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
            "using",
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
            Using,
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
            ["use"] = Section.Using,
            ["using"] = Section.Using,
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
        // start and initialize share the same position.
        private static readonly Dictionary<Section, int> SectionRank = new()
        {
            [Section.Using] = 10,
            [Section.Module] = 20,
            [Section.Enums] = 30,
            [Section.Records] = 40,
            [Section.Start] = 50,
            [Section.Initialize] = 50,
            [Section.Finalize] = 60,
            [Section.Functions] = 70,

            // Recognized but not part of the enforced flow
            [Section.Type] = 0,
            [Section.Trait] = 0,
            [Section.Properties] = 0
        };

        private const string ExpectedOrderText =
            "use, module, enums, records, start/initialize, finalize, functions";

        private int _lastRank = int.MinValue;
        private Section _lastSection = Section.None;
        private readonly HashSet<Section> _seen = new();
        private bool _typeHeaderParsed;
        private bool _traitHeaderParsed;
        private bool _moduleHeaderParsed;
        private bool _startHeaderParsed;
        private bool _initializeHeaderParsed;
        private int _currentIndentLevel;
        private int? _enumSectionIndent;
        private string? _currentEnumName;
        private List<string> _currentEnumMembers = new();
        private int? _recordSectionIndent;
        private string? _currentRecordName;
        private int? _currentRecordPackSize;
        private List<string> _currentRecordMembers = new();
        private int? _propertiesSectionIndent;
        private FileDeclarationKind _currentFileKind;
        private Node? _currentSectionNode;
        private int? _functionsSectionIndent;
        private Node? _currentFunctionNode;
        private List<Node> _currentFunctionBody = new();
        private static readonly HashSet<string> AssignmentOperators = new(StringComparer.Ordinal)
        {
            "=", "/=", "*=", "%=", "+=", "-=", "<<=", ">>=", "&=", "^=", "|="
        };
        private static readonly HashSet<string> AccessModifiers = new(StringComparer.Ordinal)
        {
            "public", "private", "internal"
        };

        private Section CurrentSection = Section.None;
        private LexerTokens? token = new LexerTokens();
        private Node? rootNode = new Node();
        private Node? currentNode = new Node();
        private Node? previousNode = new Node();
        private List<Node> ast = new List<Node>();
        private int index = 0;
        private int LineNumber = 0;
        private int ColumnNumber = 0;

        // Keep a reference to tokens to allow sub-parsers to read ahead
        private List<LexerTokens> _tokens = new();

        public Parser()
        {
            index = 0;
            LineNumber = 1;
            ColumnNumber = 1;
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
            _currentIndentLevel = 0;
            _enumSectionIndent = null;
            _currentEnumName = null;
            _currentEnumMembers = new List<string>();
            _recordSectionIndent = null;
            _currentRecordName = null;
            _currentRecordPackSize = null;
            _currentRecordMembers = new List<string>();
            _propertiesSectionIndent = null;
            _currentFileKind = FileDeclarationKind.None;
            _currentSectionNode = null;
            _functionsSectionIndent = null;
            _currentFunctionNode = null;
            _currentFunctionBody = new List<Node>();
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

                    case Section.Using:
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

            return ast;
        }

        private bool TrySwitchSection(LexerTokens? tok)
        {
            if (tok is LexerTokens t && (t.Category == TokenCategory.Identifier || t.Category == TokenCategory.Keyword))
            {
                var text = t.TokenText;
                if (_sectionMap.TryGetValue(text, out var next))
                {
                    // Duplicate check
                    if (_seen.Contains(next))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate section '{DisplayName(next)}'. Remove the extra '{DisplayName(next)}' section.");
                    }

                    // Order check
                    if (!SectionRank.TryGetValue(next, out var rank) || rank == 0)
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

                    _seen.Add(next);
                    _lastRank = rank;
                    _lastSection = next;

                    CurrentSection = next;
                    _currentSectionNode = new Node(next);
                    ast.Add(_currentSectionNode);
                    return true;
                }
            }
            return false;
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

            var returnTokens = tokens.Skip(closeIndex + 1).ToList();
            var returnType = returnTokens.Count > 0 ? BuildQualifiedName(returnTokens) : null;

            _currentFunctionNode = Node.CreateFunctionDeclaration(name, parameters, returnType, Array.Empty<Node>());
            _currentFunctionBody = new List<Node>();
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
            Section.Using => "use",
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
            TrySwitchSection(token);
        }

        private void ParseUsing(LexerTokens? token)
        {
            if (TrySwitchSection(token))
            {
                return;
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
                var memberNameTokens = memberTokens
                    .TakeWhile(t => !(t.Category == TokenCategory.Operator && t.TokenText == "="))
                    .ToList();
                var memberName = BuildQualifiedName(memberNameTokens);
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

                var memberName = memberTokens[0].TokenText;
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
                ParsePropertyDeclaration(token.Value);
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

            if (token != null && !IsIgnorable(token.Value))
            {
                ParseStatement(token.Value);
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

            if (token != null && !IsIgnorable(token.Value))
            {
                ParseStatement(token.Value);
            }
        }

        private void ParseFinalize(LexerTokens? token)
        {
            if (TrySwitchSection(token))
            {
                return;
            }

            if (token != null && !IsIgnorable(token.Value))
            {
                ParseStatement(token.Value);
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
                return;
            }

            if (_currentIndentLevel > _functionsSectionIndent && _currentFunctionNode != null)
            {
                ParseStatement(token.Value, _currentFunctionBody);
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

                return true;
            }

            return false;
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

            ast.Add(Node.CreateRecordDeclaration(_currentRecordName, _currentRecordPackSize, _currentRecordMembers));
            _currentRecordName = null;
            _currentRecordPackSize = null;
            _currentRecordMembers = new List<string>();
        }

        private void ParsePropertyDeclaration(LexerTokens firstToken)
        {
            var tokens = ReadTokensUntilEol(firstToken);
            if (tokens.Count == 0)
            {
                return;
            }

            var equalsIndex = tokens.FindIndex(t => t.Category == TokenCategory.Operator && t.TokenText == "=");
            if (equalsIndex >= 0)
            {
                var nameTokens = tokens.Take(equalsIndex).ToList();
                var valueTokens = tokens.Skip(equalsIndex + 1).ToList();
                var name = BuildQualifiedName(nameTokens);
                var value = BuildQualifiedName(valueTokens);

                if (!string.IsNullOrWhiteSpace(name))
                {
                    ast.Add(Node.CreatePropertyDeclaration(name, value, null));
                }

                return;
            }
            var fallbackName = BuildQualifiedName(tokens);
            if (!string.IsNullOrWhiteSpace(fallbackName))
            {
                throw new InvalidOperationException("Property declarations must use assignment (name = value).");
            }
        }

        private void ParseStatement(LexerTokens firstToken) => ParseStatement(firstToken, ast);

        private void ParseStatement(LexerTokens firstToken, List<Node> target)
        {
            var tokens = ReadTokensUntilEol(firstToken);
            if (tokens.Count == 0)
            {
                return;
            }

            if (TryParseMatchStatement(tokens, target))
            {
                return;
            }

            if (TryParseWhenStatement(tokens, target))
            {
                return;
            }

            if (TryParseIfStatement(tokens, target))
            {
                return;
            }

            if (TryParseWhileStatement(tokens, target))
            {
                return;
            }

            if (TryParseForStatement(tokens, target))
            {
                return;
            }

            if (TryParseRepeatStatement(tokens, target))
            {
                return;
            }

            if (TryParseHasStatement(tokens, target))
            {
                return;
            }

            if (TryParseHasTraitStatement(tokens, target))
            {
                return;
            }

            if (TryParseAssignmentStatement(tokens, target))
            {
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

            target.Add(Node.CreateAssignmentStatement(left, right, assignmentOperator));
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

            target.Add(Node.CreateRepeatStatement(expression));
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

            var conditionTokens = tokens.Skip(1).ToList();
            var condition = BuildQualifiedName(conditionTokens);
            if (string.IsNullOrWhiteSpace(condition))
            {
                throw new InvalidOperationException("Has statements require a condition.");
            }

            target.Add(Node.CreateHasStatement(condition));
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

            if (tokens[1].Category != TokenCategory.Keyword || tokens[1].TokenText != "trait")
            {
                return false;
            }

            var conditionTokens = tokens.Skip(2).ToList();
            var condition = BuildQualifiedName(conditionTokens);
            if (string.IsNullOrWhiteSpace(condition))
            {
                throw new InvalidOperationException("Has trait statements require a condition.");
            }

            target.Add(Node.CreateHasTraitStatement(condition));
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

            target.Add(Node.CreateFunctionCall(name, args));
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

            target.Add(Node.CreateIfStatement(condition));
            return true;
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

            target.Add(Node.CreateMatchStatement(expression));
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

            target.Add(Node.CreateWhenStatement(condition));
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

            target.Add(Node.CreateWhileStatement(condition));
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
                target.Add(Node.CreateForStatement(variable, container));
            }
            else
            {
                target.Add(Node.CreateForAllStatement(variable, container));
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
            return true;
        }
    }
}
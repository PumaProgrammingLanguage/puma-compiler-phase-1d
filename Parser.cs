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
            // end
            "end",
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
            end, // end of last section
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
            ["functions"] = Section.Functions,
            ["end"] = Section.end
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
            [Section.end] = 80,

            // Recognized but not part of the enforced flow
            [Section.Type] = 0,
            [Section.Trait] = 0,
            [Section.Properties] = 0
        };

        private const string ExpectedOrderText =
            "use, module, enums, records, start/initialize, finalize, functions, end";

        private int _lastRank = int.MinValue;
        private Section _lastSection = Section.None;
        private readonly HashSet<Section> _seen = new();
        private bool _typeHeaderParsed;
        private bool _traitHeaderParsed;
        private bool _moduleHeaderParsed;

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

                    case Section.end:
                        ParseEnd(token);
                        break;

                    case Section.Invalid:
                    default:
                        break;
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
                    ast.Add(new Node(next));
                    return true;
                }
            }
            return false;
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
            Section.end => "end",
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
        }
        private void ParseEnums(LexerTokens? token) => TrySwitchSection(token);
        private void ParseRecords(LexerTokens? token) => TrySwitchSection(token);
        private void ParseProperties(LexerTokens? token) => TrySwitchSection(token);

        private void ParseStart(LexerTokens? token)
        {
            // First, check if the token starts a new section
            if (TrySwitchSection(token))
                return;

            // Recognize built-in WriteLine in start section: WriteLine("...") [EOL]
            if (token is LexerTokens t && t.Category == TokenCategory.Identifier && t.TokenText == "WriteLine")
            {
                ParseWriteLineCall();
                return;
            }

            // otherwise ignore tokens inside start (for now)
        }

        private void ParseInitialize(LexerTokens? token)
        {
            // For now, just watch for next section
            TrySwitchSection(token);
        }

        private void ParseFinalize(LexerTokens? token) => TrySwitchSection(token);
        private void ParseFunctions(LexerTokens? token) => TrySwitchSection(token);

        private void ParseEnd(LexerTokens? token)
        {
            // After 'end' we ignore remaining tokens.
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
    }
}
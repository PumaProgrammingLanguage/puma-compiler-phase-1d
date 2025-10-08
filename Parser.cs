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
            "using, module, enums, records, start/initialize, finalize, functions, end";

        private int _lastRank = int.MinValue;
        private Section _lastSection = Section.None;
        private readonly HashSet<Section> _seen = new();

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
            if (tok is LexerTokens t && t.Category == TokenCategory.Identifier)
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
            Section.Using => "using",
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

        private void ParseUsing(LexerTokens? token) => TrySwitchSection(token);
        private void ParseModule(LexerTokens? token) => TrySwitchSection(token);
        private void ParseType(LexerTokens? token) => TrySwitchSection(token);
        private void ParseTrait(LexerTokens? token) => TrySwitchSection(token);
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
    }
}
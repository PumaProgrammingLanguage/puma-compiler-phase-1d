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
    /// <summary>
    /// 
    /// </summary>
    internal partial class Parser
    {
        /// <summary>
        /// Names of the sections of Puma files
        /// </summary>
        readonly string[] Sections =
        [
            // use
            "using",
            // type
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

        /// <summary>
        /// Sections of Puma files
        /// </summary>
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

        private Section CurrentSection = Section.None;
        private LexerTokens? token = new LexerTokens();
        private Node? rootNode = new Node();
        private Node? currentNode = new Node();
        private Node? previousNode = new Node();
        private List<Node> ast = new List<Node>();
        private int index = 0;
        private int LineNumber = 0;
        private int ColumnNumber = 0;

        /// <summary>
        /// constructor of the parser
        /// </summary>
        public Parser()
        {
            index = 0;
            LineNumber = 1;
            ColumnNumber = 1;
            currentNode = rootNode;
        }

        ///
        /// method to get the next token from the tokens list
        ///
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

        /// <summary>
        /// Main parser method
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns></returns>
        public List<Node> Parse(List<LexerTokens> tokens)
        {
            while (true)
            {
                token = GetNextToken(tokens);
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
                        // end of the file
                        ParseEnd(token);
                        break;

                    case Section.Invalid:
                    default:
                        break;
                }
            }

            // Return the ast list, which is guaranteed to be non-null and matches the return type.
            return ast;
        }

        private bool TrySwitchSection(LexerTokens? tok)
        {
            if (tok is LexerTokens t && t.Category == TokenCategory.Identifier)
            {
                var text = t.TokenText;
                if (_sectionMap.TryGetValue(text, out var next))
                {
                    CurrentSection = next;
                    ast.Add(new Node(next));
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Parse the file
        /// </summary>
        /// <param name="token"></param>
        private void ParseFile(LexerTokens? token)
        {
            // Look for the first section header
            TrySwitchSection(token);
        }

        /// <summary>
        /// Parse the using section
        /// </summary>
        /// <param name="token"></param>
        private void ParseUsing(LexerTokens? token)
        {
            // Switch when the next section header appears
            TrySwitchSection(token);
        }

        /// <summary>
        /// Parse the module section
        /// </summary>
        /// <param name="token"></param>
        private void ParseModule(LexerTokens? token)
        {
            TrySwitchSection(token);
        }

        /// <summary>
        /// Parse the type section
        /// </summary>
        /// <param name="token"></param>
        private void ParseType(LexerTokens? token)
        {
            TrySwitchSection(token);
        }

        /// <summary>
        /// Parse the trait section
        /// </summary>
        /// <param name="token"></param>
        private void ParseTrait(LexerTokens? token)
        {
            TrySwitchSection(token);
        }

        /// <summary>
        /// Parse the enums section
        /// </summary>
        /// <param name="token"></param>
        private void ParseEnums(LexerTokens? token)
        {
            TrySwitchSection(token);
        }

        /// <summary>
        /// Parse the records section
        /// </summary>
        /// <param name="token"></param>
        private void ParseRecords(LexerTokens? token)
        {
            TrySwitchSection(token);
        }

        /// <summary>
        /// Parse the properties section
        /// </summary>
        /// <param name="token"></param>
        private void ParseProperties(LexerTokens? token)
        {
            TrySwitchSection(token);
        }

        /// <summary>
        /// Parse the start section
        /// </summary>
        /// <param name="token"></param>
        private void ParseStart(LexerTokens? token)
        {
            TrySwitchSection(token);
        }

        /// <summary>
        /// Parse the initialize section
        /// </summary>
        /// <param name="token"></param>
        private void ParseInitialize(LexerTokens? token)
        {
            TrySwitchSection(token);
        }

        /// <summary>
        /// Parse the finalize section
        /// </summary>
        /// <param name="token"></param>
        private void ParseFinalize(LexerTokens? token)
        {
            TrySwitchSection(token);
        }

        /// <summary>
        /// Parse the functions section
        /// </summary>
        /// <param name="token"></param>
        private void ParseFunctions(LexerTokens? token)
        {
            TrySwitchSection(token);
        }

        /// <summary>
        /// Parse the end of the file
        /// </summary>
        /// <param name="token"></param>
        private void ParseEnd(LexerTokens? token)
        {
            // After 'end' we ignore remaining tokens.
        }
    }
}
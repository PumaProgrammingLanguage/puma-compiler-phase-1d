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

using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Linq;

namespace Puma
{
    /// <summary>
    /// 
    /// </summary>
    internal class Lexer
    {
        public readonly string[] Operators =
        [
            "<<=", ">>=", 
            "..",
            "==", "!=", "<=", ">=", 
            "+=", "-=", "*=", "/=", "%=", "^=", "&=", "|=",
            "<<", ">>", "++", "--",
            "=", "<", ">", "~",
            "+", "-", "*", "/", "%", "^", "&", "|" 

        ];
        List<string> _operatorsOneChar = [];
        List<string> _operatorsTwoChar = [];
        List<string> _operatorsThreeChar = [];
        readonly char[] _whitespaces = [' ', '\t'];
        readonly char[] _delimiters = ['(', ')', '{', '}', '[', ']'];
        readonly char[] _punctuation = ['.', ',', ':'];
        readonly List<string> _eol = ["\r\n", "\n", "\r", "\n\r", "\u0085"];
        List<string> _eolFirstChar = [];

        public enum TokenCategory
        {
            Unknown,
            StringLiteral,
            CharLiteral,
            NumericLiteral,
            Comment,
            Identifier,
            Whitespace,
            Delimiter,
            Punctuation,
            Operator,
            EndOfLine,
            Keyword
        }

        public struct LexerTokens
        {
            public string TokenText;
            public TokenCategory Category;
        }


        /// <summary>
        /// 
        /// </summary>
        public Lexer()
        {
            foreach (string op in Operators)
            {
                // check if the operator has three character
                if (op.Length == 3)
                {
                    _operatorsThreeChar.Add(op);
                }
                // check if the operator has two character
                else if (op.Length == 2)
                {
                    _operatorsTwoChar.Add(op);
                }
                else if (op.Length == 1)
                {
                    _operatorsOneChar.Add(op);
                }
            }

            foreach (string marker in _eol)
            {
                // check if the operator has two character
                _eolFirstChar.Add(marker[..1]);
            }
        }

        /// <summary>
        /// Tokenize the source code
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public List<LexerTokens> Tokenize(string source)
        {
            List<LexerTokens> tokens = [];
            var currentTokenType = TokenCategory.Unknown;
            var currentToken = "";

            // TODO: check if the last character is an end of line marker
            // if last character is not an end of line marker, add one
            //if (!_eol.Contains(source[-1..]))
            //{
            //    source += "\n";
            //}

            var sourceArray = source.ToCharArray();
            var lastCharInString = sourceArray.Length - 1;

            for (var i = 0; i < source.Length; i++)
            {
                var currentChar = sourceArray[i];

                switch (currentTokenType)
                {
                    case TokenCategory.Unknown:
                        IdentifyNextToken(lastCharInString, ref i, currentChar, sourceArray, tokens, ref currentToken, ref currentTokenType);
                        break;

                    case TokenCategory.Whitespace:
                        TokenizeWhitespaces(ref currentTokenType, ref currentToken, ref i, currentChar);
                        break;

                    case TokenCategory.Identifier:
                        TokenizeIdentifier(tokens, ref currentTokenType, ref currentToken, ref i, currentChar);
                        break;

                    case TokenCategory.EndOfLine:
                        TokenizeEndOfLIne(tokens, ref currentTokenType, ref currentToken, ref i, currentChar);
                        break;

                    case TokenCategory.Comment:
                        TokenizeComment(tokens, ref currentTokenType, ref currentToken, currentChar);
                        break;


                    case TokenCategory.StringLiteral:
                        TokenizeStringLiteral(tokens, ref currentTokenType, ref currentToken, currentChar);
                        break;

                    case TokenCategory.CharLiteral:
                        TokentizeCharLiteral(tokens, ref currentTokenType, ref currentToken, currentChar);
                        break;

                    case TokenCategory.NumericLiteral:
                        TokenizeNumericLiteral(tokens, ref currentTokenType, ref currentToken, ref i, currentChar);
                        break;

                    default:
                        currentTokenType = TokenizeUnidentified(tokens, ref currentToken, currentChar);
                        break;

                }
            }

            // check if the last token type was not a whitespace
            if (currentTokenType != TokenCategory.Whitespace)
            {
                // check if there is a token that didn't get added
                if (currentToken != string.Empty)
                {
                    // reached the end of the file and the last token type was not added
                    // add the last token of the line
                    tokens.Add(new LexerTokens() { TokenText = currentToken, Category = currentTokenType });
                }
            }

            return tokens;
        }

        private static TokenCategory TokenizeUnidentified(List<LexerTokens> tokens, ref string currentToken, char currentChar)
        {
            TokenCategory currentTokenType;
            // this should never happen
            currentToken += currentChar;
            tokens.Add(new LexerTokens() { TokenText = currentToken, Category = TokenCategory.Unknown });
            currentToken = string.Empty;
            currentTokenType = TokenCategory.Unknown;
            return currentTokenType;
        }

        private static void TokenizeNumericLiteral(List<LexerTokens> tokens, ref TokenCategory currentTokenType, ref string currentToken, ref int i, char currentChar)
        {
            if (char.IsDigit(currentChar) || char.IsAsciiHexDigit(currentChar) || currentChar == '.' ||
                currentChar == 'e' || currentChar == 'E' || currentChar == '-' || currentChar == '+')
            {
                // number continues
                currentToken += currentChar;
            }
            else
            {
                // found the end of the number
                tokens.Add(new LexerTokens() { TokenText = currentToken, Category = currentTokenType });
                currentToken = string.Empty;
                currentTokenType = TokenCategory.Unknown;
                // current charator is next token.  reset the index by one character
                i--;
            }
        }

        private static void TokentizeCharLiteral(List<LexerTokens> tokens, ref TokenCategory currentTokenType, ref string currentToken, char currentChar)
        {
            if (currentChar == '\'')
            {
                // found the end of the charactor
                currentToken += currentChar;
                tokens.Add(new LexerTokens() { TokenText = currentToken, Category = currentTokenType });
                currentToken = string.Empty;
                currentTokenType = TokenCategory.Unknown;
            }
            else
            {
                // charactor continues
                currentToken += currentChar;
            }
        }

        private static void TokenizeStringLiteral(List<LexerTokens> tokens, ref TokenCategory currentTokenType, ref string currentToken, char currentChar)
        {
            if (currentChar == '\"')
            {
                // found end of the string
                currentToken += currentChar;
                tokens.Add(new LexerTokens() { TokenText = currentToken, Category = currentTokenType });
                currentToken = string.Empty;
                currentTokenType = TokenCategory.Unknown;
            }
            else
            {
                // string continues
                currentToken += currentChar;
            }
        }

        private void TokenizeComment(List<LexerTokens> tokens, ref TokenCategory currentTokenType, ref string currentToken, char currentChar)
        {
            // check for end of line marker
            string currentCharString = currentChar.ToString();
            if (_eolFirstChar.Contains(currentCharString))
            {
                // comment line ends at the end of line marker
                // save the comment line as a token
                tokens.Add(new LexerTokens() { TokenText = currentToken, Category = currentTokenType });
                // place current charactor as first char of eol token
                currentToken = currentCharString;
                // set token type to end of line token
                currentTokenType = TokenCategory.EndOfLine;
            }
            else
            {
                // comment line continues
                currentToken += currentChar;
            }
        }

        private void TokenizeEndOfLIne(List<LexerTokens> tokens, ref TokenCategory currentTokenType, ref string currentToken, ref int i, char currentChar)
        {
            // check for two character end of line marker
            if (_eol.Contains(currentToken + currentChar.ToString()))
            {
                // found the second character of the end of line marker
                // normalize the end of line marker by replacing with an line feed token
                currentToken = "\n";
                tokens.Add(new LexerTokens() { TokenText = currentToken, Category = currentTokenType });
                currentToken = string.Empty;
                currentTokenType = TokenCategory.Unknown;
            }
            else
            {
                // found only a one character end of line marker
                // normalize the end of line marker
                currentToken = "\n";
                tokens.Add(new LexerTokens() { TokenText = currentToken, Category = currentTokenType });
                // normalize the end of line marker by replacing with an line feed token
                currentToken = string.Empty;
                currentTokenType = TokenCategory.Unknown;
                // current charator is next token.  reset the index by one character
                i--;
            }
        }

        private static void TokenizeIdentifier(List<LexerTokens> tokens, ref TokenCategory currentTokenType, ref string currentToken, ref int i, char currentChar)
        {
            if (char.IsLetterOrDigit(currentChar) || currentChar == '_')
            {
                // identifier continues
                currentToken += currentChar;
            }
            else
            {
                // found the end of the identifier
                tokens.Add(new LexerTokens() { TokenText = currentToken, Category = currentTokenType });
                currentToken = string.Empty;
                currentTokenType = TokenCategory.Unknown;
                // current charator is next token.  reset the index by one character
                i--;
            }
        }

        private void TokenizeWhitespaces(ref TokenCategory currentTokenType, ref string currentToken, ref int i, char currentChar)
        {
            // scanning for the end of the whitespaces
            if (!_whitespaces.Contains(currentChar))
            {
                // don't keep the whitespace tokens
                currentToken = string.Empty;
                currentTokenType = TokenCategory.Unknown;
                // current charactor is next token.  reset the index by one character
                i--;
            }
        }

        private void IdentifyNextToken(int lastCharInString, ref int i, char currentChar, char[] sourceArray, List<LexerTokens> tokens, ref string currentToken, ref TokenCategory currentTokenType)
        {
            if ((i + 2) <= lastCharInString)
            {
                char[] threeChar = [currentChar, sourceArray[i + 1], sourceArray[i + 2]];
                var currentThreeChar = string.Concat(threeChar);
                if (_operatorsThreeChar.Contains(currentThreeChar))
                {
                    // this is a three character operator. increment the index by two more characters
                    i += 2;
                    tokens.Add(new LexerTokens() { TokenText = currentThreeChar, Category = TokenCategory.Operator });
                    return;
                }
            }

            if ((i + 1) <= lastCharInString)
            {
                char[] twoChar = [currentChar, sourceArray[i + 1]];
                var currentTwoChar = string.Concat(twoChar);
                if (currentTwoChar == "//")
                {
                    // this is a two character comment marker. increment the index by one more characters
                    i += 1;
                    // save as beginning of comment token
                    currentToken = currentTwoChar;
                    currentTokenType = TokenCategory.Comment;
                    return;
                }
                else if (_operatorsTwoChar.Contains(currentTwoChar))
                {
                    // this is a two character operator. increment the index by one more characters
                    i += 1;
                    tokens.Add(new LexerTokens() { TokenText = currentTwoChar, Category = TokenCategory.Operator });
                    return;
                }
                else if (char.IsDigit(currentChar))
                {
                    // found start of a number
                    currentToken = currentChar.ToString();
                    currentTokenType = TokenCategory.NumericLiteral;
                    return;
                }
            }

            if (_whitespaces.Contains(currentChar))
            {
                // found start of a whitespace
                // don't keep the whitespace tokens
                currentTokenType = TokenCategory.Whitespace;
            }
            else if (currentChar == '\"')
            {
                // found start of a string
                currentToken = currentChar.ToString();
                currentTokenType = TokenCategory.StringLiteral;
            }
            else if (currentChar == '\'')
            {
                // found start of a charactor
                currentToken = currentChar.ToString();
                currentTokenType = TokenCategory.CharLiteral;
            }
            else if (char.IsLetter(currentChar))
            {
                // found start of an identifier
                currentToken = currentChar.ToString();
                currentTokenType = TokenCategory.Identifier;
            }
            else if (char.IsDigit(currentChar))
            {
                // found start of a number
                currentToken = currentChar.ToString();
                currentTokenType = TokenCategory.NumericLiteral;
            }
            else if (_operatorsOneChar.Contains(currentChar.ToString()))
            {
                // this is a single character operator
                tokens.Add(new LexerTokens() { TokenText = currentChar.ToString(), Category = TokenCategory.Operator });
            }
            else if (_delimiters.Contains(currentChar))
            {
                // this is a delimiter
                tokens.Add(new LexerTokens() { TokenText = currentChar.ToString(), Category = TokenCategory.Delimiter });
            }
            else if (_punctuation.Contains(currentChar))
            {
                // this is a punctuation
                tokens.Add(new LexerTokens() { TokenText = currentChar.ToString(), Category = TokenCategory.Punctuation });
            }
            else if (_eolFirstChar.Contains(currentChar.ToString()))
            {
                // first character of an end of line marker
                currentToken = currentChar.ToString();
                currentTokenType = TokenCategory.EndOfLine;
            }
            else
            {
                // this should never happen
                currentToken += currentChar;
                tokens.Add(new LexerTokens() { TokenText = currentToken, Category = currentTokenType });
                currentToken = string.Empty;
                currentTokenType = TokenCategory.Unknown;
            }
            return;
        }
    }
}

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
            "=", "<", ">", "~", "!",
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
            Indent,
            Dedent,
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

        private readonly HashSet<string> _keywords = new(StringComparer.Ordinal)
        {
            "use", "as", "type", "trait", "module", "is", "has", "value", "object", "base", "number",
            "optional", "enums", "records", "pack", "properties", "functions", "start", "initialize", "finalize",
            "return", "yield", "public", "private", "internal", "override", "delegate", "constant", "readonly",
            "readwrite", "int128", "int64", "int32", "int16", "int8", "int", "uint128", "uint64", "uint32",
            "uint16", "uint8", "uint", "flt128", "flt64", "flt32", "flt", "fix128", "fix64", "fix32", "fix",
            "char", "str", "fstr", "vstr", "bool", "true", "false", "hex", "oct", "bin", "implicit", "explicit",
            "operator", "get", "set", "with", "self", "if", "else", "and", "or", "not", "for", "in", "while",
            "repeat", "forall", "break", "continue", "match", "when", "error", "catch", "multithread",
            "multiprocess"
        };

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
            var atLineStart = true;
            var escapeString = false;
            var escapeChar = false;
            var invalidStringEscape = false;
            var invalidCharEscape = false;
            var stringHexRemaining = 0;
            var charHexRemaining = 0;
            var indentStack = new Stack<int>();
            indentStack.Push(0);

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

                if (atLineStart)
                {
                    var indentIndex = i;
                    var indentCount = 0;

                    while (indentIndex <= lastCharInString && _whitespaces.Contains(sourceArray[indentIndex]))
                    {
                        indentCount += sourceArray[indentIndex] == '\t' ? 4 : 1;
                        indentIndex++;
                    }

                    if (indentIndex <= lastCharInString && _eolFirstChar.Contains(sourceArray[indentIndex].ToString()))
                    {
                        atLineStart = false;
                        i = indentIndex - 1;
                        continue;
                    }

                    var currentIndent = indentStack.Peek();
                    if (indentCount > currentIndent)
                    {
                        indentStack.Push(indentCount);
                        tokens.Add(new LexerTokens() { TokenText = indentCount.ToString(), Category = TokenCategory.Indent });
                    }
                    else if (indentCount < currentIndent)
                    {
                        while (indentStack.Count > 0 && indentCount < indentStack.Peek())
                        {
                            indentStack.Pop();
                            tokens.Add(new LexerTokens() { TokenText = indentCount.ToString(), Category = TokenCategory.Dedent });
                        }
                    }

                    atLineStart = false;
                    i = indentIndex - 1;
                    continue;
                }

                if ((currentTokenType == TokenCategory.Unknown || currentTokenType == TokenCategory.Whitespace)
                    && currentChar == '\\' && TryGetEndOfLineLength(i + 1, sourceArray, out var eolLength))
                {
                    i += eolLength;
                    continue;
                }

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
                        TokenizeEndOfLIne(tokens, ref currentTokenType, ref currentToken, ref i, currentChar, ref atLineStart);
                        break;

                    case TokenCategory.Comment:
                        TokenizeComment(tokens, ref currentTokenType, ref currentToken, currentChar);
                        break;


                    case TokenCategory.StringLiteral:
                        TokenizeStringLiteral(tokens, ref currentTokenType, ref currentToken, currentChar, ref escapeString, ref invalidStringEscape, ref stringHexRemaining);
                        break;

                    case TokenCategory.CharLiteral:
                        TokentizeCharLiteral(tokens, ref currentTokenType, ref currentToken, currentChar, ref escapeChar, ref invalidCharEscape, ref charHexRemaining);
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
                    var category = currentTokenType == TokenCategory.Identifier && _keywords.Contains(currentToken)
                        ? TokenCategory.Keyword
                        : currentTokenType;
                    tokens.Add(new LexerTokens() { TokenText = currentToken, Category = category });
                }
            }

            while (indentStack.Count > 1)
            {
                indentStack.Pop();
                tokens.Add(new LexerTokens() { TokenText = "0", Category = TokenCategory.Dedent });
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
            var lastChar = currentToken.Length > 0 ? currentToken[^1] : '\0';
            var numberBase = GetNumberBase(currentToken);
            var hasExponent = numberBase == 10 && (currentToken.Contains('e') || currentToken.Contains('E'));
            var isExponentSign = numberBase == 10 && (currentChar == '-' || currentChar == '+') && (lastChar == 'e' || lastChar == 'E');
            var isSuffixDigit = char.IsDigit(currentChar) && currentToken.Any(char.IsLetter);
            var isBasePrefix = lastChar == '0' && currentToken.Length == 1 && (currentChar == 'x' || currentChar == 'X' || currentChar == 'b' || currentChar == 'B' || currentChar == 'o' || currentChar == 'O');
            var isSuffixLetter = char.IsLetter(currentChar) && IsAllowedNumericSuffixChar(currentToken, currentChar, hasExponent);

            if (numberBase != 10 && (char.IsLetterOrDigit(currentChar) || currentChar == '.'))
            {
                if (!IsValidDigitForBase(numberBase, currentChar) && !isSuffixLetter)
                {
                    tokens.Add(new LexerTokens() { TokenText = currentToken, Category = TokenCategory.Unknown });
                    currentToken = string.Empty;
                    currentTokenType = TokenCategory.Unknown;
                    i--;
                    return;
                }
            }

            if (char.IsDigit(currentChar) || char.IsAsciiHexDigit(currentChar) || currentChar == '.' ||
                currentChar == 'e' || currentChar == 'E' || isExponentSign || isBasePrefix || isSuffixLetter || isSuffixDigit)
            {
                // number continues
                currentToken += currentChar;
            }
            else
            {
                // found the end of the number
                if (TrySplitNumericSuffix(currentToken, out var numberToken, out var suffixToken))
                {
                    tokens.Add(new LexerTokens() { TokenText = numberToken, Category = currentTokenType });
                    var suffixCategory = IsKeyword(suffixToken) ? TokenCategory.Keyword : TokenCategory.Identifier;
                    tokens.Add(new LexerTokens() { TokenText = suffixToken, Category = suffixCategory });
                }
                else
                {
                    tokens.Add(new LexerTokens() { TokenText = currentToken, Category = currentTokenType });
                }
                currentToken = string.Empty;
                currentTokenType = TokenCategory.Unknown;
                // current charator is next token.  reset the index by one character
                i--;
            }
        }

        private static bool IsAllowedNumericSuffixChar(string currentToken, char currentChar, bool hasExponent)
        {
            if (hasExponent)
            {
                return false;
            }

            if (!currentToken.Any(char.IsDigit))
            {
                return false;
            }

            if (currentToken.Any(char.IsLetter))
            {
                return true;
            }

            return currentChar is 'i' or 'u' or 'f' or 'h' or 'o' or 'b' or 'x' or 'I' or 'U' or 'F' or 'H' or 'O' or 'B' or 'X';
        }

        private static int GetNumberBase(string currentToken)
        {
            if (currentToken.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return 16;
            }

            if (currentToken.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (currentToken.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
            {
                return 8;
            }

            return 10;
        }

        private static bool IsValidDigitForBase(int numberBase, char currentChar)
        {
            return numberBase switch
            {
                2 => currentChar is '0' or '1',
                8 => currentChar is >= '0' and <= '7',
                16 => char.IsDigit(currentChar) || (char.ToLowerInvariant(currentChar) is >= 'a' and <= 'f'),
                _ => char.IsDigit(currentChar)
            };
        }

        private static bool IsValidEscapeChar(char currentChar)
        {
            return currentChar is '\\' or '\'' or '"' or '0' or 'a' or 'b' or 'f' or 'n' or 'r' or 't' or 'v'
                or 'x' or 'u' or 'U';
        }

        private static void TokentizeCharLiteral(List<LexerTokens> tokens, ref TokenCategory currentTokenType, ref string currentToken, char currentChar, ref bool escapeChar, ref bool invalidCharEscape, ref int charHexRemaining)
        {
            if (charHexRemaining > 0)
            {
                currentToken += currentChar;
                if (!char.IsAsciiHexDigit(currentChar))
                {
                    invalidCharEscape = true;
                }
                charHexRemaining--;
                return;
            }

            if (charHexRemaining < 0)
            {
                if (char.IsAsciiHexDigit(currentChar))
                {
                    currentToken += currentChar;
                    return;
                }

                charHexRemaining = 0;
            }

            if (escapeChar)
            {
                currentToken += currentChar;
                if (currentChar is 'x' or 'u' or 'U')
                {
                    charHexRemaining = currentChar == 'x' ? -1 : currentChar == 'u' ? 4 : 8;
                    escapeChar = false;
                    return;
                }
                if (!IsValidEscapeChar(currentChar))
                {
                    invalidCharEscape = true;
                }
                escapeChar = false;
                return;
            }

            if (currentChar == '\\')
            {
                currentToken += currentChar;
                escapeChar = true;
                return;
            }

            if (currentChar == '\'')
            {
                // found the end of the charactor
                currentToken += currentChar;
                var category = invalidCharEscape ? TokenCategory.Unknown : currentTokenType;
                tokens.Add(new LexerTokens() { TokenText = currentToken, Category = category });
                currentToken = string.Empty;
                currentTokenType = TokenCategory.Unknown;
                escapeChar = false;
                invalidCharEscape = false;
            }
            else
            {
                // charactor continues
                currentToken += currentChar;
            }
        }

        private static void TokenizeStringLiteral(List<LexerTokens> tokens, ref TokenCategory currentTokenType, ref string currentToken, char currentChar, ref bool escapeString, ref bool invalidStringEscape, ref int stringHexRemaining)
        {
            if (stringHexRemaining > 0)
            {
                currentToken += currentChar;
                if (!char.IsAsciiHexDigit(currentChar))
                {
                    invalidStringEscape = true;
                }
                stringHexRemaining--;
                return;
            }

            if (stringHexRemaining < 0)
            {
                if (char.IsAsciiHexDigit(currentChar))
                {
                    currentToken += currentChar;
                    return;
                }

                stringHexRemaining = 0;
            }

            if (escapeString)
            {
                currentToken += currentChar;
                if (currentChar is 'x' or 'u' or 'U')
                {
                    stringHexRemaining = currentChar == 'x' ? -1 : currentChar == 'u' ? 4 : 8;
                    escapeString = false;
                    return;
                }
                if (!IsValidEscapeChar(currentChar))
                {
                    invalidStringEscape = true;
                }
                escapeString = false;
                return;
            }

            if (currentChar == '\\')
            {
                currentToken += currentChar;
                escapeString = true;
                return;
            }

            if (currentChar == '\"')
            {
                // found end of the string
                currentToken += currentChar;
                var category = invalidStringEscape ? TokenCategory.Unknown : currentTokenType;
                tokens.Add(new LexerTokens() { TokenText = currentToken, Category = category });
                currentToken = string.Empty;
                currentTokenType = TokenCategory.Unknown;
                escapeString = false;
                invalidStringEscape = false;
            }
            else
            {
                // string continues
                currentToken += currentChar;
            }
        }

        private void TokenizeComment(List<LexerTokens> tokens, ref TokenCategory currentTokenType, ref string currentToken, char currentChar)
        {
            if (currentToken.StartsWith("/."))
            {
                currentToken += currentChar;
                if (currentToken.EndsWith("./"))
                {
                    tokens.Add(new LexerTokens() { TokenText = currentToken, Category = currentTokenType });
                    currentToken = string.Empty;
                    currentTokenType = TokenCategory.Unknown;
                }
                return;
            }

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

        private void TokenizeEndOfLIne(List<LexerTokens> tokens, ref TokenCategory currentTokenType, ref string currentToken, ref int i, char currentChar, ref bool atLineStart)
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
                atLineStart = true;
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
                atLineStart = true;
            }
        }

        private void TokenizeIdentifier(List<LexerTokens> tokens, ref TokenCategory currentTokenType, ref string currentToken, ref int i, char currentChar)
        {
            if (IsIdentifierPartChar(currentChar))
            {
                // identifier continues
                currentToken += currentChar;
            }
            else
            {
                // found the end of the identifier
                var category = _keywords.Contains(currentToken) ? TokenCategory.Keyword : currentTokenType;
                tokens.Add(new LexerTokens() { TokenText = currentToken, Category = category });
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
                else if (currentTwoChar == "/.")
                {
                    // this is a two character multiline comment marker. increment the index by one more characters
                    i += 1;
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
            else if (IsIdentifierStartChar(currentChar))
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

        private bool TryGetEndOfLineLength(int startIndex, char[] sourceArray, out int length)
        {
            length = 0;

            if (startIndex > sourceArray.Length - 1)
            {
                return false;
            }

            if (startIndex + 1 <= sourceArray.Length - 1)
            {
                var twoChar = string.Concat(sourceArray[startIndex], sourceArray[startIndex + 1]);
                if (_eol.Contains(twoChar))
                {
                    length = 2;
                    return true;
                }
            }

            var oneChar = sourceArray[startIndex].ToString();
            if (_eol.Contains(oneChar))
            {
                length = 1;
                return true;
            }

            return false;
        }

        private static bool IsIdentifierStartChar(char currentChar)
        {
            return currentChar == '_' || char.IsLetter(currentChar);
        }

        private static bool IsIdentifierPartChar(char currentChar)
        {
            return currentChar == '_' || char.IsLetterOrDigit(currentChar);
        }

        private static bool TrySplitNumericSuffix(string token, out string numberToken, out string suffixToken)
        {
            numberToken = token;
            suffixToken = string.Empty;

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var numberBase = GetNumberBase(token);
            var startIndex = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("0b", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("0o", StringComparison.OrdinalIgnoreCase)
                ? 2
                : 0;

            var seenDigit = false;
            for (var i = startIndex; i < token.Length; i++)
            {
                var ch = token[i];
                if (numberBase == 10 && (ch == 'e' || ch == 'E'))
                {
                    seenDigit = true;
                    if (i + 1 < token.Length && (token[i + 1] == '+' || token[i + 1] == '-'))
                    {
                        i++;
                    }
                    continue;
                }

                if (char.IsDigit(ch) || (numberBase == 16 && char.IsAsciiHexDigit(ch)) || (numberBase == 8 && ch is >= '0' and <= '7') || (numberBase == 2 && ch is '0' or '1') || (numberBase == 10 && ch == '.'))
                {
                    seenDigit = true;
                    continue;
                }

                if (seenDigit && char.IsLetter(ch))
                {
                    numberToken = token[..i];
                    suffixToken = token[i..];
                    return true;
                }

                break;
            }

            return false;
        }

        private static bool IsKeyword(string token)
        {
            return token switch
            {
                "use" or "as" or "type" or "trait" or "module" or "is" or "has" or "value" or "object" or "base" or "number"
                    or "optional" or "enums" or "records" or "pack" or "properties" or "functions" or "start" or "initialize" or "finalize"
                    or "return" or "yield" or "public" or "private" or "internal" or "override" or "delegate" or "constant" or "readonly"
                    or "readwrite" or "int128" or "int64" or "int32" or "int16" or "int8" or "int" or "uint128" or "uint64" or "uint32"
                    or "uint16" or "uint8" or "uint" or "flt128" or "flt64" or "flt32" or "flt" or "fix128" or "fix64" or "fix32" or "fix"
                    or "char" or "str" or "fstr" or "vstr" or "bool" or "true" or "false" or "hex" or "oct" or "bin" or "implicit" or "explicit"
                    or "operator" or "get" or "set" or "with" or "self" or "if" or "else" or "and" or "or" or "not" or "for" or "in" or "while"
                    or "repeat" or "forall" or "break" or "continue" or "match" or "when" or "error" or "catch" or "multithread"
                    or "multiprocess" => true,
                _ => false
            };
        }
    }
}

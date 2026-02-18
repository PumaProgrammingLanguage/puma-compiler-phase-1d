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

using static Puma.Parser;

namespace Puma
{
    internal enum NodeKind
    {
        Section,
        WriteLine,
        UseStatement,
        TypeDeclaration,
        EnumDeclaration,
        RecordDeclaration,
        PropertyDeclaration,
        AssignmentStatement,
        FunctionCall,
        IfStatement,
        MatchStatement,
        WhenStatement
    }

    internal class Node
    {
        public NodeKind Kind { get; set; } = NodeKind.Section;
        public Section Section { get; set; } = Section.None;

        // For WriteLine nodes
        public string? StringValue { get; set; }

        // For UseStatement nodes
        public string? UseTarget { get; set; }
        public string? UseAlias { get; set; }
        public bool UseIsFilePath { get; set; }

        // For TypeDeclaration nodes
        public string? DeclarationKind { get; set; }
        public string? DeclarationName { get; set; }
        public string? BaseTypeName { get; set; }
        public List<string> TraitNames { get; } = new();

        // For EnumDeclaration nodes
        public string? EnumName { get; set; }
        public List<string> EnumMembers { get; } = new();

        // For RecordDeclaration nodes
        public string? RecordName { get; set; }
        public int? RecordPackSize { get; set; }
        public List<string> RecordMembers { get; } = new();

        // For PropertyDeclaration nodes
        public string? PropertyName { get; set; }
        public string? PropertyValue { get; set; }
        public string? PropertyType { get; set; }

        // For AssignmentStatement nodes
        public string? AssignmentLeft { get; set; }
        public string? AssignmentRight { get; set; }

        // For FunctionCall nodes
        public string? FunctionName { get; set; }
        public string? FunctionArguments { get; set; }

        // For IfStatement nodes
        public string? IfCondition { get; set; }

        // For MatchStatement nodes
        public string? MatchExpression { get; set; }

        // For WhenStatement nodes
        public string? WhenCondition { get; set; }

        public Node()
        {
        }

        public Node(Section section)
        {
            Kind = NodeKind.Section;
            Section = section;
        }

        public static Node CreateWriteLine(string literal)
        {
            return new Node
            {
                Kind = NodeKind.WriteLine,
                StringValue = literal
            };
        }

        public static Node CreateUseStatement(string target, string? alias, bool isFilePath)
        {
            return new Node
            {
                Kind = NodeKind.UseStatement,
                UseTarget = target,
                UseAlias = alias,
                UseIsFilePath = isFilePath
            };
        }

        public static Node CreateTypeDeclaration(string declarationKind, string name, string? baseType, IEnumerable<string>? traits = null)
        {
            var node = new Node
            {
                Kind = NodeKind.TypeDeclaration,
                DeclarationKind = declarationKind,
                DeclarationName = name,
                BaseTypeName = baseType
            };

            if (traits != null)
            {
                node.TraitNames.AddRange(traits);
            }

            return node;
        }

        public static Node CreateEnumDeclaration(string name, IEnumerable<string> members)
        {
            var node = new Node
            {
                Kind = NodeKind.EnumDeclaration,
                EnumName = name
            };
            node.EnumMembers.AddRange(members);
            return node;
        }

        public static Node CreateRecordDeclaration(string name, int? packSize, IEnumerable<string> members)
        {
            var node = new Node
            {
                Kind = NodeKind.RecordDeclaration,
                RecordName = name,
                RecordPackSize = packSize
            };
            node.RecordMembers.AddRange(members);
            return node;
        }

        public static Node CreatePropertyDeclaration(string name, string? value, string? type)
        {
            return new Node
            {
                Kind = NodeKind.PropertyDeclaration,
                PropertyName = name,
                PropertyValue = value,
                PropertyType = type
            };
        }

        public static Node CreateAssignmentStatement(string left, string right)
        {
            return new Node
            {
                Kind = NodeKind.AssignmentStatement,
                AssignmentLeft = left,
                AssignmentRight = right
            };
        }

        public static Node CreateFunctionCall(string name, string arguments)
        {
            return new Node
            {
                Kind = NodeKind.FunctionCall,
                FunctionName = name,
                FunctionArguments = arguments
            };
        }

        public static Node CreateIfStatement(string condition)
        {
            return new Node
            {
                Kind = NodeKind.IfStatement,
                IfCondition = condition
            };
        }

        public static Node CreateMatchStatement(string expression)
        {
            return new Node
            {
                Kind = NodeKind.MatchStatement,
                MatchExpression = expression
            };
        }

        public static Node CreateWhenStatement(string condition)
        {
            return new Node
            {
                Kind = NodeKind.WhenStatement,
                WhenCondition = condition
            };
        }
    }
}
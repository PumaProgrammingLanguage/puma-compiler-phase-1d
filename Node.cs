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
        WhenStatement,
        WhileStatement,
        ForStatement,
        ForAllStatement,
        RepeatStatement
        ,
        HasStatement,
        HasTraitStatement
        ,
        FunctionDeclaration,
        ReturnStatement,
        YieldStatement,
        BreakStatement,
        ContinueStatement,
        ErrorStatement,
        CatchStatement,
        DelegateDeclaration,
        ElseStatement
    }

    internal enum ExpressionKind
    {
        Identifier,
        Literal,
        Unary,
        Binary,
        MemberAccess,
        Index,
        Call
    }

    internal sealed class ExpressionNode
    {
        public ExpressionKind Kind { get; set; }
        public string? Value { get; set; }
        public ExpressionNode? Left { get; set; }
        public ExpressionNode? Right { get; set; }
        public List<ExpressionNode> Arguments { get; } = new();
    }

    internal class Node
    {
        internal sealed class ParameterInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string? DefaultValue { get; set; }
            public List<string> Modifiers { get; } = new();
        }
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
        public List<string> PropertyModifiers { get; } = new();

        // For AssignmentStatement nodes
        public string? AssignmentLeft { get; set; }
        public string? AssignmentRight { get; set; }
        public string? AssignmentOperator { get; set; }
        public ExpressionNode? AssignmentLeftExpression { get; set; }
        public ExpressionNode? AssignmentRightExpression { get; set; }

        // For Section nodes
        public string? SectionParameters { get; set; }
        public List<ParameterInfo> SectionParameterList { get; } = new();

        // For FunctionCall nodes
        public string? FunctionName { get; set; }
        public string? FunctionArguments { get; set; }

        // For IfStatement nodes
        public string? IfCondition { get; set; }
        public List<Node> ElseBody { get; } = new();
        public ExpressionNode? ConditionExpression { get; set; }

        // For MatchStatement nodes
        public string? MatchExpression { get; set; }
        public ExpressionNode? MatchExpressionNode { get; set; }

        // For WhenStatement nodes
        public string? WhenCondition { get; set; }
        public ExpressionNode? WhenExpression { get; set; }

        // For WhileStatement nodes
        public string? WhileCondition { get; set; }
        public ExpressionNode? WhileExpression { get; set; }

        // For ForStatement nodes
        public string? ForVariable { get; set; }
        public string? ForContainer { get; set; }
        public ExpressionNode? ForContainerExpression { get; set; }

        // For RepeatStatement nodes
        public string? RepeatExpression { get; set; }
        public ExpressionNode? RepeatExpressionNode { get; set; }

        // For HasStatement nodes
        public string? HasCondition { get; set; }
        public ExpressionNode? HasExpression { get; set; }

        // For HasTraitStatement nodes
        public string? HasTraitCondition { get; set; }
        public ExpressionNode? HasTraitExpression { get; set; }

        // For FunctionDeclaration nodes
        public string? FunctionDeclarationName { get; set; }
        public string? FunctionDeclarationParameters { get; set; }
        public string? FunctionDeclarationReturnType { get; set; }
        public List<Node> FunctionBody { get; } = new();
        public List<ParameterInfo> FunctionParameterList { get; } = new();

        // For DelegateDeclaration nodes
        public string? DelegateName { get; set; }
        public List<ParameterInfo> DelegateParameterList { get; } = new();

        // For statement nodes
        public string? StatementValue { get; set; }
        public List<Node> StatementBody { get; } = new();
        public ExpressionNode? StatementExpression { get; set; }

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

        public static Node CreatePropertyDeclaration(string name, string? value, string? type, IEnumerable<string>? modifiers = null)
        {
            var node = new Node
            {
                Kind = NodeKind.PropertyDeclaration,
                PropertyName = name,
                PropertyValue = value,
                PropertyType = type
            };

            if (modifiers != null)
            {
                node.PropertyModifiers.AddRange(modifiers);
            }

            return node;
        }

        public static Node CreateAssignmentStatement(string left, string right, string assignmentOperator)
        {
            return new Node
            {
                Kind = NodeKind.AssignmentStatement,
                AssignmentLeft = left,
                AssignmentRight = right,
                AssignmentOperator = assignmentOperator
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

        public static Node CreateWhileStatement(string condition)
        {
            return new Node
            {
                Kind = NodeKind.WhileStatement,
                WhileCondition = condition
            };
        }

        public static Node CreateForStatement(string variable, string container)
        {
            return new Node
            {
                Kind = NodeKind.ForStatement,
                ForVariable = variable,
                ForContainer = container
            };
        }

        public static Node CreateForAllStatement(string variable, string container)
        {
            return new Node
            {
                Kind = NodeKind.ForAllStatement,
                ForVariable = variable,
                ForContainer = container
            };
        }

        public static Node CreateRepeatStatement(string expression)
        {
            return new Node
            {
                Kind = NodeKind.RepeatStatement,
                RepeatExpression = expression
            };
        }

        public static Node CreateHasStatement(string condition)
        {
            return new Node
            {
                Kind = NodeKind.HasStatement,
                HasCondition = condition
            };
        }

        public static Node CreateHasTraitStatement(string condition)
        {
            return new Node
            {
                Kind = NodeKind.HasTraitStatement,
                HasTraitCondition = condition
            };
        }

        public static Node CreateFunctionDeclaration(string name, string? parameters, string? returnType, IEnumerable<Node> body, IEnumerable<ParameterInfo> parameterList)
        {
            var node = new Node
            {
                Kind = NodeKind.FunctionDeclaration,
                FunctionDeclarationName = name,
                FunctionDeclarationParameters = parameters,
                FunctionDeclarationReturnType = returnType
            };

            node.FunctionParameterList.AddRange(parameterList);
            node.FunctionBody.AddRange(body);
            return node;
        }

        public static Node CreateStatement(NodeKind kind, string? value = null)
        {
            return new Node
            {
                Kind = kind,
                StatementValue = value
            };
        }

        public static Node CreateDelegateDeclaration(string name, IEnumerable<ParameterInfo> parameterList)
        {
            var node = new Node
            {
                Kind = NodeKind.DelegateDeclaration,
                DelegateName = name
            };

            node.DelegateParameterList.AddRange(parameterList);
            return node;
        }
    }
}
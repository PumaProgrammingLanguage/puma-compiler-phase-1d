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
        RepeatStatement,
        HasStatement,
        HasTraitStatement,
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
        Cast,
        Conditional,
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

    internal sealed class WriteLineAstNode : Node
    {
        public string? StringValue { get; set; }

        public WriteLineAstNode()
        {
            Kind = NodeKind.WriteLine;
        }
    }

    internal sealed class EnumDeclarationAstNode : Node
    {
        public string? EnumName { get; set; }
        public List<string> EnumMembers { get; } = new();

        public EnumDeclarationAstNode()
        {
            Kind = NodeKind.EnumDeclaration;
        }
    }

    internal sealed class RecordDeclarationAstNode : Node
    {
        public string? RecordName { get; set; }
        public int? RecordPackSize { get; set; }
        public List<string> RecordMembers { get; } = new();
        public Dictionary<string, string> RecordMemberTypes { get; } = new(StringComparer.Ordinal);

        public RecordDeclarationAstNode()
        {
            Kind = NodeKind.RecordDeclaration;
        }
    }

    internal sealed class UseStatementAstNode : Node
    {
        public string? Target { get; set; }
        public string? Alias { get; set; }
        public bool IsFilePath { get; set; }

        public UseStatementAstNode()
        {
            Kind = NodeKind.UseStatement;
        }
    }

    internal sealed class FunctionCallAstNode : Node
    {
        public string? Name { get; set; }
        public string? Arguments { get; set; }
        public ExpressionNode? Expression { get; set; }

        public FunctionCallAstNode()
        {
            Kind = NodeKind.FunctionCall;
        }
    }

    internal sealed class IfStatementAstNode : Node
    {
        public string? IfCondition { get; set; }
        public ExpressionNode? ConditionExpression { get; set; }
        public List<Node> IfBody { get; } = new();
        public List<Node> ElseBody { get; } = new();

        public IfStatementAstNode()
        {
            Kind = NodeKind.IfStatement;
        }
    }

    internal sealed class MatchStatementAstNode : Node
    {
        public string? Expression { get; set; }
        public ExpressionNode? ExpressionNode { get; set; }
        public List<Node> StatementBody { get; } = new();

        public MatchStatementAstNode()
        {
            Kind = NodeKind.MatchStatement;
        }
    }

    internal sealed class WhenStatementAstNode : Node
    {
        public string? WhenCondition { get; set; }
        public ExpressionNode? WhenExpression { get; set; }
        public List<Node> StatementBody { get; } = new();

        public WhenStatementAstNode()
        {
            Kind = NodeKind.WhenStatement;
        }
    }

    internal sealed class WhileStatementAstNode : Node
    {
        public string? WhileCondition { get; set; }
        public ExpressionNode? WhileExpression { get; set; }
        public List<Node> StatementBody { get; } = new();

        public WhileStatementAstNode()
        {
            Kind = NodeKind.WhileStatement;
        }
    }

    internal sealed class ForStatementAstNode : Node
    {
        public string? ForVariable { get; set; }
        public string? ForContainer { get; set; }
        public ExpressionNode? ForContainerExpression { get; set; }
        public List<Node> StatementBody { get; } = new();

        public ForStatementAstNode()
        {
            Kind = NodeKind.ForStatement;
        }
    }

    internal sealed class ForAllStatementAstNode : Node
    {
        public string? ForVariable { get; set; }
        public string? ForContainer { get; set; }
        public ExpressionNode? ForContainerExpression { get; set; }
        public List<Node> StatementBody { get; } = new();

        public ForAllStatementAstNode()
        {
            Kind = NodeKind.ForAllStatement;
        }
    }

    internal sealed class RepeatStatementAstNode : Node
    {
        public string? RepeatExpression { get; set; }
        public ExpressionNode? RepeatExpressionNode { get; set; }
        public List<Node> StatementBody { get; } = new();

        public RepeatStatementAstNode()
        {
            Kind = NodeKind.RepeatStatement;
        }
    }

    internal sealed class HasStatementAstNode : Node
    {
        public string? HasCondition { get; set; }
        public ExpressionNode? HasExpression { get; set; }
        public List<Node> StatementBody { get; } = new();

        public HasStatementAstNode()
        {
            Kind = NodeKind.HasStatement;
        }
    }

    internal sealed class HasTraitStatementAstNode : Node
    {
        public string? HasTraitCondition { get; set; }
        public ExpressionNode? HasTraitExpression { get; set; }
        public string? HasTraitTypeName { get; set; }
        public string? HasTraitVariableName { get; set; }
        public List<Node> StatementBody { get; } = new();

        public HasTraitStatementAstNode()
        {
            Kind = NodeKind.HasTraitStatement;
        }
    }

    internal sealed class AssignmentStatementAstNode : Node
    {
        public string? Left { get; set; }
        public string? Right { get; set; }
        public string? Operator { get; set; }
        public string? AssignmentLeft { get; set; }
        public string? AssignmentRight { get; set; }
        public string? AssignmentOperator { get; set; }
        public ExpressionNode? AssignmentLeftExpression { get; set; }
        public ExpressionNode? AssignmentRightExpression { get; set; }
        public bool IsLoweredPostfixMutation { get; set; }

        public AssignmentStatementAstNode()
        {
            Kind = NodeKind.AssignmentStatement;
        }
    }

    internal sealed class TypeDeclarationAstNode : Node
    {
        public string? DeclarationKind { get; set; }
        public string? DeclarationName { get; set; }
        public string? BaseTypeName { get; set; }
        public List<string> TraitNames { get; } = new();
        public List<Node> TypeProperties { get; } = new();
        public List<Node> TypeFunctions { get; } = new();

        public TypeDeclarationAstNode()
        {
            Kind = NodeKind.TypeDeclaration;
        }
    }

    internal sealed class PropertyDeclarationAstNode : Node
    {
        public string? PropertyName { get; set; }
        public string? PropertyValue { get; set; }
        public string? PropertyType { get; set; }
        public List<string> PropertyModifiers { get; } = new();

        public PropertyDeclarationAstNode()
        {
            Kind = NodeKind.PropertyDeclaration;
        }
    }

    internal sealed class FunctionDeclarationAstNode : Node
    {
        public string? FunctionDeclarationName { get; set; }
        public string? FunctionDeclarationParameters { get; set; }
        public string? FunctionDeclarationReturnType { get; set; }
        public List<string> FunctionModifiers { get; } = new();
        public List<Node> FunctionBody { get; } = new();
        public List<Node.ParameterInfo> FunctionParameterList { get; } = new();

        public FunctionDeclarationAstNode()
        {
            Kind = NodeKind.FunctionDeclaration;
        }
    }

    internal sealed class DelegateDeclarationAstNode : Node
    {
        public string? DelegateName { get; set; }
        public List<Node.ParameterInfo> DelegateParameterList { get; } = new();

        public DelegateDeclarationAstNode()
        {
            Kind = NodeKind.DelegateDeclaration;
        }
    }

    internal sealed class StatementAstNode : Node
    {
        public string? StatementValue { get; set; }
        public List<Node> StatementBody { get; } = new();
        public ExpressionNode? StatementExpression { get; set; }

        public StatementAstNode(NodeKind kind)
        {
            Kind = kind;
        }
    }

    internal sealed class SectionAstNode : Node
    {
        public string? SectionName { get; set; }
        public string? SectionParameters { get; set; }
        public List<Node.ParameterInfo> SectionParameterList { get; } = new();
        public int LeadingBlankLines { get; set; }

        public SectionAstNode(Section section)
        {
            Kind = NodeKind.Section;
            Section = section;
            SectionName = section.ToString();
        }
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

        public Node()
        {
        }

        public Node(Section section) : this()
        {
            Kind = NodeKind.Section;
            Section = section;
        }

        public static Node CreateSection(Section section, int leadingBlankLines = 0)
        {
            return new SectionAstNode(section)
            {
                LeadingBlankLines = leadingBlankLines
            };
        }

        public static Node CreateWriteLine(string literal)
        {
            return new WriteLineAstNode
            {
                StringValue = literal
            };
        }

        public static Node CreateUseStatement(string target, string? alias, bool isFilePath)
        {
            return new UseStatementAstNode
            {
                Target = target,
                Alias = alias,
                IsFilePath = isFilePath
            };
        }

        public static Node CreateTypeDeclaration(string declarationKind, string name, string? baseType, IEnumerable<string>? traits = null)
        {
            var node = new TypeDeclarationAstNode
            {
                DeclarationKind = declarationKind,
                DeclarationName = name,
                BaseTypeName = baseType
            };

            if (traits != null)
            {
                foreach (var trait in traits)
                {
                    node.TraitNames.Add(trait);
                }
            }

            return node;
        }

        public static Node CreateEnumDeclaration(string name, IEnumerable<string> members)
        {
            var node = new EnumDeclarationAstNode
            {
                EnumName = name
            };
            foreach (var member in members)
            {
                node.EnumMembers.Add(member);
            }
            return node;
        }

        public static Node CreateRecordDeclaration(string name, int? packSize, IEnumerable<string> members)
        {
            var node = new RecordDeclarationAstNode
            {
                RecordName = name,
                RecordPackSize = packSize
            };
            foreach (var member in members)
            {
                node.RecordMembers.Add(member);
            }
            return node;
        }

        public static Node CreatePropertyDeclaration(string name, string? value, string? type, IEnumerable<string>? modifiers = null)
        {
            var node = new PropertyDeclarationAstNode
            {
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
            return new AssignmentStatementAstNode
            {
                AssignmentLeft = left,
                AssignmentRight = right,
                AssignmentOperator = assignmentOperator,
                Left = left,
                Right = right,
                Operator = assignmentOperator
            };
        }

        public static Node CreateFunctionCall(string name, string arguments, ExpressionNode? expression = null)
        {
            return new FunctionCallAstNode
            {
                Name = name,
                Arguments = arguments,
                Expression = expression
            };
        }

        public static Node CreateIfStatement(string condition)
        {
            return new IfStatementAstNode
            {
                IfCondition = condition
            };
        }

        public static Node CreateMatchStatement(string expression)
        {
            return new MatchStatementAstNode
            {
                Expression = expression
            };
        }

        public static Node CreateWhenStatement(string condition)
        {
            return new WhenStatementAstNode
            {
                WhenCondition = condition
            };
        }

        public static Node CreateWhileStatement(string condition)
        {
            return new WhileStatementAstNode
            {
                WhileCondition = condition
            };
        }

        public static Node CreateForStatement(string variable, string container)
        {
            return new ForStatementAstNode
            {
                ForVariable = variable,
                ForContainer = container
            };
        }

        public static Node CreateForAllStatement(string variable, string container)
        {
            return new ForAllStatementAstNode
            {
                ForVariable = variable,
                ForContainer = container
            };
        }

        public static Node CreateRepeatStatement(string expression)
        {
            return new RepeatStatementAstNode
            {
                RepeatExpression = expression
            };
        }

        public static Node CreateHasStatement(string condition)
        {
            return new HasStatementAstNode
            {
                HasCondition = condition
            };
        }

        public static Node CreateHasTraitStatement(string condition, string? traitTypeName = null, string? traitVariableName = null)
        {
            return new HasTraitStatementAstNode
            {
                HasTraitCondition = condition,
                HasTraitTypeName = traitTypeName,
                HasTraitVariableName = traitVariableName
            };
        }

        public static Node CreateFunctionDeclaration(string name, string? parameters, string? returnType, IEnumerable<Node> body, IEnumerable<ParameterInfo> parameterList, IEnumerable<string>? modifiers = null)
        {
            var node = new FunctionDeclarationAstNode
            {
                FunctionDeclarationName = name,
                FunctionDeclarationParameters = parameters,
                FunctionDeclarationReturnType = returnType
            };

            node.FunctionParameterList.AddRange(parameterList);
            node.FunctionBody.AddRange(body);
            if (modifiers != null)
            {
                node.FunctionModifiers.AddRange(modifiers);
            }
            return node;
        }

        public static Node CreateStatement(NodeKind kind, string? value = null)
        {
            return new StatementAstNode(kind)
            {
                StatementValue = value
            };
        }

        public static Node CreateDelegateDeclaration(string name, IEnumerable<ParameterInfo> parameterList)
        {
            var node = new DelegateDeclarationAstNode
            {
                DelegateName = name
            };

            node.DelegateParameterList.AddRange(parameterList);
            return node;
        }
    }
}
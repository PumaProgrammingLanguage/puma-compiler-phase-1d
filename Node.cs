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

        public MatchStatementAstNode()
        {
            Kind = NodeKind.MatchStatement;
        }
    }

    internal sealed class WhenStatementAstNode : Node
    {
        public string? WhenCondition { get; set; }
        public ExpressionNode? WhenExpression { get; set; }

        public WhenStatementAstNode()
        {
            Kind = NodeKind.WhenStatement;
        }
    }

    internal sealed class WhileStatementAstNode : Node
    {
        public string? WhileCondition { get; set; }
        public ExpressionNode? WhileExpression { get; set; }

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

        public ForAllStatementAstNode()
        {
            Kind = NodeKind.ForAllStatement;
        }
    }

    internal sealed class RepeatStatementAstNode : Node
    {
        public string? RepeatExpression { get; set; }
        public ExpressionNode? RepeatExpressionNode { get; set; }

        public RepeatStatementAstNode()
        {
            Kind = NodeKind.RepeatStatement;
        }
    }

    internal sealed class HasStatementAstNode : Node
    {
        public string? HasCondition { get; set; }
        public ExpressionNode? HasExpression { get; set; }

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

        // For TypeDeclaration nodes
        public struct TypeDeclarationNodes
        {
            public TypeDeclarationNodes()
            {
            }

            public string? DeclarationKind { get; set; }
            public string? DeclarationName { get; set; }
            public string? BaseTypeName { get; set; }
            public List<string> TraitNames { get; } = new();
            public List<Node> TypeProperties { get; } = new();
            public List<Node> TypeFunctions { get; } = new();
        }
        public TypeDeclarationNodes TypeDeclarationNode;

        // For EnumDeclaration nodes
        public struct EnumDeclarationNodes
        {
            public EnumDeclarationNodes()
            {
            }

            public string? EnumName { get; set; }
            public List<string> EnumMembers { get; } = new();
        }
        public EnumDeclarationNodes EnumDeclarationNode;

        // For RecordDeclaration nodes
        public struct RecordDeclarationNodes
        {
            public RecordDeclarationNodes()
            {
            }

            public string? RecordName { get; set; }
            public int? RecordPackSize { get; set; }
            public List<string> RecordMembers { get; } = new();
            public Dictionary<string, string> RecordMemberTypes { get; } = new(StringComparer.Ordinal);
        }
        public RecordDeclarationNodes RecordDeclarationNode;

        // For PropertyDeclaration nodes
        public struct PropertyDeclarationNodes
        {
            public PropertyDeclarationNodes()
            {
            }
            public string? PropertyName { get; set; }
            public string? PropertyValue { get; set; }
            public string? PropertyType { get; set; }
            public List<string> PropertyModifiers { get; } = new();
        }
        public PropertyDeclarationNodes PropertyDeclarationNode;

        // For AssignmentStatement nodes
        public struct AssignmentStatementNodes
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
        }
        public AssignmentStatementNodes AssignmentStatementNode;

        // For Section nodes
        public struct SectionNodes
        {
            public SectionNodes()
            {
            }

            public string? SectionName { get; set; }
            public string? SectionParameters { get; set; }
            public List<ParameterInfo> SectionParameterList { get; } = new();
            public int LeadingBlankLines { get; set; }
        }
        public SectionNodes SectionNode;

        // For IfStatement nodes
        public struct IfStatementNodes
        {
            public IfStatementNodes()
            {
            }

            public string? IfCondition { get; set; }
            public ExpressionNode? ConditionExpression { get; set; }
            public List<Node> IfBody { get; } = new();
            public List<Node> ElseBody { get; } = new();
        }
        public IfStatementNodes IfStatementNode;

        // For MatchStatement nodes
        public struct MatchStatementNodes
        {
            public string? Expression { get; set; }
            public ExpressionNode? ExpressionNode { get; set; }
        }
        public MatchStatementNodes MatchStatementNode;

        // For WhenStatement nodes
        public struct WhenStatementNodes
        {
            public string? WhenCondition { get; set; }
            public ExpressionNode? WhenExpression { get; set; }
        }
        public WhenStatementNodes WhenStatementNode;

        // For WhileStatement nodes
        public struct WhileStatementNodes
        {
            public string? WhileCondition { get; set; }
            public ExpressionNode? WhileExpression { get; set; }
        }
        public WhileStatementNodes WhileStatementNode;

        // For ForStatement nodes
        public struct ForStatementNodes
        {
            public string? ForVariable { get; set; }
            public string? ForContainer { get; set; }
            public ExpressionNode? ForContainerExpression { get; set; }
        }
        public ForStatementNodes ForStatementNode;

        // For RepeatStatement nodes
        public struct RepeatStatementNodes
        {
            public string? RepeatExpression { get; set; }
            public ExpressionNode? RepeatExpressionNode { get; set; }
        }
        public RepeatStatementNodes RepeatStatementNode;

        // For HasStatement nodes
        public struct HasStatementNodes
        {
            public string? HasCondition { get; set; }
            public ExpressionNode? HasExpression { get; set; }
        }
        public HasStatementNodes HasStatementNode;

        // For HasTraitStatement nodes
        public struct HasTraitStatementNodes
        {
            public string? HasTraitCondition { get; set; }
            public ExpressionNode? HasTraitExpression { get; set; }
            public string? HasTraitTypeName { get; set; }
            public string? HasTraitVariableName { get; set; }
        }
        public HasTraitStatementNodes HasTraitStatementNode;

        // For FunctionDeclaration nodes
        public struct FunctionDeclarationNodes
        {
            public FunctionDeclarationNodes()
            {
            }

            public string? FunctionDeclarationName { get; set; }
            public string? FunctionDeclarationParameters { get; set; }
            public string? FunctionDeclarationReturnType { get; set; }
            public List<string> FunctionModifiers { get; } = new();
            public List<Node> FunctionBody { get; } = new();
            public List<ParameterInfo> FunctionParameterList { get; } = new();
        }
        public FunctionDeclarationNodes FunctionDeclarationNode;

        // For DelegateDeclaration nodes
        public struct DelegateDeclarationNodes
        {
            public DelegateDeclarationNodes()
            {
            }

            public string? DelegateName { get; set; }
            public List<ParameterInfo> DelegateParameterList { get; } = new();
        }
        public DelegateDeclarationNodes DelegateDeclarationNode;

        // For statement nodes
        public struct StatementNodes
        {
            public StatementNodes()
            {
            }
            public string? StatementValue { get; set; }
            public List<Node> StatementBody { get; } = new();
            public ExpressionNode? StatementExpression { get; set; }
        }
        public StatementNodes StatementNode;

        public Node()
        {
            StatementNode = new StatementNodes();
        }

        public Node(Section section) : this()
        {
            Kind = NodeKind.Section;
            Section = section;
            SectionNode = new SectionNodes
            {
                SectionName = section.ToString()
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
                BaseTypeName = baseType,
                TypeDeclarationNode = new TypeDeclarationNodes
                {
                    DeclarationKind = declarationKind,
                    DeclarationName = name,
                    BaseTypeName = baseType
                }
            };

            if (traits != null)
            {
                foreach (var trait in traits)
                {
                    node.TraitNames.Add(trait);
                    node.TypeDeclarationNode.TraitNames.Add(trait);
                }
            }

            return node;
        }

        public static Node CreateEnumDeclaration(string name, IEnumerable<string> members)
        {
            var node = new Node
            {
                Kind = NodeKind.EnumDeclaration,
                EnumDeclarationNode = new EnumDeclarationNodes
                {
                    EnumName = name
                }
            };
            foreach (var member in members)
            {
                node.EnumDeclarationNode.EnumMembers.Add(member);
            }
            return node;
        }

        public static Node CreateRecordDeclaration(string name, int? packSize, IEnumerable<string> members)
        {
            var node = new Node
            {
                Kind = NodeKind.RecordDeclaration,
                RecordDeclarationNode = new RecordDeclarationNodes
                {
                    RecordName = name,
                    RecordPackSize = packSize
                }
            };
            foreach (var member in members)
            {
                node.RecordDeclarationNode.RecordMembers.Add(member);
            }
            return node;
        }

        public static Node CreatePropertyDeclaration(string name, string? value, string? type, IEnumerable<string>? modifiers = null)
        {
            var node = new PropertyDeclarationAstNode
            {
                PropertyName = name,
                PropertyValue = value,
                PropertyType = type,
                PropertyDeclarationNode = new PropertyDeclarationNodes
                {
                    PropertyName = name,
                    PropertyValue = value,
                    PropertyType = type
                }
            };

            if (modifiers != null)
            {
                node.PropertyModifiers.AddRange(modifiers);
                node.PropertyDeclarationNode.PropertyModifiers.AddRange(modifiers);
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
                Operator = assignmentOperator,
                AssignmentStatementNode = new AssignmentStatementNodes
                {
                    AssignmentLeft = left,
                    AssignmentRight = right,
                    AssignmentOperator = assignmentOperator,
                    Left = left,
                    Right = right,
                    Operator = assignmentOperator
                }
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
                IfCondition = condition,
                IfStatementNode = new IfStatementNodes
                {
                    IfCondition = condition
                }
            };
        }

        public static Node CreateMatchStatement(string expression)
        {
            return new MatchStatementAstNode
            {
                Expression = expression,
                MatchStatementNode = new MatchStatementNodes
                {
                    Expression = expression
                }
            };
        }

        public static Node CreateWhenStatement(string condition)
        {
            return new WhenStatementAstNode
            {
                WhenCondition = condition,
                WhenStatementNode = new WhenStatementNodes
                {
                    WhenCondition = condition
                }
            };
        }

        public static Node CreateWhileStatement(string condition)
        {
            return new WhileStatementAstNode
            {
                WhileCondition = condition,
                WhileStatementNode = new WhileStatementNodes
                {
                    WhileCondition = condition
                }
            };
        }

        public static Node CreateForStatement(string variable, string container)
        {
            return new ForStatementAstNode
            {
                ForVariable = variable,
                ForContainer = container,
                ForStatementNode = new ForStatementNodes
                {
                    ForVariable = variable,
                    ForContainer = container
                }
            };
        }

        public static Node CreateForAllStatement(string variable, string container)
        {
            return new ForAllStatementAstNode
            {
                ForVariable = variable,
                ForContainer = container,
                ForStatementNode = new ForStatementNodes
                {
                    ForVariable = variable,
                    ForContainer = container
                }
            };
        }

        public static Node CreateRepeatStatement(string expression)
        {
            return new RepeatStatementAstNode
            {
                RepeatExpression = expression,
                RepeatStatementNode = new RepeatStatementNodes
                {
                    RepeatExpression = expression
                }
            };
        }

        public static Node CreateHasStatement(string condition)
        {
            return new HasStatementAstNode
            {
                HasCondition = condition,
                HasStatementNode = new HasStatementNodes
                {
                    HasCondition = condition
                }
            };
        }

        public static Node CreateHasTraitStatement(string condition, string? traitTypeName = null, string? traitVariableName = null)
        {
            return new HasTraitStatementAstNode
            {
                HasTraitCondition = condition,
                HasTraitTypeName = traitTypeName,
                HasTraitVariableName = traitVariableName,
                HasTraitStatementNode = new HasTraitStatementNodes
                {
                    HasTraitCondition = condition,
                    HasTraitTypeName = traitTypeName,
                    HasTraitVariableName = traitVariableName
                }
            };
        }

        public static Node CreateFunctionDeclaration(string name, string? parameters, string? returnType, IEnumerable<Node> body, IEnumerable<ParameterInfo> parameterList, IEnumerable<string>? modifiers = null)
        {
            var node = new FunctionDeclarationAstNode
            {
                FunctionDeclarationName = name,
                FunctionDeclarationParameters = parameters,
                FunctionDeclarationReturnType = returnType,
                FunctionDeclarationNode = new FunctionDeclarationNodes
                {
                    FunctionDeclarationName = name,
                    FunctionDeclarationParameters = parameters,
                    FunctionDeclarationReturnType = returnType
                }
            };

            node.FunctionParameterList.AddRange(parameterList);
            node.FunctionBody.AddRange(body);
            node.FunctionDeclarationNode.FunctionParameterList.AddRange(parameterList);
            node.FunctionDeclarationNode.FunctionBody.AddRange(body);
            if (modifiers != null)
            {
                node.FunctionModifiers.AddRange(modifiers);
                node.FunctionDeclarationNode.FunctionModifiers.AddRange(modifiers);
            }
            return node;
        }

        public static Node CreateStatement(NodeKind kind, string? value = null)
        {
            return new StatementAstNode(kind)
            {
                StatementValue = value,
                StatementNode = new StatementNodes
                {
                    StatementValue = value
                }
            };
        }

        public static Node CreateDelegateDeclaration(string name, IEnumerable<ParameterInfo> parameterList)
        {
            var node = new DelegateDeclarationAstNode
            {
                DelegateName = name,
                DelegateDeclarationNode = new DelegateDeclarationNodes
                {
                    DelegateName = name
                }
            };

            node.DelegateParameterList.AddRange(parameterList);
            node.DelegateDeclarationNode.DelegateParameterList.AddRange(parameterList);
            return node;
        }
    }
}
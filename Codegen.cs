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

using System.Text;
using static Puma.Parser;

namespace Puma
{
    internal class Codegen
    {
        public Codegen()
        {
        }

        internal string Generate(List<Node> ast)
        {
            var sb = new StringBuilder();
            var includes = new HashSet<string>(StringComparer.Ordinal);
            var hasWriteLine = ast.Any(n => n.Kind == NodeKind.WriteLine);
            if (hasWriteLine)
            {
                includes.Add("<stdio.h>");
            }

            foreach (var node in ast.Where(n => n.Kind == NodeKind.UseStatement))
            {
                if (node.UseIsFilePath && !string.IsNullOrWhiteSpace(node.UseTarget))
                {
                    includes.Add($"\"{node.UseTarget}\"");
                }
                else if (!string.IsNullOrWhiteSpace(node.UseTarget))
                {
                    includes.Add($"<{node.UseTarget.Replace('.', '/')}>");
                }
            }

            foreach (var include in includes.OrderBy(i => i, StringComparer.Ordinal))
            {
                sb.AppendLine($"#include {include}");
            }

            if (includes.Count > 0)
            {
                sb.AppendLine();
            }

            var typeDeclarations = ast.Where(n => n.Kind == NodeKind.TypeDeclaration).ToList();
            var typeProperties = typeDeclarations.SelectMany(n => n.TypeProperties).ToHashSet();
            var typeFunctions = typeDeclarations.SelectMany(n => n.TypeFunctions).ToHashSet();

            EmitEnums(ast, sb);
            EmitRecords(ast, sb);
            EmitGlobals(ast, sb, typeProperties);
            EmitFunctions(ast, sb, typeFunctions);
            EmitInitializeFinalize(ast, sb);
            EmitMain(ast, sb);
            EmitTypes(typeDeclarations, sb);
            EmitTraits(typeDeclarations, sb);

            if (sb.Length == 0 && ast.Any(n => n.Kind == NodeKind.Section && n.Section == Section.Functions))
            {
                sb.Append("// functions\\n");
            }

            return sb.ToString();
        }

        private static string SectionToString(Section section) => section switch
        {
            Section.Use => "use",
            Section.Module => "module",
            Section.Type => "type",
            Section.Trait => "trait",
            Section.Enums => "enums",
            Section.Records => "records",
            Section.Properties => "properties",
            Section.Start => "start",
            Section.Initialize => "initialize",
            Section.Finalize => "finalize",
            Section.Functions => "functions",
            _ => string.Empty
        };

        private static void EmitEnums(List<Node> ast, StringBuilder sb)
        {
            foreach (var node in ast.Where(n => n.Kind == NodeKind.EnumDeclaration))
            {
                sb.AppendLine($"typedef enum {node.EnumName} {{");
                for (var i = 0; i < node.EnumMembers.Count; i++)
                {
                    var suffix = i == node.EnumMembers.Count - 1 ? string.Empty : ",";
                    sb.AppendLine($"    {node.EnumMembers[i]}{suffix}");
                }
                sb.AppendLine($"}} {node.EnumName};");
                sb.AppendLine();
            }
        }

        private static void EmitRecords(List<Node> ast, StringBuilder sb)
        {
            foreach (var node in ast.Where(n => n.Kind == NodeKind.RecordDeclaration))
            {
                sb.AppendLine($"typedef struct {node.RecordName} {{");
                foreach (var member in node.RecordMembers)
                {
                    var memberType = string.Equals(member, "Name", StringComparison.Ordinal) ? "stdstr" : "int";
                    sb.AppendLine($"    {memberType} {member};");
                }
                sb.AppendLine($"}} {node.RecordName};");
                sb.AppendLine();
            }
        }

        private static void EmitGlobals(List<Node> ast, StringBuilder sb, HashSet<Node> typeProperties)
        {
            foreach (var node in ast.Where(n => n.Kind == NodeKind.PropertyDeclaration && !typeProperties.Contains(n)))
            {
                var (type, value) = InferCTypeAndValue(node.PropertyValue);
                var modifiers = node.PropertyModifiers.Contains("constant") ? "const " : string.Empty;
                sb.AppendLine($"{modifiers}{type} {node.PropertyName} = {value};");
            }

            if (ast.Any(n => n.Kind == NodeKind.PropertyDeclaration && !typeProperties.Contains(n)))
            {
                sb.AppendLine();
            }
        }

        private static void EmitFunctions(List<Node> ast, StringBuilder sb, HashSet<Node> typeFunctions)
        {
            foreach (var node in ast.Where(n => n.Kind == NodeKind.DelegateDeclaration))
            {
                var parameters = string.Join(", ", node.DelegateParameterList.Select(FormatParameter));
                sb.AppendLine($"typedef void (*{node.DelegateName})({parameters});");
            }

            if (ast.Any(n => n.Kind == NodeKind.DelegateDeclaration))
            {
                sb.AppendLine();
            }

            foreach (var node in ast.Where(n => n.Kind == NodeKind.FunctionDeclaration && !typeFunctions.Contains(n)))
            {
                var returnType = MapType(node.FunctionDeclarationReturnType) ?? "void";
                var parameters = string.Join(", ", node.FunctionParameterList.Select(FormatParameter));
                sb.AppendLine($"{returnType} {node.FunctionDeclarationName}({parameters})");
                sb.AppendLine("{");
                EmitStatements(node.FunctionBody, sb, "    ");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        private static void EmitInitializeFinalize(List<Node> ast, StringBuilder sb)
        {
            EmitSectionFunction(ast, sb, Section.Initialize, "initialize");
            EmitSectionFunction(ast, sb, Section.Finalize, "finalize");
        }

        private static void EmitSectionFunction(List<Node> ast, StringBuilder sb, Section section, string name)
        {
            var (index, sectionNode) = FindSection(ast, section);
            if (index < 0 || sectionNode == null)
            {
                return;
            }

            var parameters = string.Join(", ", sectionNode.SectionParameterList.Select(FormatParameter));
            sb.AppendLine($"void {name}({parameters})");
            sb.AppendLine("{");
            var statements = CollectStatements(ast, index + 1);
            EmitStatements(statements, sb, "    ");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private static void EmitMain(List<Node> ast, StringBuilder sb)
        {
            var (index, _) = FindSection(ast, Section.Start);
            if (index < 0)
            {
                return;
            }

            var (initIndex, initSection) = FindSection(ast, Section.Initialize);
            var (finalIndex, finalSection) = FindSection(ast, Section.Finalize);

            sb.AppendLine("int main()");
            sb.AppendLine("{");
            if (initIndex >= 0 && initSection != null)
            {
                sb.AppendLine($"    initialize({FormatArguments(initSection.SectionParameterList)});");
            }
            var statements = CollectStatements(ast, index + 1);
            EmitStatements(statements, sb, "    ");
            if (finalIndex >= 0 && finalSection != null)
            {
                sb.AppendLine($"    finalize({FormatArguments(finalSection.SectionParameterList)});");
            }
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private static void EmitTypes(List<Node> typeDeclarations, StringBuilder sb)
        {
            foreach (var node in typeDeclarations.Where(n => n.DeclarationKind == "type"))
            {
                var name = ToCppQualifiedName(node.DeclarationName) ?? "Type";
                var bases = new List<string>();
                if (!string.IsNullOrWhiteSpace(node.BaseTypeName))
                {
                    var baseName = ToCppQualifiedName(node.BaseTypeName);
                    if (!(string.Equals(baseName, "object", StringComparison.OrdinalIgnoreCase) && node.TraitNames.Count > 0))
                    {
                        bases.Add($"public {baseName}");
                    }
                }

                foreach (var trait in node.TraitNames)
                {
                    bases.Add($"public {ToCppQualifiedName(trait)}");
                }

                var inheritance = bases.Count > 0 ? $" : {string.Join(", ", bases)}" : string.Empty;
                sb.AppendLine($"class {name}{inheritance}");
                sb.AppendLine("{");
                sb.AppendLine("public:");
                EmitTypeProperties(node, sb, "    ");
                EmitTypeFunctions(node, sb, "    ");
                sb.AppendLine("};");
                sb.AppendLine();
            }
        }

        private static void EmitTraits(List<Node> typeDeclarations, StringBuilder sb)
        {
            foreach (var node in typeDeclarations.Where(n => n.DeclarationKind == "trait"))
            {
                var name = ToCppQualifiedName(node.DeclarationName) ?? "Trait";
                sb.AppendLine($"class {name}");
                sb.AppendLine("{");
                sb.AppendLine("public:");
                EmitTypeProperties(node, sb, "    ");
                EmitTypeFunctions(node, sb, "    ");
                sb.AppendLine("};");
                sb.AppendLine();
            }
        }

        private static void EmitTypeProperties(Node node, StringBuilder sb, string indent)
        {
            foreach (var property in node.TypeProperties)
            {
                var type = MapType(property.PropertyType) ?? InferCTypeAndValue(property.PropertyValue).Type;
                var value = property.PropertyValue ?? InferCTypeAndValue(property.PropertyValue).Value;
                var modifiers = property.PropertyModifiers.Contains("constant") ? "const " : string.Empty;
                sb.AppendLine($"{indent}{modifiers}{type} {property.PropertyName} = {value};");
            }
        }

        private static void EmitTypeFunctions(Node node, StringBuilder sb, string indent)
        {
            foreach (var function in node.TypeFunctions)
            {
                var returnType = MapType(function.FunctionDeclarationReturnType) ?? "void";
                var parameters = string.Join(", ", function.FunctionParameterList.Select(FormatParameter));
                sb.AppendLine($"{indent}{returnType} {function.FunctionDeclarationName}({parameters})");
                sb.AppendLine($"{indent}{{");
                EmitStatements(function.FunctionBody, sb, indent + "    ");
                sb.AppendLine($"{indent}}}");
            }
        }

        private static void EmitStatements(List<Node> statements, StringBuilder sb, string indent)
        {
            for (var i = 0; i < statements.Count; i++)
            {
                var node = statements[i];
                switch (node.Kind)
                {
                    case NodeKind.AssignmentStatement:
                    {
                        var leftExpression = GenerateExpression(node.AssignmentLeftExpression, node.AssignmentLeft);
                        var rightExpression = GenerateExpression(node.AssignmentRightExpression, node.AssignmentRight);
                        if (node.AssignmentRightExpression == null
                            && !string.IsNullOrWhiteSpace(node.AssignmentRight)
                            && node.AssignmentRight.Contains('(')
                            && node.AssignmentRight.Contains(')'))
                        {
                            rightExpression = node.AssignmentRight;
                        }

                        if (node.IsLoweredPostfixMutation && (node.AssignmentOperator == "+=" || node.AssignmentOperator == "-="))
                        {
                            var op = node.AssignmentOperator == "+=" ? "++" : "--";
                            sb.AppendLine($"{indent}{leftExpression}{op};");
                            break;
                        }

                        if (node.AssignmentOperator == "="
                            && node.AssignmentLeftExpression?.Kind == ExpressionKind.Binary && node.AssignmentLeftExpression.Value == ","
                            && node.AssignmentRightExpression?.Kind == ExpressionKind.Binary && node.AssignmentRightExpression.Value == ",")
                        {
                            var left0 = GenerateExpression(node.AssignmentLeftExpression.Left, null);
                            var left1 = GenerateExpression(node.AssignmentLeftExpression.Right, null);
                            var right0 = GenerateExpression(node.AssignmentRightExpression.Left, null);
                            var right1 = GenerateExpression(node.AssignmentRightExpression.Right, null);
                            sb.AppendLine($"{indent}{left0} = {right0};");
                            sb.AppendLine($"{indent}{left1} = {right1});");
                            break;
                        }

                        if (node.AssignmentOperator == "=" && node.AssignmentRightExpression?.Kind == ExpressionKind.Binary)
                        {
                            rightExpression = UnwrapOutermostParentheses(rightExpression);
                        }

                        if (node.AssignmentOperator == "=" && node.AssignmentRightExpression?.Kind == ExpressionKind.Conditional)
                        {
                            var allConditionalAssignments = statements.Count > 1
                                && statements.All(s => s.Kind == NodeKind.AssignmentStatement
                                    && s.AssignmentRightExpression?.Kind == ExpressionKind.Conditional);

                            if (allConditionalAssignments && i == 0)
                            {
                                rightExpression = $"({rightExpression}";
                            }

                            if (allConditionalAssignments && i == statements.Count - 1)
                            {
                                rightExpression = $"{rightExpression})";
                            }
                        }

                        sb.AppendLine($"{indent}{leftExpression} {node.AssignmentOperator} {rightExpression};");
                        break;
                    }
                    case NodeKind.FunctionCall:
                    {
                        var callExpressionNode = node.FunctionCallExpression ?? node.StatementExpression;
                        var callExpression = GenerateExpression(callExpressionNode, null);
                        if (!string.IsNullOrWhiteSpace(callExpression) && callExpressionNode?.Kind == ExpressionKind.Call)
                        {
                            sb.AppendLine($"{indent}{callExpression};");
                        }
                        else
                        {
                            sb.AppendLine($"{indent}{node.FunctionName}({node.FunctionArguments});");
                        }
                        break;
                    }
                    case NodeKind.WriteLine:
                        if (!string.IsNullOrWhiteSpace(node.StringValue))
                        {
                            sb.AppendLine($"{indent}puts({node.StringValue});");
                        }
                        break;
                    case NodeKind.IfStatement:
                        sb.AppendLine($"{indent}if ({UnwrapOutermostParentheses(GenerateExpression(node.ConditionExpression, node.IfCondition))})");
                        sb.AppendLine($"{indent}{{");
                        EmitStatements(node.StatementBody, sb, indent + "    ");
                        sb.AppendLine($"{indent}}}");
                        if (node.ElseBody.Count > 0)
                        {
                            sb.AppendLine($"{indent}else");
                            sb.AppendLine($"{indent}{{");
                            EmitStatements(node.ElseBody, sb, indent + "    ");
                            sb.AppendLine($"{indent}}}");
                        }
                        break;
                    case NodeKind.MatchStatement:
                        sb.AppendLine($"{indent}switch ({GenerateExpression(node.MatchExpressionNode, node.MatchExpression)})");
                        sb.AppendLine($"{indent}{{");
                        foreach (var when in node.StatementBody.Where(n => n.Kind == NodeKind.WhenStatement))
                        {
                            sb.AppendLine($"{indent}    case {GenerateExpression(when.WhenExpression, when.WhenCondition)}:");
                            EmitStatements(when.StatementBody, sb, indent + "        ");
                            sb.AppendLine($"{indent}        break;");
                        }
                        sb.AppendLine($"{indent}}}");
                        break;
                    case NodeKind.WhenStatement:
                        sb.AppendLine($"{indent}/* when {GenerateExpression(node.WhenExpression, node.WhenCondition)} */");
                        break;
                    case NodeKind.WhileStatement:
                        sb.AppendLine($"{indent}while ({UnwrapOutermostParentheses(GenerateExpression(node.WhileExpression, node.WhileCondition))})");
                        sb.AppendLine($"{indent}{{");
                        EmitStatements(node.StatementBody, sb, indent + "    ");
                        sb.AppendLine($"{indent}}}");
                        break;
                    case NodeKind.ForStatement:
                    case NodeKind.ForAllStatement:
                        sb.AppendLine($"{indent}for (auto {node.ForVariable} : {GenerateExpression(node.ForContainerExpression, node.ForContainer)})");
                        sb.AppendLine($"{indent}{{");
                        EmitStatements(node.StatementBody, sb, indent + "    ");
                        sb.AppendLine($"{indent}}}");
                        break;
                    case NodeKind.RepeatStatement:
                        {
                            var repeatCondition = GenerateExpression(node.RepeatExpressionNode, node.RepeatExpression) ?? "1";
                            sb.AppendLine($"{indent}do");
                            sb.AppendLine($"{indent}{{");
                            EmitStatements(node.StatementBody, sb, indent + "    ");
                            sb.AppendLine($"{indent}}} while ({repeatCondition});");
                            break;
                        }
                    case NodeKind.HasStatement:
                        sb.AppendLine($"{indent}if ({GenerateExpression(node.HasExpression, node.HasCondition)} != nullptr)");
                        sb.AppendLine($"{indent}{{");
                        EmitStatements(node.StatementBody, sb, indent + "    ");
                        sb.AppendLine($"{indent}}}");
                        break;
                    case NodeKind.HasTraitStatement:
                    {
                        var variable = node.HasTraitVariableName ?? GenerateExpression(node.HasTraitExpression, node.HasTraitCondition);
                        var traitType = node.HasTraitTypeName ?? "Trait";
                        sb.AppendLine($"{indent}if ({variable} != null && typeof({variable}) == typeof({traitType}))");
                        sb.AppendLine($"{indent}{{");
                        EmitStatements(node.StatementBody, sb, indent + "    ");
                        sb.AppendLine($"{indent}}}");
                        break;
                    }
                    case NodeKind.ReturnStatement:
                        sb.AppendLine($"{indent}return {UnwrapOutermostParentheses(GenerateExpression(node.StatementExpression, node.StatementValue))};");
                        break;
                    case NodeKind.YieldStatement:
                        sb.AppendLine($"{indent}/* yield {GenerateExpression(node.StatementExpression, node.StatementValue)} */");
                        break;
                    case NodeKind.BreakStatement:
                        sb.AppendLine($"{indent}break;");
                        break;
                    case NodeKind.ContinueStatement:
                        sb.AppendLine($"{indent}continue;");
                        break;
                    case NodeKind.ErrorStatement:
                        sb.AppendLine($"{indent}/* error {GenerateExpression(node.StatementExpression, node.StatementValue)} */");
                        break;
                    case NodeKind.CatchStatement:
                        sb.AppendLine($"{indent}/* catch {GenerateExpression(node.StatementExpression, node.StatementValue)} */");
                        break;
                }
            }
        }

        private static string? GenerateExpression(ExpressionNode? node, string? fallback)
        {
            if (node == null)
            {
                return fallback;
            }

            return node.Kind switch
            {
                ExpressionKind.Identifier => node.Value ?? string.Empty,
                ExpressionKind.Literal => node.Value ?? string.Empty,
                ExpressionKind.Unary => $"{node.Value}{GenerateExpression(node.Left, null)}",
                ExpressionKind.Cast => $"({node.Value}){GenerateExpression(node.Left, null)}",
                ExpressionKind.Conditional => $"({GenerateExpression(node.Left, null)} ? {GenerateExpression(node.Right, null)} : {GenerateExpression(node.Arguments.FirstOrDefault(), null)})",
                ExpressionKind.Binary => $"({GenerateExpression(node.Left, null)} {node.Value} {GenerateExpression(node.Right, null)})",
                ExpressionKind.MemberAccess => $"{GenerateExpression(node.Left, null)}.{node.Value}",
                ExpressionKind.Index => $"{GenerateExpression(node.Left, null)}[{GenerateExpression(node.Right, null)}]",
                ExpressionKind.Call => $"{GenerateExpression(node.Left, null)}({string.Join(", ", node.Arguments.Select(a => GenerateExpression(a, null)))})",
                _ => fallback ?? string.Empty
            };
        }

        private static string FormatParameter(Node.ParameterInfo parameter)
        {
            var type = MapType(parameter.Type) ?? "int64_t";
            return string.IsNullOrWhiteSpace(parameter.Name) ? type : $"{type} {parameter.Name}";
        }

        private static string FormatArguments(List<Node.ParameterInfo> parameters)
        {
            if (parameters.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(", ", parameters.Select(FormatDefaultArgument));
        }

        private static string FormatDefaultArgument(Node.ParameterInfo parameter)
        {
            if (!string.IsNullOrWhiteSpace(parameter.DefaultValue))
            {
                return parameter.DefaultValue;
            }

            var type = MapType(parameter.Type) ?? "int64_t";
            return type switch
            {
                "stdstr" => "\"\"",
                "bool_t" => "false",
                _ => "0"
            };
        }

        private static string? MapType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return null;
            }

            return type switch
            {
                "int" or "int64" => "int64_t",
                "int32" => "int32_t",
                "int16" => "int16_t",
                "int8" => "int8_t",
                "uint" or "uint64" => "uint64_t",
                "uint32" => "uint32_t",
                "uint16" => "uint16_t",
                "uint8" => "uint8_t",
                "flt" or "flt64" => "double",
                "flt32" => "float",
                "fix" or "fix64" => "int64_t",
                "fix32" => "int32_t",
                "bool" => "bool_t",
                "char" => "uint8[4]",
                "str" => "stdstr",
                _ => type
            };
        }

        private static bool UsesBool(Node node)
        {
            if (node.Kind == NodeKind.PropertyDeclaration && string.Equals(node.PropertyValue, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (node.Kind == NodeKind.PropertyDeclaration && string.Equals(node.PropertyValue, "false", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return node.FunctionParameterList.Any(p => string.Equals(p.Type, "bool", StringComparison.OrdinalIgnoreCase))
                || node.DelegateParameterList.Any(p => string.Equals(p.Type, "bool", StringComparison.OrdinalIgnoreCase));
        }

        private static string? ToCppQualifiedName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return name.Replace(".", "::", StringComparison.Ordinal);
        }

        private static (string Type, string Value) InferCTypeAndValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ("int64_t", "0");
            }

            if (value.StartsWith("\"", StringComparison.Ordinal))
            {
                return ("stdstr", value);
            }

            if (bool.TryParse(value, out _))
            {
                return ("bool_t", value.ToLowerInvariant());
            }

            if (int.TryParse(value, out _))
            {
                return ("int64_t", value);
            }

            if (double.TryParse(value, out _))
            {
                return ("double", value);
            }

            return (value, "{0}");
        }

        private static string UnwrapOutermostParentheses(string? expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return expression ?? string.Empty;
            }

            var trimmed = expression.Trim();
            if (trimmed.Length < 2 || trimmed[0] != '(' || trimmed[^1] != ')')
            {
                return trimmed;
            }

            var depth = 0;
            for (var i = 0; i < trimmed.Length; i++)
            {
                var ch = trimmed[i];
                if (ch == '(')
                {
                    depth++;
                }
                else if (ch == ')')
                {
                    depth--;
                }

                if (depth == 0 && i < trimmed.Length - 1)
                {
                    return trimmed;
                }
            }

            return trimmed[1..^1];
        }

        private static (int Index, Node? SectionNode) FindSection(List<Node> ast, Section section)
        {
            for (int i = 0; i < ast.Count; i++)
            {
                if (ast[i].Kind == NodeKind.Section && ast[i].Section == section)
                {
                    return (i, ast[i]);
                }
            }

            return (-1, null);
        }

        private static List<Node> CollectStatements(List<Node> ast, int startIndex)
        {
            var statements = new List<Node>();
            for (int i = startIndex; i < ast.Count; i++)
            {
                if (ast[i].Kind == NodeKind.Section)
                {
                    break;
                }

                statements.Add(ast[i]);
            }

            return statements;
        }
    }
}
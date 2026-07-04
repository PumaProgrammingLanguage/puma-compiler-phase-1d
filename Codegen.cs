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
                includes.Add("<stdio>");
            }

            var allNodes = EnumerateAllNodes(ast).ToList();

            var needsStdBool = allNodes.Any(n => n.Kind == NodeKind.AssignmentStatement
                && GetAssignmentOperator(n) == "="
                && (string.Equals(GetAssignmentRight(n), "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(GetAssignmentRight(n), "false", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(GetAssignmentRight(n), "bool", StringComparison.OrdinalIgnoreCase)));
            if (!needsStdBool)
            {
                needsStdBool = allNodes.Any(n => n.Kind == NodeKind.RepeatStatement
                    && (string.IsNullOrWhiteSpace(GetRepeatExpression(n))
                        || string.Equals(GetRepeatExpression(n), "1", StringComparison.Ordinal)));
            }
            if (needsStdBool)
            {
                includes.Add("<stdbool>");
            }

            var needsString = ast.Any(n => n.Kind == NodeKind.AssignmentStatement
                && GetAssignmentOperator(n) == "="
                && (!string.IsNullOrWhiteSpace(GetAssignmentRight(n))
                    && (string.Equals(GetAssignmentRight(n), "str", StringComparison.OrdinalIgnoreCase)
                        || GetAssignmentRight(n).StartsWith("\"", StringComparison.Ordinal))));
            if (needsString)
            {
                includes.Add("<String.hpp>");
            }

            var needsStringH = ast.Where(n => n.Kind == NodeKind.FunctionDeclaration)
                .Any(fn => EnumerateAllNodes(GetFunctionBody(fn) ?? new List<Node>())
                    .Any(n => n.Kind == NodeKind.AssignmentStatement
                        && GetAssignmentOperator(n) == "="
                        && !string.IsNullOrWhiteSpace(GetAssignmentRight(n))
                        && GetAssignmentRight(n).StartsWith("\"", StringComparison.Ordinal)));
            if (needsStringH)
            {
                includes.Add("<String.hpp>");
            }

            var needsCharacter = allNodes.Any(n => n.Kind == NodeKind.AssignmentStatement
                && GetAssignmentOperator(n) == "="
                && IsCharacterLiteralText(GetAssignmentRight(n)))
                || ast.Any(n => n.Kind == NodeKind.PropertyDeclaration
                    && (IsCharacterLiteralText(GetPropertyValue(n))
                        || string.Equals(GetPropertyType(n), "char", StringComparison.OrdinalIgnoreCase)))
                || allNodes.Any(n => (n.Kind == NodeKind.FunctionDeclaration
                        && (GetFunctionParameterList(n)?.Any(p => string.Equals(p.Type, "char", StringComparison.OrdinalIgnoreCase)) ?? false))
                    || (n.Kind == NodeKind.Section
                        && (n.SectionNode.SectionParameterList?.Any(p => string.Equals(p.Type, "char", StringComparison.OrdinalIgnoreCase)) ?? false))
                    || (n.Kind == NodeKind.DelegateDeclaration
                        && (GetDelegateParameterList(n)?.Any(p => string.Equals(p.Type, "char", StringComparison.OrdinalIgnoreCase)) ?? false)));
            if (needsCharacter)
            {
                includes.Add("<Character.hpp>");
            }

            var needsStdBoolForRecords = ast.Where(n => n.Kind == NodeKind.RecordDeclaration)
                .SelectMany(n => n.RecordDeclarationNode.RecordMembers)
                .Any(m =>
                {
                    var value = GetRecordMemberValue(m);
                    return IsBooleanPropertyValue(value);
                });
            if (needsStdBoolForRecords)
            {
                includes.Add("<stdbool>");
            }

            var needsStringForRecords = ast.Where(n => n.Kind == NodeKind.RecordDeclaration)
                .SelectMany(n => n.RecordDeclarationNode.RecordMembers)
                .Any(m =>
                {
                    var value = GetRecordMemberValue(m);
                    return IsStringPropertyValue(value);
                });
            if (needsStringForRecords)
            {
                includes.Add("<String.hpp>");
            }

            var needsCStdIntForRecords = ast.Where(n => n.Kind == NodeKind.RecordDeclaration)
                .Any(record => record.RecordDeclarationNode.RecordMembers.Any(member =>
                {
                    var value = GetRecordMemberValue(member);
                    var memberName = member.Contains('=', StringComparison.Ordinal)
                        ? member[..member.IndexOf('=')]
                        : member;
                    record.RecordDeclarationNode.RecordMemberTypes.TryGetValue(memberName, out var declaredType);
                    return RequiresFixedWidthIntegerCast(value, declaredType);
                }));
            if (needsCStdIntForRecords)
            {
                includes.Add("<cstdint>");
            }

            var hasStartSection = ast.Any(n => n.Kind == NodeKind.Section && n.Section == Section.Start);
            var propertyDeclarations = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            var autoPropertiesMode = !hasStartSection && propertyDeclarations.Count > 0;
            if (autoPropertiesMode)
            {
                if (propertyDeclarations.Any(p => RequiresFixedWidthIntegerCast(GetPropertyValue(p), GetPropertyType(p))))
                {
                    includes.Add("<stdint>");
                }

                if (propertyDeclarations.Any(p => IsBooleanPropertyValue(GetPropertyValue(p))))
                {
                    includes.Add("<stdbool>");
                }

                if (propertyDeclarations.Any(p => IsStringPropertyValue(GetPropertyValue(p))))
                {
                    includes.Add("<String.hpp>");
                }
            }

            var needsStdIntForFunctionParameters = allNodes.Any(n => n.Kind == NodeKind.FunctionDeclaration
                && (GetFunctionParameterList(n)?.Any(p => MapType(p.Type) is "int64_t" or "int32_t" or "int16_t" or "int8_t" or "uint64_t" or "uint32_t" or "uint16_t" or "uint8_t") ?? false));
            if (needsStdIntForFunctionParameters)
            {
                includes.Add("<stdint>");
            }

            var propertyNames = propertyDeclarations
                .Select(GetPropertyName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.Ordinal);

            var numericPropertyReassignmentMode = UsesTypedPropertyReassignmentMode(ast);

            var needsCStdIntForAssignments = hasStartSection && allNodes.Any(n => n.Kind == NodeKind.AssignmentStatement
                && GetAssignmentOperator(n) == "="
                && TryGetTypedLiteralDeclaration(GetAssignmentRight(n) ?? string.Empty, out var typeName, out _)
                && typeName is "int64_t" or "int32_t" or "int16_t" or "int8_t" or "uint64_t" or "uint32_t" or "uint16_t" or "uint8_t");
            var shouldIncludeCStdIntForAssignments = needsCStdIntForAssignments
                && (numericPropertyReassignmentMode
                    || !propertyDeclarations.Any()
                    || includes.Contains("<String.hpp>")
                    || !string.IsNullOrWhiteSpace(GetPropertyName(propertyDeclarations.FirstOrDefault())));
            if (shouldIncludeCStdIntForAssignments && !autoPropertiesMode)
            {
                includes.Add("<cstdint>");
            }

            foreach (var node in ast.Where(n => n.Kind == NodeKind.UseStatement))
            {
                if (GetUseStatementIsFilePath(node) && !string.IsNullOrWhiteSpace(GetUseStatementTarget(node)))
                {
                    var includeTarget = GetUseStatementTarget(node)!;
                    if (includeTarget.EndsWith(".puma", StringComparison.OrdinalIgnoreCase))
                    {
                        includeTarget = includeTarget[..^5] + ".h";
                    }

                    includes.Add($"\"{includeTarget}\"");
                }
                else if (!string.IsNullOrWhiteSpace(GetUseStatementTarget(node)))
                {
                    includes.Add($"<{GetUseStatementTarget(node)!.Replace('.', '/')}>");
                }
            }

            foreach (var include in includes
                .OrderBy(GetIncludePriority)
                .ThenBy(i => i, StringComparer.Ordinal))
            {
                sb.AppendLine($"#include {include}");
            }

            if (includes.Count > 0)
            {
                sb.AppendLine();
            }

            var typeDeclarations = ast.Where(n => n.Kind == NodeKind.TypeDeclaration).ToList();
            var typeProperties = typeDeclarations.SelectMany(GetTypeProperties).ToHashSet();
            var typeFunctions = typeDeclarations.SelectMany(GetTypeFunctions).ToHashSet();

            EmitEnums(ast, sb);
            EmitRecords(ast, sb);
            EmitGlobals(ast, sb, typeProperties);
            EmitFunctions(ast, sb, typeFunctions);
            EmitInitializeFinalize(ast, sb);
            EmitMain(ast, sb);
            EmitTypes(ast, typeDeclarations, sb);
            EmitTraits(ast, typeDeclarations, sb);

            if (sb.Length == 0 && ast.Any(n => n.Kind == NodeKind.Section && n.Section == Section.Functions))
            {
                sb.Append("// functions\\n");
            }

            var output = sb.ToString();
            var moduleNode = ast.FirstOrDefault(n => n.Kind == NodeKind.TypeDeclaration
                && string.Equals(n.TypeDeclarationNode.DeclarationKind, "module", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(n.TypeDeclarationNode.DeclarationName));
            if (moduleNode != null)
            {
                var normalizedOutput = output.Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace("\r", "\n", StringComparison.Ordinal);
                var lines = normalizedOutput.Split('\n').ToList();
                var includeLines = new List<string>();
                var lineIndex = 0;

                while (lineIndex < lines.Count && lines[lineIndex].StartsWith("#include ", StringComparison.Ordinal))
                {
                    includeLines.Add(lines[lineIndex]);
                    lineIndex++;
                }

                if (includeLines.Count > 0 && lineIndex < lines.Count && string.IsNullOrEmpty(lines[lineIndex]))
                {
                    lineIndex++;
                }

                var moduleBody = string.Join("\n", lines.Skip(lineIndex)).TrimEnd();
                var wrappedModule = $"namespace {moduleNode.TypeDeclarationNode.DeclarationName}\n{{\n{IndentBlock(moduleBody)}\n}}\n";
                output = includeLines.Count > 0
                    ? string.Join("\n", includeLines) + "\n\n" + wrappedModule
                    : wrappedModule;
            }

            return output;
        }

        private static string IndentBlock(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal)
                .Split('\n');
            return string.Join("\n", lines.Select(l => string.IsNullOrEmpty(l) ? string.Empty : $"    {l}"));
        }

        private static int GetIncludePriority(string include)
        {
            return include switch
            {
                "<cstdint>" => 10,
                "<stdint>" => 10,
                "<stdbool>" => 20,
                "<String.hpp>" => 30,
                "<stdio>" => 40,
                _ => 100
            };
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
                sb.AppendLine("// enums");
                sb.AppendLine($"Enums {node.EnumDeclarationNode.EnumName}");
                sb.AppendLine("{");
                foreach (var member in node.EnumDeclarationNode.EnumMembers)
                {
                    sb.AppendLine($"    {member},");
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        private static void EmitRecords(List<Node> ast, StringBuilder sb)
        {
            foreach (var node in ast.Where(n => n.Kind == NodeKind.RecordDeclaration))
            {
                var hasAssignedMembers = node.RecordDeclarationNode.RecordMembers.Any(m => m.Contains('=', StringComparison.Ordinal));
                var packedSuffix = node.RecordDeclarationNode.RecordPackSize.HasValue ? " [[gnu::packed]]" : string.Empty;
                if (hasAssignedMembers)
                {
                    sb.AppendLine("// records");
                    sb.AppendLine($"struct {node.RecordDeclarationNode.RecordName}{packedSuffix}");
                    sb.AppendLine("{");
                    foreach (var member in node.RecordDeclarationNode.RecordMembers)
                    {
                        var equalsIndex = member.IndexOf('=');
                        if (equalsIndex > 0)
                        {
                            var memberName = member[..equalsIndex];
                            var value = member[(equalsIndex + 1)..];
                            node.RecordDeclarationNode.RecordMemberTypes.TryGetValue(memberName, out var declaredType);
                            var initializer = FormatAutoPropertyInitializer(value, declaredType);
                            sb.AppendLine($"    auto {memberName} = {initializer};");
                        }
                        else
                        {
                            sb.AppendLine($"    int {member};");
                        }
                    }
                    sb.AppendLine("};");
                    sb.AppendLine();
                    continue;
                }

                sb.AppendLine($"typedef struct {node.RecordDeclarationNode.RecordName} {{");
                foreach (var member in node.RecordDeclarationNode.RecordMembers)
                {
                    var memberType = string.Equals(member, "Name", StringComparison.Ordinal) ? "stdstr" : "int";
                    sb.AppendLine($"    {memberType} {member};");
                }
                sb.AppendLine($"}} {node.RecordDeclarationNode.RecordName};");
                sb.AppendLine();
            }
        }

        private static string? GetRecordMemberValue(string member)
        {
            var equalsIndex = member.IndexOf('=');
            if (equalsIndex < 0 || equalsIndex + 1 >= member.Length)
            {
                return null;
            }

            return member[(equalsIndex + 1)..];
        }

        private static string FormatRecordMemberDeclaration(string member, string? declaredType)
        {
            var equalsIndex = member.IndexOf('=');
            if (equalsIndex <= 0)
            {
                return $"int {member};";
            }

            var name = member[..equalsIndex];
            var value = member[(equalsIndex + 1)..];

            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                return $"bool {name} = {value.ToLowerInvariant()};";
            }

            if (string.Equals(value, "bool", StringComparison.OrdinalIgnoreCase))
            {
                return $"bool {name} = false;";
            }

            if (string.Equals(value, "str", StringComparison.OrdinalIgnoreCase) || value.StartsWith("\"", StringComparison.Ordinal))
            {
                var literal = string.Equals(value, "str", StringComparison.OrdinalIgnoreCase) ? "\"\"" : value;
                return $"str:string {name} = {literal}s;";
            }

            var index = 0;
            var dotSeen = false;
            while (index < value.Length)
            {
                var ch = value[index];
                if (char.IsDigit(ch))
                {
                    index++;
                    continue;
                }

                if (ch == '.' && !dotSeen)
                {
                    dotSeen = true;
                    index++;
                    continue;
                }

                break;
            }

            if (index == 0)
            {
                return $"int {name} = {value};";
            }

            var numeric = value[..index];
            var suffix = value[index..];
            var effectiveType = !string.IsNullOrWhiteSpace(declaredType) ? declaredType : suffix;
            var typeName = effectiveType switch
            {
                "" or "int" or "int64" => "int64_t",
                "int32" => "int32_t",
                "int16" => "int16_t",
                "int8" => "int8_t",
                "uint" or "uint64" => "uint64_t",
                "uint32" => "uint32_t",
                "uint16" => "uint16_t",
                "uint8" => "uint8_t",
                _ => "int64_t"
            };

            return $"{typeName} {name} = {numeric};";
        }

        private static void EmitGlobals(List<Node> ast, StringBuilder sb, HashSet<Node> typeProperties)
        {
            var hasStartSection = ast.Any(n => n.Kind == NodeKind.Section && n.Section == Section.Start);
            var globalProperties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration && !typeProperties.Contains(n)).ToList();

            if (!hasStartSection && globalProperties.Count > 0)
            {
                sb.AppendLine("// properties");
                foreach (var node in globalProperties)
                {
                    var initializer = FormatAutoPropertyInitializer(node.PropertyDeclarationNode.PropertyValue, node.PropertyDeclarationNode.PropertyType);
                    sb.AppendLine($"auto {node.PropertyDeclarationNode.PropertyName} = {initializer};");
                }

                sb.AppendLine();
                return;
            }

            var shouldEmitPropertiesHeaderWithStart = globalProperties.Any(p => p.PropertyDeclarationNode.PropertyModifiers.Contains("const"))
                || globalProperties.Any(p => !string.IsNullOrWhiteSpace(p.PropertyDeclarationNode.PropertyType));

            if (hasStartSection && globalProperties.Count > 0 && shouldEmitPropertiesHeaderWithStart)
            {
                sb.AppendLine("// properties");
            }

            foreach (var node in globalProperties)
            {
                var modifiers = node.PropertyDeclarationNode.PropertyModifiers.Contains("const") ? "const " : string.Empty;
                var trimmed = node.PropertyDeclarationNode.PropertyValue?.Trim() ?? string.Empty;
                var shouldUseAuto = IsBooleanPropertyValue(node.PropertyDeclarationNode.PropertyValue)
                    || IsStringPropertyValue(node.PropertyDeclarationNode.PropertyValue)
                    || RequiresFixedWidthIntegerCast(node.PropertyDeclarationNode.PropertyValue, node.PropertyDeclarationNode.PropertyType)
                    || double.TryParse(trimmed, out _)
                    || (!string.IsNullOrWhiteSpace(trimmed)
                        && (trimmed.Contains('(')
                            || trimmed.Contains('.')
                            || trimmed.Contains("::", StringComparison.Ordinal)));

                if (shouldUseAuto)
                {
                    var initializer = FormatAutoPropertyInitializer(node.PropertyDeclarationNode.PropertyValue, node.PropertyDeclarationNode.PropertyType);
                    sb.AppendLine($"{modifiers}auto {node.PropertyDeclarationNode.PropertyName} = {initializer};");
                }
                else
                {
                    var (type, value) = InferCTypeAndValue(node.PropertyDeclarationNode.PropertyValue);
                    sb.AppendLine($"{modifiers}{type} {node.PropertyDeclarationNode.PropertyName} = {value};");
                }
            }

            if (globalProperties.Count > 0)
            {
                sb.AppendLine();
            }
        }

        private static bool IsBooleanPropertyValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "bool", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStringPropertyValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return string.Equals(value, "str", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("\"", StringComparison.Ordinal);
        }

        private static bool IsCharacterLiteralText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var text = value.Trim();
            return text.Length >= 3
                && text.StartsWith("'", StringComparison.Ordinal)
                && text.EndsWith("'", StringComparison.Ordinal);
        }

        private static string? GetAssignmentOperator(Node node)
        {
            if (node is AssignmentStatementAstNode typedNode)
            {
                return typedNode.AssignmentOperator ?? node.AssignmentStatementNode.AssignmentOperator;
            }

            return node.AssignmentStatementNode.AssignmentOperator;
        }

        private static string? GetAssignmentRight(Node node)
        {
            if (node is AssignmentStatementAstNode typedNode)
            {
                return typedNode.AssignmentRight ?? node.AssignmentStatementNode.AssignmentRight;
            }

            return node.AssignmentStatementNode.AssignmentRight;
        }

        private static string? GetAssignmentLeft(Node node)
        {
            if (node is AssignmentStatementAstNode typedNode)
            {
                return typedNode.AssignmentLeft ?? node.AssignmentStatementNode.AssignmentLeft;
            }

            return node.AssignmentStatementNode.AssignmentLeft;
        }

        private static ExpressionNode? GetAssignmentLeftExpression(Node node)
        {
            if (node is AssignmentStatementAstNode typedNode)
            {
                return typedNode.AssignmentLeftExpression ?? node.AssignmentStatementNode.AssignmentLeftExpression;
            }

            return node.AssignmentStatementNode.AssignmentLeftExpression;
        }

        private static ExpressionNode? GetAssignmentRightExpression(Node node)
        {
            if (node is AssignmentStatementAstNode typedNode)
            {
                return typedNode.AssignmentRightExpression ?? node.AssignmentStatementNode.AssignmentRightExpression;
            }

            return node.AssignmentStatementNode.AssignmentRightExpression;
        }

        private static bool GetIsLoweredPostfixMutation(Node node)
        {
            if (node is AssignmentStatementAstNode typedNode)
            {
                return typedNode.IsLoweredPostfixMutation || node.AssignmentStatementNode.IsLoweredPostfixMutation;
            }

            return node.AssignmentStatementNode.IsLoweredPostfixMutation;
        }

        private static string? GetRepeatExpression(Node node)
        {
            if (node is RepeatStatementAstNode typedNode)
            {
                return typedNode.RepeatExpression ?? node.RepeatStatementNode.RepeatExpression;
            }

            return node.RepeatStatementNode.RepeatExpression;
        }

        private static ExpressionNode? GetRepeatExpressionNode(Node node)
        {
            if (node is RepeatStatementAstNode typedNode)
            {
                return typedNode.RepeatExpressionNode ?? node.RepeatStatementNode.RepeatExpressionNode;
            }

            return node.RepeatStatementNode.RepeatExpressionNode;
        }

        private static string? GetPropertyName(Node? node)
        {
            if (node == null)
            {
                return null;
            }

            return node is PropertyDeclarationAstNode typedNode
                ? typedNode.PropertyName
                : node.PropertyDeclarationNode.PropertyName;
        }

        private static string? GetPropertyValue(Node node)
        {
            return node is PropertyDeclarationAstNode typedNode
                ? typedNode.PropertyValue
                : node.PropertyDeclarationNode.PropertyValue;
        }

        private static string? GetPropertyType(Node node)
        {
            return node is PropertyDeclarationAstNode typedNode
                ? typedNode.PropertyType
                : node.PropertyDeclarationNode.PropertyType;
        }

        private static List<Node>? GetFunctionBody(Node node)
        {
            if (node is FunctionDeclarationAstNode typedNode)
            {
                if (typedNode.FunctionBody.Count > 0)
                {
                    return typedNode.FunctionBody;
                }

                return node.FunctionDeclarationNode.FunctionBody;
            }

            return node.FunctionDeclarationNode.FunctionBody;
        }

        private static List<Node.ParameterInfo>? GetFunctionParameterList(Node node)
        {
            if (node is FunctionDeclarationAstNode typedNode)
            {
                if (typedNode.FunctionParameterList.Count > 0)
                {
                    return typedNode.FunctionParameterList;
                }

                return node.FunctionDeclarationNode.FunctionParameterList;
            }

            return node.FunctionDeclarationNode.FunctionParameterList;
        }

        private static List<Node.ParameterInfo>? GetDelegateParameterList(Node node)
        {
            if (node is DelegateDeclarationAstNode typedNode)
            {
                if (typedNode.DelegateParameterList.Count > 0)
                {
                    return typedNode.DelegateParameterList;
                }

                return node.DelegateDeclarationNode.DelegateParameterList;
            }

            return node.DelegateDeclarationNode.DelegateParameterList;
        }

        private static string? GetUseStatementTarget(Node node)
        {
            return node is UseStatementAstNode typedNode
                ? typedNode.Target
                : null;
        }

        private static bool GetUseStatementIsFilePath(Node node)
        {
            return node is UseStatementAstNode typedNode
                ? typedNode.IsFilePath
                : false;
        }

        private static List<string> GetPropertyModifiers(Node node)
        {
            if (node is PropertyDeclarationAstNode typedNode && typedNode.PropertyModifiers.Count > 0)
            {
                return typedNode.PropertyModifiers;
            }

            return node.PropertyDeclarationNode.PropertyModifiers;
        }

        private static List<string> GetFunctionModifiers(Node node)
        {
            if (node is FunctionDeclarationAstNode typedNode && typedNode.FunctionModifiers.Count > 0)
            {
                return typedNode.FunctionModifiers;
            }

            return node.FunctionDeclarationNode.FunctionModifiers;
        }

        private static string? GetDelegateDeclarationName(Node node)
        {
            if (node is DelegateDeclarationAstNode typedNode)
            {
                return typedNode.DelegateName ?? node.DelegateDeclarationNode.DelegateName;
            }

            return node.DelegateDeclarationNode.DelegateName;
        }

        private static List<Node> GetTypeProperties(Node node)
        {
            if (node is TypeDeclarationAstNode typedNode)
            {
                if (typedNode.TypeProperties.Count > 0)
                {
                    return typedNode.TypeProperties;
                }

                return node.TypeDeclarationNode.TypeProperties;
            }

            return node.TypeDeclarationNode.TypeProperties;
        }

        private static List<Node> GetTypeFunctions(Node node)
        {
            if (node is TypeDeclarationAstNode typedNode)
            {
                if (typedNode.TypeFunctions.Count > 0)
                {
                    return typedNode.TypeFunctions;
                }

                return node.TypeDeclarationNode.TypeFunctions;
            }

            return node.TypeDeclarationNode.TypeFunctions;
        }

        private static string? GetFunctionDeclarationName(Node node)
        {
            if (node is FunctionDeclarationAstNode typedNode)
            {
                return typedNode.FunctionDeclarationName ?? node.FunctionDeclarationNode.FunctionDeclarationName;
            }

            return node.FunctionDeclarationNode.FunctionDeclarationName;
        }

        private static string? GetFunctionDeclarationReturnType(Node node)
        {
            if (node is FunctionDeclarationAstNode typedNode)
            {
                return typedNode.FunctionDeclarationReturnType ?? node.FunctionDeclarationNode.FunctionDeclarationReturnType;
            }

            return node.FunctionDeclarationNode.FunctionDeclarationReturnType;
        }

        private static ExpressionNode? GetFunctionCallExpression(Node node)
        {
            return node is FunctionCallAstNode typedNode
                ? typedNode.Expression
                : null;
        }

        private static string? GetFunctionCallName(Node node)
        {
            return node is FunctionCallAstNode typedNode
                ? typedNode.Name
                : null;
        }

        private static string? GetFunctionCallArguments(Node node)
        {
            return node is FunctionCallAstNode typedNode
                ? typedNode.Arguments
                : null;
        }

        private static string? GetWriteLineStringValue(Node node)
        {
            return node is WriteLineAstNode typedNode
                ? typedNode.StringValue
                : null;
        }

        private static string? GetTypeDeclarationKind(Node node)
        {
            if (node is TypeDeclarationAstNode typedNode)
            {
                return typedNode.DeclarationKind ?? node.TypeDeclarationNode.DeclarationKind;
            }

            return node.TypeDeclarationNode.DeclarationKind;
        }

        private static string? GetTypeDeclarationName(Node node)
        {
            if (node is TypeDeclarationAstNode typedNode)
            {
                return typedNode.DeclarationName ?? node.TypeDeclarationNode.DeclarationName;
            }

            return node.TypeDeclarationNode.DeclarationName;
        }

        private static string? GetTypeBaseTypeName(Node node)
        {
            if (node is TypeDeclarationAstNode typedNode)
            {
                return typedNode.BaseTypeName ?? node.TypeDeclarationNode.BaseTypeName;
            }

            return node.TypeDeclarationNode.BaseTypeName;
        }

        private static List<string> GetTypeTraitNames(Node node)
        {
            if (node is TypeDeclarationAstNode typedNode)
            {
                if (typedNode.TraitNames.Count > 0)
                {
                    return typedNode.TraitNames;
                }

                return node.TypeDeclarationNode.TraitNames;
            }

            return node.TypeDeclarationNode.TraitNames;
        }

        private static string? GetStatementValue(Node node)
        {
            if (node is StatementAstNode typedNode)
            {
                return typedNode.StatementValue ?? node.StatementNode.StatementValue;
            }

            return node.StatementNode.StatementValue;
        }

        private static ExpressionNode? GetStatementExpression(Node node)
        {
            if (node is StatementAstNode typedNode)
            {
                return typedNode.StatementExpression ?? node.StatementNode.StatementExpression;
            }

            return node.StatementNode.StatementExpression;
        }

        private static List<Node> GetStatementBody(Node node)
        {
            if (node is StatementAstNode typedNode)
            {
                if (typedNode.StatementBody.Count > 0)
                {
                    return typedNode.StatementBody;
                }

                return node.StatementNode.StatementBody;
            }

            return node.StatementNode.StatementBody;
        }

        private static string? GetIfCondition(Node node)
        {
            if (node is IfStatementAstNode typedNode)
            {
                return typedNode.IfCondition ?? node.IfStatementNode.IfCondition;
            }

            return node.IfStatementNode.IfCondition;
        }

        private static ExpressionNode? GetIfConditionExpression(Node node)
        {
            if (node is IfStatementAstNode typedNode)
            {
                return typedNode.ConditionExpression ?? node.IfStatementNode.ConditionExpression;
            }

            return node.IfStatementNode.ConditionExpression;
        }

        private static List<Node> GetIfElseBody(Node node)
        {
            if (node is IfStatementAstNode typedNode)
            {
                if (typedNode.ElseBody.Count > 0)
                {
                    return typedNode.ElseBody;
                }

                return node.IfStatementNode.ElseBody;
            }

            return node.IfStatementNode.ElseBody;
        }

        private static string? GetMatchExpression(Node node)
        {
            if (node is MatchStatementAstNode typedNode)
            {
                return typedNode.Expression ?? node.MatchStatementNode.Expression;
            }

            return node.MatchStatementNode.Expression;
        }

        private static ExpressionNode? GetMatchExpressionNode(Node node)
        {
            if (node is MatchStatementAstNode typedNode)
            {
                return typedNode.ExpressionNode ?? node.MatchStatementNode.ExpressionNode;
            }

            return node.MatchStatementNode.ExpressionNode;
        }

        private static string? GetWhenCondition(Node node)
        {
            if (node is WhenStatementAstNode typedNode)
            {
                return typedNode.WhenCondition ?? node.WhenStatementNode.WhenCondition;
            }

            return node.WhenStatementNode.WhenCondition;
        }

        private static ExpressionNode? GetWhenExpression(Node node)
        {
            if (node is WhenStatementAstNode typedNode)
            {
                return typedNode.WhenExpression ?? node.WhenStatementNode.WhenExpression;
            }

            return node.WhenStatementNode.WhenExpression;
        }

        private static string? GetWhileCondition(Node node)
        {
            if (node is WhileStatementAstNode typedNode)
            {
                return typedNode.WhileCondition ?? node.WhileStatementNode.WhileCondition;
            }

            return node.WhileStatementNode.WhileCondition;
        }

        private static ExpressionNode? GetWhileExpression(Node node)
        {
            if (node is WhileStatementAstNode typedNode)
            {
                return typedNode.WhileExpression ?? node.WhileStatementNode.WhileExpression;
            }

            return node.WhileStatementNode.WhileExpression;
        }

        private static string? GetForVariable(Node node)
        {
            return node.Kind switch
            {
                NodeKind.ForStatement when node is ForStatementAstNode typedNode => typedNode.ForVariable ?? node.ForStatementNode.ForVariable,
                NodeKind.ForAllStatement when node is ForAllStatementAstNode typedNode => typedNode.ForVariable ?? node.ForStatementNode.ForVariable,
                _ => node.ForStatementNode.ForVariable
            };
        }

        private static string? GetForContainer(Node node)
        {
            return node.Kind switch
            {
                NodeKind.ForStatement when node is ForStatementAstNode typedNode => typedNode.ForContainer ?? node.ForStatementNode.ForContainer,
                NodeKind.ForAllStatement when node is ForAllStatementAstNode typedNode => typedNode.ForContainer ?? node.ForStatementNode.ForContainer,
                _ => node.ForStatementNode.ForContainer
            };
        }

        private static ExpressionNode? GetForContainerExpression(Node node)
        {
            return node.Kind switch
            {
                NodeKind.ForStatement when node is ForStatementAstNode typedNode => typedNode.ForContainerExpression ?? node.ForStatementNode.ForContainerExpression,
                NodeKind.ForAllStatement when node is ForAllStatementAstNode typedNode => typedNode.ForContainerExpression ?? node.ForStatementNode.ForContainerExpression,
                _ => node.ForStatementNode.ForContainerExpression
            };
        }

        private static string? GetHasCondition(Node node)
        {
            if (node is HasStatementAstNode typedNode)
            {
                return typedNode.HasCondition ?? node.HasStatementNode.HasCondition;
            }

            return node.HasStatementNode.HasCondition;
        }

        private static ExpressionNode? GetHasExpression(Node node)
        {
            if (node is HasStatementAstNode typedNode)
            {
                return typedNode.HasExpression ?? node.HasStatementNode.HasExpression;
            }

            return node.HasStatementNode.HasExpression;
        }

        private static string? GetHasTraitTypeName(Node node)
        {
            if (node is HasTraitStatementAstNode typedNode)
            {
                return typedNode.HasTraitTypeName ?? node.HasTraitStatementNode.HasTraitTypeName;
            }

            return node.HasTraitStatementNode.HasTraitTypeName;
        }

        private static string? GetHasTraitVariableName(Node node)
        {
            if (node is HasTraitStatementAstNode typedNode)
            {
                return typedNode.HasTraitVariableName ?? node.HasTraitStatementNode.HasTraitVariableName;
            }

            return node.HasTraitStatementNode.HasTraitVariableName;
        }

        private static string? GetHasTraitCondition(Node node)
        {
            if (node is HasTraitStatementAstNode typedNode)
            {
                return typedNode.HasTraitCondition ?? node.HasTraitStatementNode.HasTraitCondition;
            }

            return node.HasTraitStatementNode.HasTraitCondition;
        }

        private static ExpressionNode? GetHasTraitExpression(Node node)
        {
            if (node is HasTraitStatementAstNode typedNode)
            {
                return typedNode.HasTraitExpression ?? node.HasTraitStatementNode.HasTraitExpression;
            }

            return node.HasTraitStatementNode.HasTraitExpression;
        }

        private static string FormatAutoPropertyInitializer(string? value, string? declaredType)
        {
            var text = value?.Trim() ?? string.Empty;
            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
            {
                return text.ToLowerInvariant();
            }

            if (string.Equals(text, "bool", StringComparison.OrdinalIgnoreCase))
            {
                return "false";
            }

            if (string.Equals(text, "str", StringComparison.OrdinalIgnoreCase) || text.StartsWith("\"", StringComparison.Ordinal))
            {
                var literal = string.Equals(text, "str", StringComparison.OrdinalIgnoreCase) ? "\"\"" : text;
                return ToPumaStringLiteral(NormalizeAutoStringLiteral(literal));
            }

            if (IsCharacterLiteralText(text))
            {
                return $"Character({text})";
            }

            if (LooksLikeObjectConstructorCall(text))
            {
                return $"new {text}";
            }

            var sign = string.Empty;
            var startIndex = 0;
            if (text.StartsWith("-", StringComparison.Ordinal) || text.StartsWith("+", StringComparison.Ordinal))
            {
                sign = text[..1];
                startIndex = 1;
            }

            var index = startIndex;
            var dotSeen = false;
            while (index < text.Length)
            {
                var ch = text[index];
                if (char.IsDigit(ch))
                {
                    index++;
                    continue;
                }

                if (ch == '.' && !dotSeen)
                {
                    dotSeen = true;
                    index++;
                    continue;
                }

                break;
            }

            if (index == 0)
            {
                return text;
            }

            var numeric = sign + text[startIndex..index];
            var suffix = text[index..];
            var effectiveType = !string.IsNullOrWhiteSpace(declaredType) ? declaredType : suffix;
            var castType = effectiveType switch
            {
                "" => dotSeen ? "double" : "int64_t",
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
                _ => "int64_t"
            };

            return $"({castType}){numeric}";
        }

        private static bool LooksLikeObjectConstructorCall(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();
            if (trimmed.StartsWith("new ", StringComparison.Ordinal)
                || !trimmed.EndsWith(")", StringComparison.Ordinal))
            {
                return false;
            }

            var openIndex = trimmed.IndexOf('(');
            if (openIndex <= 0)
            {
                return false;
            }

            var ctorName = trimmed[..openIndex].Trim();
            if (ctorName.Length == 0)
            {
                return false;
            }

            if (ctorName is "List" or "Range" or "Array")
            {
                return false;
            }

            return char.IsUpper(ctorName[0]);
        }

        private static bool RequiresFixedWidthIntegerCast(string? value, string? declaredType)
        {
            var text = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var signOffset = (text.StartsWith("-", StringComparison.Ordinal) || text.StartsWith("+", StringComparison.Ordinal)) ? 1 : 0;
            var index = signOffset;
            var dotSeen = false;
            while (index < text.Length)
            {
                var ch = text[index];
                if (char.IsDigit(ch))
                {
                    index++;
                    continue;
                }

                if (ch == '.' && !dotSeen)
                {
                    dotSeen = true;
                    index++;
                    continue;
                }

                break;
            }

            if (index == signOffset)
            {
                return false;
            }

            var suffix = text[index..];
            var effectiveType = !string.IsNullOrWhiteSpace(declaredType) ? declaredType : suffix;
            var castType = effectiveType switch
            {
                "" => dotSeen ? "double" : "int64_t",
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
                _ => "int64_t"
            };

            return castType is "int64_t" or "int32_t" or "int16_t" or "int8_t" or "uint64_t" or "uint32_t" or "uint16_t" or "uint8_t";
        }

        private static string NormalizeAutoStringLiteral(string literal)
        {
            return literal;
        }

        private static string ToPumaStringLiteral(string literal)
        {
            if (string.IsNullOrWhiteSpace(literal))
            {
                return "String(\"\")";
            }

            if (literal.StartsWith("String(", StringComparison.Ordinal))
            {
                return literal;
            }

            return $"String({literal})";
        }

        private static void EmitFunctions(List<Node> ast, StringBuilder sb, HashSet<Node> typeFunctions)
        {
            var globalNames = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration)
                .Select(GetPropertyName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var node in ast.Where(n => n.Kind == NodeKind.DelegateDeclaration))
            {
                var parameters = string.Join(", ", (GetDelegateParameterList(node) ?? new List<Node.ParameterInfo>()).Select(FormatParameter));
                sb.AppendLine($"typedef void (*{(node is DelegateDeclarationAstNode typedDelegate ? typedDelegate.DelegateName ?? node.DelegateDeclarationNode.DelegateName : node.DelegateDeclarationNode.DelegateName)})({parameters});");
            }

            if (ast.Any(n => n.Kind == NodeKind.DelegateDeclaration))
            {
                sb.AppendLine();
            }

            var globalFunctions = ast.Where(n => n.Kind == NodeKind.FunctionDeclaration && !typeFunctions.Contains(n)).ToList();
            if (globalFunctions.Count > 0)
            {
                sb.AppendLine("// functions");
            }

            foreach (var node in globalFunctions)
            {
                var returnType = string.IsNullOrWhiteSpace(GetFunctionDeclarationReturnType(node))
                    ? "void"
                    : GetFunctionDeclarationReturnType(node);
                var functionParameterList = GetFunctionParameterList(node) ?? new List<Node.ParameterInfo>();
                var functionBody = GetFunctionBody(node) ?? new List<Node>();
                var parameters = functionParameterList.Count == 0
                    ? "void"
                    : string.Join(", ", functionParameterList.Select(FormatFunctionSignatureParameter));
                sb.AppendLine($"{returnType} {GetFunctionDeclarationName(node)}({parameters})");
                sb.AppendLine("{");
                if (string.Equals(GetFunctionDeclarationReturnType(node), "char", StringComparison.OrdinalIgnoreCase))
                {
                    EmitStatementsWithLocalDeclarations(functionBody, sb, "    ", new HashSet<string?>(globalNames, StringComparer.Ordinal));
                }
                else
                {
                    EmitStatementsWithStringLocalDeclarations(functionBody, sb, "    ", ast);
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        private static string FormatFunctionSignatureParameter(Node.ParameterInfo parameter)
        {
            var type = MapType(parameter.Type) ?? parameter.Type;
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                return type;
            }

            return $"{type} {parameter.Name}";
        }

        private static void EmitInitializeFinalize(List<Node> ast, StringBuilder sb)
        {
            var hasTypeOrTrait = ast.Any(n => n.Kind == NodeKind.TypeDeclaration
                && (string.Equals(GetTypeDeclarationKind(n), "type", StringComparison.Ordinal)
                    || string.Equals(GetTypeDeclarationKind(n), "trait", StringComparison.Ordinal)));

            if (!hasTypeOrTrait)
            {
                EmitSectionFunction(ast, sb, Section.Initialize, "initialize");
            }
            EmitSectionFunction(ast, sb, Section.Finalize, "finalize");
        }

        private static void EmitSectionFunction(List<Node> ast, StringBuilder sb, Section section, string name)
        {
            var (index, sectionNode) = FindSection(ast, section);
            if (index < 0 || sectionNode == null)
            {
                return;
            }

            var sectionParameters = sectionNode.SectionNode.SectionParameterList ?? new List<Node.ParameterInfo>();
            var parameters = sectionParameters.Count == 0
                ? "void"
                : string.Join(", ", sectionParameters.Select(FormatParameter));
            sb.AppendLine($"// {name}");
            sb.AppendLine($"void {name}({parameters})");
            sb.AppendLine("{");
            var statements = CollectStatements(ast, index + 1);
            if (section == Section.Initialize)
            {
                var globalNames = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration)
                    .Select(GetPropertyName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToHashSet(StringComparer.Ordinal);
                EmitStatementsWithLocalDeclarations(statements, sb, "    ", new HashSet<string?>(globalNames, StringComparer.Ordinal));
            }
            else
            {
                EmitStatements(statements, sb, "    ");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private static void EmitMain(List<Node> ast, StringBuilder sb)
        {
            var (initIndex, initSection) = FindSection(ast, Section.Initialize);
            var (finalIndex, finalSection) = FindSection(ast, Section.Finalize);
            var (startIndex, startSection) = FindSection(ast, Section.Start);
            var globalPropertyNames = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration)
                .Select(GetPropertyName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.Ordinal);

            if (startIndex < 0)
            {
                return;
            }

            if (startSection != null)
            {
                for (var i = 0; i < startSection.SectionNode.LeadingBlankLines; i++)
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine("// start");
            sb.AppendLine("int main()");
            sb.AppendLine("{");
            if (initIndex >= 0 && initSection != null)
            {
                sb.AppendLine($"    initialize({FormatArguments(initSection.SectionNode.SectionParameterList ?? new List<Node.ParameterInfo>())});");
            }
            var statements = startIndex >= 0 ? CollectStatements(ast, startIndex + 1) : new List<Node>();
            var numericPropertyReassignmentMode = UsesTypedPropertyReassignmentMode(ast);
            var globalNames = globalPropertyNames;
            var localNames = new HashSet<string>(StringComparer.Ordinal);
            var bufferedStatements = new List<Node>();
            var emittedExpressionBasedLocalDeclaration = false;
            var emittedPropertyTypedAssignment = false;
            var heapAllocatedGlobalProperties = ast
                .Where(n => n.Kind == NodeKind.PropertyDeclaration
                    && !string.IsNullOrWhiteSpace(GetPropertyName(n))
                    && LooksLikeObjectConstructorCall(GetPropertyValue(n)?.Trim() ?? string.Empty))
                .Select(n => GetPropertyName(n)!)
                .ToList();
            var functionsReturningConstructedObject = ast
                .Where(n => n.Kind == NodeKind.FunctionDeclaration
                    && ((GetFunctionBody(n)?.Any(s => s.Kind == NodeKind.ReturnStatement
                        && LooksLikeObjectConstructorCall(GenerateExpression(GetStatementExpression(s), GetStatementValue(s))?.Trim() ?? string.Empty)))
                        ?? false))
                .Select(GetFunctionDeclarationName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.Ordinal);
            var propertiesAssignedToNone = new HashSet<string>(StringComparer.Ordinal);
            var transferredOwnershipLocals = new Dictionary<string, string>(StringComparer.Ordinal);
            var ownedLocalsToDelete = new HashSet<string>(StringComparer.Ordinal);

            foreach (var statement in statements)
            {
                TrackOwnershipTransfer(statement, heapAllocatedGlobalProperties, globalNames, propertiesAssignedToNone, transferredOwnershipLocals, functionsReturningConstructedObject, ownedLocalsToDelete);

                if (TryEmitMainLocalDeclaration(statement, globalNames, localNames, sb, "    ", out var usedExpressionFallback, out var usedPropertyTypedAssignment))
                {
                    emittedExpressionBasedLocalDeclaration |= usedExpressionFallback;
                    emittedPropertyTypedAssignment |= usedPropertyTypedAssignment;
                    if (bufferedStatements.Count > 0)
                    {
                        EmitStatements(bufferedStatements, sb, "    ");
                        bufferedStatements.Clear();
                    }
                    continue;
                }

                bufferedStatements.Add(statement);
            }

            if (bufferedStatements.Count > 0)
            {
                EmitStatements(bufferedStatements, sb, "    ", ast, () => emittedPropertyTypedAssignment = true);
            }

            if (emittedExpressionBasedLocalDeclaration || emittedPropertyTypedAssignment)
            {
                sb.AppendLine();
            }

            if (finalIndex >= 0 && finalSection != null)
            {
                sb.AppendLine($"    finalize({FormatArguments(finalSection.SectionNode.SectionParameterList ?? new List<Node.ParameterInfo>())});");
            }

            foreach (var propertyName in heapAllocatedGlobalProperties)
            {
                if (propertiesAssignedToNone.Contains(propertyName)
                    && transferredOwnershipLocals.TryGetValue(propertyName, out var localOwner)
                    && !string.IsNullOrWhiteSpace(localOwner))
                {
                    sb.AppendLine($"    delete {localOwner};");
                    ownedLocalsToDelete.Remove(localOwner);
                    continue;
                }

                sb.AppendLine($"    delete {propertyName};");
            }

            foreach (var localOwner in ownedLocalsToDelete)
            {
                sb.AppendLine($"    delete {localOwner};");
            }

            sb.AppendLine("    return 0;");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private static void EmitTypes(List<Node> ast, List<Node> typeDeclarations, StringBuilder sb)
        {
            var (initIndex, _) = FindSection(ast, Section.Initialize);
            var initializeStatements = initIndex >= 0 ? CollectStatements(ast, initIndex + 1) : new List<Node>();

            foreach (var node in typeDeclarations.Where(n => GetTypeDeclarationKind(n) == "type"))
            {
                var name = ToCppQualifiedName(GetTypeDeclarationName(node)) ?? "Type";
                var bases = new List<string>();
                if (!string.IsNullOrWhiteSpace(GetTypeBaseTypeName(node)))
                {
                    var baseName = ToCppQualifiedName(GetTypeBaseTypeName(node));
                    if (!(string.Equals(baseName, "object", StringComparison.OrdinalIgnoreCase) && GetTypeTraitNames(node).Count > 0))
                    {
                        bases.Add($"public {baseName}");
                    }
                }

                foreach (var trait in GetTypeTraitNames(node))
                {
                    bases.Add($"public {ToCppQualifiedName(trait)}");
                }

                var inheritance = bases.Count > 0 ? $" : {string.Join(", ", bases)}" : string.Empty;
                sb.AppendLine($"class {name}{inheritance}");
                sb.AppendLine("{");
                if (initializeStatements.Count > 0)
                {
                    sb.AppendLine("public:");
                    sb.AppendLine($"    {name}()");
                    sb.AppendLine("    {");
                    EmitStatementsWithLocalDeclarations(initializeStatements, sb, "        ", new HashSet<string?>(StringComparer.Ordinal));
                    sb.AppendLine("    }");
                }
                EmitTypeProperties(node, sb, "    ");
                EmitTypeFunctions(node, sb, "    ");
                sb.AppendLine("};");
                sb.AppendLine();
            }
        }

        private static void EmitTraits(List<Node> ast, List<Node> typeDeclarations, StringBuilder sb)
        {
            var (initIndex, _) = FindSection(ast, Section.Initialize);
            var initializeStatements = initIndex >= 0 ? CollectStatements(ast, initIndex + 1) : new List<Node>();

            foreach (var node in typeDeclarations.Where(n => GetTypeDeclarationKind(n) == "trait"))
            {
                var name = ToCppQualifiedName(GetTypeDeclarationName(node)) ?? "Trait";
                sb.AppendLine($"class {name}");
                sb.AppendLine("{");
                if (initializeStatements.Count > 0)
                {
                    sb.AppendLine("public:");
                    sb.AppendLine($"    {name}()");
                    sb.AppendLine("    {");
                    EmitTraitInitializeStatements(initializeStatements, sb, "        ");
                    sb.AppendLine("    }");
                }
                EmitTypeProperties(node, sb, "    ");
                EmitTypeFunctions(node, sb, "    ");
                sb.AppendLine("};");
                sb.AppendLine();
            }
        }

        private static void EmitStatementsWithLocalDeclarations(List<Node> statements, StringBuilder sb, string indent, HashSet<string?> globalNames)
        {
            var localNames = new HashSet<string>(StringComparer.Ordinal);
            var bufferedStatements = new List<Node>();

            foreach (var statement in statements)
            {
                if (TryEmitMainLocalDeclaration(statement, globalNames, localNames, sb, indent))
                {
                    if (bufferedStatements.Count > 0)
                    {
                        EmitStatements(bufferedStatements, sb, indent);
                        bufferedStatements.Clear();
                    }
                    continue;
                }

                bufferedStatements.Add(statement);
            }

            if (bufferedStatements.Count > 0)
            {
                EmitStatements(bufferedStatements, sb, indent);
            }
        }

        private static void EmitStatementsWithStringLocalDeclarations(List<Node> statements, StringBuilder sb, string indent, List<Node>? ast)
        {
            var declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (var statement in statements)
            {
                if (statement.Kind == NodeKind.AssignmentStatement
                    && GetAssignmentOperator(statement) == "="
                    && !string.IsNullOrWhiteSpace(GetAssignmentLeft(statement))
                    && IsSimpleIdentifier(GetAssignmentLeft(statement))
                    && !declared.Contains(GetAssignmentLeft(statement))
                    && !string.IsNullOrWhiteSpace(GetAssignmentRight(statement))
                    && GetAssignmentRight(statement)!.StartsWith("\"", StringComparison.Ordinal))
                {
                    var value = ToPumaStringLiteral(GetAssignmentRight(statement)!);
                    sb.AppendLine($"{indent}auto {GetAssignmentLeft(statement)} = {value};");
                    declared.Add(GetAssignmentLeft(statement)!);
                    continue;
                }

                EmitStatements(new List<Node> { statement }, sb, indent, ast);
            }
        }

        private static string BuildCallWithDefaultArguments(string functionName, ExpressionNode callExpressionNode, List<Node>? ast)
        {
            if (ast == null)
            {
                return $"{functionName}({string.Join(", ", callExpressionNode.Arguments.Select(a => GenerateExpression(a, null)))})";
            }

            var declaration = ast.FirstOrDefault(n => n.Kind == NodeKind.FunctionDeclaration
                && string.Equals(GetFunctionDeclarationName(n), functionName, StringComparison.Ordinal));
            if (declaration == null)
            {
                return $"{functionName}({string.Join(", ", callExpressionNode.Arguments.Select(a => GenerateExpression(a, null)))})";
            }

            var arguments = callExpressionNode.Arguments
                .Select(a => GenerateExpression(a, null))
                .ToList();

            var declarationParameters = GetFunctionParameterList(declaration) ?? new List<Node.ParameterInfo>();

            for (var i = arguments.Count; i < declarationParameters.Count; i++)
            {
                arguments.Add(FormatDefaultArgument(declarationParameters[i]));
            }

            return $"{functionName}({string.Join(", ", arguments)})";
        }

        private static void EmitTraitInitializeStatements(List<Node> statements, StringBuilder sb, string indent)
        {
            var localNames = new HashSet<string>(StringComparer.Ordinal);
            var globalNames = new HashSet<string?>(StringComparer.Ordinal);

            foreach (var statement in statements)
            {
                if (TryEmitMainLocalDeclaration(statement, globalNames, localNames, sb, indent))
                {
                    continue;
                }

                EmitStatements(new List<Node> { statement }, sb, indent);
            }
        }

        private static void EmitTypeProperties(Node node, StringBuilder sb, string indent)
        {
            var protectedProperties = new List<Node>();
            var publicProperties = new List<Node>();

            foreach (var property in GetTypeProperties(node))
            {
                if (GetPropertyModifiers(property).Contains("public"))
                {
                    publicProperties.Add(property);
                }
                else
                {
                    // Puma private/internal/default map to C++ protected.
                    protectedProperties.Add(property);
                }
            }

            EmitPropertiesForAccess(protectedProperties, "protected", sb, indent);
            EmitPropertiesForAccess(publicProperties, "public", sb, indent);

            if ((protectedProperties.Count > 0 || publicProperties.Count > 0) && GetTypeFunctions(node).Count > 0)
            {
                sb.AppendLine();
            }
        }

        private static void EmitPropertiesForAccess(List<Node> properties, string access, StringBuilder sb, string indent)
        {
            if (properties.Count == 0)
            {
                return;
            }

            sb.AppendLine($"{indent}// properties");
            sb.AppendLine($"{indent}{access}:");
            foreach (var property in properties)
            {
                var value = FormatAutoPropertyInitializer(GetPropertyValue(property), GetPropertyType(property));
                var modifiers = GetPropertyModifiers(property).Contains("constant") ? "const " : string.Empty;
                sb.AppendLine($"{indent}{modifiers}auto {GetPropertyName(property)} = {value};");
            }
        }

        private static void EmitTypeFunctions(Node node, StringBuilder sb, string indent)
        {
            var protectedFunctions = new List<Node>();
            var publicFunctions = new List<Node>();

            foreach (var function in GetTypeFunctions(node))
            {
                if (GetFunctionModifiers(function).Contains("private")
                    || GetFunctionModifiers(function).Contains("internal"))
                {
                    protectedFunctions.Add(function);
                }
                else
                {
                    publicFunctions.Add(function);
                }
            }

            EmitFunctionsForAccess(protectedFunctions, "protected", sb, indent);
            EmitFunctionsForAccess(publicFunctions, "public", sb, indent);
        }

        private static void EmitFunctionsForAccess(List<Node> functions, string access, StringBuilder sb, string indent)
        {
            if (functions.Count == 0)
            {
                return;
            }

            sb.AppendLine($"{indent}// functions");
            sb.AppendLine($"{indent}{access}:");

            foreach (var function in functions)
            {
                var returnType = MapType(GetFunctionDeclarationReturnType(function)) ?? "void";
                var parameters = string.Join(", ", (GetFunctionParameterList(function) ?? new List<Node.ParameterInfo>()).Select(FormatParameter));
                sb.AppendLine($"{indent}{returnType} {GetFunctionDeclarationName(function)}({parameters})");
                sb.AppendLine($"{indent}{{");
                EmitStatements(GetFunctionBody(function) ?? new List<Node>(), sb, indent + "    ");
                sb.AppendLine($"{indent}}}");
            }
        }

        private static void EmitStatements(List<Node> statements, StringBuilder sb, string indent)
        {
            EmitStatements(statements, sb, indent, null, null);
        }

        private static void EmitStatements(List<Node> statements, StringBuilder sb, string indent, List<Node>? ast)
        {
            EmitStatements(statements, sb, indent, ast, null);
        }

        private static void EmitStatements(List<Node> statements, StringBuilder sb, string indent, List<Node>? ast, Action? onTypedPropertyLiteralAssignment)
        {
            for (var i = 0; i < statements.Count; i++)
            {
                var node = statements[i];
                switch (node.Kind)
                {
                    case NodeKind.AssignmentStatement:
                        {
                            var leftExpression = GenerateExpression(GetAssignmentLeftExpression(node), GetAssignmentLeft(node));
                            var rightExpression = GenerateExpression(GetAssignmentRightExpression(node), GetAssignmentRight(node));
                            if (GetAssignmentRightExpression(node) == null
                                && !string.IsNullOrWhiteSpace(GetAssignmentRight(node))
                                && GetAssignmentRight(node)!.Contains('(')
                                && GetAssignmentRight(node)!.Contains(')'))
                            {
                                rightExpression = GetAssignmentRight(node);
                            }

                            if (GetIsLoweredPostfixMutation(node) && (GetAssignmentOperator(node) == "+=" || GetAssignmentOperator(node) == "-="))
                            {
                                var op = GetAssignmentOperator(node) == "+=" ? "++" : "--";
                                sb.AppendLine($"{indent}{leftExpression}{op};");
                                break;
                            }

                            if (GetAssignmentOperator(node) == "="
                                && GetAssignmentLeftExpression(node)?.Kind == ExpressionKind.Binary && GetAssignmentLeftExpression(node)!.Value == ","
                                && GetAssignmentRightExpression(node)?.Kind == ExpressionKind.Binary && GetAssignmentRightExpression(node)!.Value == ",")
                            {
                                var left0 = GenerateExpression(GetAssignmentLeftExpression(node)!.Left, null);
                                var left1 = GenerateExpression(GetAssignmentLeftExpression(node)!.Right, null);
                                var right0 = GenerateExpression(GetAssignmentRightExpression(node)!.Left, null);
                                var right1 = GenerateExpression(GetAssignmentRightExpression(node)!.Right, null);
                                sb.AppendLine($"{indent}{left0} = {right0};");
                                sb.AppendLine($"{indent}{left1} = {right1});");
                                break;
                            }

                            if (GetAssignmentOperator(node) == "=" && GetAssignmentRightExpression(node)?.Kind == ExpressionKind.Binary)
                            {
                                rightExpression = UnwrapOutermostParentheses(rightExpression);
                            }

                            if (GetAssignmentOperator(node) == "="
                                && GetAssignmentRightExpression(node)?.Kind == ExpressionKind.Literal
                                && !string.IsNullOrWhiteSpace(rightExpression)
                                && rightExpression.StartsWith("\"", StringComparison.Ordinal)
                                && !rightExpression.EndsWith("s", StringComparison.Ordinal))
                            {
                                rightExpression = ToPumaStringLiteral(rightExpression);
                            }

                            if (GetAssignmentOperator(node) == "="
                                && GetAssignmentRightExpression(node)?.Kind == ExpressionKind.Literal
                                && IsCharacterLiteralText(rightExpression))
                            {
                                rightExpression = $"Character({rightExpression})";
                            }

                            var emittedTypedPropertyLiteral = false;
                            if (GetAssignmentOperator(node) == "=" && GetAssignmentRightExpression(node)?.Kind == ExpressionKind.Literal)
                            {
                                var propertyNode = ast?.FirstOrDefault(n => n.Kind == NodeKind.PropertyDeclaration
                                    && string.Equals(GetPropertyName(n), GetAssignmentLeft(node), StringComparison.Ordinal));
                                if (propertyNode != null
                                    && ast != null
                                    && UsesTypedPropertyReassignmentMode(ast)
                                    && TryGetTypedLiteralDeclaration(GetAssignmentRight(node) ?? string.Empty, out var typedLiteralName, out var typedLiteralValue))
                                {
                                    rightExpression = typedLiteralName switch
                                    {
                                        "Puma::Type::String" => ToPumaStringLiteral(typedLiteralValue),
                                        "bool" => typedLiteralValue,
                                        _ => $"({typedLiteralName}){typedLiteralValue}"
                                    };
                                    emittedTypedPropertyLiteral = true;
                                }
                            }

                            if (emittedTypedPropertyLiteral)
                            {
                                onTypedPropertyLiteralAssignment?.Invoke();
                            }

                            if (GetAssignmentOperator(node) == "=" && GetAssignmentRightExpression(node)?.Kind == ExpressionKind.Conditional)
                            {
                                var allConditionalAssignments = statements.Count > 1
                                    && statements.All(s => s.Kind == NodeKind.AssignmentStatement
                                        && GetAssignmentRightExpression(s)?.Kind == ExpressionKind.Conditional);

                                if (allConditionalAssignments && i == 0)
                                {
                                    rightExpression = $"({rightExpression}";
                                }

                                if (allConditionalAssignments && i == statements.Count - 1)
                                {
                                    rightExpression = $"{rightExpression})";
                                }
                            }

                            sb.AppendLine($"{indent}{leftExpression} {GetAssignmentOperator(node)} {rightExpression};");
                            break;
                        }
                    case NodeKind.FunctionCall:
                        {
                            var callExpressionNode = GetFunctionCallExpression(node) ?? GetStatementExpression(node);
                            var callExpression = GenerateExpression(callExpressionNode, null);
                            if (!string.IsNullOrWhiteSpace(callExpression) && callExpressionNode?.Kind == ExpressionKind.Call)
                            {
                                var functionName = GenerateExpression(callExpressionNode.Left, null);
                                if (!string.IsNullOrWhiteSpace(functionName)
                                    && IsSimpleIdentifier(functionName)
                                    && callExpressionNode.Arguments.Count >= 0)
                                {
                                    callExpression = BuildCallWithDefaultArguments(functionName, callExpressionNode, ast);
                                }

                                sb.AppendLine($"{indent}{callExpression};");
                            }
                            else
                            {
                                sb.AppendLine($"{indent}{GetFunctionCallName(node)}({GetFunctionCallArguments(node)});");
                            }
                            break;
                        }
                    case NodeKind.WriteLine:
                        if (!string.IsNullOrWhiteSpace(GetWriteLineStringValue(node)))
                        {
                            sb.AppendLine($"{indent}puts({GetWriteLineStringValue(node)});");
                        }
                        break;
                    case NodeKind.IfStatement:
                        sb.AppendLine($"{indent}if ({UnwrapOutermostParentheses(GenerateExpression(GetIfConditionExpression(node), GetIfCondition(node)))})");
                        sb.AppendLine($"{indent}{{");
                        EmitStatements(GetStatementBody(node), sb, indent + "    ");
                        sb.AppendLine($"{indent}}}");
                        if (GetIfElseBody(node).Count > 0)
                        {
                            sb.AppendLine($"{indent}else");
                            sb.AppendLine($"{indent}{{");
                            EmitStatements(GetIfElseBody(node), sb, indent + "    ");
                            sb.AppendLine($"{indent}}}");
                        }
                        break;
                    case NodeKind.MatchStatement:
                        sb.AppendLine($"{indent}switch ({GenerateExpression(GetMatchExpressionNode(node), GetMatchExpression(node))})");
                        sb.AppendLine($"{indent}{{");
                        foreach (var when in GetStatementBody(node).Where(n => n.Kind == NodeKind.WhenStatement))
                        {
                            sb.AppendLine($"{indent}    case {GenerateExpression(GetWhenExpression(when), GetWhenCondition(when))}:");
                            EmitStatements(GetStatementBody(when), sb, indent + "        ");
                            sb.AppendLine($"{indent}        break;");
                        }
                        sb.AppendLine($"{indent}}}");
                        break;
                    case NodeKind.WhenStatement:
                        sb.AppendLine($"{indent}/* when {GenerateExpression(GetWhenExpression(node), GetWhenCondition(node))} */");
                        break;
                    case NodeKind.WhileStatement:
                        sb.AppendLine($"{indent}while ({UnwrapOutermostParentheses(GenerateExpression(GetWhileExpression(node), GetWhileCondition(node)))})");
                        sb.AppendLine($"{indent}{{");
                        EmitStatements(GetStatementBody(node), sb, indent + "    ");
                        sb.AppendLine($"{indent}}}");
                        break;
                    case NodeKind.ForStatement:
                    case NodeKind.ForAllStatement:
                        sb.AppendLine($"{indent}for (auto {GetForVariable(node)} : {GenerateExpression(GetForContainerExpression(node), GetForContainer(node))})");
                        sb.AppendLine($"{indent}{{");
                        EmitStatements(GetStatementBody(node), sb, indent + "    ");
                        sb.AppendLine($"{indent}}}");
                        break;
                    case NodeKind.RepeatStatement:
                        {
                            var repeatCondition = GenerateExpression(GetRepeatExpressionNode(node), GetRepeatExpression(node));
                            if (string.IsNullOrWhiteSpace(repeatCondition) || repeatCondition == "1")
                            {
                                repeatCondition = "true";
                            }
                            sb.AppendLine($"{indent}do");
                            sb.AppendLine($"{indent}{{");
                            EmitStatements(GetStatementBody(node), sb, indent + "    ");
                            sb.AppendLine($"{indent}}} while ({repeatCondition});");
                            break;
                        }
                    case NodeKind.HasStatement:
                        sb.AppendLine($"{indent}if ({GenerateExpression(GetHasExpression(node), GetHasCondition(node))} != null)");
                        sb.AppendLine($"{indent}{{");
                        EmitStatements(GetStatementBody(node), sb, indent + "    ");
                        sb.AppendLine($"{indent}}}");
                        break;
                    case NodeKind.HasTraitStatement:
                        {
                            var variable = GetHasTraitVariableName(node) ?? GenerateExpression(GetHasTraitExpression(node), GetHasTraitCondition(node));
                            var traitType = GetHasTraitTypeName(node) ?? "Trait";
                            sb.AppendLine($"{indent}if ({variable} != null && typeof({variable}) == typeof({traitType}))");
                            sb.AppendLine($"{indent}{{");
                            EmitStatements(GetStatementBody(node), sb, indent + "    ");
                            sb.AppendLine($"{indent}}}");
                            break;
                        }
                    case NodeKind.ReturnStatement:
                        {
                            var returnExpression = UnwrapOutermostParentheses(GenerateExpression(GetStatementExpression(node), GetStatementValue(node)));
                            if (string.IsNullOrWhiteSpace(returnExpression))
                            {
                                sb.AppendLine($"{indent}return;");
                            }
                            else
                            {
                                sb.AppendLine($"{indent}return {returnExpression};");
                            }

                            break;
                        }
                    case NodeKind.YieldStatement:
                        sb.AppendLine($"{indent}/* yield {GenerateExpression(GetStatementExpression(node), GetStatementValue(node))} */");
                        break;
                    case NodeKind.BreakStatement:
                        sb.AppendLine($"{indent}break;");
                        break;
                    case NodeKind.ContinueStatement:
                        sb.AppendLine($"{indent}continue;");
                        break;
                    case NodeKind.ErrorStatement:
                        sb.AppendLine($"{indent}/* error {GenerateExpression(GetStatementExpression(node), GetStatementValue(node))} */");
                        break;
                    case NodeKind.CatchStatement:
                        sb.AppendLine($"{indent}/* catch {GenerateExpression(GetStatementExpression(node), GetStatementValue(node))} */");
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
                ExpressionKind.Identifier => string.Equals(node.Value, "none", StringComparison.OrdinalIgnoreCase)
                    ? "null"
                    : node.Value ?? string.Empty,
                ExpressionKind.Literal => node.Value ?? string.Empty,
                ExpressionKind.Unary => string.Equals(node.Value, "not", StringComparison.Ordinal)
                    ? $"not {GenerateExpression(node.Left, null)}"
                    : $"{node.Value}{GenerateExpression(node.Left, null)}",
                ExpressionKind.Cast => $"({MapType(node.Value) ?? node.Value}) {GenerateExpression(node.Left, null)}",
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
                "Puma::Type::String" => "String(\"\")",
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
                "char" => "Puma::Type::Character",
                "str" => "Puma::Type::String",
                _ => type
            };
        }

        private static bool UsesBool(Node node)
        {
            if (node.Kind == NodeKind.PropertyDeclaration && string.Equals(GetPropertyValue(node), "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (node.Kind == NodeKind.PropertyDeclaration && string.Equals(GetPropertyValue(node), "false", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return node.Kind == NodeKind.FunctionDeclaration
                && (GetFunctionParameterList(node)?.Any(p => string.Equals(p.Type, "bool", StringComparison.OrdinalIgnoreCase)) ?? false)
                || (node.Kind == NodeKind.DelegateDeclaration
                    && (GetDelegateParameterList(node)?.Any(p => string.Equals(p.Type, "bool", StringComparison.OrdinalIgnoreCase)) ?? false));
        }

        private static void TrackOwnershipTransfer(
            Node statement,
            List<string> heapAllocatedGlobalProperties,
            HashSet<string?> globalNames,
            HashSet<string> propertiesAssignedToNone,
            Dictionary<string, string> transferredOwnershipLocals,
            HashSet<string> functionsReturningConstructedObject,
            HashSet<string> ownedLocalsToDelete)
        {
            if (statement.Kind != NodeKind.AssignmentStatement || GetAssignmentOperator(statement) != "=")
            {
                return;
            }

            var leftName = GetAssignmentLeft(statement)?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(leftName)
                && heapAllocatedGlobalProperties.Contains(leftName)
                && IsNoneExpression(GetAssignmentRightExpression(statement), GetAssignmentRight(statement)))
            {
                propertiesAssignedToNone.Add(leftName);
                return;
            }

            if (string.IsNullOrWhiteSpace(leftName)
                || !IsSimpleIdentifier(leftName)
                || globalNames.Contains(leftName))
            {
                return;
            }

            if (GetAssignmentRightExpression(statement)?.Kind == ExpressionKind.Identifier)
            {
                var source = GetAssignmentRightExpression(statement)!.Value ?? string.Empty;
                if (heapAllocatedGlobalProperties.Contains(source))
                {
                    transferredOwnershipLocals[source] = leftName;
                    ownedLocalsToDelete.Add(leftName);
                }

                return;
            }

            if (GetAssignmentRightExpression(statement)?.Kind != ExpressionKind.Call)
            {
                return;
            }

            if (GetAssignmentRightExpression(statement)!.Left?.Kind == ExpressionKind.Identifier)
            {
                var functionName = GetAssignmentRightExpression(statement)!.Left!.Value ?? string.Empty;
                var callText = GenerateExpression(GetAssignmentRightExpression(statement), GetAssignmentRight(statement)) ?? string.Empty;
                if (LooksLikeObjectConstructorCall(callText))
                {
                    ownedLocalsToDelete.Add(leftName);
                    return;
                }

                if (functionsReturningConstructedObject.Contains(functionName))
                {
                    ownedLocalsToDelete.Add(leftName);
                }

                return;
            }

            var callTarget = GetAssignmentRightExpression(statement)!.Left;
            if (callTarget?.Kind != ExpressionKind.MemberAccess || callTarget.Left?.Kind != ExpressionKind.Identifier)
            {
                return;
            }

            var sourceName = callTarget.Left.Value ?? string.Empty;
            if (heapAllocatedGlobalProperties.Contains(sourceName))
            {
                transferredOwnershipLocals[sourceName] = leftName;
            }
        }

        private static bool IsNoneExpression(ExpressionNode? expression, string? fallback)
        {
            if (expression?.Kind == ExpressionKind.Identifier
                && string.Equals(expression.Value, "none", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(fallback?.Trim(), "none", StringComparison.OrdinalIgnoreCase);
        }

        private static bool UsesTypedPropertyReassignmentMode(List<Node> ast)
        {
            var properties = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            if (properties.Count == 0)
            {
                return false;
            }

            var propertyNames = properties
                .Select(GetPropertyName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.Ordinal);

            if (!properties.All(p => TryGetTypedLiteralDeclaration(GetPropertyValue(p) ?? string.Empty, out var typeName, out _)
                && typeName is not "Puma::Type::String" and not "bool"))
            {
                return false;
            }

            var (startIndex, _) = FindSection(ast, Section.Start);
            if (startIndex < 0)
            {
                return false;
            }

            var startStatements = CollectStatements(ast, startIndex + 1);
            return startStatements.Count > 0
                && startStatements.All(s => s.Kind == NodeKind.AssignmentStatement
                    && GetAssignmentOperator(s) == "="
                    && !string.IsNullOrWhiteSpace(GetAssignmentLeft(s))
                    && propertyNames.Contains(GetAssignmentLeft(s)));
        }

        private static bool TryGetExpressionTypeAndInitializer(Node statement, out string typeName, out string initializer)
        {
            typeName = "int64_t";
            initializer = string.Empty;

            if (GetAssignmentRightExpression(statement) == null)
            {
                return false;
            }

            var expressionText = GenerateExpression(GetAssignmentRightExpression(statement), GetAssignmentRight(statement));
            if (string.IsNullOrWhiteSpace(expressionText))
            {
                return false;
            }

            if (ContainsBooleanKeyword(GetAssignmentRightExpression(statement)))
            {
                typeName = "bool";
            }
            else if (ContainsStringLiteral(GetAssignmentRightExpression(statement)))
            {
                typeName = "Puma::Type::String";
            }
            else if (ContainsDecimalLiteral(GetAssignmentRightExpression(statement)))
            {
                typeName = "double";
            }
            else
            {
                typeName = "int64_t";
            }

            initializer = expressionText;
            return true;
        }

        private static bool ContainsDecimalLiteral(ExpressionNode? expression)
        {
            if (expression == null)
            {
                return false;
            }

            if (expression.Kind == ExpressionKind.Literal
                && !string.IsNullOrWhiteSpace(expression.Value)
                && expression.Value.Contains('.'))
            {
                return true;
            }

            return ContainsDecimalLiteral(expression.Left)
                || ContainsDecimalLiteral(expression.Right)
                || expression.Arguments.Any(ContainsDecimalLiteral);
        }

        private static bool ContainsStringLiteral(ExpressionNode? expression)
        {
            if (expression == null)
            {
                return false;
            }

            if (expression.Kind == ExpressionKind.Literal
                && !string.IsNullOrWhiteSpace(expression.Value)
                && expression.Value.StartsWith("\"", StringComparison.Ordinal))
            {
                return true;
            }

            return ContainsStringLiteral(expression.Left)
                || ContainsStringLiteral(expression.Right)
                || expression.Arguments.Any(ContainsStringLiteral);
        }

        private static bool ContainsBooleanKeyword(ExpressionNode? expression)
        {
            if (expression == null)
            {
                return false;
            }

            if (expression.Kind == ExpressionKind.Identifier
                && (string.Equals(expression.Value, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(expression.Value, "false", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(expression.Value, "bool", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return ContainsBooleanKeyword(expression.Left)
                || ContainsBooleanKeyword(expression.Right)
                || expression.Arguments.Any(ContainsBooleanKeyword);
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
                return ("Puma::Type::String", value);
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

        private static bool TryEmitMainLocalDeclaration(Node statement, HashSet<string?> globalNames, HashSet<string> localNames, StringBuilder sb, string indent)
        {
            return TryEmitMainLocalDeclaration(statement, globalNames, localNames, sb, indent, out _, out _);
        }

        private static bool TryEmitMainLocalDeclaration(Node statement, HashSet<string?> globalNames, HashSet<string> localNames, StringBuilder sb, string indent, out bool usedExpressionFallback, out bool usedPropertyTypedAssignment)
        {
            usedExpressionFallback = false;
            usedPropertyTypedAssignment = false;

            if (statement.Kind != NodeKind.AssignmentStatement || GetAssignmentOperator(statement) != "=")
            {
                return false;
            }

            var leftName = GetAssignmentLeft(statement);
            if (string.IsNullOrWhiteSpace(leftName) || !IsSimpleIdentifier(leftName))
            {
                return false;
            }

            if (globalNames.Contains(leftName) || localNames.Contains(leftName))
            {
                return false;
            }

            var expressionFallback = false;
            if (!TryGetTypedLiteralDeclaration(GetAssignmentRight(statement) ?? string.Empty, out var typeName, out var value))
            {
                if (!TryGetExpressionTypeAndInitializer(statement, out typeName, out value))
                {
                    return false;
                }

                expressionFallback = true;
            }

            if (expressionFallback)
            {
                usedExpressionFallback = true;
                var normalized = GetAssignmentRightExpression(statement)?.Kind == ExpressionKind.Conditional
                    ? value
                    : UnwrapOutermostParentheses(value);
                sb.AppendLine($"{indent}auto {leftName} = {normalized};");
                localNames.Add(leftName);
                return true;
            }

            if (typeName == "Puma::Type::Character")
            {
                usedExpressionFallback = true;
            }

            var initializer = typeName switch
            {
                "Puma::Type::String" => ToPumaStringLiteral(value),
                "Puma::Type::Character" => $"Character({value})",
                "bool" => value,
                _ => $"({typeName}){value}"
            };

            sb.AppendLine($"{indent}auto {leftName} = {initializer};");
            localNames.Add(leftName);
            return true;
        }

        private static bool IsSimpleIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!(char.IsLetter(value[0]) || value[0] == '_'))
            {
                return false;
            }

            for (var i = 1; i < value.Length; i++)
            {
                if (!(char.IsLetterOrDigit(value[i]) || value[i] == '_'))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetTypedLiteralDeclaration(string rightText, out string typeName, out string literalValue)
        {
            typeName = "int64_t";
            literalValue = rightText;

            if (string.IsNullOrWhiteSpace(rightText))
            {
                return false;
            }

            var text = rightText.Trim();

            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
            {
                typeName = "bool";
                literalValue = text.ToLowerInvariant();
                return true;
            }

            if (string.Equals(text, "bool", StringComparison.OrdinalIgnoreCase))
            {
                typeName = "bool";
                literalValue = "false";
                return true;
            }

            if (text.StartsWith("\"", StringComparison.Ordinal))
            {
                typeName = "Puma::Type::String";
                literalValue = text;
                return true;
            }

            if (string.Equals(text, "str", StringComparison.OrdinalIgnoreCase))
            {
                typeName = "Puma::Type::String";
                literalValue = "\"\"";
                return true;
            }

            if (IsCharacterLiteralText(text))
            {
                typeName = "Puma::Type::Character";
                literalValue = text;
                return true;
            }

            var signLength = 0;
            if (text.StartsWith("-", StringComparison.Ordinal) || text.StartsWith("+", StringComparison.Ordinal))
            {
                signLength = 1;
            }

            var index = signLength;
            var dotSeen = false;
            var exponentSeen = false;
            var hasDigits = false;

            if (index + 1 < text.Length
                && text[index] == '0'
                && (text[index + 1] == 'x' || text[index + 1] == 'X'))
            {
                index += 2;
                var hexStart = index;
                while (index < text.Length && Uri.IsHexDigit(text[index]))
                {
                    index++;
                }

                hasDigits = index > hexStart;
            }
            else if (index + 1 < text.Length
                && text[index] == '0'
                && (text[index + 1] == 'b' || text[index + 1] == 'B'))
            {
                index += 2;
                var binStart = index;
                while (index < text.Length && (text[index] == '0' || text[index] == '1'))
                {
                    index++;
                }

                hasDigits = index > binStart;
            }
            else if (index + 1 < text.Length
                && text[index] == '0'
                && (text[index + 1] == 'o' || text[index + 1] == 'O'))
            {
                index += 2;
                var octStart = index;
                while (index < text.Length && text[index] >= '0' && text[index] <= '7')
                {
                    index++;
                }

                hasDigits = index > octStart;
            }
            else
            {
                while (index < text.Length)
                {
                    var ch = text[index];
                    if (char.IsDigit(ch))
                    {
                        hasDigits = true;
                        index++;
                        continue;
                    }

                    if (ch == '.' && !dotSeen && !exponentSeen)
                    {
                        dotSeen = true;
                        index++;
                        continue;
                    }

                    if ((ch == 'e' || ch == 'E') && !exponentSeen && hasDigits)
                    {
                        var expIndex = index + 1;
                        if (expIndex < text.Length && (text[expIndex] == '+' || text[expIndex] == '-'))
                        {
                            expIndex++;
                        }

                        var expDigitsStart = expIndex;
                        while (expIndex < text.Length && char.IsDigit(text[expIndex]))
                        {
                            expIndex++;
                        }

                        if (expIndex > expDigitsStart)
                        {
                            exponentSeen = true;
                            index = expIndex;
                            continue;
                        }
                    }

                    break;
                }
            }

            if (!hasDigits)
            {
                return false;
            }

            literalValue = text[..index];
            var suffix = text[index..];
            if (exponentSeen)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(suffix))
            {
                typeName = (dotSeen || exponentSeen) ? "double" : "int64_t";
                return true;
            }

            // Non-literal expressions (e.g. 2.0*PI*(r*r)) should be handled by expression inference.
            if (suffix.Any(ch => !char.IsLetterOrDigit(ch)))
            {
                return false;
            }

            typeName = suffix switch
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
                _ => "int64_t"
            };

            return true;
        }

        private static IEnumerable<Node> EnumerateAllNodes(IEnumerable<Node> nodes)
        {
            foreach (var node in nodes)
            {
                yield return node;

                foreach (var functionNode in node.Kind == NodeKind.FunctionDeclaration
                    ? EnumerateAllNodes(GetFunctionBody(node) ?? new List<Node>())
                    : Enumerable.Empty<Node>())
                {
                    yield return functionNode;
                }

                foreach (var statementNode in EnumerateAllNodes(GetStatementBody(node) ?? new List<Node>()))
                {
                    yield return statementNode;
                }

                foreach (var elseNode in node.Kind == NodeKind.IfStatement
                    ? EnumerateAllNodes(GetIfElseBody(node))
                    : Enumerable.Empty<Node>())
                {
                    yield return elseNode;
                }

                foreach (var typeFunction in node.Kind == NodeKind.TypeDeclaration
                    ? EnumerateAllNodes(GetTypeFunctions(node))
                    : Enumerable.Empty<Node>())
                {
                    yield return typeFunction;
                }
            }
        }
    }
}


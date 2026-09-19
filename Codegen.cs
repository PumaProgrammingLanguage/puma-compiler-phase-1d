// LLVM Compiler for the Puma programming language
//   as defined in the document "The Puma Programming Language Specification"
//   available at https://github.com/ThePumaProgrammingLanguage
//   
// Copyright © 2024-2026 by Darryl Anthony Burchfield
//
//   Licensed under the Apache License, Version 2.0 (the "License") WITH LLVM-exception;
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
    internal sealed record CodeGenerationResult(string SourceCode, IReadOnlyList<string> RequiredRuntimeLibraries);

    internal partial class Codegen
    {
        public Codegen()
        {
        }

        internal string Generate(List<Node> ast) => GenerateResult(ast).SourceCode;

        internal CodeGenerationResult GenerateResult(List<Node> ast)
        {
            var sb = new StringBuilder();
            var includes = new HashSet<string>(StringComparer.Ordinal);
            var hasWriteLine = ast.Any(n => n.Kind == NodeKind.WriteLine);
            if (hasWriteLine)
            {
                includes.Add("<PumaConsole/Console.hpp>");
            }

            var allNodes = EnumerateAllNodes(ast).ToList();
            var defaultExpressions = allNodes.SelectMany(GetParameters)
                .Select(parameter => parameter.DefaultExpression).ToList();
            if (defaultExpressions.Any(ContainsStringLiteral)) includes.Add("<PumaType/String.hpp>");
            if (defaultExpressions.Any(ContainsCharacterLiteral)) includes.Add("<PumaType/Character.hpp>");
            if (defaultExpressions.Any(ContainsBooleanKeyword)) includes.Add("<stdbool>");

            if (allNodes.SelectMany(GetExpressionRoots).Any(RequiresTypedIntegerHeader))
            {
                includes.Add("<cstdint>");
            }

            var needsStdBool = allNodes.Any(n => n.Kind == NodeKind.AssignmentStatement
                && GetAssignmentOperator(n) == "="
                && (ContainsBooleanKeyword(GetAssignmentRightExpression(n))
                    || IsIdentifier(GetAssignmentRightExpression(n), "bool")));
            if (!needsStdBool)
            {
                needsStdBool = allNodes.Any(n => n.Kind == NodeKind.RepeatStatement
                    && (GetRepeatExpressionNode(n) == null
                        || GetRepeatExpressionNode(n) is { Kind: ExpressionKind.Literal, Value: "1", DeclaredType: null }));
            }
            if (needsStdBool)
            {
                includes.Add("<stdbool>");
            }

            var needsString = allNodes.Any(n => n.Kind == NodeKind.AssignmentStatement
                && GetAssignmentOperator(n) == "="
                && (IsIdentifier(GetAssignmentRightExpression(n), "str")
                    || ContainsStringLiteral(GetAssignmentRightExpression(n))));
            if (needsString)
            {
                includes.Add("<PumaType/String.hpp>");
            }

            var needsCharacter = allNodes.Any(n => n.Kind == NodeKind.AssignmentStatement
                && GetAssignmentOperator(n) == "="
                && ContainsCharacterLiteral(GetAssignmentRightExpression(n)))
                || ast.Any(n => n.Kind == NodeKind.PropertyDeclaration
                    && (ContainsCharacterLiteral(GetPropertyValueExpression(n))
                        || string.Equals(GetPropertyType(n), "char", StringComparison.OrdinalIgnoreCase)))
                || allNodes.Any(n => (n.Kind == NodeKind.FunctionDeclaration
                        && (GetFunctionParameterList(n)?.Any(p => string.Equals(p.Type, "char", StringComparison.OrdinalIgnoreCase)) ?? false))
                    || (n.Kind == NodeKind.Section
                        && GetSectionParameterList(n).Any(p => string.Equals(p.Type, "char", StringComparison.OrdinalIgnoreCase)))
                    || (n.Kind == NodeKind.DelegateDeclaration
                        && (GetDelegateParameterList(n)?.Any(p => string.Equals(p.Type, "char", StringComparison.OrdinalIgnoreCase)) ?? false)));
            if (needsCharacter)
            {
                includes.Add("<PumaType/Character.hpp>");
            }

            var recordInitializers = ast.Where(n => n.Kind == NodeKind.RecordDeclaration)
                .SelectMany(GetRecordMemberDeclarations)
                .Select(member => member.ValueExpression)
                .ToList();
            var needsStdBoolForRecords = recordInitializers.Any(ContainsBooleanKeyword);
            if (needsStdBoolForRecords)
            {
                includes.Add("<stdbool>");
            }

            var needsStringForRecords = recordInitializers.Any(expression =>
                IsIdentifier(expression, "str") || ContainsStringLiteral(expression));
            if (needsStringForRecords)
            {
                includes.Add("<PumaType/String.hpp>");
            }

            if (recordInitializers.Any(ContainsCharacterLiteral))
            {
                includes.Add("<PumaType/Character.hpp>");
            }

            var needsCStdIntForRecords = recordInitializers.Any(expression =>
                TryGetTypedLiteralDeclaration(expression, out var typeName, out _)
                && typeName is "int64_t" or "int32_t" or "int16_t" or "int8_t"
                    or "uint64_t" or "uint32_t" or "uint16_t" or "uint8_t");
            if (needsCStdIntForRecords)
            {
                includes.Add("<cstdint>");
            }

            var hasStartSection = ast.Any(n => n.Kind == NodeKind.Section && n.Section == Section.Start);
            var propertyDeclarations = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration).ToList();
            var autoPropertiesMode = !hasStartSection && propertyDeclarations.Count > 0;
            if (autoPropertiesMode)
            {
                if (propertyDeclarations.Any(p => TryGetTypedLiteralDeclaration(GetPropertyValueExpression(p), out var typeName, out _)
                    && typeName is "int64_t" or "int32_t" or "int16_t" or "int8_t" or "uint64_t" or "uint32_t" or "uint16_t" or "uint8_t"))
                {
                    includes.Add("<cstdint>");
                }

                if (propertyDeclarations.Any(p => ContainsBooleanKeyword(GetPropertyValueExpression(p))))
                {
                    includes.Add("<stdbool>");
                }

                if (propertyDeclarations.Any(p => ContainsStringLiteral(GetPropertyValueExpression(p))
                    || IsIdentifier(GetPropertyValueExpression(p), "str")))
                {
                    includes.Add("<PumaType/String.hpp>");
                }
            }

            var needsStdIntForFunctionParameters = allNodes.Any(n => n.Kind == NodeKind.FunctionDeclaration
                && (GetFunctionParameterList(n)?.Any(p => MapType(p.Type) is "int64_t" or "int32_t" or "int16_t" or "int8_t" or "uint64_t" or "uint32_t" or "uint16_t" or "uint8_t") ?? false));
            if (needsStdIntForFunctionParameters)
            {
                includes.Add("<cstdint>");
            }

            var propertyNames = propertyDeclarations
                .Select(GetPropertyName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.Ordinal);

            var numericPropertyReassignmentMode = UsesTypedPropertyReassignmentMode(ast);

            var needsCStdIntForAssignments = hasStartSection && allNodes.Any(n => n.Kind == NodeKind.AssignmentStatement
                && GetAssignmentOperator(n) == "="
                && TryGetTypedLiteralDeclaration(n, out var typeName, out _)
                && typeName is "int64_t" or "int32_t" or "int16_t" or "int8_t" or "uint64_t" or "uint32_t" or "uint16_t" or "uint8_t");
            var shouldIncludeCStdIntForAssignments = needsCStdIntForAssignments
                && (numericPropertyReassignmentMode
                    || !propertyDeclarations.Any()
                    || includes.Contains("<PumaType/String.hpp>")
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
                else
                {
                    var include = GetUseInclude(node);
                    if (!string.IsNullOrWhiteSpace(include))
                    {
                        includes.Add(include);
                    }
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
                && string.Equals(GetTypeDeclarationKind(n), "module", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(GetTypeDeclarationName(n)));
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
                var wrappedModule = $"namespace {GetTypeDeclarationName(moduleNode)}\n{{\n{IndentBlock(moduleBody)}\n}}\n";
                output = includeLines.Count > 0
                    ? string.Join("\n", includeLines) + "\n\n" + wrappedModule
                    : wrappedModule;
            }

            return new CodeGenerationResult(output, GetRequiredRuntimeLibraries(includes));
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
                "<stdbool>" => 20,
                "<PumaType/Character.hpp>" => 30,
                "<PumaType/StringIterator.hpp>" => 31,
                "<PumaType/String.hpp>" => 32,
                "<PumaConsole/Console.hpp>" => 40,
                "<PumaFile/Directory.hpp>" => 50,
                "<PumaFile/Text.hpp>" => 51,
                _ => 100
            };
        }

        private static IReadOnlyList<string> GetRequiredRuntimeLibraries(IEnumerable<string> includes)
        {
            var requiredLibraries = new HashSet<string>(StringComparer.Ordinal);

            foreach (var include in includes)
            {
                if (include.StartsWith("<PumaConsole/", StringComparison.Ordinal))
                {
                    requiredLibraries.Add("PumaConsole");
                    requiredLibraries.Add("PumaType");
                }

                if (include.StartsWith("<PumaFile/", StringComparison.Ordinal))
                {
                    requiredLibraries.Add("PumaFile");
                    requiredLibraries.Add("PumaType");
                }

                if (include.StartsWith("<PumaType/", StringComparison.Ordinal))
                {
                    requiredLibraries.Add("PumaType");
                }
            }

            return [.. new[] { "PumaConsole", "PumaFile", "PumaType" }.Where(requiredLibraries.Contains)];
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
                sb.AppendLine($"Enums {GetEnumName(node)}");
                sb.AppendLine("{");
                foreach (var member in GetEnumMembers(node))
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
                var recordMembers = GetRecordMemberDeclarations(node);
                var hasAssignedMembers = recordMembers.Any(m => m.ValueExpression != null);
                var packedSuffix = GetRecordPackSize(node).HasValue ? " [[gnu::packed]]" : string.Empty;
                if (hasAssignedMembers)
                {
                    sb.AppendLine("// records");
                    sb.AppendLine($"struct {GetRecordName(node)}{packedSuffix}");
                    sb.AppendLine("{");
                    foreach (var member in recordMembers)
                    {
                        if (member.ValueExpression != null)
                        {
                            var initializer = FormatInitializer(member.ValueExpression);
                            sb.AppendLine($"    auto {member.Name} = {initializer};");
                        }
                        else
                        {
                            sb.AppendLine($"    int {member.Name};");
                        }
                    }
                    sb.AppendLine("};");
                    sb.AppendLine();
                    continue;
                }

                sb.AppendLine($"typedef struct {GetRecordName(node)} {{");
                foreach (var member in recordMembers)
                {
                    var memberType = string.Equals(member.Name, "Name", StringComparison.Ordinal) ? "stdstr" : "int";
                    sb.AppendLine($"    {memberType} {member.Name};");
                }
                sb.AppendLine($"}} {GetRecordName(node)};");
                sb.AppendLine();
            }
        }

        private static string FormatInitializer(ExpressionNode? expression)
        {
            if (expression == null)
            {
                throw new InvalidOperationException("Expression AST node is required for code generation.");
            }

            if (TryGetTypedLiteralDeclaration(expression, out var typeName, out var literalValue))
            {
                return typeName switch
                {
                    "bool" => literalValue,
                    "PumaType::String" => ToPumaStringLiteral(literalValue),
                    "PumaType::Character" => $"Character({literalValue})",
                    _ => $"({typeName}){literalValue}"
                };
            }

            var initializer = GenerateExpression(expression);
            return IsObjectConstructorCall(expression) ? $"new {initializer}" : initializer;
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
                    var initializer = FormatInitializer(GetPropertyValueExpression(node));
                    sb.AppendLine($"auto {GetPropertyName(node)} = {initializer};");
                }

                sb.AppendLine();
                return;
            }

            var shouldEmitPropertiesHeaderWithStart = globalProperties.Any(p => GetPropertyModifiers(p).Contains("const"))
                || globalProperties.Any(p => !string.IsNullOrWhiteSpace(GetPropertyType(p)));

            if (hasStartSection && globalProperties.Count > 0 && shouldEmitPropertiesHeaderWithStart)
            {
                sb.AppendLine("// properties");
            }

            foreach (var node in globalProperties)
            {
                var propertyExpression = GetPropertyValueExpression(node);
                var propertyName = GetPropertyName(node);
                var modifiers = GetPropertyModifiers(node).Contains("const") ? "const " : string.Empty;
                var shouldUseAuto = propertyExpression?.Kind != ExpressionKind.Identifier
                    || IsIdentifier(propertyExpression, "true")
                    || IsIdentifier(propertyExpression, "false")
                    || IsIdentifier(propertyExpression, "bool")
                    || IsIdentifier(propertyExpression, "str");

                if (shouldUseAuto)
                {
                    var initializer = FormatInitializer(propertyExpression);
                    sb.AppendLine($"{modifiers}auto {propertyName} = {initializer};");
                }
                else
                {
                    sb.AppendLine($"{modifiers}{propertyExpression!.Value} {propertyName} = {{0}};");
                }
            }

            if (globalProperties.Count > 0)
            {
                sb.AppendLine();
            }
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

        private static List<Node.ParameterInfo> GetSectionParameterList(Node node)
        {
            return node is SectionAstNode typedNode
                ? typedNode.SectionParameterList
                : new List<Node.ParameterInfo>();
        }

        private static int GetSectionLeadingBlankLines(Node node)
        {
            return node is SectionAstNode typedNode
                ? typedNode.LeadingBlankLines
                : 0;
        }

        private static string? GetEnumName(Node node)
        {
            return node is EnumDeclarationAstNode typedNode
                ? typedNode.EnumName
                : null;
        }

        private static List<string> GetEnumMembers(Node node)
        {
            return node is EnumDeclarationAstNode typedNode
                ? typedNode.EnumMembers
                : new List<string>();
        }

        private static string? GetRecordName(Node node)
        {
            return node is RecordDeclarationAstNode typedNode
                ? typedNode.RecordName
                : null;
        }

        private static int? GetRecordPackSize(Node node)
        {
            return node is RecordDeclarationAstNode typedNode
                ? typedNode.RecordPackSize
                : null;
        }

        private static List<RecordMemberInfo> GetRecordMemberDeclarations(Node node)
        {
            return node is RecordDeclarationAstNode typedNode
                ? typedNode.MemberDeclarations
                : new List<RecordMemberInfo>();
        }

        private static string? GetAssignmentOperator(Node node)
        {
            return node is AssignmentStatementAstNode typedNode
                ? typedNode.AssignmentOperator
                : null;
        }

        private static ExpressionNode? GetAssignmentLeftExpression(Node node)
        {
            return node is AssignmentStatementAstNode typedNode
                ? typedNode.AssignmentLeftExpression
                : null;
        }

        private static ExpressionNode? GetAssignmentRightExpression(Node node)
        {
            return node is AssignmentStatementAstNode typedNode
                ? typedNode.AssignmentRightExpression
                : null;
        }

        private static bool GetIsLoweredPostfixMutation(Node node)
        {
            return node is AssignmentStatementAstNode typedNode
                ? typedNode.IsLoweredPostfixMutation
                : false;
        }

        private static ExpressionNode? GetRepeatExpressionNode(Node node)
        {
            return node is RepeatStatementAstNode typedNode
                ? typedNode.RepeatExpressionNode
                : null;
        }

        private static string? GetPropertyName(Node? node)
        {
            if (node == null)
            {
                return null;
            }

            return node is PropertyDeclarationAstNode typedNode
                ? typedNode.PropertyName
                : null;
        }

        private static ExpressionNode? GetPropertyValueExpression(Node node)
        {
            return node is PropertyDeclarationAstNode typedNode
                ? typedNode.PropertyValueExpression
                : null;
        }

        private static string? GetPropertyType(Node node)
        {
            return node is PropertyDeclarationAstNode typedNode
                ? typedNode.PropertyValueExpression?.DeclaredType
                : null;
        }

        private static List<Node>? GetFunctionBody(Node node)
        {
            return node is FunctionDeclarationAstNode typedNode
                ? typedNode.FunctionBody
                : null;
        }

        private static List<Node.ParameterInfo>? GetFunctionParameterList(Node node)
        {
            return node is FunctionDeclarationAstNode typedNode
                ? typedNode.FunctionParameterList
                : null;
        }

        private static List<Node.ParameterInfo>? GetDelegateParameterList(Node node)
        {
            return node is DelegateDeclarationAstNode typedNode
                ? typedNode.DelegateParameterList
                : null;
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

        private static string GetUseInclude(Node node)
        {
            var target = GetUseStatementTarget(node);
            if (string.IsNullOrWhiteSpace(target))
            {
                return string.Empty;
            }

            if (GetUseStatementIsFilePath(node))
            {
                var includeTarget = target;
                if (includeTarget.EndsWith(".puma", StringComparison.OrdinalIgnoreCase))
                {
                    includeTarget = includeTarget[..^5] + ".h";
                }

                return $"\"{includeTarget}\"";
            }

            var normalizedTarget = target.Replace('.', '/');
            if (normalizedTarget.StartsWith("PumaType/", StringComparison.Ordinal)
                || normalizedTarget.StartsWith("PumaConsole/", StringComparison.Ordinal)
                || normalizedTarget.StartsWith("PumaFile/", StringComparison.Ordinal))
            {
                if (!normalizedTarget.EndsWith(".hpp", StringComparison.OrdinalIgnoreCase)
                    && !normalizedTarget.EndsWith(".h", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedTarget += ".hpp";
                }
            }

            return $"<{normalizedTarget}>";
        }

        private static List<string> GetPropertyModifiers(Node node)
        {
            if (node is PropertyDeclarationAstNode typedNode && typedNode.PropertyModifiers.Count > 0)
            {
                return typedNode.PropertyModifiers;
            }

            return new List<string>();
        }

        private static List<string> GetFunctionModifiers(Node node)
        {
            if (node is FunctionDeclarationAstNode typedNode && typedNode.FunctionModifiers.Count > 0)
            {
                return typedNode.FunctionModifiers;
            }

            return new List<string>();
        }

        private static string? GetDelegateDeclarationName(Node node)
        {
            return node is DelegateDeclarationAstNode typedNode
                ? typedNode.DelegateName
                : null;
        }

        private static List<Node> GetTypeProperties(Node node)
        {
            return node is TypeDeclarationAstNode typedNode
                ? typedNode.TypeProperties
                : new List<Node>();
        }

        private static List<Node> GetTypeFunctions(Node node)
        {
            return node is TypeDeclarationAstNode typedNode
                ? typedNode.TypeFunctions
                : new List<Node>();
        }

        private static string? GetFunctionDeclarationName(Node node)
        {
            return node is FunctionDeclarationAstNode typedNode
                ? typedNode.FunctionDeclarationName
                : null;
        }

        private static string? GetFunctionDeclarationReturnType(Node node)
        {
            return node is FunctionDeclarationAstNode typedNode
                ? typedNode.FunctionDeclarationReturnType
                : null;
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
            return node is TypeDeclarationAstNode typedNode
                ? typedNode.DeclarationKind
                : null;
        }

        private static string? GetTypeDeclarationName(Node node)
        {
            return node is TypeDeclarationAstNode typedNode
                ? typedNode.DeclarationName
                : null;
        }

        private static string? GetTypeBaseTypeName(Node node)
        {
            return node is TypeDeclarationAstNode typedNode
                ? typedNode.BaseTypeName
                : null;
        }

        private static List<string> GetTypeTraitNames(Node node)
        {
            return node is TypeDeclarationAstNode typedNode
                ? typedNode.TraitNames
                : new List<string>();
        }

        private static ExpressionNode? GetStatementExpression(Node node)
        {
            return node is StatementAstNode typedNode
                ? typedNode.StatementExpression
                : null;
        }

        private static List<Node> GetStatementBody(Node node)
        {
            return node.Kind switch
            {
                NodeKind.IfStatement when node is IfStatementAstNode typedIf => typedIf.IfBody,
                NodeKind.MatchStatement when node is MatchStatementAstNode typedMatch => typedMatch.StatementBody,
                NodeKind.WhenStatement when node is WhenStatementAstNode typedWhen => typedWhen.StatementBody,
                NodeKind.WhileStatement when node is WhileStatementAstNode typedWhile => typedWhile.StatementBody,
                NodeKind.ForStatement when node is ForStatementAstNode typedFor => typedFor.StatementBody,
                NodeKind.ForAllStatement when node is ForAllStatementAstNode typedForAll => typedForAll.StatementBody,
                NodeKind.RepeatStatement when node is RepeatStatementAstNode typedRepeat => typedRepeat.StatementBody,
                NodeKind.HasStatement when node is HasStatementAstNode typedHas => typedHas.StatementBody,
                NodeKind.HasTraitStatement when node is HasTraitStatementAstNode typedHasTrait => typedHasTrait.StatementBody,
                NodeKind.ErrorStatement or NodeKind.CatchStatement or NodeKind.ElseStatement when node is StatementAstNode typedStatement => typedStatement.StatementBody,
                _ => new List<Node>()
            };
        }

        private static ExpressionNode? GetIfConditionExpression(Node node)
        {
            return node is IfStatementAstNode typedNode
                ? typedNode.ConditionExpression
                : null;
        }

        private static List<Node> GetIfElseBody(Node node)
        {
            return node is IfStatementAstNode typedNode
                ? typedNode.ElseBody
                : new List<Node>();
        }

        private static ExpressionNode? GetMatchExpressionNode(Node node)
        {
            return node is MatchStatementAstNode typedNode
                ? typedNode.ExpressionNode
                : null;
        }

        private static ExpressionNode? GetWhenExpression(Node node)
        {
            return node is WhenStatementAstNode typedNode
                ? typedNode.WhenExpression
                : null;
        }

        private static ExpressionNode? GetWhileExpression(Node node)
        {
            return node is WhileStatementAstNode typedNode
                ? typedNode.WhileExpression
                : null;
        }

        private static string? GetForVariable(Node node)
        {
            return node.Kind switch
            {
                NodeKind.ForStatement when node is ForStatementAstNode typedNode => typedNode.ForVariable,
                NodeKind.ForAllStatement when node is ForAllStatementAstNode typedNode => typedNode.ForVariable,
                _ => null
            };
        }

        private static string? GetForContainer(Node node)
        {
            return node.Kind switch
            {
                NodeKind.ForStatement when node is ForStatementAstNode typedNode => typedNode.ForContainer,
                NodeKind.ForAllStatement when node is ForAllStatementAstNode typedNode => typedNode.ForContainer,
                _ => null
            };
        }

        private static ExpressionNode? GetForContainerExpression(Node node)
        {
            return node.Kind switch
            {
                NodeKind.ForStatement when node is ForStatementAstNode typedNode => typedNode.ForContainerExpression,
                NodeKind.ForAllStatement when node is ForAllStatementAstNode typedNode => typedNode.ForContainerExpression,
                _ => null
            };
        }

        private static ExpressionNode? GetHasExpression(Node node)
        {
            return node is HasStatementAstNode typedNode
                ? typedNode.HasExpression
                : null;
        }

        private static string? GetHasTraitTypeName(Node node)
        {
            return node is HasTraitStatementAstNode typedNode
                ? typedNode.HasTraitTypeName
                : null;
        }

        private static ExpressionNode? GetHasTraitExpression(Node node)
        {
            return node is HasTraitStatementAstNode typedNode
                ? typedNode.HasTraitExpression
                : null;
        }

        private static bool IsObjectConstructorCall(ExpressionNode? expression)
        {
            return expression?.Kind == ExpressionKind.Call
                && expression.Left?.Kind == ExpressionKind.Identifier
                && expression.Left.Value is { Length: > 0 } name
                && name is not "List" and not "Range" and not "Array"
                && char.IsUpper(name[0]);
        }

        private static string ToPumaStringLiteral(string literal)
        {
            if (string.IsNullOrWhiteSpace(literal))
            {
                return "PumaType::String(\"\", sizeof(\"\") - 1)";
            }

            if (literal.StartsWith("PumaType::String(", StringComparison.Ordinal))
            {
                return literal;
            }

            return $"PumaType::String({literal}, sizeof({literal}) - 1)";
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
                sb.AppendLine($"typedef void (*{GetDelegateDeclarationName(node)})({parameters});");
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

            var sectionParameters = GetSectionParameterList(sectionNode);
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
                for (var i = 0; i < GetSectionLeadingBlankLines(startSection); i++)
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine("// start");
            sb.AppendLine("int main()");
            sb.AppendLine("{");
            if (initIndex >= 0 && initSection != null)
            {
                sb.AppendLine($"    initialize({FormatArguments(GetSectionParameterList(initSection))});");
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
                    && IsObjectConstructorCall(GetPropertyValueExpression(n)))
                .Select(n => GetPropertyName(n)!)
                .ToList();
            var functionsReturningConstructedObject = ast
                .Where(n => n.Kind == NodeKind.FunctionDeclaration
                    && ((GetFunctionBody(n)?.Any(s => s.Kind == NodeKind.ReturnStatement
                        && IsObjectConstructorCall(GetStatementExpression(s))))
                        ?? false))
                .Select(GetFunctionDeclarationName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
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
                sb.AppendLine($"    finalize({FormatArguments(GetSectionParameterList(finalSection))});");
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
                var left = GetSimpleIdentifier(GetAssignmentLeftExpression(statement));
                var rightExpression = GetAssignmentRightExpression(statement);
                if (statement.Kind == NodeKind.AssignmentStatement
                    && GetAssignmentOperator(statement) == "="
                    && !string.IsNullOrWhiteSpace(left)
                    && IsSimpleIdentifier(left)
                    && !declared.Contains(left)
                    && ContainsStringLiteral(rightExpression))
                {
                    var value = ToPumaStringLiteral(GenerateExpression(rightExpression));
                    sb.AppendLine($"{indent}auto {left} = {value};");
                    declared.Add(left);
                    continue;
                }

                EmitStatements(new List<Node> { statement }, sb, indent, ast);
            }
        }

        private static string BuildCallWithDefaultArguments(string functionName, ExpressionNode callExpressionNode, List<Node>? ast)
        {
            if (ast == null)
            {
                return $"{functionName}({string.Join(", ", callExpressionNode.Arguments.Select(GenerateExpression))})";
            }

            var declaration = ast.FirstOrDefault(n => n.Kind == NodeKind.FunctionDeclaration
                && string.Equals(GetFunctionDeclarationName(n), functionName, StringComparison.Ordinal));
            if (declaration == null)
            {
                return $"{functionName}({string.Join(", ", callExpressionNode.Arguments.Select(GenerateExpression))})";
            }

            var arguments = callExpressionNode.Arguments
                .Select(GenerateExpression)
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
                var value = FormatInitializer(GetPropertyValueExpression(property));
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
                            var leftExpression = GenerateExpression(GetAssignmentLeftExpression(node));
                            var rightExpression = GenerateExpression(GetAssignmentRightExpression(node));

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
                                var left0 = GenerateExpression(GetAssignmentLeftExpression(node)!.Left);
                                var left1 = GenerateExpression(GetAssignmentLeftExpression(node)!.Right);
                                var right0 = GenerateExpression(GetAssignmentRightExpression(node)!.Left);
                                var right1 = GenerateExpression(GetAssignmentRightExpression(node)!.Right);
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
                                && ContainsStringLiteral(GetAssignmentRightExpression(node)))
                            {
                                rightExpression = ToPumaStringLiteral(rightExpression);
                            }

                            if (GetAssignmentOperator(node) == "="
                                && GetAssignmentRightExpression(node)?.Kind == ExpressionKind.Literal
                                && ContainsCharacterLiteral(GetAssignmentRightExpression(node)))
                            {
                                rightExpression = $"Character({rightExpression})";
                            }

                            var emittedTypedPropertyLiteral = false;
                            if (GetAssignmentOperator(node) == "=" && GetAssignmentRightExpression(node)?.Kind == ExpressionKind.Literal)
                            {
                                var propertyNode = ast?.FirstOrDefault(n => n.Kind == NodeKind.PropertyDeclaration
                                    && string.Equals(GetPropertyName(n), GetSimpleIdentifier(GetAssignmentLeftExpression(node)), StringComparison.Ordinal));
                                if (propertyNode != null
                                    && ast != null
                                    && UsesTypedPropertyReassignmentMode(ast)
                                    && TryGetTypedLiteralDeclaration(node, out var typedLiteralName, out var typedLiteralValue))
                                {
                                    rightExpression = typedLiteralName switch
                                    {
                                        "PumaType::String" => ToPumaStringLiteral(typedLiteralValue),
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
                            var callExpression = GenerateExpression(callExpressionNode);
                            if (!string.IsNullOrWhiteSpace(callExpression) && callExpressionNode?.Kind == ExpressionKind.Call)
                            {
                                var functionName = GenerateExpression(callExpressionNode.Left);
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
                            sb.AppendLine($"{indent}PumaConsole::WriteLn({GetWriteLineStringValue(node)});");
                        }
                        break;
                    case NodeKind.IfStatement:
                        sb.AppendLine($"{indent}if ({UnwrapOutermostParentheses(GenerateExpression(GetIfConditionExpression(node)))})");
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
                        sb.AppendLine($"{indent}switch ({GenerateExpression(GetMatchExpressionNode(node))})");
                        sb.AppendLine($"{indent}{{");
                        foreach (var when in GetStatementBody(node).Where(n => n.Kind == NodeKind.WhenStatement))
                        {
                            sb.AppendLine($"{indent}    case {GenerateExpression(GetWhenExpression(when))}:");
                            EmitStatements(GetStatementBody(when), sb, indent + "        ");
                            sb.AppendLine($"{indent}        break;");
                        }
                        sb.AppendLine($"{indent}}}");
                        break;
                    case NodeKind.WhenStatement:
                        sb.AppendLine($"{indent}/* when {GenerateExpression(GetWhenExpression(node))} */");
                        break;
                    case NodeKind.WhileStatement:
                        sb.AppendLine($"{indent}while ({UnwrapOutermostParentheses(GenerateExpression(GetWhileExpression(node)))})");
                        sb.AppendLine($"{indent}{{");
                        EmitStatements(GetStatementBody(node), sb, indent + "    ");
                        sb.AppendLine($"{indent}}}");
                        break;
                    case NodeKind.ForStatement:
                    case NodeKind.ForAllStatement:
                        sb.AppendLine($"{indent}for (auto {GetForVariable(node)} : {GenerateExpression(GetForContainerExpression(node))})");
                        sb.AppendLine($"{indent}{{");
                        EmitStatements(GetStatementBody(node), sb, indent + "    ");
                        sb.AppendLine($"{indent}}}");
                        break;
                    case NodeKind.RepeatStatement:
                        {
                            var repeatCondition = GetRepeatExpressionNode(node) is { } expression
                                ? GenerateExpression(expression)
                                : "true";
                            sb.AppendLine($"{indent}do");
                            sb.AppendLine($"{indent}{{");
                            EmitStatements(GetStatementBody(node), sb, indent + "    ");
                            sb.AppendLine($"{indent}}} while ({repeatCondition});");
                            break;
                        }
                    case NodeKind.HasStatement:
                        sb.AppendLine($"{indent}if ({GenerateExpression(GetHasExpression(node))} != null)");
                        sb.AppendLine($"{indent}{{");
                        EmitStatements(GetStatementBody(node), sb, indent + "    ");
                        sb.AppendLine($"{indent}}}");
                        break;
                    case NodeKind.HasTraitStatement:
                        {
                            var variable = GenerateExpression(GetHasTraitExpression(node));
                            var traitType = GetHasTraitTypeName(node) ?? "Trait";
                            sb.AppendLine($"{indent}if ({variable} != null && typeof({variable}) == typeof({traitType}))");
                            sb.AppendLine($"{indent}{{");
                            EmitStatements(GetStatementBody(node), sb, indent + "    ");
                            sb.AppendLine($"{indent}}}");
                            break;
                        }
                    case NodeKind.ReturnStatement:
                        {
                            if (GetStatementExpression(node) is not { } expression)
                            {
                                sb.AppendLine($"{indent}return;");
                            }
                            else
                            {
                                var returnExpression = UnwrapOutermostParentheses(GenerateExpression(expression));
                                sb.AppendLine($"{indent}return {returnExpression};");
                            }

                            break;
                        }
                    case NodeKind.YieldStatement:
                        sb.AppendLine($"{indent}/* yield {GenerateExpression(GetStatementExpression(node))} */");
                        break;
                    case NodeKind.BreakStatement:
                        sb.AppendLine($"{indent}break;");
                        break;
                    case NodeKind.ContinueStatement:
                        sb.AppendLine($"{indent}continue;");
                        break;
                    case NodeKind.ErrorStatement:
                        sb.AppendLine($"{indent}/* error {GenerateExpression(GetStatementExpression(node))} */");
                        break;
                    case NodeKind.CatchStatement:
                        sb.AppendLine($"{indent}/* catch {GenerateExpression(GetStatementExpression(node))} */");
                        break;
                }
            }
        }

        private static string GenerateExpression(ExpressionNode? node)
        {
            if (node == null)
            {
                throw new InvalidOperationException("Expression AST node is required for code generation.");
            }

            return node.Kind switch
            {
                ExpressionKind.Identifier => string.Equals(node.Value, "none", StringComparison.OrdinalIgnoreCase)
                    ? "null"
                    : node.Value ?? string.Empty,
                ExpressionKind.Literal => !string.IsNullOrWhiteSpace(node.DeclaredType)
                    ? $"({MapType(node.DeclaredType) ?? node.DeclaredType}){node.Value}"
                    : node.Value ?? string.Empty,
                ExpressionKind.Unary => string.Equals(node.Value, "not", StringComparison.Ordinal)
                    ? $"!{GenerateExpression(node.Left)}"
                    : $"{node.Value}{GenerateExpression(node.Left)}",
                ExpressionKind.Cast => $"({MapType(node.Value) ?? node.Value}) {GenerateExpression(node.Left)}",
                ExpressionKind.Conditional => $"({GenerateExpression(node.Left)} ? {GenerateExpression(node.Right)} : {GenerateExpression(node.Arguments.FirstOrDefault())})",
                ExpressionKind.Binary => $"({GenerateExpression(node.Left)} {MapBinaryOperator(node.Value)} {GenerateExpression(node.Right)})",
                ExpressionKind.MemberAccess => $"{GenerateExpression(node.Left)}.{node.Value}",
                ExpressionKind.Index => $"{GenerateExpression(node.Left)}[{GenerateExpression(node.Right)}]",
                ExpressionKind.Call => $"{GenerateExpression(node.Left)}({string.Join(", ", node.Arguments.Select(GenerateExpression))})",
                _ => throw new InvalidOperationException($"Unsupported expression kind '{node.Kind}'.")
            };
        }

        private static string? MapBinaryOperator(string? value) => value switch
        {
            "and" => "&&",
            "or" => "||",
            _ => value
        };

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
            if (parameter.DefaultExpression is { } expression)
            {
                return expression.Kind == ExpressionKind.Literal
                    && (ContainsStringLiteral(expression) || ContainsCharacterLiteral(expression))
                    ? FormatInitializer(expression)
                    : GenerateExpression(expression);
            }

            var type = MapType(parameter.Type) ?? "int64_t";
            return type switch
            {
                "PumaType::String" => "PumaType::String(\"\", sizeof(\"\") - 1)",
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
                "char" => "PumaType::Character",
                "str" => "PumaType::String",
                _ => type
            };
        }

        private static bool UsesBool(Node node)
        {
            if (node.Kind == NodeKind.PropertyDeclaration && ContainsBooleanKeyword(GetPropertyValueExpression(node)))
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

            var leftName = GetSimpleIdentifier(GetAssignmentLeftExpression(statement)) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(leftName)
                && heapAllocatedGlobalProperties.Contains(leftName)
                && IsNoneExpression(GetAssignmentRightExpression(statement)))
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
                if (IsObjectConstructorCall(GetAssignmentRightExpression(statement)))
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

        private static bool IsNoneExpression(ExpressionNode? expression)
        {
            return expression?.Kind == ExpressionKind.Identifier
                && string.Equals(expression.Value, "none", StringComparison.OrdinalIgnoreCase);
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

            if (!properties.All(p => TryGetTypedLiteralDeclaration(GetPropertyValueExpression(p), out var typeName, out _)
                && typeName is not "PumaType::String" and not "bool"))
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
                    && GetSimpleIdentifier(GetAssignmentLeftExpression(s)) is { } assignmentName
                    && propertyNames.Contains(assignmentName));
        }

        private static bool TryGetExpressionTypeAndInitializer(Node statement, out string typeName, out string initializer)
        {
            typeName = "int64_t";
            initializer = string.Empty;

            if (GetAssignmentRightExpression(statement) == null)
            {
                return false;
            }

            var expressionText = GenerateExpression(GetAssignmentRightExpression(statement));
            if (string.IsNullOrWhiteSpace(expressionText))
            {
                return false;
            }

            if (IsIdentifier(GetAssignmentRightExpression(statement), "bool"))
            {
                typeName = "bool";
                initializer = "false";
                return true;
            }

            if (IsIdentifier(GetAssignmentRightExpression(statement), "str"))
            {
                typeName = "PumaType::String";
                initializer = ToPumaStringLiteral("\"\"");
                return true;
            }

            if (ContainsBooleanKeyword(GetAssignmentRightExpression(statement)))
            {
                typeName = "bool";
            }
            else if (ContainsStringLiteral(GetAssignmentRightExpression(statement)))
            {
                typeName = "PumaType::String";
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

            var leftName = GetSimpleIdentifier(GetAssignmentLeftExpression(statement));
            if (string.IsNullOrWhiteSpace(leftName) || !IsSimpleIdentifier(leftName))
            {
                return false;
            }

            if (globalNames.Contains(leftName) || localNames.Contains(leftName))
            {
                return false;
            }

            var expressionFallback = false;
            string typeName;
            string value;
            var structuredInitializer = GenerateExpression(GetAssignmentRightExpression(statement));
            if (GetAssignmentRightExpression(statement)?.Kind is ExpressionKind.Cast or ExpressionKind.Binary or ExpressionKind.Conditional)
            {
                typeName = "int64_t";
                value = structuredInitializer;
                expressionFallback = true;
            }
            else if (!TryGetTypedLiteralDeclaration(statement, out typeName, out value))
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

            if (typeName == "PumaType::Character")
            {
                usedExpressionFallback = true;
            }

            var initializer = typeName switch
            {
                "PumaType::String" => ToPumaStringLiteral(value),
                "PumaType::Character" => $"Character({value})",
                "bool" => value,
                _ when value.StartsWith($"({typeName})", StringComparison.Ordinal) => value,
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

        private static string? GetSimpleIdentifier(ExpressionNode? expression)
        {
            return expression?.Kind == ExpressionKind.Identifier && IsSimpleIdentifier(expression.Value ?? string.Empty)
                ? expression.Value
                : null;
        }

        private static bool IsIdentifier(ExpressionNode? expression, string value)
        {
            return expression?.Kind == ExpressionKind.Identifier
                && string.Equals(expression.Value, value, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsLiteral(ExpressionNode? expression, Func<string, bool> predicate)
        {
            if (expression == null)
            {
                return false;
            }

            return expression.Kind == ExpressionKind.Literal && predicate(expression.Value ?? string.Empty)
                || ContainsLiteral(expression.Left, predicate)
                || ContainsLiteral(expression.Right, predicate)
                || expression.Arguments.Any(argument => ContainsLiteral(argument, predicate));
        }

        private static bool ContainsCharacterLiteral(ExpressionNode? expression) =>
            ContainsLiteral(expression, IsCharacterLiteralText);

        private static bool TryGetTypedLiteralDeclaration(ExpressionNode? expression, out string typeName, out string literalValue)
        {
            typeName = "int64_t";
            literalValue = string.Empty;

            if (expression == null)
            {
                return false;
            }

            var literal = expression;
            var unaryOperator = string.Empty;
            if (literal.Kind == ExpressionKind.Unary && literal.Value is "+" or "-")
            {
                unaryOperator = literal.Value;
                literal = literal.Left;
            }

            if (literal?.Kind is not (ExpressionKind.Literal or ExpressionKind.Identifier)
                || string.IsNullOrWhiteSpace(literal.Value))
            {
                return false;
            }

            var value = literal.Value;
            literalValue = unaryOperator + value;
            if (value is "true" or "false" or "bool")
            {
                typeName = "bool";
                literalValue = value == "bool" ? "false" : value;
                return true;
            }

            if (value == "str" || value.StartsWith('"'))
            {
                typeName = "PumaType::String";
                literalValue = value == "str" ? "\"\"" : value;
                return true;
            }

            if (IsCharacterLiteralText(value))
            {
                typeName = "PumaType::Character";
                return true;
            }

            var unsignedValue = value.TrimStart('+', '-');
            if (literal.Kind != ExpressionKind.Literal || unsignedValue.Length == 0 || !char.IsDigit(unsignedValue[0]))
            {
                return false;
            }

            var isPrefixedInteger = unsignedValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                || unsignedValue.StartsWith("0b", StringComparison.OrdinalIgnoreCase)
                || unsignedValue.StartsWith("0o", StringComparison.OrdinalIgnoreCase);
            var isFloatingPoint = !isPrefixedInteger && (value.Contains('.') || value.Contains('e') || value.Contains('E'));
            typeName = MapType(literal.DeclaredType) ?? (isFloatingPoint ? "double" : "int64_t");
            return true;
        }

        private static bool TryGetTypedLiteralDeclaration(Node statement, out string typeName, out string literalValue)
        {
            return TryGetTypedLiteralDeclaration(GetAssignmentRightExpression(statement), out typeName, out literalValue);
        }

        private static IEnumerable<Node.ParameterInfo> GetParameters(Node node) => node switch
        {
            FunctionDeclarationAstNode function => function.FunctionParameterList,
            SectionAstNode section => section.SectionParameterList,
            DelegateDeclarationAstNode declaration => declaration.DelegateParameterList,
            _ => Enumerable.Empty<Node.ParameterInfo>()
        };

        private static IEnumerable<ExpressionNode?> GetExpressionRoots(Node node) => node switch
        {
            FunctionDeclarationAstNode or SectionAstNode or DelegateDeclarationAstNode => GetParameters(node).Select(parameter => parameter.DefaultExpression),
            AssignmentStatementAstNode assignment => new[] { assignment.AssignmentLeftExpression, assignment.AssignmentRightExpression },
            PropertyDeclarationAstNode property => new[] { property.PropertyValueExpression },
            RecordDeclarationAstNode record => record.MemberDeclarations.Select(member => member.ValueExpression),
            TypeDeclarationAstNode type => type.TypeProperties.SelectMany(GetExpressionRoots),
            FunctionCallAstNode call => new[] { call.Expression },
            IfStatementAstNode conditional => new[] { conditional.ConditionExpression },
            MatchStatementAstNode match => new[] { match.ExpressionNode },
            WhenStatementAstNode whenStatement => new[] { whenStatement.WhenExpression },
            WhileStatementAstNode loop => new[] { loop.WhileExpression },
            ForStatementAstNode loop => new[] { loop.ForContainerExpression },
            ForAllStatementAstNode loop => new[] { loop.ForContainerExpression },
            RepeatStatementAstNode loop => new[] { loop.RepeatExpressionNode },
            HasStatementAstNode has => new[] { has.HasExpression },
            HasTraitStatementAstNode has => new[] { has.HasTraitExpression },
            StatementAstNode statement => new[] { statement.StatementExpression },
            _ => Enumerable.Empty<ExpressionNode?>()
        };

        private static bool RequiresTypedIntegerHeader(ExpressionNode? expression)
        {
            if (expression == null)
            {
                return false;
            }

            var type = expression.Kind == ExpressionKind.Cast ? expression.Value : expression.DeclaredType;
            return MapType(type) is "int64_t" or "int32_t" or "int16_t" or "int8_t"
                    or "uint64_t" or "uint32_t" or "uint16_t" or "uint8_t"
                || RequiresTypedIntegerHeader(expression.Left)
                || RequiresTypedIntegerHeader(expression.Right)
                || expression.Arguments.Any(RequiresTypedIntegerHeader);
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


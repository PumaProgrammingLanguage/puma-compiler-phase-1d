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
                && n.AssignmentOperator == "="
                && (string.Equals(n.AssignmentRight, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n.AssignmentRight, "false", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n.AssignmentRight, "bool", StringComparison.OrdinalIgnoreCase)));
            if (!needsStdBool)
            {
                needsStdBool = allNodes.Any(n => n.Kind == NodeKind.RepeatStatement
                    && (string.IsNullOrWhiteSpace(n.RepeatExpression)
                        || string.Equals(n.RepeatExpression, "1", StringComparison.Ordinal)));
            }
            if (needsStdBool)
            {
                includes.Add("<stdbool>");
            }

            var needsString = ast.Any(n => n.Kind == NodeKind.AssignmentStatement
                && n.AssignmentOperator == "="
                && (!string.IsNullOrWhiteSpace(n.AssignmentRight)
                    && (string.Equals(n.AssignmentRight, "str", StringComparison.OrdinalIgnoreCase)
                        || n.AssignmentRight.StartsWith("\"", StringComparison.Ordinal))));
            if (needsString)
            {
                includes.Add("<String.hpp>");
            }

            var needsStringH = ast.Where(n => n.Kind == NodeKind.FunctionDeclaration)
                .Any(fn => EnumerateAllNodes(fn.FunctionBody)
                    .Any(n => n.Kind == NodeKind.AssignmentStatement
                        && n.AssignmentOperator == "="
                        && !string.IsNullOrWhiteSpace(n.AssignmentRight)
                        && n.AssignmentRight.StartsWith("\"", StringComparison.Ordinal)));
            if (needsStringH)
            {
                includes.Add("<String.hpp>");
            }

            var needsStdBoolForRecords = ast.Where(n => n.Kind == NodeKind.RecordDeclaration)
                .SelectMany(n => n.RecordMembers)
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
                .SelectMany(n => n.RecordMembers)
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
                .Any(record => record.RecordMembers.Any(member =>
                {
                    var value = GetRecordMemberValue(member);
                    var memberName = member.Contains('=', StringComparison.Ordinal)
                        ? member[..member.IndexOf('=')]
                        : member;
                    record.RecordMemberTypes.TryGetValue(memberName, out var declaredType);
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
                if (propertyDeclarations.Any(p => RequiresFixedWidthIntegerCast(p.PropertyValue, p.PropertyType)))
                {
                    includes.Add("<stdint>");
                }

                if (propertyDeclarations.Any(p => IsBooleanPropertyValue(p.PropertyValue)))
                {
                    includes.Add("<stdbool>");
                }

                if (propertyDeclarations.Any(p => IsStringPropertyValue(p.PropertyValue)))
                {
                    includes.Add("<String.hpp>");
                }
            }

            var needsStdIntForFunctionParameters = allNodes.Any(n => n.Kind == NodeKind.FunctionDeclaration
                && n.FunctionParameterList.Any(p => MapType(p.Type) is "int64_t" or "int32_t" or "int16_t" or "int8_t" or "uint64_t" or "uint32_t" or "uint16_t" or "uint8_t"));
            if (needsStdIntForFunctionParameters)
            {
                includes.Add("<stdint>");
            }

            var needsCStdIntForAssignments = hasStartSection && allNodes.Any(n => n.Kind == NodeKind.AssignmentStatement
                && n.AssignmentOperator == "="
                && TryGetTypedLiteralDeclaration(n.AssignmentRight ?? string.Empty, out var typeName, out _)
                && typeName is "int64_t" or "int32_t" or "int16_t" or "int8_t" or "uint64_t" or "uint32_t" or "uint16_t" or "uint8_t");
            if (needsCStdIntForAssignments && !autoPropertiesMode)
            {
                includes.Add("<cstdint>");
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
            var typeProperties = typeDeclarations.SelectMany(n => n.TypeProperties).ToHashSet();
            var typeFunctions = typeDeclarations.SelectMany(n => n.TypeFunctions).ToHashSet();

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
                && string.Equals(n.DeclarationKind, "module", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(n.DeclarationName));
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
                var wrappedModule = $"namespace {moduleNode.DeclarationName}\n{{\n{IndentBlock(moduleBody)}\n}}\n";
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
            return string.Join("\n", lines.Select(l => $"    {l}"));
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
                sb.AppendLine($"Enums {node.EnumName}");
                sb.AppendLine("{");
                foreach (var member in node.EnumMembers)
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
                var hasAssignedMembers = node.RecordMembers.Any(m => m.Contains('=', StringComparison.Ordinal));
                var packedSuffix = node.RecordPackSize.HasValue ? " [[gnu::packed]]" : string.Empty;
                if (hasAssignedMembers)
                {
                    sb.AppendLine("// records");
                    sb.AppendLine($"struct {node.RecordName}{packedSuffix}");
                    sb.AppendLine("{");
                    foreach (var member in node.RecordMembers)
                    {
                        var equalsIndex = member.IndexOf('=');
                        if (equalsIndex > 0)
                        {
                            var memberName = member[..equalsIndex];
                            var value = member[(equalsIndex + 1)..];
                            node.RecordMemberTypes.TryGetValue(memberName, out var declaredType);
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
                    var initializer = FormatAutoPropertyInitializer(node.PropertyValue, node.PropertyType);
                    sb.AppendLine($"auto {node.PropertyName} = {initializer};");
                }

                sb.AppendLine();
                return;
            }

            foreach (var node in globalProperties)
            {
                var (type, value) = InferCTypeAndValue(node.PropertyValue);
                var modifiers = node.PropertyModifiers.Contains("constant") ? "const " : string.Empty;
                sb.AppendLine($"{modifiers}{type} {node.PropertyName} = {value};");
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
            foreach (var node in ast.Where(n => n.Kind == NodeKind.DelegateDeclaration))
            {
                var parameters = string.Join(", ", node.DelegateParameterList.Select(FormatParameter));
                sb.AppendLine($"typedef void (*{node.DelegateName})({parameters});");
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
                var returnType = string.IsNullOrWhiteSpace(node.FunctionDeclarationReturnType)
                    ? "void"
                    : node.FunctionDeclarationReturnType;
                var parameters = node.FunctionParameterList.Count == 0
                    ? "void"
                    : string.Join(", ", node.FunctionParameterList.Select(FormatFunctionSignatureParameter));
                sb.AppendLine($"{returnType} {node.FunctionDeclarationName}({parameters})");
                sb.AppendLine("{");
                EmitStatementsWithStringLocalDeclarations(node.FunctionBody, sb, "    ");
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
                && (string.Equals(n.DeclarationKind, "type", StringComparison.Ordinal)
                    || string.Equals(n.DeclarationKind, "trait", StringComparison.Ordinal)));

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

            var parameters = sectionNode.SectionParameterList.Count == 0
                ? "void"
                : string.Join(", ", sectionNode.SectionParameterList.Select(FormatParameter));
            sb.AppendLine($"// {name}");
            sb.AppendLine($"void {name}({parameters})");
            sb.AppendLine("{");
            var statements = CollectStatements(ast, index + 1);
            if (section == Section.Initialize)
            {
                EmitStatementsWithLocalDeclarations(statements, sb, "    ", new HashSet<string?>(StringComparer.Ordinal));
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
            var globalNames = ast.Where(n => n.Kind == NodeKind.PropertyDeclaration)
                .Select(n => n.PropertyName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.Ordinal);
            var localNames = new HashSet<string>(StringComparer.Ordinal);
            var bufferedStatements = new List<Node>();

            foreach (var statement in statements)
            {
                if (TryEmitMainLocalDeclaration(statement, globalNames, localNames, sb, "    "))
                {
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
                EmitStatements(bufferedStatements, sb, "    ");
            }
            if (finalIndex >= 0 && finalSection != null)
            {
                sb.AppendLine($"    finalize({FormatArguments(finalSection.SectionParameterList)});");
            }
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private static void EmitTypes(List<Node> ast, List<Node> typeDeclarations, StringBuilder sb)
        {
            var (initIndex, _) = FindSection(ast, Section.Initialize);
            var initializeStatements = initIndex >= 0 ? CollectStatements(ast, initIndex + 1) : new List<Node>();

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

            foreach (var node in typeDeclarations.Where(n => n.DeclarationKind == "trait"))
            {
                var name = ToCppQualifiedName(node.DeclarationName) ?? "Trait";
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

        private static void EmitStatementsWithStringLocalDeclarations(List<Node> statements, StringBuilder sb, string indent)
        {
            var declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (var statement in statements)
            {
                if (statement.Kind == NodeKind.AssignmentStatement
                    && statement.AssignmentOperator == "="
                    && !string.IsNullOrWhiteSpace(statement.AssignmentLeft)
                    && IsSimpleIdentifier(statement.AssignmentLeft)
                    && !declared.Contains(statement.AssignmentLeft)
                    && !string.IsNullOrWhiteSpace(statement.AssignmentRight)
                    && statement.AssignmentRight.StartsWith("\"", StringComparison.Ordinal))
                {
                    var value = ToPumaStringLiteral(statement.AssignmentRight);
                    sb.AppendLine($"{indent}auto {statement.AssignmentLeft} = {value};");
                    declared.Add(statement.AssignmentLeft);
                    continue;
                }

                EmitStatements(new List<Node> { statement }, sb, indent);
            }
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

            foreach (var property in node.TypeProperties)
            {
                if (property.PropertyModifiers.Contains("public"))
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
        }

        private static void EmitPropertiesForAccess(List<Node> properties, string access, StringBuilder sb, string indent)
        {
            if (properties.Count == 0)
            {
                return;
            }

            sb.AppendLine($"{indent}{access}:");
            sb.AppendLine($"{indent}// properties");
            foreach (var property in properties)
            {
                var value = FormatAutoPropertyInitializer(property.PropertyValue, property.PropertyType);
                var modifiers = property.PropertyModifiers.Contains("constant") ? "const " : string.Empty;
                sb.AppendLine($"{indent}{modifiers}auto {property.PropertyName} = {value};");
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

                            if (node.AssignmentOperator == "="
                                && node.AssignmentRightExpression?.Kind == ExpressionKind.Literal
                                && !string.IsNullOrWhiteSpace(rightExpression)
                                && rightExpression.StartsWith("\"", StringComparison.Ordinal)
                                && !rightExpression.EndsWith("s", StringComparison.Ordinal))
                            {
                                rightExpression = ToPumaStringLiteral(rightExpression);
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
                            var repeatCondition = GenerateExpression(node.RepeatExpressionNode, node.RepeatExpression);
                            if (string.IsNullOrWhiteSpace(repeatCondition) || repeatCondition == "1")
                            {
                                repeatCondition = "true";
                            }
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
                ExpressionKind.Unary => string.Equals(node.Value, "not", StringComparison.Ordinal)
                    ? $"not {GenerateExpression(node.Left, null)}"
                    : $"{node.Value}{GenerateExpression(node.Left, null)}",
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
                "char" => "Puma::Type::Charactor",
                "str" => "Puma::Type::String",
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
            if (statement.Kind != NodeKind.AssignmentStatement || statement.AssignmentOperator != "=")
            {
                return false;
            }

            var leftName = statement.AssignmentLeft;
            if (string.IsNullOrWhiteSpace(leftName) || !IsSimpleIdentifier(leftName))
            {
                return false;
            }

            if (globalNames.Contains(leftName) || localNames.Contains(leftName))
            {
                return false;
            }

            if (!TryGetTypedLiteralDeclaration(statement.AssignmentRight ?? string.Empty, out var typeName, out var value))
            {
                return false;
            }

            var initializer = typeName switch
            {
                "Puma::Type::String" => ToPumaStringLiteral(value),
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

            var index = 0;
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
                return false;
            }

            literalValue = text[..index];
            var suffix = text[index..];
            if (string.IsNullOrWhiteSpace(suffix))
            {
                typeName = dotSeen ? "double" : "int64_t";
                return true;
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

                foreach (var functionNode in EnumerateAllNodes(node.FunctionBody))
                {
                    yield return functionNode;
                }

                foreach (var statementNode in EnumerateAllNodes(node.StatementBody))
                {
                    yield return statementNode;
                }

                foreach (var elseNode in EnumerateAllNodes(node.ElseBody))
                {
                    yield return elseNode;
                }

                foreach (var typeFunction in EnumerateAllNodes(node.TypeFunctions))
                {
                    yield return typeFunction;
                }
            }
        }
    }
}
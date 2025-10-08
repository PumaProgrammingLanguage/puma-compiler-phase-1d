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

            // Include stdio if any WriteLine is present
            var hasWriteLine = ast.Any(n => n.Kind == NodeKind.WriteLine);
            if (hasWriteLine)
            {
                sb.AppendLine("#include <stdio.h> // needed for puts()");
                sb.AppendLine();
            }

            // Walk the AST and render sections and statements.
            for (int i = 0; i < ast.Count; i++)
            {
                var node = ast[i];
                if (node.Kind == NodeKind.Section)
                {
                    var name = SectionToString(node.Section);
                    if (string.IsNullOrEmpty(name))
                        continue;

                    if (node.Section == Section.Start)
                    {
                        // // start
                        sb.AppendLine("// start");
                        // Emit main with statements belonging to start until next section
                        sb.AppendLine("int main() { ");
                        // Emit statements under start
                        int j = i + 1;
                        while (j < ast.Count && ast[j].Kind != NodeKind.Section)
                        {
                            var stmt = ast[j];
                            if (stmt.Kind == NodeKind.WriteLine && !string.IsNullOrEmpty(stmt.StringValue))
                            {
                                sb.AppendLine($"    puts({stmt.StringValue});");
                            }
                            j++;
                        }
                        // Always return 0 for C++ main
                        sb.AppendLine("    return 0; ");
                        sb.AppendLine("}");
                        // Add a blank line if there are more nodes after start block
                        if (j < ast.Count)
                        {
                            sb.AppendLine();
                        }
                        // Skip the statements we just consumed
                        i = j - 1;
                    }
                    else
                    {
                        // Other sections are emitted as comments only
                        sb.AppendLine($"// {name}");
                        // Add a blank line if not the last item and next meaningful output follows
                        bool last = (i == ast.Count - 1);
                        if (!last)
                        {
                            sb.AppendLine();
                        }
                    }
                }
                else
                {
                    // Statements outside a section are ignored for now
                }
            }

            return sb.ToString();
        }

        private static string SectionToString(Section section) => section switch
        {
            Section.Using => "using",
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
            Section.end => "end",
            _ => string.Empty
        };
    }
}
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
            // Collect only recognized (non-empty) section names in order.
            var sections = new List<Section>();
            foreach (var node in ast)
            {
                if (!string.IsNullOrEmpty(SectionToString(node.Section)))
                {
                    sections.Add(node.Section);
                }
            }

            var sb = new StringBuilder();

            for (int i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                var name = SectionToString(section);

                // Emit the section marker comment
                sb.AppendLine($"// {name}");

                // Special handling: inline main stub inside the 'start' section immediately
                if (section == Section.Start)
                {
                    // Emit the C entry point stub inside the start section
                    sb.Append("void main(void)\n{\n");
                    // put more code here later
                    sb.AppendLine("    return;\n}\n");
                }
                else
                {
                    // Add a separating blank line after the section if not the last section.
                    // Matches the original formatting where only the last section omits the trailing blank line.
                    if (i < sections.Count - 1)
                    {
                        sb.AppendLine();
                    }
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
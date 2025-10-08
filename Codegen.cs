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
        public Codegen() { }

        internal string Generate(List<Node> ast)
        {
            // Collect only recognized (non-empty) section names in order.
            var sections = new List<Parser.Section>();
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

                // Emit a valid C++ main inside 'start'
                if (section == Parser.Section.Start)
                {
                    sb.AppendLine();
                    sb.AppendLine("int main() { return 0; }");
                }

                if (i < sections.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static string SectionToString(Parser.Section section) => section switch
        {
            Parser.Section.Using => "using",
            Parser.Section.Module => "module",
            Parser.Section.Type => "type",
            Parser.Section.Trait => "trait",
            Parser.Section.Enums => "enums",
            Parser.Section.Records => "records",
            Parser.Section.Properties => "properties",
            Parser.Section.Start => "start",
            Parser.Section.Initialize => "initialize",
            Parser.Section.Finalize => "finalize",
            Parser.Section.Functions => "functions",
            Parser.Section.end => "end",
            _ => string.Empty
        };
    }
}
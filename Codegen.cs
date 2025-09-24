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
            var names = new List<string>();
            foreach (var node in ast)
            {
                var name = SectionToString(node.Section);
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }

            var sb = new StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                sb.AppendLine($"// {names[i]}");
                if (i < names.Count - 1)
                {
                    sb.AppendLine();
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
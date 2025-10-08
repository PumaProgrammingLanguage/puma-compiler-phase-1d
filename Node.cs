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

using static Puma.Parser;

namespace Puma
{
    internal enum NodeKind
    {
        Section,
        WriteLine
    }

    internal class Node
    {
        public NodeKind Kind { get; set; } = NodeKind.Section;
        public Section Section { get; set; } = Section.None;

        // For WriteLine nodes
        public string? StringValue { get; set; }

        public Node()
        {
        }

        public Node(Section section)
        {
            Kind = NodeKind.Section;
            Section = section;
        }

        public static Node CreateWriteLine(string literal)
        {
            return new Node
            {
                Kind = NodeKind.WriteLine,
                StringValue = literal
            };
        }
    }
}
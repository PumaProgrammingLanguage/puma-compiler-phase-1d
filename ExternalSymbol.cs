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

namespace Puma
{
    internal enum ExternalSymbolKind
    {
        Type,
        Function,
        ValueType,
        Trait,
        Module
    }

    internal sealed record ExternalSymbol(string Name, ExternalSymbolKind Kind, bool ReturnsOwnedObject = false, string? CppName = null);

    internal sealed record ResolvedPumaImport(string Target, string? Alias, string Header);
}

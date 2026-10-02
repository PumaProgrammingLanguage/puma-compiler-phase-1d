# Remaining Compiler Tasks

Use this checklist to track the remaining compiler improvements. Mark a task complete only after its implementation, tests, and build validation succeed.

## 1. AST Semantic-Model Migration

- [ ] Make structured `ExpressionNode` trees authoritative for expression semantics.
  - [x] Emit explicitly typed numeric literals from expression metadata in nested expressions, calls, and returns.
  - [x] Infer typed local declarations without consulting legacy assignment inference fields.
  - [x] Discover fixed-width integer header dependencies by traversing structured expressions.
  - [x] Use structured record initializers for literal formatting and bool/string/character/integer dependencies.
  - [x] Format global/type property initializers and select declaration forms from structured expressions; classify constructor ownership without reparsing generated calls.
  - [x] Parse and emit parameter defaults as expression nodes, including nested calls and runtime/header dependencies.
  - [x] Use AST identifiers for assignment validation and AST expressions for `has trait` generation.
- [ ] Retain source text and source spans only for diagnostics.
  - [x] Remove duplicate record `name=value` strings, member-type dictionaries, and redundant member type metadata.
  - [x] Remove duplicate property type metadata and derive parser numeric conversion types from initializer ASTs.
  - [x] Replace parameter `DefaultValue` text with `DefaultExpression` and its source span.
  - [x] Remove redundant function-call name/argument strings and unused generic statement value text; construct and validate these paths from expression trees.
  - [x] Remove redundant control-flow expression text and trait receiver text; construct conditions, loop expressions, and trait receivers directly from ASTs.
  - [x] Remove duplicate assignment operands/inferred-type text, property initializer text, and function/section parameter text; retain AST metadata and diagnostic spans.
  - [ ] Audit and retire retained legacy expression-text fields after their remaining consumers are migrated.
	- [x] Replace enum member initializer strings with structured declarations and initializer ASTs.
	- [x] Remove generated-string wrapping/cast-prefix decisions and unused generalized local type guesses; select initializer behavior from AST metadata.
	- [x] Resolve declared function/type calls from AST declarations during constructor emission and ownership discovery; traverse nested factory return ASTs.
	- [ ] Complete the final semantic-consumer audit, including unresolved external call/type metadata, before closing the broader migration.
- [ ] Remove raw string fallback generation incrementally after each syntax path has complete structured AST coverage.
  - [x] Remove record substring-based codegen and emit record initializers directly from expression nodes.
  - [x] Remove property initializer/type text-parsing helpers and share AST initializer formatting with records.
  - [x] Remove the obsolete raw-text literal parser and `has` accessors; eliminate assignment `none` text fallback and generated-text classification for repeat/string/character decisions.
  - [x] Migrate built-in `WriteLn` raw argument storage/emission to structured expressions.
  - [x] Remove function-call raw-text emission fallback and preserve complete call-containing expressions.
  - [x] Emit enum member initializers from structured expressions and discover fixed-width dependencies through their ASTs.
  - [x] Preserve string-containing call/compound initializer ASTs instead of wrapping their generated text as string literals.
- [ ] Add exact compiler-module regression coverage for each migrated path.
  - [x] Validate typed literal, unary, conditional, cast, and call-argument output, plus native compilation of nested integer casts.
  - [x] Validate record numeric forms, runtime headers, initializer AST changes, and parser reuse with exact-output tests.
  - [x] Validate property numeric forms, casts, default strings, AST replacement, type properties, constructors/owner deletion, and bare-type initialization with exact-output tests.
  - [x] Validate parameter numeric/string/character/bool defaults, compound/nested expressions, AST replacement, section headers, missing-default diagnostics, parenthesized assignment validation, and `has trait` AST authority.
  - [x] Validate built-in `WriteLn` exact output, argument source spans, AST replacement/dependency traversal, parser reuse, missing-AST diagnostics, unchanged syntax errors, and native console execution.
  - [x] Validate complete call expressions, nested/member calls, call AST replacement/default selection/dependencies, missing-call AST diagnostics, generic statement AST authority, source spans, and parser reuse.
  - [x] Validate control-flow exact output/source spans, condition AST replacement/dependencies, nested match/when, bare repeat, missing-condition diagnostics, and parser reuse.
  - [x] Validate assignment AST metadata/spans, postfix lowering, missing operands, function/section parameter-list replacement, retained numeric inference diagnostics, and parser reuse; update existing tests to assert AST equivalents without changing expected C++ output.
  - [x] Validate enum compound/unary/cast/conditional/call initializers, source spans, dependencies, AST replacement, mixed/multiple declarations, exact diagnostics, and parser reuse after successful and failed parses.
  - [x] Validate string literal versus call/compound root selection, literal/call AST replacement, single numeric casts, general local initializers, and bare bool/string defaults.
  - [x] Validate declaration-aware constructor/function classification, global/local/record/type/trait initializers, nested factory ownership, numeric-result non-deletion, declared lowercase types, declaration AST changes, and codegen reuse.

Latest validation: 305/305 tests passed in Visual Studio, with no skips, including 11 new constructor/ownership regression cases; solution build passed. Constructor classification now consults function/type AST declarations: declared uppercase functions are not allocated or deleted as constructors, and declared lowercase types are recognized. Property/record/type/trait initializer formatting receives declaration context, including owner methods. Factory discovery traverses nested return ASTs; global and local factory-produced owners retain cleanup, while numeric function results are not deleted. Declaration AST changes and codegen reuse update classification without stale state. Separate Debug/Release runs are not claimed. The broader migration remains open because unresolved external bare calls still use the existing uppercase convention until external symbol/type metadata is available; final semantic-consumer review remains pending. Standard C++ enum syntax and global numeric return mapping remain separate follow-ups. Postponed feature scenarios, the conversion table, and project architecture settings are unchanged; the previously observed MSB3270 architecture warning remains a separate follow-up.

- [ ] Map global numeric function return signatures to C++ types (for example, `int32` to `int32_t`) and add exact-output/native compilation coverage. This pre-existing issue was discovered during typed-return testing; current return-expression regressions use the supported `int` signature.
- [ ] Emit standard C++ enum declarations and add exact-output/native compilation coverage; the current legacy output uses `Enums` and no terminating semicolon.

## 2. Broader Location-Aware Diagnostics

- [x] Add line and column information to assignment and mutability diagnostics.
- [x] Add line and column information to expression and conditional diagnostics.
- [x] Add line and column information to type, loop, and `use` diagnostics.
- [x] Add AST source spans where validation occurs after token consumption.
- [x] Add exact diagnostic-message regression coverage for assignment and expression diagnostics.

## 3. Runtime-Backed Integration Execution

- [x] Execute the generated `WriteLn` sample and assert its output and exit code.
- [x] Add native execution coverage for string runtime dependencies.
- [ ] Add native execution coverage for character runtime dependencies after the runtime exports the authoritative `Character` API.
- [x] Add native execution coverage for the console runtime dependency.
- [ ] Add native execution coverage for the file runtime dependency after Puma source supports creating and calling `PumaFile` objects.
- [x] Keep compilation, linking, and execution tests independent from test-helper behavior.

## 4. Compiler Installation and Publishing

- [x] Add a Release publish/install target for Puma compiler artifacts.
- [x] Decide whether distributions should be framework-dependent, self-contained, or native AOT.
- [x] Document the selected deployment model and required runtime files.
- [x] Validate the installed compiler from a clean terminal session.

## 5. CLI Quality Improvements

- [x] Reject duplicate source-file arguments with a clear diagnostic.
- [x] Reject a missing value after `-o` or `--output`.
- [x] Preserve unknown flags as `clang++` pass-through arguments.
- [x] Add CLI integration tests for exit codes and generated source files.

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
  - [ ] Audit and retire retained legacy expression-text fields after their remaining consumers are migrated.
- [ ] Remove raw string fallback generation incrementally after each syntax path has complete structured AST coverage.
  - [x] Remove record substring-based codegen and emit record initializers directly from expression nodes.
  - [x] Remove property initializer/type text-parsing helpers and share AST initializer formatting with records.
  - [x] Remove the obsolete raw-text literal parser and `has` accessors; eliminate assignment `none` text fallback and generated-text classification for repeat/string/character decisions.
  - [ ] Migrate built-in `WriteLn` raw argument storage/emission to structured expressions.
- [ ] Add exact compiler-module regression coverage for each migrated path.
  - [x] Validate typed literal, unary, conditional, cast, and call-argument output, plus native compilation of nested integer casts.
  - [x] Validate record numeric forms, runtime headers, initializer AST changes, and parser reuse with exact-output tests.
  - [x] Validate property numeric forms, casts, default strings, AST replacement, type properties, constructors/owner deletion, and bare-type initialization with exact-output tests.
  - [x] Validate parameter numeric/string/character/bool defaults, compound/nested expressions, AST replacement, section headers, missing-default diagnostics, parenthesized assignment validation, and `has trait` AST authority.

Latest validation: 203/203 tests passed in both Debug and Release, with no skips; solution build passed. The parameter-default migration, bounded semantic-fallback audit, and regression pass are complete. Nested defaults now preserve parameter boundaries, and a section-header newline prevents parenthesized body statements from being mistaken for parameters. The broader migration remains open: next migrate built-in `WriteLn` raw arguments, then complete the legacy expression-text field audit. The previously observed MSB3270 architecture warning remains a separate follow-up; this pass did not change project architecture settings.

- [ ] Map global numeric function return signatures to C++ types (for example, `int32` to `int32_t`) and add exact-output/native compilation coverage. This pre-existing issue was discovered during typed-return testing; current return-expression regressions use the supported `int` signature.

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

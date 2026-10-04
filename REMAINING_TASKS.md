# Remaining Compiler Tasks

Use this checklist to track the remaining compiler improvements. Mark a task complete only after its implementation, tests, and build validation succeed.

## 1. AST Semantic-Model Migration

- [x] Make structured `ExpressionNode` trees authoritative for expression semantics.
  - [x] Emit explicitly typed numeric literals from expression metadata in nested expressions, calls, and returns.
  - [x] Infer typed local declarations without consulting legacy assignment inference fields.
  - [x] Discover fixed-width integer header dependencies by traversing structured expressions.
  - [x] Use structured record initializers for literal formatting and bool/string/character/integer dependencies.
  - [x] Format global/type property initializers and select declaration forms from structured expressions; classify constructor ownership without reparsing generated calls.
  - [x] Parse and emit parameter defaults as expression nodes, including nested calls and runtime/header dependencies.
  - [x] Use AST identifiers for assignment validation and AST expressions for `has trait` generation.
- [x] Retain source text and source spans only for diagnostics.
  - [x] Remove duplicate record `name=value` strings, member-type dictionaries, and redundant member type metadata.
  - [x] Remove duplicate property type metadata and derive parser numeric conversion types from initializer ASTs.
  - [x] Replace parameter `DefaultValue` text with `DefaultExpression` and its source span.
  - [x] Remove redundant function-call name/argument strings and unused generic statement value text; construct and validate these paths from expression trees.
  - [x] Remove redundant control-flow expression text and trait receiver text; construct conditions, loop expressions, and trait receivers directly from ASTs.
  - [x] Remove duplicate assignment operands/inferred-type text, property initializer text, and function/section parameter text; retain AST metadata and diagnostic spans.
	- [x] Audit and retire retained legacy expression-text fields after their remaining consumers are migrated.
	- [x] Replace enum member initializer strings with structured declarations and initializer ASTs.
	- [x] Remove generated-string wrapping/cast-prefix decisions and unused generalized local type guesses; select initializer behavior from AST metadata.
	- [x] Resolve declared function/type calls from AST declarations during constructor emission and ownership discovery; traverse nested factory return ASTs.
	- [x] Complete the bounded semantic-consumer audit: distinguish AST lexemes and output formatting from full-expression semantic reparsing; track external symbol resolution independently.
- [x] Remove raw string fallback generation incrementally after each syntax path has complete structured AST coverage.
  - [x] Remove record substring-based codegen and emit record initializers directly from expression nodes.
  - [x] Remove property initializer/type text-parsing helpers and share AST initializer formatting with records.
  - [x] Remove the obsolete raw-text literal parser and `has` accessors; eliminate assignment `none` text fallback and generated-text classification for repeat/string/character decisions.
  - [x] Migrate built-in `WriteLn` raw argument storage/emission to structured expressions.
  - [x] Remove function-call raw-text emission fallback and preserve complete call-containing expressions.
  - [x] Emit enum member initializers from structured expressions and discover fixed-width dependencies through their ASTs.
  - [x] Preserve string-containing call/compound initializer ASTs instead of wrapping their generated text as string literals.
- [x] Add exact compiler-module regression coverage for each migrated path.
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

Closure validation: the bounded audit of `Node.cs`, `Parser.cs`, and `Codegen.cs` found no remaining duplicate full-expression text fields or generated-C++ semantic reparsing. Expression operands, defaults, initializers, conditions, dependency discovery, and ownership classification use AST nodes and metadata. Retained literal lexemes, declaration/type names, import/header metadata, and output-formatting helpers are not raw-expression fallbacks. No compiler or test changes were needed in the closing pass. A fresh solution build succeeded and all 305/305 tests passed in Visual Studio. Separate Debug/Release runs are not claimed. The original migration is complete; this does not claim complete language semantics or universal C++ correctness. External symbol resolution, numeric return mapping, and standard C++ enum output are independent tasks in section 6. Postponed feature scenarios, the conversion table, and project architecture settings are unchanged; the previously observed MSB3270 architecture warning remains a separate follow-up.

## 2. Broader Location-Aware Diagnostics

- [x] Add line and column information to assignment and mutability diagnostics.
- [x] Add line and column information to expression and conditional diagnostics.
- [x] Add line and column information to type, loop, and `use` diagnostics.
- [x] Add AST source spans where validation occurs after token consumption.
- [x] Add exact diagnostic-message regression coverage for assignment and expression diagnostics.

## 3. Runtime-Backed Integration Execution

- [x] Execute the generated `WriteLn` sample and assert its output and exit code.
- [x] Add native execution coverage for string runtime dependencies.
- [x] Add native execution coverage for character runtime dependencies using the authoritative `PumaType::Character` UTF-8 constructor. Character literal ASTs emit explicit UTF-8 bytes through the installed `const uint8_t*` API; global `char` return signatures map to `PumaType::Character`. Added 19 exact-output/native clang compilation, linking, and execution cases covering ASCII, control/quote/backslash escapes, hex/Unicode escapes, multibyte characters, parameters, returns, defaults, reassignment, AST replacement, and codegen reuse. Direct assignment-call defaults now reuse the existing AST declaration expansion. Solution build succeeded and all 346/346 IDE tests passed with no skips; separate Debug/Release runs are not claimed. Runtime files were not changed; character comparison/operator support and invalid Unicode scalar diagnostics are outside this task.
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

## 6. Independent Semantic and C++ Output Follow-Ups

- [ ] Resolve external call/type symbols from metadata rather than the current uppercase convention for unresolved bare-call AST identifiers; add exact-output/AST-authority coverage. This is a separate semantic capability, not a raw-expression storage or generation fallback.
  - [x] Add the compiler API metadata prerequisite: `Generate` and `GenerateResult` accept optional immutable `ExternalSymbol` metadata for type/function classification and owned function results. Identifier AST bindings refresh on every generation; local declarations take precedence. Known external symbols no longer depend on capitalization for constructor/cleanup decisions, and owned external returns propagate through nested local factories independently of declaration order. Added 24 exact-output/AST-authority/reuse/diagnostic cases; solution build and all 370/370 IDE tests passed. No new native compilation cases or separate Debug/Release runs are claimed.
  - [ ] Populate the API from imported Puma modules, with alias/namespace handling and import diagnostics; decide the unresolved-symbol policy and retire the uppercase compatibility heuristic once imports provide authoritative metadata. The CLI does not yet supply external metadata. External C++ signature manifests and argument/return type validation are not implemented by the API prerequisite.
- [x] Map global numeric function return signatures to C++ types (for example, `int32` to `int32_t`) and add exact-output/native compilation coverage. Signed/unsigned widths and aliases reuse existing type mappings; `flt32` emits `float`, and `flt`/`flt64` emit `double`. Integer return declarations supply `<cstdint>` independently of function-body expressions. Added 16 regression cases, including 13 clang compilations and AST replacement/reuse coverage; solution build and all 321/321 IDE tests passed with no skips. Fixed-point and nonnumeric global return behavior is unchanged; separate Debug/Release runs are not claimed.
- [x] Emit standard C++ enum declarations and add exact-output/native compilation coverage. Generated unscoped `enum` declarations now end with `};`, preserving existing unqualified member references and initializer AST emission. Added six exact-output/native clang cases covering automatic/mixed members, typed arithmetic, negative/conditional constants, and multiple enums with cross-enum references; all compile under C++20 with `-pedantic-errors`. Solution build and all 327/327 IDE tests passed. This fixes declaration syntax, not constant-expression validation for arbitrary initializer calls; separate Debug/Release runs are not claimed.

General notes

Recent updates

- Renamed the Puma built-in output statement from `WriteLine` to `WriteLn` while preserving generated `PumaConsole::WriteLn` output.
- Added post-build installation of Puma.exe, Puma.dll, and required .NET runtime metadata to `%USERPROFILE%\Puma`.
- Matched clang++ native builds to the Puma runtime's dynamic MSVC runtime with `-fms-runtime-lib=dll`; runtime-backed compilation and linking coverage now passes.
- Added runtime-backed native compilation coverage and corrected `WriteLine` output to use the runtime's `PumaConsole` namespace; installed `/GL` runtime libraries must be rebuilt before LLVM linking can succeed.
- Added runtime-independent native C++ integration coverage that compiles generated output with the discovered LLVM `clang++` toolchain.
- Added explicit code-generation runtime dependency metadata, updated the CLI to consume it, and expanded location-aware function/parameter diagnostics with compiler-module coverage.
- Added one-based lexer token positions and location-aware section diagnostics, with exact compiler-module assertions for source positions, generated C++ output, and diagnostic messages.
- Hardened compiler CLI execution: explicit success/failure exit codes, clean source/tool-launch error reporting, and concurrent `clang++` output/error draining prevent redirected-stream deadlocks.
- Consolidated generated string-header detection on the full AST traversal and added regression coverage for string assignments nested in `start` conditionals.
- Added empty-source lexer/parser/codegen regression coverage; tokenization now safely normalizes empty input before checking for a trailing end-of-line marker.
- Invalid implicit conversion validation now rejects incompatible assignments for property-to-property, property-to-local, local-to-property, and local-to-local cases.
- `InvalidImplicitConvertionTest` was expanded and is currently passing after parser updates.
- Added compiler-module coverage for invalid implicit conversions in non-assignment contexts (return statements, function-call arguments, and conditional expressions), and parser validation now rejects those invalid implicit conversions.
- Added integer-width boundary-value compiler-module test coverage (min/max literals for uint8/uint16/uint32/uint64 and int8/int16/int32/int64) with full codegen output assertions.
- Added floating boundary-value compiler-module test coverage with precision-sensitive flt32/flt64 exponent literals and codegen output assertions.
- Added regression coverage that verifies the invalid implicit conversion error message is consistent across start, initialize, and functions sections.
- Added mixed-expression numeric conversion coverage for binary operations across uint16, int32, and flt64 with full generated-output assertions.
- Added three constant-mutation diagnostics tests for `start`, `initialize`, and `functions`, verifying consistent parser errors for constant property reassignment.
- Added `start` vs `initialize` coverage: module initialize-only output is validated with no `main`, and parser now has regression coverage for rejecting files that declare both sections.
- Added stable diagnostics regression coverage for section parsing errors with exact-message assertions (duplicate sections and invalid section ordering cases).
- Added typed function return validation coverage for invalid implicit return conversions from flt64 to int32 using literal, identifier, and conditional return expressions; parser now rejects typed literal return mismatches.
- Added function-call argument validation coverage with mixed argument expressions and typed parameters, including parser rejection tests for invalid implicit flt64-to-int32 call arguments (literal, identifier, conditional).
- Added parser/codegen coverage for function parameter default values with exact generated call-site default argument expansion, and implemented call-site emission of missing trailing default arguments.
- Added numeric literal edge-case coverage with exact generated C/C++ expectations (signed decimals, exponent forms, hex/bin/octal prefixes with typed suffixes), and fixed local typed-literal parsing in codegen to preserve those forms.
- Added parser/codegen regression coverage for `match/when`, `error/catch`, and `yield` expression handling with exact generated C/C++ output assertions.
- Added ownership-model object/reference compiler-module tests for co-owner transition on reassignment to `none`, borrower non-deletion behavior, `own` transfer parsing, and return ownership handoff cleanup in outer scope.
- Added parser validation that rejects assigning `none` to non-optional properties, with explicit error-message assertions.
- Updated lexer/parser/codegen to support `own` keyword parsing in expressions and function modifiers, plus ownership-aware delete emission for transferred/returned owners.
- Completed remaining optional-scenario coverage by adding parser/codegen tests for optional local assignment (`optional` + `none` mapping) and null-check output consistency in `has` statements.
- Updated parser/codegen to support trailing `optional` on local assignments and emit `null` in `has`-statement checks for consistent generated output expectations.
- Documented Puma mutability/constant semantics for upcoming coverage: readonly/readwrite affect reference mutability, var/const affect binding, var is default, readonly propagation rules apply on assignment, and readwrite-source assignment defaults the target to readonly unless explicitly cast.
- Added readonly mutation diagnostics coverage for properties in `start`, `initialize`, and `functions`, and added readwrite coverage tests for local variables, properties, and parameters.
- Added const-parameter coverage: parser now recognizes `const` modifiers and rejects mutation of const parameters with a parser error message.
- Standardized const keyword usage in tests/compiler paths (`constant` -> `const`) and updated codegen const-property emission checks to match Puma syntax.
- Added readonly local/parameter mutation diagnostics coverage: readonly local reassignment is now rejected across `start`, `initialize`, and `functions`, and readonly parameter mutation is rejected in `functions`.
- Added `var`/`const` rebinding coverage: var-default local rebinding and readwrite-parameter mutation are explicitly allowed, while const-property rebinding is rejected.
- Expanded `var`/`const` rebinding coverage: var-default parameter rebinding is allowed, and a local variable initialized from a const property remains var and rebindable.
- Added assignment-propagation coverage: assignments from readonly/readwrite sources default targets to readonly (mutation rejected), and explicit `readwrite` target modifier allows reassignment.
- Expanded assignment-propagation coverage across parameters/properties: readonly parameter/property sources propagate readonly to local targets, and readwrite property sources support explicit `readwrite` target override for mutation.
- Added advanced assignment-propagation edge-case coverage: readonly propagation chains remain readonly across multiple hops, explicit `readonly` target modifier is enforced, and readwrite->readonly cast transitions reject subsequent mutation.
- Extended propagation support to section parameters: `initialize` readonly/readwrite parameters now participate in propagation checks; added section/function property-source override coverage and aligned generated output expectations.
- Added mixed-source and multi-step override coverage: function/property readwrite-overridden locals still propagate readonly on subsequent assignment, and initialize mixed readonly/readwrite source chains now assert readonly mutation rejection.
- Added initialize/functions local-source propagation coverage: readonly local->local assignment now rejects target mutation, and functions readwrite local-source override (`target = source readwrite`) is explicitly validated as mutable.
- Added final override/precedence coverage: explicit `readonly` target override from readwrite local sources now rejects mutation in `initialize`/`functions`, and mixed local/parameter/property chains enforce readonly at the terminal assignment.
- Converted readonly/readwrite propagation tests from value-type literals to object/reference (`Shape`) scenarios, fixed function-body indentation in readonly-parameter propagation coverage, and validated the targeted propagation suite (18/18 passing).
- Extracted unit tests into a dedicated `Puma.Tests` project; removed test dependencies and compile files from `Puma.csproj` to maintain clean AOT publishing.
- Fixed nullable reference warnings in `Codegen.cs`, resolved all compiler warnings, and enabled `TreatWarningsAsErrors=True` in `Puma.csproj`.
- Began AST semantic-model migration: property initializers now retain `ExpressionNode` trees; code generation emits assignments, calls, control-flow conditions, ownership checks, and property initializers from structured expressions. Added exact structured-expression output and incomplete-AST diagnostics coverage; all 162 tests pass. Remaining migration work is source-text use for type inference, include selection, and ownership-name analysis.
- Continued AST semantic-model migration: expression nodes now retain recursive source spans and numeric suffix metadata; assignment validation and declaration inference use structured nodes; record members retain structured declarations and are emitted without substring parsing. Removed duplicate assignment fields and obsolete raw-expression codegen accessors. The legacy raw fields retained for parser compatibility still prevent marking the overall migration task complete.
- Completed the typed-expression emission increment: explicit numeric casts and fixed-width integer header dependencies now come from expression metadata, and typed local declarations no longer consult legacy assignment inference. Fixed the text-based initializer classification that introduced blank-line regressions. Added exact return/call/cast/unary/conditional coverage and native compilation of nested integer casts; all 172 tests pass in Debug and Release, and the solution build passes. Task #1 remains open for remaining raw-text semantics.
- Completed the record semantic-model increment: removed duplicate record strings/type dictionaries and redundant member type metadata; initializer formatting and header selection now use expression nodes. Fixed truncated exponent/signed/hex initializers, missing character headers, and stale dependencies after AST changes. Four new compiler-module regressions pass; all 176 tests pass in Debug and Release, and the solution build passes. Existing record C++ expectations remain unchanged; property initializer/type helpers are the next migration target.
- Release validation reports MSB3270 because the MSIL test project references the AMD64 compiler assembly. Tests pass; project architecture alignment remains a separate follow-up, and no project settings were changed in the record migration.

1) Unit test TODO list

1. Add global numeric return-signature mapping coverage (`int32` must emit `int32_t`), including native compilation; current global codegen emits the Puma return type verbatim.
2. Extend exact-output and source-text-independence coverage as remaining raw-text property initializer/type and other semantic paths are migrated; record dependency coverage is complete for this increment.

2) Separate special-feature TODO list - On hold until core language features are implemented

1. Add postponed tests for `fix32` conversions once custom C++ fixed-point classes are implemented.
2. Add postponed tests for `fix64` conversions once custom C++ fixed-point classes are implemented.
3. Add postponed tests for `forall` and container-focused scenarios.
4. Add postponed tests for `override` behavior.
5. Add postponed tests for range operator `..` scenarios.
6. Add postponed tests for boxing/unboxing scenarios.
7. Retain remaining readonly/reference-mutability edge cases in the postponed special-feature backlog.
8. Retain additional optional property/local-variable edge cases in the postponed special-feature backlog.


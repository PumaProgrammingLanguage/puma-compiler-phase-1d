# Remaining Compiler Tasks

Use this checklist to track the remaining compiler improvements. Mark a task complete only after its implementation, tests, and build validation succeed.

## 1. AST Semantic-Model Migration

- [ ] Make structured `ExpressionNode` trees authoritative for expression semantics.
  - [x] Emit explicitly typed numeric literals from expression metadata in nested expressions, calls, and returns.
  - [x] Infer typed local declarations without consulting legacy assignment inference fields.
  - [x] Discover fixed-width integer header dependencies by traversing structured expressions.
- [ ] Retain source text and source spans only for diagnostics.
- [ ] Remove raw string fallback generation incrementally after each syntax path has complete structured AST coverage.
- [ ] Add exact compiler-module regression coverage for each migrated path.
  - [x] Validate typed literal, unary, conditional, cast, and call-argument output, plus native compilation of nested integer casts.

Typed-expression validation: 172/172 tests passed in Debug and Release; solution build passed. The broader migration remains open because raw-text initializer/type helpers and legacy record-member dependency paths still exist.

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

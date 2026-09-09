# Remaining Compiler Tasks

Use this checklist to track the remaining compiler improvements. Mark a task complete only after its implementation, tests, and build validation succeed.

## 1. AST Semantic-Model Migration

- [ ] Make structured `ExpressionNode` trees authoritative for expression semantics.
- [ ] Retain source text and source spans only for diagnostics.
- [ ] Remove raw string fallback generation incrementally after each syntax path has complete structured AST coverage.
- [ ] Add exact compiler-module regression coverage for each migrated path.

## 2. Broader Location-Aware Diagnostics

- [ ] Add line and column information to assignment and mutability diagnostics.
- [ ] Add line and column information to type, expression, loop, conditional, and `use` diagnostics.
- [ ] Add AST source spans where validation occurs after token consumption.
- [ ] Add exact diagnostic-message regression coverage.

## 3. Runtime-Backed Integration Execution

- [x] Execute the generated `WriteLn` sample and assert its output and exit code.
- [ ] Add native execution coverage for string and character runtime dependencies.
- [ ] Add native execution coverage for console and file runtime dependencies.
- [ ] Keep compilation, linking, and execution tests independent from test-helper behavior.

## 4. Compiler Installation and Publishing

- [ ] Add a Release publish/install target for Puma compiler artifacts.
- [ ] Decide whether distributions should be framework-dependent, self-contained, or native AOT.
- [ ] Document the selected deployment model and required runtime files.
- [ ] Validate the installed compiler from a clean terminal session.

## 5. CLI Quality Improvements

- [ ] Reject duplicate source-file arguments with a clear diagnostic.
- [ ] Reject a missing value after `-o` or `--output`.
- [ ] Reject unknown Puma options while preserving valid `clang++` pass-through flags.
- [ ] Add CLI integration tests for exit codes, output paths, and generated source files.

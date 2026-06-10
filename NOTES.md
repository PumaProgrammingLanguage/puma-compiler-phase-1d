General notes

Recent updates

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

1) Unit test TODO list



2) Separate postponed/special-feature TODO list

1. Add parser/codegen tests for `readonly`/`readwrite` mutation diagnostics across locals, properties, and parameters in `start`, `initialize`, and `functions`.
2. Add parser/codegen tests for `var`/`const` rebinding semantics across locals, properties, and parameters (var default, const protection).
3. Add parser/codegen tests for assignment propagation rules (`readonly` propagation, `readwrite` source forcing readonly target unless cast, explicit cast exceptions).
4. Add postponed tests for `fix32` conversions once custom C++ fixed-point classes are implemented.
5. Add postponed tests for `fix64` conversions once custom C++ fixed-point classes are implemented.
6. Add postponed tests for `forall` and container-focused scenarios.
7. Add postponed tests for `override` behavior.
8. Add postponed tests for range operator `..` scenarios.
9. Add postponed tests for boxing/unboxing scenarios.


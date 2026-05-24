General notes

Recent updates

- Invalid implicit conversion validation now rejects incompatible assignments for property-to-property, property-to-local, local-to-property, and local-to-local cases.
- `InvalidImplicitConvertionTest` was expanded and is currently passing after parser updates.
- Added compiler-module coverage for invalid implicit conversions in non-assignment contexts (return statements, function-call arguments, and conditional expressions), and parser validation now rejects those invalid implicit conversions.
- Added integer-width boundary-value compiler-module test coverage (min/max literals for uint8/uint16/uint32/uint64 and int8/int16/int32/int64) with full codegen output assertions.
- Added floating boundary-value compiler-module test coverage with precision-sensitive flt32/flt64 exponent literals and codegen output assertions.
- Added regression coverage that verifies the invalid implicit conversion error message is consistent across start, initialize, and functions sections.
- Added mixed-expression numeric conversion coverage for binary operations across uint16, int32, and flt64 with full generated-output assertions.

1) Unit test TODO list

1. Add parser/codegen tests that verify module section behavior for `start` vs `initialize` (including the rule that only one is allowed and that `main` is not emitted when `start` is absent).
2. Add diagnostics regression tests for duplicate/invalid section ordering messages to ensure stable and actionable compiler errors.
3. Add compiler-module tests for typed function return validation coverage with literals, identifiers, and conditional expressions across numeric types.
4. Add compiler-module tests for function-call argument validation coverage with mixed argument expressions and typed parameters.
5. Add parser/codegen tests for default parameter values in function declarations and generated call-site defaults.
6. Add parser/codegen tests for `constant` property mutation diagnostics across `start`, `initialize`, and `functions`.
7. Add lexer/parser/codegen tests for numeric literal tokenization edge cases (signed literals, exponent forms, base prefixes, and suffix combinations).
8. Add parser/codegen tests for `match/when`, `error/catch`, and `yield` statement expression handling and generated output consistency.

2) Separate postponed/special-feature TODO list

1. Add parser/codegen tests for `readonly` property mutation diagnostics across `start`, `initialize`, and `functions`.
2. Add parser/codegen tests for `optional` properties and optional local variables (declaration, assignment, null checks, and generated output consistency).
3. Add postponed tests for `fix32` conversions once custom C++ fixed-point classes are implemented.
4. Add postponed tests for `fix64` conversions once custom C++ fixed-point classes are implemented.
5. Add postponed tests for `forall` and container-focused scenarios.
6. Add postponed tests for `override` behavior.
7. Add postponed tests for range operator `..` scenarios.
8. Add postponed tests for boxing/unboxing scenarios.


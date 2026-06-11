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
- Added readonly mutation diagnostics coverage for properties in `start`, `initialize`, and `functions`, and added readwrite coverage tests for local variables, properties, and parameters.
- Added const-parameter coverage: parser now recognizes `const` modifiers and rejects mutation of const parameters with a parser error message.
- Standardized const keyword usage in tests/compiler paths (`constant` -> `const`) and updated codegen const-property emission checks to match Puma syntax.
- Added readonly local/parameter mutation diagnostics coverage: readonly local reassignment is now rejected across `start`, `initialize`, and `functions`, and readonly parameter mutation is rejected in `functions`.
- Added `var`/`const` rebinding coverage: var-default local rebinding and readwrite-parameter mutation are explicitly allowed, while const-property rebinding is rejected.
- Expanded `var`/`const` rebinding coverage: var-default parameter rebinding is allowed, and a local variable initialized from a const property remains var and rebindable.
- Added assignment-propagation coverage: assignments from readonly/readwrite sources default targets to readonly (mutation rejected), and explicit `readwrite` target modifier allows reassignment.
- Expanded assignment-propagation coverage across parameters/properties: readonly parameter/property sources propagate readonly to local targets, and readwrite property sources support explicit `readwrite` target override for mutation.
- Added advanced assignment-propagation edge-case coverage: readonly propagation chains remain readonly across multiple hops, explicit `readonly` target modifier is enforced, and readwrite->readonly cast transitions reject subsequent mutation.

1) Unit test TODO list
1. Add remaining parser/codegen tests for advanced assignment propagation/cast forms across function parameter/property combinations and section variants.



2) Separate special-feature TODO list - On hold until core language features are implemented

1. Add postponed tests for `fix32` conversions once custom C++ fixed-point classes are implemented.
2. Add postponed tests for `fix64` conversions once custom C++ fixed-point classes are implemented.
3. Add postponed tests for `forall` and container-focused scenarios.
4. Add postponed tests for `override` behavior.
5. Add postponed tests for range operator `..` scenarios.
6. Add postponed tests for boxing/unboxing scenarios.


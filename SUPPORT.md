# Feature Unit Test Status

## Features with Unit Test Coverage

1. Section behavior: `start` vs `initialize` (including rejection when both are present and no `main` when `start` is absent).
2. Section diagnostics: duplicate sections and out-of-order section errors (exact message regression checks).
3. Conversion coverage:
   - explicit conversions
   - implicit conversions
   - invalid implicit conversions (assignment, return, function-call arguments, conditional expressions)
   - conversion error message consistency across sections
4. Numeric conversion boundaries:
   - integer width boundaries
   - floating boundary literals (including exponent forms)
   - mixed-expression numeric conversion behavior
5. Function behavior:
   - function declarations and parameter validation errors
   - default parameter parsing
   - generated call-site default argument expansion
6. Expression/operator coverage:
   - arithmetic, logical, bitwise, shift, unary, conditional, member/index/call expressions
   - assignment/compound assignment operators
   - operator precedence scenarios
7. Control-flow/codegen coverage:
   - `if/else`
   - `match/when`
   - `while`
   - `repeat`
   - `break`
   - `yield`
   - `error/catch`
8. Constant property mutation diagnostics across `start`, `initialize`, and `functions`.
9. Type/trait/module basics:
   - default and explicit visibility mapping
   - module namespace emission
   - trait/type initialize scenarios
10. Other parser/codegen coverage:
   - records/enums
   - use section (namespace and file-path forms)
   - has-trait statements
   - lexer/parser fallback and error-path tests

## Features Without Unit Test Coverage (Postponed / Outstanding)

1. `readonly` property mutation diagnostics across `start`, `initialize`, and `functions`.
2. `optional` properties and optional local variables (declaration, assignment, null checks, and generated output consistency).
3. `fix32` conversion scenarios (postponed until custom C++ fixed-point classes exist).
4. `fix64` conversion scenarios (postponed until custom C++ fixed-point classes exist).
5. `forall` and container-focused scenarios.
6. `override` behavior.
7. Range operator `..` scenarios.
8. Boxing/unboxing scenarios.

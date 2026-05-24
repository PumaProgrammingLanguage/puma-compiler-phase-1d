General notes

Recent updates

- Invalid implicit conversion validation now rejects incompatible assignments for property-to-property, property-to-local, local-to-property, and local-to-local cases.
- `InvalidImplicitConvertionTest` was expanded and is currently passing after parser updates.
- Added compiler-module coverage for invalid implicit conversions in non-assignment contexts (return statements, function-call arguments, and conditional expressions), and parser validation now rejects those invalid implicit conversions.

Unit test TODO list

- Add boundary-value conversion tests for integer widths (min/max literals) to validate parser/codegen behavior at limits.
- Add boundary-value conversion tests for floating conversions (precision/rounding-sensitive literals) and assert generated output expectations.
- Add regression tests that validate implicit/explicit conversion error message consistency across sections (start, initialize, functions).
- Add tests for mixed-expression conversion behavior (binary operations with differing numeric types) and assert generated expression output.


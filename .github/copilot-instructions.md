# Copilot Instructions

## Project Guidelines
- Properties in the Puma parser must be declared/initialized by assignment (use `name = Type`, not `name Type`).
- Properties are private by default; private in Puma maps to protected in C++; public in Puma maps to public in C++; traits never have inheritance.
- If a module/type/trait section is missing but other sections exist, the default file type is module. 
- If a file has sections, there should be no code outside the sections. 
- If a file has no sections, the whole file defaults to the start section.

## Task Management
- Break large tasks into smaller tasks that can be completed in one session, then continue iteratively. 
- Prioritize parser/lexer work first; do codegen unit tests last.
- Do as many small tasks as possible each session, then report remaining tasks.

## Unit Testing

## Specification Reference
- The specification text file is located at `C:\Users\dabur\source\repos\Puma Programming Language Specification.txt`.

## Version Control
- Always commit `.github/upgrades/scenarios/new-dotnet-version_7f6cdd/scenario.json` when it is modified, alongside related changes.
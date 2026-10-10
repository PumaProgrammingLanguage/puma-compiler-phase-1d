# Copilot Instructions

## Project Guidelines
- Properties in the Puma parser must be declared/initialized by assignment (use `name = Type`, not `name Type`).
- Properties are private by default; private in Puma maps to protected in C++; public in Puma maps to public in C++; traits never have inheritance.
- If a module/type/trait section is missing but other sections exist, the default file type is module. 
- If a file has sections, there should be no code outside the sections. 
- If a file has no sections, the whole file defaults to the start section.
- Use `PUMA_STDLIB_ROOT` as the configured root for Puma standard libraries (lib and include).

## Task Management
- Break large tasks into smaller tasks that can be completed in one session, then continue iteratively. 
- Prioritize parser/lexer work first; do codegen unit tests last.
- Keep task completion bounded: do not add new tasks unless necessary to complete the requested task; document optional suggestions in a separate document rather than expanding the task checklist.
- Do as many small tasks as possible each session, then report remaining tasks.
- Do not commit or push changes automatically. At the end of each work session, list what changed and instruct the user to commit and push; the user handles Git commits and pushes to reduce credit usage.

## Implementation Options
- When presenting implementation options, proceed with the recommended option by default rather than repeatedly asking the user to choose, unless a genuine blocker or required approval prevents proceeding.

## Unit Testing

## Specification Reference
- The specification text file is located at `C:\Users\dabur\source\repos\Puma Programming Language Specification.txt`.

## Version Control
- Always commit `.github/upgrades/scenarios/new-dotnet-version_7f6cdd/scenario.json` when it is modified, alongside related changes.

## C++ API Updates
- The updated PumaFile C++ API uses `PumaFile::Text::Open()` and `PumaFile::Text::Close()`, with capital O and C.
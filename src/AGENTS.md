# Repository Guidelines

## Project Structure & Module Organization
- `fusion.sln` defines the solution; keep new projects referenced here so CI picks them up.
- Runtime code lives in `fusion.runner/Program.cs`. Place additional Discord client logic in this project unless a dedicated module warrants a new class library.
- `fusion.runner/bin` and `fusion.runner/obj` are build outputs; do not commit their contents.
- When introducing tests, mirror the project name (e.g., `fusion.runner.Tests/`) under `tests/` to keep production and test code clearly separated.

## Build, Test, and Development Commands
- `dotnet restore` downloads NuGet dependencies; run it after adding or updating packages.
- `dotnet build` compiles all projects in Debug configuration by default and surfaces compiler warnings.
- `dotnet run --project fusion.runner` executes the bot locally; supply Discord tokens via environment variables or user secrets.
- `dotnet test` runs the full test suite once a test project exists; add `--collect:"XPlat Code Coverage"` when validating coverage.

## Coding Style & Naming Conventions
- Use 4-space indentation, file-scoped namespaces, and implicit `using` directives where practical.
- Follow C# conventions: PascalCase for classes and public members, camelCase for locals, and suffix async methods with `Async`.
- Keep Discord handlers in feature-specific classes to prevent `Program.cs` bloat.
- Run `dotnet format` before pushing to enforce spacing, ordering, and analyzer fixes.

## Testing Guidelines
- Prefer xUnit for new test projects; place shared fixtures under `tests/Common`.
- Name test classes `<Feature>Tests` and methods `MethodUnderTest_State_ExpectedResult`.
- Mock Discord clients or external APIs to keep tests deterministic; avoid hitting live services inside unit tests.
- Execute `dotnet test` locally before opening a pull request and ensure new features include failing-first tests when practical.

## Commit & Pull Request Guidelines
- **Conventional commits are required.** Use prefixes like `feat:`, `fix:`, `chore:`, `refactor:`, etc., for every commit so changelog tooling stays accurate. If a change mixes scopes, split the work instead of using a generic prefix.
- Keep commits focused on a single concern and include relevant context in the body.
- Pull requests should summarize the change, link tracking issues, and note manual verification (e.g., guild used).
- Include screenshots or logs when updating command outputs so reviewers can confirm Discord embeds and formatting.

### Discord Quote Commands Reference
- `/quote add` creates quotes, generating short IDs plus mention metadata, so future features should reuse the existing repository abstractions when persisting related data.
- `/quote find` resolves an exact short ID (with fuzzy fallback) and `/quote search` performs regex matching against quote messages/tags. New commands should call the repository instead of hitting Mongo directly to keep logic centralized.

### Backlog Hygiene
- Keep `backlog.md` up to date: when we discuss new features (e.g., quote moderation, CI), add or update checkboxes there so priorities remain visible.
- Before starting new work, skim the backlog and mark completed items in commits to ensure long-running tasks don’t get lost.

## Configuration & Security Tips
- Store Discord bot tokens and secrets in environment variables or `dotnet user-secrets`; never commit them.
- Document new configuration keys in the README or sample `.env` files so others can reproduce safely.

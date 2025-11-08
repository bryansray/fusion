# Repository Guidelines

## Project Structure & Module Organization
- `fusion.sln` defines the solution; keep new projects referenced here so CI picks them up.
- Runtime code lives in `fusion.runner/Program.cs`. Place additional Discord client logic in this project unless a dedicated module warrants a new class library.
- `fusion.runner/bin` and `fusion.runner/obj` are build outputs; do not commit their contents.
- Interaction modules live under `Fusion.Bot/Modules`. Group commands by domain (Quotes, Warcraft, etc.) instead of dumping everything into one module.
- Infrastructure clients (Blizzard/Warcraft, Raider.IO, WarcraftLogs) belong under `Fusion.Infrastructure/<Provider>`; surface interfaces so Discord modules can depend on abstractions. Follow the Blizzard/Raider.IO client pattern (options + HttpClient + tests) when adding new providers.
- Test projects mirror production projects (`Fusion.Bot.Tests`, `Fusion.Infrastructure.Tests`); add new suites when standing up more clients or slash commands.

## Build, Test, and Development Commands
- `dotnet restore` downloads NuGet dependencies; run it after adding or updating packages.
- `dotnet build` compiles all projects in Debug configuration by default and surfaces compiler warnings.
- `dotnet run --project fusion.runner` executes the bot locally; supply Discord tokens via environment variables or user secrets.
- `dotnet test` runs the full test suite. For focused runs: `dotnet test src/Fusion.Bot.Tests/Fusion.Bot.Tests.csproj` or `dotnet test src/Fusion.Infrastructure.Tests/Fusion.Infrastructure.Tests.csproj`.

## Coding Style & Naming Conventions
- Use 4-space indentation, file-scoped namespaces, and implicit `using` directives where practical.
- Follow C# conventions: PascalCase for classes and public members, camelCase for locals, and suffix async methods with `Async`.
- Keep Discord handlers in feature-specific classes to prevent `Program.cs` bloat.
- Run `dotnet format` before pushing to enforce spacing, ordering, and analyzer fixes.
- When adding new slash commands (e.g., `/warcraft character`), follow the existing logging + validation pattern and ensure responses acknowledge the interaction even if you only log for now.

## Testing Guidelines
- Prefer xUnit for new test projects; place shared fixtures under `tests/Common`.
- Name test classes `<Feature>Tests` and methods `MethodUnderTest_State_ExpectedResult`.
- Mock Discord clients or external APIs to keep tests deterministic; avoid hitting live services inside unit tests. For external HTTP clients, follow the `TestHttpMessageHandler` pattern already used by the Warcraft client tests.
- Execute `dotnet test` locally before opening a pull request and ensure new features include failing-first tests when practical.

## Commit & Pull Request Guidelines
- **Conventional commits are required.** Use prefixes like `feat:`, `fix:`, `chore:`, `refactor:`, etc., for every commit so changelog tooling stays accurate. If a change mixes scopes, split the work instead of using a generic prefix.
- Keep commits focused on a single concern and include relevant context in the body (e.g., `feat: scaffold warcraft slash command`).
- Pull requests should summarize the change, link tracking issues, and note manual verification (e.g., guild used).
- Include screenshots or logs when updating command outputs so reviewers can confirm Discord embeds and formatting.

### Discord Quote Commands Reference
- `/quote add` creates quotes, generating short IDs plus mention metadata, so future features should reuse the existing repository abstractions when persisting related data.
- `/quote find` resolves an exact short ID (with fuzzy fallback) and `/quote search` performs regex matching against quote messages/tags. New commands should call the repository instead of hitting Mongo directly to keep logic centralized.
- `/warcraft character` lives in `WarcraftModule`; it currently logs lookups, calls `IWarcraftClient`, and responds with a text summary. Extend this module when adding Raider.IO or WarcraftLogs interactions.


### Testing Stack
- `Fusion.Bot.Tests` exercises slash-command logic; run `dotnet test src/Fusion.Bot.Tests/Fusion.Bot.Tests.csproj`.
- `Fusion.Infrastructure.Tests` focuses on HTTP clients (e.g., Warcraft). Reuse the test HTTP handler helper for new integrations so you can fake OAuth/token responses easily.
- `Fusion.Persistence.Tests` covers persistence models and repositories. It includes Testcontainers-based integration tests (skipped by default); enable them when Docker is available to verify real Mongo interactions.

### Backlog Hygiene
- Keep `backlog.md` up to date: when we discuss new features (e.g., quote moderation, CI), add or update checkboxes there so priorities remain visible.
- Mirror new slash commands in `docs/slash-commands/`. Each command family (Quotes, Warcraft, future Raider.IO/WarcraftLogs) should have a Markdown reference describing parameters, behavior, configuration, and testing notes so docs stay current with the bot.
- Before starting new work, skim the backlog and mark completed items in commits to ensure long-running tasks don’t get lost.

## Configuration & Security Tips
- Store Discord bot tokens and secrets in environment variables or `dotnet user-secrets`; never commit them.
- Treat third-party API credentials (Blizzard, Raider.IO, etc.) the same way—only reference them through configuration bindings and never log their values.
- Document new configuration keys in the README or sample `.env` files so others can reproduce safely.

# Fusion Discord Bot

Fusion is a .NET 9 Discord bot focused on capturing memorable quotes from your community. It exposes a set of slash commands for collecting, searching, and moderating quotes, persists all data in MongoDB, and is hosted via the generic host so it can run locally or from any container-friendly environment.

## Highlights
- Slash commands built with `Discord.Net` interaction modules (`/ping`, `/quote add/find/search/delete/restore`).
- MongoDB-backed quote storage with short identifiers, fuzzy lookup, soft delete, and automatic index creation.
- Structured Serilog logging and health-oriented hosted services (socket lifecycle + Mongo index initializer).
- Modular solution layout: runner, bot, and persistence layers with corresponding unit tests.

## Repository Layout
- `src/Fusion.Runner` – host application (`dotnet run`) that wires configuration, logging, Discord socket, and persistence.
- `src/Fusion.Bot` – interaction modules, option classes, and services that define slash-command behavior.
- `src/Fusion.Persistence` – MongoDB repositories, options, and supporting models/utilities.
- `src/Fusion.*.Tests` – xUnit test projects for bot and persistence layers.

## Prerequisites
- .NET 9.0 SDK
- MongoDB 6.x+ (local instance or Atlas cluster)
- Discord Application with a bot user, OAuth2 `bot` + `applications.commands` scopes, and the privileged Message Content intent enabled.

## Configuration
Fusion reads configuration from `appsettings.json`, environment variables, and user secrets. The important sections are:

| Section            | Key                       | Description |
|--------------------|---------------------------|-------------|
| `Discord`          | `Token`                   | **Required.** Bot token; keep this in user secrets or an env var. |
|                    | `ClientId`                | Used when inviting the bot and logging context. |
|                    | `GuildId`                 | Optional. When set, slash commands register to that guild; otherwise they register globally (and take up to an hour to appear). |
|                    | `Status`                  | Optional status text shown in Discord. |
| `Mongo`            | `ConnectionString`        | **Required.** MongoDB connection string. |
|                    | `DatabaseName`            | Defaults to `fusion`. |
|                    | `QuotesCollectionName`    | Defaults to `quotes`. |

### Example local secrets
```bash
cd src/Fusion.Runner
dotnet user-secrets set "Discord:Token" "<bot-token>"
dotnet user-secrets set "Discord:ClientId" "<application-id>"
dotnet user-secrets set "Discord:GuildId" "<dev-guild-id>"   # optional
dotnet user-secrets set "Mongo:ConnectionString" "mongodb://localhost:27017"
```

You can override any value with environment variables (e.g., `Discord__Token`, `Mongo__DatabaseName`).

## Getting Started
1. Install prerequisites and ensure MongoDB is running locally (`docker run -p 27017:27017 mongo` works well).
2. Clone the repo and restore packages: `dotnet restore src/fusion.sln`.
3. Configure Discord + Mongo settings (see above) via `appsettings.Development.json` or user secrets.
4. Run the bot locally:
   ```bash
   dotnet run --project src/Fusion.Runner
   ```
5. Invite the bot to your guild using the OAuth2 URL generated with the `applications.commands` scope. If you set `GuildId`, commands appear instantly; otherwise Discord’s global propagation may take ~1 hour.

## Slash Commands
- `/ping` – quick connectivity check.
- `/quote add` – supply person, quote text, optional tags, mentions, and NSFW flag. Stores quote, assigns a short id, and acknowledges privately.
- `/quote find <short-id>` – retrieves a quote by short id (with fuzzy prefix fallback).
- `/quote search <query> [limit]` – searches quote text and tags, returning up to 10 matches.
- `/quote delete <short-id>` & `/quote restore <short-id>` – soft-delete/restore quotes; restricted to members with `Manage Messages`/`Manage Server`/`Administrator`.

Each successful lookup increments the `Uses` counter so you can later determine which quotes are most referenced.

## Running Tests
```bash
dotnet test src/fusion.sln
```
Persistence tests expect a MongoDB instance; they spin up disposable databases but rely on `Mongo:ConnectionString` being reachable (you can direct them to a test container via environment variables).

## Operational Notes
- The `MongoIndexInitializer` hosted service ensures the quotes collection has the necessary unique indexes on startup.
- Logging defaults to Serilog console formatting; adjust sinks via `Serilog` settings.
- Because slash commands are registered at runtime, restart the bot after adding or renaming commands so Discord picks up the new definitions.

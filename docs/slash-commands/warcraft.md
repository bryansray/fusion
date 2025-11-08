# Warcraft Slash Commands

Fusion currently exposes the `/warcraft character` command for quick lookups against Blizzard's World of Warcraft profile API.

## `/warcraft character <realm> <name>`

| Parameter | Required | Description |
|-----------|----------|-------------|
| `realm`   | Yes      | Realm name or slug (e.g., `Area 52`, `illidan`). Spaces are allowed and will be normalized. |
| `name`    | Yes      | Character name as it appears in game. Case-insensitive. |

### Behavior
- Region defaults to **US**. Future work will expose a per-command or per-user override.
- Interactions are logged via `WarcraftModule` and fulfilled by `IWarcraftClient` in `Fusion.Infrastructure`.
- Successful lookups return a short text summary (level, class, realm, item level, last login). Missing characters return a friendly “not found” message.
- Failures (API outage, auth issues) are logged and reported generically to the user.

### Configuration Checklist
1. Create a Blizzard application at <https://develop.battle.net> and capture `ClientId`/`ClientSecret`.
2. Set configuration via user secrets or environment variables:
   ```bash
   dotnet user-secrets set "Warcraft:ClientId" "<client-id>"
   dotnet user-secrets set "Warcraft:ClientSecret" "<client-secret>"
   dotnet user-secrets set "Warcraft:Region" "us"
   dotnet user-secrets set "Warcraft:Locale" "en_US"
   ```
3. Ensure the `Discord` section has a valid bot token so the slash command registers.

### Testing Tips
- Unit tests live in `Fusion.Infrastructure.Tests` and use a fake `HttpMessageHandler`. Follow this pattern when adding Raider.IO/WarcraftLogs commands.
- When manually testing, restart the bot after changing command definitions so Discord re-syncs the metadata.

### Roadmap Ideas
- Allow `/warcraft character` to accept an optional `region` choice list (US/EU/KR/TW/CN).
- Enrich responses with Discord embeds, gear thumbnails, and Raider.IO/WarcraftLogs links once those integrations land.

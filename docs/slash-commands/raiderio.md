# Raider.IO Slash Commands

Commands in this group call the `IRaiderIoClient` defined in `Fusion.Infrastructure` and currently focus on character lookups.

## `/raiderio character <server> <character>`

| Parameter | Required | Description |
|-----------|----------|-------------|
| `server`  | Yes      | Realm/server name or slug (e.g., `Area 52`, `illidan`). |
| `character` | Yes   | Character name. Case-insensitive. |

### Behavior
- Uses the default region configured in `RaiderIO:Region` (US if unspecified).
- Retrieves character JSON via `https://raider.io/api/v1/characters/profile` and formats an ephemeral summary (class/spec, item level, Mythic+ ranks, last crawl time).
- Logs every lookup so failures can be traced via Serilog.
- API key support: if `RaiderIO:ApiKey` is set, the module sends it through `x-api-key` headers automatically.

### Configuration Checklist
1. Provide `RaiderIO` settings via `appsettings.json`, environment variables, or `dotnet user-secrets`:
   ```bash
   dotnet user-secrets set "RaiderIO:Region" "us"
   dotnet user-secrets set "RaiderIO:DefaultFields" "gear,mythic_plus_scores_by_season:current"
   dotnet user-secrets set "RaiderIO:ApiKey" "<optional-api-key>"
   ```
2. Restart the bot so Discord re-synchronizes the slash command metadata.

### Future Enhancements
- Allow `region` as an optional argument on the command.
- Enrich responses with Discord embeds, linking to the Raider.IO profile page.
- Add Raider.IO endpoints for guilds, Mythic+ runs, and raid progression.

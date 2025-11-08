# Quote System Slash Commands

All quote functionality lives inside `Fusion.Bot.Modules.QuoteModule` and relies on the Mongo-backed `IQuoteRepository`. Slash commands are registered globally unless a specific `GuildId` is configured.

## `/quote add`
Add a new quote with metadata.

| Option | Required | Description |
|--------|----------|-------------|
| `person` | Yes | Speaker (name or @mention). Mention parsing captures user IDs automatically.
| `message` | Yes | Quoted text. Mentions inside the text are stored separately.
| `tags` | No | Comma- or space-separated tags. Normalized to lowercase and stored as an array.
| `nsfw` | No | Boolean toggle. Defaults to `false`.

Response: ephemeral acknowledgment with generated short ID.

## `/quote find <short-id>`
Fetch a single quote by its short identifier. Fuzzy fallback will pick the closest match if the exact ID is missing. Increments the `Uses` counter on every successful fetch.

## `/quote search <query> [limit]`
Regex-style search across quote text and tags. `limit` defaults to 5 (max 10). Returns an ephemeral list summarizing each hit and increments `Uses` afterward.

## `/quote delete <short-id>`
Soft-delete a quote. Restricted to members with `Manage Messages`, `Manage Server`, or `Administrator`. Stores the moderator ID and timestamp.

## `/quote restore <short-id>`
Undo a soft delete using the same permission checks.

## Implementation Notes
- Quotes are persisted in MongoDB via `MongoQuoteRepository` using automatic indexes on `ShortId`, `PersonKey`, `Tags`, and `DeletedAt`.
- Slash commands call repository abstractions directly; do not query Mongo from modules.
- `QuoteAddRequest` handles tag parsing, mention detection, and normalization logic.

## Future Enhancements
- Embeds/buttons for `/quote find` so users can like or resurface quotes inline.
- Pagination commands (`/quote list`, `/quote top`, `/quote stats`).
- Import/export helpers for migrating legacy quotes.

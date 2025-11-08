# Quote Management System

## Completed / Foundations
- [x] Harden quote retrieval (increment `Uses` on lookups, add atomic repository method)
- [x] `/quote delete` (soft delete, permissions enforced)
- [x] `/quote restore`
- [x] Hide deleted quotes in lookups/search
- [x] Create MongoDB indexes for quote queries (`ShortId`, `PersonKey`, `Tags`, soft-delete indicator)

## Moderation & Governance
- [ ] Role-based configuration/perms (beyond initial `ManageMessages` check) with per-guild override list.
- [ ] Add audit logging for moderation actions (delete/restore) with Serilog enrichment.
- [ ] Allow NSFW quotes to be retrieved only in channels flagged opt-in; guard `/quote find`/`search` output based on channel settings.

## User Experience & Commands
- [ ] Improve UX with embeds and buttons for `/quote find` (display tags, mentions, like/resurface actions).
- [ ] Add interactive buttons to increment/decrement `Likes` so the existing counter has a user-facing action.
- [ ] Implement `/quote random` with optional filters (person, tag, NSFW toggle).
- [ ] Implement `/quote top` or `/quote stats` to surface most-used or most-liked quotes.
- [ ] Provide `/quote list` with pagination to browse quotes by person or tag.
- [ ] Add `/quote import` to batch-upload quotes from CSV/JSON for onboarding older history.

## Persistence & Reliability
- [ ] Add background job/command to rebuild `PersonKey` for legacy quotes if normalization rules change.
- [ ] Introduce soft-delete TTL or archival collection so purged quotes eventually leave the primary collection.
- [ ] Add caching layer (e.g., in-memory/LiteDB) for hot quote responses to reduce Mongo load in active guilds.
- [ ] Capture health/metrics (success/failure counts, latency) via `EventCounters` or OpenTelemetry for monitoring.

## Tooling, Tests & Deployment
- [ ] Bootstrap `fusion.runner.Tests` with coverage for quote helpers and repository logic.
- [ ] Expand Fusion.Bot tests to cover `QuoteModule` edge cases (permissions, fuzzy search fallbacks, truncation).
- [ ] Wire up GitHub Actions workflow for build + test automation on PRs.
- [ ] Add Dockerfile (runner + bot) and optional `docker-compose` for Mongo + bot dev stack.
- [ ] Publish deployment guide for hosting on containers/VMs, including systemd service sample.

# Spellbound TODO

Living punch list for `ACE.Mods.Spellbound`. Open items only — once something lands, the rule it encodes belongs in [CLAUDE.md](CLAUDE.md), not here. Update as you go: check items off, add new ones when discovered, remove ones that no longer apply.

## Achievement system

- [ ] **Wire the remaining `SpellboundEventTrigger` values.** Wired today: `Player_OnKill`, `Player_OnLevel`, `Player_OnDeath`, `Player_PreCast`. Roughly 22 enum values still unwired (regen events, portal entry/exit, loot, quests, item use, magic resist, evades, PK kill/death, account create/login, etc.). Bulk expansion deferred — pick this up one trigger at a time when an achievement requires it. Pattern: add payload under `Model/Events/Payloads/`, register in `EventBus._payloadFor`, drop a `<Trigger>Handler.cs` under `EventHandlers/AchievementRules/`.
- [ ] **Multiplier-style award types.** `ExperienceBonus`, `LuminanceBonus`, `FlatDamage`, `PercentDamage`, `FlatCritDamage`, `PercentCritDamage`, `ArmorLevel`, `AllResists` — `AchievementService.ApplyToCharacter` currently logs and skips them. Each needs a dedicated runtime calc-path Harmony hook (XP grant path, damage calc, etc.) since they're per-event multipliers, not one-shot stat mutations. Decide which calc paths to patch before picking this up.

## Connection management
- [ ] Allow non-admins to only have 2 accounts logged in at the same time (from IP).  With an exception of if they are in the 'marketplace' landblock.  So it's a max of 2 accounts per IP that can be in non-marketplace landblocks.  Add an ip whitelist for this also so certain ips (that will be admins) can have any number of active accounts logged in.

## Website / Asheron's Eye (low priority)

Blazor app branded **Asheron's Eye** (the user-facing moniker — page titles, headers, marketing copy; the project itself stays `ACE.Mods.Spellbound.Web`). Functionally parallel to the old Blizzard WoW Armory: players log in with game credentials and view characters (stats / skills / gear), achievement badges, and leaderboards. Separate ASP.NET Core service that runs as its own process on the same box as the game server, queries the existing MySQL databases directly. **Does not load into the ACE game server process** (ACE has no HTTP surface and we don't want to add one).

### Architecture
- New project `Source/ACE.Mods.Spellbound.Web/` (Blazor Web App with Interactive Server render mode — components run on the server, SignalR pushes UI diffs to the browser). Sibling to the mod project; not loaded into the game server.
- Project-references `ACE.Database` + `ACE.Common` so we reuse the `Account` entity and `BCryptProvider.Verify` rather than forking auth code. Also references the mod project for the EF model + entities.
- Cookie-based auth against `ace_auth.Account.PasswordHash`. No account creation in the web app — accounts are created in-game. Reject login for banned accounts (use whatever AccessLevel / AccountStatus the game already enforces at login).
- Privacy: **all-or-nothing**. Any authenticated, non-banned account sees everything the site exposes. No per-character or per-account public/private toggles.
- DB scopes: read-only against `ace_auth` and `ace_shard`; read/write against `ace_mod_spellbound` (web sessions, audit log, leaderboard counters, snapshots).
- Hosting: same box as the game server.
- Visual design: AC-client-native aesthetic — reuse icons, UI chrome, and palette extracted from `DATS/client_portal.dat` rather than a generic web component library. Tailwind CSS for layout/utilities; data grids only (likely MudBlazor or Radzen) where hand-rolling a sortable/paged table isn't worth it. See Phase 0.

### Snapshot strategy
Most read paths are served from snapshot tables in `ace_mod_spellbound` rather than live game state. Snapshot writers live in the **mod** (Harmony hooks + timers); the web app is read-only against snapshots.

| Snapshot | Cadence |
|---|---|
| Online roster (`/who` page) | Every 5 minutes + on login / logout |
| Character profile (stats + skills) | On logout + every 30 minutes while online |
| Character equipment | On logout + every 30 minutes while online |
| Leaderboards | Live (event-driven counter UPSERT — see Phase 3) |

### Phase 0 — Asset extraction from dat files
Pull visual assets out of the local AC dat files so the site looks like the AC client. Dats are already in the repo at `DATS/client_portal.dat`. `ACE.DatLoader.FileTypes.Texture.ExportTexture()` already handles DXT decompression + palette lookup + PNG encoding, so this is a thin extraction tool, not net-new image code. Can run in parallel with Phase 1; only blocks final visual polish.

Legal posture: same gray-space the rest of the emulator already operates in (we're using assets from dats the user supplied to a server they run). Keep extracted assets bundled with the web app; don't redistribute the raw dats.

- [ ] New project `Source/ACE.Mods.Spellbound.DatExtractor/` (console app, references `ACE.DatLoader`). One-shot tool — not part of the normal build, rerun on dat updates.
- [ ] Initialize `DatManager` against `DATS/`, iterate the texture range (`0x06000000–0x07FFFFFF`), call `ExportTexture()` per entry.
- [ ] Use `SkillTable` (0x0E000004) and `SpellTable` to map icon IDs to human-readable names — output `out/skills/heavy_weapons.png`, `out/spells/strength_self_vi.png`, not raw hex IDs.
- [ ] Categorize output directories: `out/skills/`, `out/spells/`, `out/items/`, `out/attributes/`, `out/ui-chrome/`, `out/uncategorized/`. UI chrome + uncategorized need a manual visual review pass.
- [ ] Curated subset (likely a few hundred files) committed into `Source/ACE.Mods.Spellbound.Web/wwwroot/img/ac/`. Web app references those as static files; no dat dependency at runtime.

### Phase 1 — MVP (auth + profile + badges + /who)
- [ ] Scaffold `Source/ACE.Mods.Spellbound.Web/`; cookie auth + login page that verifies against `ace_auth.Account` via BCrypt; banned-account rejection.
- [ ] Rate-limit login attempts (shared game-account credentials are a spraying target).
- [ ] Schema: `CharacterProfileSnapshot`, `OnlinePlayers` in `ace_mod_spellbound`. Updates + Baseline mirror.
- [ ] Snapshot writers in the mod: profile snapshot on logout + 30-min timer; roster snapshot on login/logout + 5-min timer. New directory `EventHandlers/SnapshotRules/` (or fold into existing handlers — decide at impl time).
- [ ] Character search by name (case-insensitive, excludes `is_Deleted = 1`).
- [ ] Character profile page: level, total XP, attributes (Str/End/Coord/Quick/Focus/Self), vitals (Health/Stamina/Mana), skills with trained/specialized state. Source from `CharacterProfileSnapshot`.
- [ ] Achievement badge wall — `AccountAchievements` joined to `Achievement`, filter `AwardedAt IS NOT NULL`.
- [ ] `/who` page from `OnlinePlayers` snapshot.

### Phase 2 — Equipment display
- [ ] Schema: `CharacterEquipmentSnapshot` (slot rows or JSON blob — decide at impl time). Updates + Baseline mirror.
- [ ] Snapshot writer hooked into the same logout + 30-min path as profile; serializes equipped items (name, workmanship, properties, spells).
- [ ] Render equipped slots + tooltip on the profile page.

### Phase 3 — Leaderboards
Today only per-achievement `Progress` is tracked. No per-creature kill map, no PK kill counter independent of an achievement.

**Event channel:** Leaderboard counters need a separate dispatch path that subscribes to the same `SpellboundEventTrigger` events as achievements. Two reasonable shapes — pick at impl time:
- Extend `SpellboundDispatcher.Run` with a 4th step calling `LeaderboardService.RecordEvent(payload)`. Keeps primary dispatch consolidated.
- Parallel `[SpellboundEvent]` subscribers under `EventHandlers/LeaderboardRules/<Trigger>Handler.cs`, mirroring `AchievementRules/`. Truly independent dispatch (this is one of CLAUDE.md's "earn its own subscriber" exceptions).

**Schema:** `Leaderboards (Category VARCHAR, CharacterId BIGINT, CharacterName VARCHAR, Count INT, SnapshottedAt DATETIME)` with unique index on `(Category, CharacterId)`. Categories are free-form strings the rule author picks (e.g. `kills.virindi`, `kills.pk`, `deaths.pvp`, `achievements.earned`). On each event the channel does an atomic UPSERT: bump `Count`, set `SnapshottedAt = NOW()`, refresh `CharacterName` (in case of rename). All work goes through `RunDbWork` so the tick thread never blocks on the leaderboard DB write.

**Season behavior:** Truncated on season reset. Add `TRUNCATE Leaderboards;` to `Database/Spellbound/Operations/season-wipe.sql`.

- [ ] Schema + Updates + Baseline mirror + season-wipe.sql truncate.
- [ ] `LeaderboardService` with the UPSERT primitive.
- [ ] PK-kill `SpellboundEventTrigger` (overlaps with the "wire remaining triggers" item above — do it here if Phase 3 lands first).
- [ ] Wire the counter channel onto `Player_OnKill` / `Player_OnDeath` / additional triggers as we add them.
- [ ] Define the initial Category set (creature-type kills, PK kills, deaths, achievements earned, etc.) — list in code, not config, so the rules stay reviewable.
- [ ] Leaderboard pages: top N per category, with paging.

### Phase 4 — Admin (much lower priority)
- [ ] Achievement / `WorldStateRule` / Zone CRUD UI.
- [ ] Extended account properties (Discord handle, etc.).

## Server-Specific Decal Plugin (low priority)
- [ ] Ideate / define feature set that would make sense inside a custom decal plugin (like lum per hour)
- [ ] 

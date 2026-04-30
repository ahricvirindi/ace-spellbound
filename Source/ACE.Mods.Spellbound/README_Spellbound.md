# ACE.Mods.Spellbound

Custom mod that turns this fork of [ACEmulator/ACE](https://github.com/ACEmulator/ACE) into the **Spellbound** server. Three features:

1. **Account-level achievements** — bonuses that persist across characters on the same account, including new ones rolled after the achievement was earned.
2. **Seasonal narrative zone stages** — characters wipe between seasons, but accounts and achievements survive. Zones (towns, dungeons, wilderness — anywhere narrative beats live) advance through stages (driven by event rules or by `/zone stage <n>`), reloading the landblock from a per-stage SQL bundle. `/zone` is the unified subcommand surface: info / `name` / `stage` / `settele` / `tele`.
3. **Permanent character-name reservations** — handles you owned in a prior season are reserved to your account across the wipe. Populated automatically by `season-wipe.sql` (snapshot of `(name, account_Id)` pre-truncate); enforced at character creation via Harmony prefix. Admin commands `/reservename`, `/unreservename`, and `/reservednames` cover retroactive edits.

The mod uses [Harmony](https://github.com/pardeike/Harmony) for runtime injection into the core ACE projects, so we almost never need to edit upstream code. **All custom code lives under this directory.**

## Folder map

```
Mod.cs                         ← entry point; auto-loads SpellboundPatchBase subclasses,
                                 boots EventBus + CustomAchievementRegistry
Base/SpellboundPatchBase.cs    ← every patch inherits this; provides DB primitives
CommandHandlers/
  AdminCommands/               ← AccessLevel.Admin slash-commands (/zone, /who,
                                 /grantachievement, /reservename, etc.)
  PlayerCommands/              ← AccessLevel.Player slash-commands
Config/Settings.cs             ← typed settings loaded from Settings.json
Data/SpellboundContext.cs      ← EF Core DbContext for our private MySQL DB
EventHandlers/
  AchievementRules/            ← <Trigger>Handler.cs — Harmony publisher + dispatcher
                                 subscriber. Achievement-domain lifecycle Harmony
                                 patches without EventBus also live here
                                 (PlayerOnCharacterCreatedHandler,
                                 PlayerOnEnterWorldHandler).
    CustomAchievementRules/    ← code-driven [CustomAchievement] evaluators
                                 (nested under AchievementRules)
  AccountRules/                ← lifecycle Harmony patches that gate account-side
                                 actions across season boundaries
                                 (PlayerOnCreateReservedNameHandler enforces the
                                 ReservedNames table at character creation).
  GameplayRules/               ← independent [SpellboundEvent] subscribers that
                                 mutate gameplay (e.g. cancel a cast).
Helpers/                       ← cross-cutting utilities (SpellboundLog,
                                 PlayerMessaging, LandblockNaming)
Model/                         ← EF entities; Events/ holds EventBus + payload records
Services/
  AchievementService.cs        ← atomic award SQL, apply-to-characters
  WorldStateService.cs         ← zone stage mutation + landblock reload
  RuleEvaluator.cs             ← load-and-match loop for rule rows
  RuleMatcher.cs               ← payload-aware (FilterType, Target) match
  SpellboundDispatcher.cs      ← shared three-step flow every trigger handler runs
  CustomAchievementRegistry.cs ← discovery + dispatch for [CustomAchievement]
GlobalUsings.cs                ← project-wide using directives
Meta.json                      ← mod manifest read by ACE's mod loader
Settings.json                  ← local config (DB creds, paths) — see below
```

## Build & deploy

The project's `<OutputPath>` points at `MODS/ACE.Mods.Spellbound/` at the repo root, which is where the running ACE server expects to find loadable mods. Building drops the DLL straight into that location.

```
dotnet build Source/ACE.Mods.Spellbound/ACE.Mods.Spellbound.csproj
```

`Meta.json` has `HotReload: true`, so a running server will pick up rebuilds — but Harmony patch *changes* on hot reload are notoriously fragile. Restart the server when in doubt.

## Settings.json

`Settings.json` carries local secrets (MySQL host / user / password) and absolute paths (e.g. the zone-stages content directory). It's checked into git for new-clone bootstrap, but you almost certainly want your local edits to stay out of commits. Use git's `assume-unchanged` flag:

```
git update-index --assume-unchanged   ./Source/ACE.Mods.Spellbound/Settings.json
git update-index --no-assume-unchanged ./Source/ACE.Mods.Spellbound/Settings.json
```

(First line: stop tracking local edits. Second: opt back in.)

## Slash commands

Most live-ops happens through the slash commands below. Admin commands require `AccessLevel.Admin` (or higher) on the issuing account; player commands are open to everyone in-world. Square brackets are optional, angle brackets are required.

**Admin — zones (a "zone" is a named landblock that may also have stage state):**
- `/zone` — show the zone for the landblock you're standing in: name, current stage, last-updated, and any stage SQL files on disk for it. If there's no zone here, you'll be told to make one.
- `/zone name <name...>` — create the zone for this landblock, or rename an existing one. Names must be unique across all zones (case-insensitive). If you rename a staged zone, you also need to rename `Content/zone-stages/<oldName>/` to match — the command warns you about this.
- `/zone stage` — list the stage SQL files available for this zone (i.e. what numbers `/zone stage <n>` would actually find).
- `/zone stage <n>` — set the zone to stage `n` (0..10). Imports `Content/zone-stages/<Name>/<n>.sql` against the world DB inside a transaction, then reloads the landblock so all the new content shows up. Players in the affected landblock get a "the world shifts..." broadcast first.
- `/zone settele` — record your current position (cell + xyz + rotation) as the zone's tele point. Zone must already exist.
- `/zone tele [<name>]` — teleport to a zone's tele point. With no argument, jumps to the current landblock's zone (if it has a tele point set). With a name, jumps to the named zone.

**Admin — population & accounts:**
- `/who` — list every online player with character name, level, current location, and `[accountname]`. Useful before running `/reservename` or `/grantachievement` since both take an account name as argument.
- `/grantachievement <accountName> <achievementId>` — force-grant an achievement, bypassing whatever event normally awards it. Re-walks all the account's characters to apply the bonus. Idempotent if already granted.
- `/reservename <characterName> <accountName>` — permanently reserve a character handle to an account. Used to retroactively claim handles that `season-wipe.sql` missed (e.g. a character deleted just before the wipe).
- `/unreservename <characterName>` — clear a reservation, freeing the name for any account.
- `/reservednames [accountName]` — list every reservation grouped by account name. Pass an account name to filter.

**Player:**
- `/achievements` — show the player's earned + in-progress achievements for their account (works across all their characters since the achievement system is account-scoped).

## Zone staging — operational guide

A *zone* is just a landblock you've given a name to. Naming a landblock unlocks two things: it shows up in `/who` output instead of a raw hex id, and (if you set up stage SQL files) it can advance through narrative stages where its content gets rebuilt from a new SQL bundle.

### Setting up a new zone

1. **Stand in the landblock** you want to name. Run `/zone name Arwic`. The zone now exists with stage 0 and no tele point.
2. **Optional: set a tele point.** Walk to the spot you'd want to drop in (a portal landing, a town center). Run `/zone settele`. Now `/zone tele Arwic` will land any admin there.
3. **Optional: set up staging.** If this zone is going to progress through narrative beats, create a folder `Content/zone-stages/Arwic/` and put numbered SQL files in it (`0.sql`, `1.sql`, `2.sql`, ...). Each file should contain the `landblock_instance` rows you want for that stage — typically exported from a content tool like Lifestoned. Stage 0 is "the starting state"; stage 1 is "after the first beat," etc.
4. **Verify.** Run `/zone` (no args) — you should see the name, current stage, and the list of stage numbers found on disk.

### Advancing a zone

- Manually: `/zone stage 2` while in the zone. The world DB transaction either fully imports or rolls back; if the import fails, the stage column is **not** updated, so you can fix the SQL and try again. Players in the landblock get a system-chat broadcast and the landblock fully reloads (corpses + dropped items get unloaded; static spawns reload from the new SQL).
- Automatically: drop `WorldStateRule` rows in the DB that match `(EventTrigger, FilterType, Target) → (ZoneId, TargetStage)`. When a matching event fires, the zone advances to that stage. (Only advances upward — event-driven rules can't regress a stage. Use `/zone stage` for that.)

### Common pitfalls

- **You renamed the zone but the stages broke.** `/zone name` only renames the database row. You also need to rename `Content/zone-stages/<oldName>/` to the new name on disk, or stage advances will fail to find the SQL files. The command warns you about this when the rename is on a zone that's at stage > 0.
- **You ran `/zone stage 1` and got "no SQL file found."** Either the stage SQL doesn't exist on disk yet (check `Content/zone-stages/<Name>/`), or the `ZoneStagesDirectory` setting in `Settings.json` points at the wrong place.
- **A zone advanced and now content looks broken.** Check the stage SQL file for that zone and stage — typos or duplicate rows in the import will manifest as missing/duplicated NPCs. The transaction rolls back on syntax errors but not on logical mistakes.

### Season wipe

Between seasons, run `Database/Spellbound/Operations/season-wipe.sql` against the database with the server **stopped**. The script in order: snapshots all current characters into `ReservedNames` (so handles stay claimed), truncates the shard tables (deleting every character + biota + house permission), and resets every zone's stage to 0. Account rows, achievements, zone definitions, and reservations all survive. Restart the server after.

## Achievements — operational guide

An *achievement* is a row in the `Achievement` table that defines a bonus the player can earn. Achievements are **account-level** — earning one applies the bonus to every character on that account, including future characters created after the season wipe. The bonus persists across seasons.

### Anatomy of an achievement row

| Column | Meaning |
|---|---|
| `Id` | Stable integer key. Code-driven achievements reference this id directly, so don't renumber. |
| `Name` | Display name shown in `/achievements`. |
| `AwardDescription` | Flavor text for UI. |
| `EventTrigger` | Which game event drives this (kill, level, death, etc.). See `Model/Enumerations/SpellboundEventTrigger.cs` for the int values. |
| `FilterType`, `Target` | What kind of match (a specific creature id, a creature type, a level number, etc.). `Target = NULL` means wildcard — any event of that trigger counts. |
| `AwardType` | What stat gets bumped on award (Strength, Health, MagicDefense, etc.). See `Model/Enumerations/AchievementAwardTypes.cs`. Some types (XP bonus, damage modifiers) aren't fully wired yet — `AchievementService` logs and skips them. |
| `AwardValue` | How much of the stat to add. |
| `AmountRequired` | How many matching events to flip the achievement from in-progress to earned. `1` for one-shots, `100` for "kill 100 creatures." |

### Adding a new achievement

1. Pick an unused id (the seed file uses 9001+ for code-driven, conventionally use higher ranges for new data-driven achievements).
2. Add an `INSERT IGNORE INTO Achievement (...)` row to `Database/Spellbound/Seeds/achievements.sql`. Look at existing examples in that file for the shape.
3. Run that statement against the database. (`INSERT IGNORE` makes it safe to run the whole seed file — existing rows are skipped.)
4. Trigger the matching event in-game and verify the player sees it via `/achievements`.

### Two kinds of achievements

- **Data-driven** — the fields on the row decide eligibility. Most achievements are this kind. Example: "kill 100 of any creature" is `EventTrigger=Player_OnKill, FilterType=WeenieId, Target=NULL, AmountRequired=100`. No code change required to add a new one.
- **Code-driven** — the row only carries Name + AwardType + AwardValue. A C# evaluator with `[CustomAchievement(<id>, <trigger>)]` decides whether the event qualifies. Use this when eligibility logic doesn't fit the simple `(FilterType, Target)` shape. Example: "first critical kill" needs to read damage details that aren't in the row's fields, so it's `EventHandlers/AchievementRules/CustomAchievementRules/FirstCriticalKill.cs`.

### Operational tasks

- **Force-grant an achievement** (e.g. compensating a player for a bug, retroactively rewarding event participation): `/grantachievement <accountName> <id>`. Re-walks every character on the account and applies the bonus. Safe to re-run — already-granted is a no-op.
- **Check what a player has**: have them run `/achievements` themselves, or as an admin you can `SELECT * FROM AccountAchievements WHERE AccountId = <id>` directly.
- **Edit an existing achievement's bonus**: edit the row in the DB (the seed file's `INSERT IGNORE` won't overwrite). If you change `AwardType` or `AwardValue` after some accounts have already earned it, those accounts' character bonuses won't retroactively update — they got what was current at award time.

## Database

Spellbound owns its own MySQL database (`ace_custom_spellbound` by default per `Settings.json`); the upstream `ace_world` / `ace_shard` / `ace_auth` are unchanged.

There's no full EF Core migrations setup. Schema artifacts live under `Database/Spellbound/`:

- `Baseline/CreateSpellboundDb.sql` — full schema for a fresh DB. Equivalent to "blank DB + every `Updates/*.sql` applied in chronological order."
- `Updates/<YYYY-MM-DD-NNN>-<slug>.sql` — dated, hand-written deltas applied manually after entity / `OnModelCreating` changes.
- `Seeds/{achievements,zones}.sql` — canonical INSERT IGNORE rows; stable Ids referenced by code-driven evaluators.
- `Operations/season-wipe.sql` — manual between-season procedure (snapshot reservations, truncate shard, reset zone stages). Run with the server stopped.

When you change an entity or `OnModelCreating`, write BOTH a new `Updates/*.sql` AND mirror the delta into the baseline so a fresh-box bootstrap stays equivalent to "blank + all updates."

## Where to read next

- [CLAUDE.md](../../CLAUDE.md) — design rules, threading contract, conventions, and the "how to add a feature" sequence. Required reading before non-trivial changes.
- [TODO.md](../../TODO.md) — living punch list of in-progress work and known gaps.

> **Keep CLAUDE.md current.** When you change a pattern — directory layout, dispatcher shape, threading rule, helper convention — fold the change into [CLAUDE.md](../../CLAUDE.md) in the same commit. If CLAUDE.md drifts from what the code actually does, the next contributor (human or AI) wastes time chasing stale rules, or worse, follows them. Treat doc updates as part of "done," not as cleanup for later.

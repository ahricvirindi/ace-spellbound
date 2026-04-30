# CLAUDE.md — ACE-Spellbound

Instructions for Claude when working in this repository. Read this first; it establishes the rules of engagement for everything below.

## What this repo is

`ACE-Spellbound` is a fork of the [ACEmulator/ACE](https://github.com/ACEmulator/ACE) Asheron's Call server emulator. The fork tracks upstream and adds a custom mod, **`ACE.Mods.Spellbound`**, that delivers two features:

1. **Account-level achievements** — bonuses (stats, XP, damage modifiers, etc.) earned by characters that apply to *every* character on that account, including future characters.
2. **Seasonal content with narrative zone stages** — characters get wiped between seasons (accounts and achievements persist). Admin commands swap zones in/out by importing per-stage SQL bundles.  For example a town can progress (or regress) through narrative beats.

## The Cardinal Rule: Stay out of the core projects

This is a fork, and we want clean upstream merges. **All custom code goes in `Source/ACE.Mods.Spellbound/`.** The mod uses [Harmony](https://github.com/pardeike/Harmony) for runtime code injection into the core ACE projects, so we almost never need to edit them directly.

Core projects you should NOT modify unless absolutely necessary:
- `Source/ACE.Adapter`
- `Source/ACE.Common`
- `Source/ACE.DatLoader`
- `Source/ACE.Database`
- `Source/ACE.Entity`
- `Source/ACE.Server`

If a change in core feels unavoidable, **stop and discuss with the user first**. There is almost always a Harmony patch, prefix, postfix, or transpiler that will achieve the same thing without touching upstream code. The cost of one merge conflict beats the cost of an awkward Harmony patch.

Acceptable exceptions are rare: a bug genuinely in upstream that's blocking us, or a hook point that doesn't exist and can't be reached via Harmony. Even then, prefer opening a small PR upstream over carrying a local diff.

## Repo layout

```
Source/
  ACE.*                 ← upstream core projects (don't touch)
  ACE.Mods.Spellbound/  ← OUR code lives here
    Mod.cs              ← entry point; auto-loads SpellboundPatchBase subclasses
    Base/
      SpellboundPatchBase.cs   ← all patches inherit this; provides DB access
    CommandHandlers/
      Admin/                   ← AccessLevel.Admin slash-commands
      Player/                  ← AccessLevel.Player slash-commands
    Config/Settings.cs         ← typed settings (loaded from Settings.json)
    Data/
      SpellboundContext.cs     ← EF Core DbContext for our private MySQL DB
    EventHandlers/             ← three peer subfolders, one per kind of subscriber
      AchievementRules/        ← Mostly ONE file per SpellboundEventTrigger:
                                 <Trigger>Handler.cs — Harmony publisher + the single
                                 [SpellboundEvent] subscriber that runs the standard
                                 three-step dispatch (RuleEvaluator → AchievementService /
                                 WorldStateService / CustomAchievementRegistry).
                                 Lifecycle Harmony patches that don't go through EventBus
                                 (e.g. NewCharacterApplyHandler — re-applies granted
                                 achievements on character create) live here too.
      GameplayRules/           ← independent [SpellboundEvent] subscribers that mutate
                                 gameplay (e.g., cancel a cast). One file per rule.
      CustomAchievementRules/  ← [CustomAchievement]-tagged code-driven achievement
                                 evaluators — for eligibility logic that doesn't fit
                                 the data-driven (FilterType, Target) shape
    Helpers/                   ← cross-cutting utilities, namespace globally imported
      SpellboundLog.cs         ← Info/Warn/Error wrappers that own the [Spellbound] log prefix
      PlayerMessaging.cs       ← Tell / TellAccount / BroadcastWorld extensions on Player
    Services/
      AchievementService.cs    ← atomic award SQL, apply-to-characters walk, base-stat mutation
      WorldStateService.cs     ← town stage mutation, transactional SQL import, landblock reload
      RuleMatcher.cs           ← payload-aware (FilterType, Target) match
      RuleEvaluator.cs         ← load-and-match loop for Achievement / WorldStateRule rows
      SpellboundDispatcher.cs  ← the standard three-step pipeline every per-trigger handler runs
      CustomAchievementRegistry.cs ← boot-time discovery + dispatch for [CustomAchievement]
    Model/                     ← EF entities (Achievement, AccountAchievement,
                                  AwardedCharacterAchievement, Town, WorldStateRule, ...)
      Events/                  ← EventBus + payload records + attribute(s).
                                  SpellboundEventArgs exposes Subject (the player this is
                                  about) and AccountId — payloads override Subject so handlers
                                  don't re-implement the null-check + extraction.
    GlobalUsings.cs            ← all the ACE.* and Harmony usings live here
    Meta.json                  ← mod manifest read by ACE's mod loader
    Settings.json              ← local config (DB creds, paths, HarmonyDebug) — see note below
MODS/
  ACE.Mods.Spellbound/  ← build output drops here; ACE loads mods from this dir
Content/
  town-stages/<townName>/<stageNumber>.sql   ← stage SQL bundles
Database/
  ...                   ← upstream DB schema files (don't touch)
  Spellbound/           ← OUR SQL artifacts
    Baseline/CreateSpellboundDb.sql          ← fresh-DB bootstrap. Equivalent to
                                              "blank DB + every Updates/*.sql in order."
    Updates/<YYYY-MM-DD-NNN>-<slug>.sql      ← dated, hand-authored migrations.
                                              Apply manually after entity / schema changes.
    Seeds/{achievements,towns}.sql           ← canonical INSERT IGNORE seed rows; stable Ids
                                              referenced by code-driven evaluators.
    Operations/season-wipe.sql               ← manual season-wipe procedure;
                                              run with the server stopped.
```

`Settings.json` contains local secrets (MySQL creds). It's tracked in git but should be `assume-unchanged`-ed locally — see `Source/ACE.Mods.Spellbound/README_Spellbound.md` for the git command.

## Build & deploy loop

The mod project's `<OutputPath>` is `C:\SRC\ACE-Spellbound\MODS\ACE.Mods.Spellbound`. Building the mod drops the DLL where the running ACE server expects to find it. The mod's `Meta.json` has `HotReload: true`, so an actively running server will pick up rebuilds — but Harmony patch *changes* on hot reload are notoriously fragile. When in doubt, restart the server.

Build it via `dotnet build Source/ACE.Mods.Spellbound/ACE.Mods.Spellbound.csproj` (or via the solution). Don't try to run the full solution test suite to "validate" mod changes — the upstream tests don't exercise our code.

For diagnosing patch failures, set `"HarmonyDebug": true` in `Settings.json` and restart the server. Off by default; verbose Harmony IL logging is expensive enough not to leave on in production.

## How patches and event handlers work

There are two registration paths and they work together:

1. **Harmony patches** — `Mod.cs` reflectively instantiates every concrete subclass of `SpellboundPatchBase`. Apply `[HarmonyPatch]` / `[HarmonyPostfix]` etc. to static methods on that class and they get wired automatically. Use this for the *publisher* side: a postfix that reads inputs from the upstream method and calls `EventBus.Publish(...)`.
2. **EventBus subscribers** — static methods tagged with `[SpellboundEvent(<trigger>)]` that take a single payload parameter. `Mod.cs` runs `EventBus.DiscoverAndRegister` once at boot and validates everything against `EventBus._payloadFor`. Use this for the *subscriber* side: react to a published payload, dispatch to services.

**One file per trigger, one subscriber per trigger.** A trigger handler (e.g. `EventHandlers/AchievementRules/PlayerOnKillHandler.cs`) is a `SpellboundPatchBase` subclass that holds:
  - the Harmony postfix that publishes the event
  - the single `[SpellboundEvent(<trigger>)]` static method that hands off to `SpellboundDispatcher.Run`. The dispatcher owns the standard three-step flow — DB-driven achievements (via `RuleEvaluator` + `AchievementService`), DB-driven world-state advances (via `RuleEvaluator` + `WorldStateService`), and code-driven custom achievements (via `CustomAchievementRegistry`). The handler keeps trigger-specific entry guards (e.g. `e.CancelCast` short-circuit on PreCast, `if (e.AccountId is not uint accountId) return;`) and the `RunDbWork` dispatch onto the ThreadPool.

Don't add a second `[SpellboundEvent(<trigger>)]` subscriber for the "primary dispatch" — the EventBus permits it but it splits responsibility you'd rather keep in one place. Two exceptions earn their own subscriber:
  - **Gameplay rules** that mutate the payload (e.g. set `CancelCast = true` on a `PlayerPreCastEvent`) — these live under `EventHandlers/GameplayRules/`. They're independent from each other and the dispatcher, so each gets its own file.
  - **Custom achievements** — code-driven achievement evaluators tagged `[CustomAchievement(achievementId, trigger)]`, in their own files under `EventHandlers/CustomAchievementRules/`. Discovered separately by `CustomAchievementRegistry`, dispatched FROM the per-trigger handler — they are not direct EventBus subscribers.

**Prefix + postfix with `__state`.** When the publisher needs a value the postfix can't reach (e.g. `Player.CheckForLevelup` stores `startingLevel` in a local), use Harmony's `__state` parameter to pass it from prefix to postfix. Cheaper than a transpiler, more reliable than caching state in a static dict keyed by player. See `EventHandlers/AchievementRules/PlayerOnLevelHandler.cs` for the canonical shape.

**Lifecycle Harmony patches without EventBus.** Some hooks aren't really "events" with rule-driven dispatch — they're just "do this work when X happens." Example: `NewCharacterApplyHandler` re-applies granted achievements when a character is created. These live under `EventHandlers/AchievementRules/` next to the related domain code, but they're plain `[HarmonyPatch]` classes with no `[SpellboundEvent]` subscriber. Add a comment explaining why EventBus was bypassed so future-you doesn't try to refactor it back.

Reference: `EventHandlers/AchievementRules/PlayerOnKillHandler.cs` (publisher + single dispatcher) and `EventHandlers/AchievementRules/PlayerPreCastHandler.cs` paired with `EventHandlers/GameplayRules/BlockNonItemEnchantmentSpells.cs` (publisher + cancel-aware dispatcher + a separate gameplay rule subscriber). `EventHandlers/CustomAchievementRules/FirstCriticalKill.cs` is the canonical [CustomAchievement] template. `EventHandlers/AchievementRules/NewCharacterApplyHandler.cs` is the canonical lifecycle-only Harmony patch.

`SpellboundPatchBase` exposes three DB primitives — pick the right one:

- `CreateDbContext()` — fresh context, sync. Lazy-initialized factory is thread-safe (`Lazy<T>` w/ `ExecutionAndPublication`). Always `using var db = CreateDbContext();` — never cache or share across threads.
- `RunDbWork(Action<SpellboundContext>)` — fire-and-forget DB op on the ThreadPool. Use this from Harmony patches that run on landblock tick / network threads so we don't block the tick on MySQL latency. Exceptions are logged, never rethrown.
- `RunDbWork<T>(Func<SpellboundContext,T>, Action<T>)` — same but with a result callback. The callback runs on the ThreadPool; if it needs to touch a `WorldObject`, marshal back via that object's `ActionChain` inside the callback body.

Rule of thumb: if the caller is a slash command, sync `CreateDbContext()` is fine (the user is already waiting). If the caller is a Harmony postfix on a hot gameplay event (kill, damage, level), use `RunDbWork`.

## Database

Four databases are involved:

1. **Core ACE databases** (`ace_world`, `ace_shard`, `ace_auth`) — managed by upstream. Don't touch their schema for our features.
2. **Our private database** (`ace_custom_spellbound` per `Settings.json`) — owned by `SpellboundContext` and the entities under `Model/`. This is where achievements, per-character idempotency rows, account verifications, and town state live.

No EF Core migrations. Schema lives in two places under `Database/Spellbound/`:

- **`Baseline/CreateSpellboundDb.sql`** — full schema for a fresh DB. Equivalent to "blank DB + every `Updates/*.sql` applied in chronological order." Run once on bootstrap.
- **`Updates/<YYYY-MM-DD-NNN>-<slug>.sql`** — dated, hand-written deltas for changes since the baseline. Applied manually against `ace_custom_spellbound`.

**Maintenance rule:** when you change an entity or `OnModelCreating`, you write BOTH (a) a new `Updates/*.sql` so existing deployments can upgrade, AND (b) the matching delta into `Baseline/CreateSpellboundDb.sql` in the same change so a fresh-box bootstrap stays equivalent to "blank + all updates." Don't rely on `EnsureCreated`.

`Database/Spellbound/Seeds/` carries canonical INSERT-IGNORE rows for achievements + towns. Seed Ids are stable; code-driven `[CustomAchievement]` evaluators reference them by Id (e.g. `FirstCriticalKill` → 9001). `Database/Spellbound/Operations/season-wipe.sql` is the manual between-season procedure.

Mutable entities carry a `[ConcurrencyCheck] int Version` column for optimistic concurrency. Manually `entity.Version++` inside the same transaction that mutates the row, so racing `SaveChanges` calls trigger `DbUpdateConcurrencyException` instead of silent last-write-wins. Pomelo/MySQL doesn't support SQL-Server-style `rowversion`, hence the manual int.

## Achievements: rules to enforce

The achievement system grants permanent account-level bonuses, so anti-cheat and idempotency are first-class concerns:

- **One award per (account, achievement) — period.** Every code path that awards an achievement must do an existence check or rely on a DB unique constraint on `(AccountId, AchievementId)`. The unique constraint should exist; if it doesn't yet, add it (and the matching index) before relying on it.
- **Use `AwardedAt IS NOT NULL` as the "fully granted" signal.** `Progress` is for in-flight counters (kill X mobs, reach level Y). Don't apply bonuses while progress is incomplete.
- **Awards must be transactional.** Increment counters, check the threshold, write the `AwardedAt` timestamp, and commit in a single transaction. A crash mid-flow must not leave a row that looks awarded but never applied bonuses to characters.
- **Apply bonuses to all characters on the account, including future ones.** When an achievement is granted, walk every existing character on the account and apply; on character create, walk all granted achievements and apply. Both paths must converge to the same end state.
- **Watch for duplicate-trigger pitfalls.** A single in-game event (e.g., a mob death) can fire multiple Harmony patches if registered carelessly. Register each trigger exactly once and reason about whether the event hook is called per-attacker, per-tick, or per-damage-instance.
- **Account-scope, not character-scope.** Always look up `Player.Account.AccountId` (or equivalent) — never `Player.Guid`. A character-scoped award would re-grant bonuses every time the player rerolls.
- **Per-character bonus application is idempotent via `AwardedCharacterAchievements`.** The unique `(CharacterId, AchievementId)` index gates application: both the on-grant walk and the on-character-create walk attempt an `INSERT IGNORE` and treat duplicate-key as "already applied; no-op." Don't bypass `AchievementService.ApplyToCharacter` — it's the only sanctioned write path for the bonus side, mirroring how `TryAwardAtomic` is the only one for the award side.
- **Stat-style awards mutate the character's BASE stat** (`PropertyAttribute.InitLevel` / `PropertyAttribute2nd.InitLevel` / `PropertiesSkill.InitLevel`) so players can still raise the stat the normal number of times after the bonus lands. Multiplier-style awards (XP / Lum bonus, damage modifiers, ArmorLevel, AllResists) are deferred — they need runtime calc-path hooks, not one-shot mutations. `ApplyToCharacter` currently logs and skips them.

If you're about to write achievement-award code, re-read this section first.

## Seasons & town stages

- Characters wipe between seasons; accounts, `AccountAchievement` rows, `Achievement` catalog, and `WorldStateRule` defs persist. `AwardedCharacterAchievements` rows are wiped (they reference now-deleted character GUIDs). Any new account-tied data we add must survive a character wipe — design schema with that in mind.
- The wipe procedure is `Database/Spellbound/Operations/season-wipe.sql` — manual SQL run with the server stopped. It truncates every `character` / `biota` / `*_properties_*` shard table and resets `Towns.Stage = 0`. Smoke-check `SELECT`s at the bottom of the script confirm survivors are intact.
- Town staging works by importing a SQL file (`Content/town-stages/<town>/<stage>.sql`) that rewrites `landblock_instance` rows for that town's landblock, then reloading the landblock. The flow lives in `Services/WorldStateService.cs`; `CommandHandlers/Admin/SetTownStageCommandHandler.cs` (admin, force-set) and the per-trigger event handlers (event-driven, advance-only) both funnel into it. The SQL import runs in an explicit world-DB transaction (`TryImportStageSql`) so a typo mid-batch rolls back instead of leaving `landblock_instance` half-rewritten.
- Automatic triggering is wired via `WorldStateRule` rows: `(EventTrigger, FilterType, Target) → (TownId, TargetStage)`. Same `FilterType` / `Target` shape as `Achievement`, so `Services/RuleMatcher.cs` evaluates both.
- Players in the affected landblock get a system-chat broadcast right before the destroy/reload — see `WorldStateService.DispatchLandblockReload`. Same method has an inline audit comment of exactly what `DestroyAllNonPlayerObjects` removes (corpses + dropped items unload but biota survives; pets / projectiles / non-shard NPCs destroyed; static spawns reload from the just-imported SQL).
- Stage SQL files are author-controlled — but `WorldStateService` builds the file path from the town's `Name` field and the stage number, so be cautious if any of those become user-influenced. Today both come from the DB, so it's fine; if that changes, validate the path stays under `TownStagesDirectory`.

## Thread safety

ACE is heavily multithreaded. Player actions, network events, landblock ticks, and timed callbacks can all hit our patches concurrently. Rules:

- **Never share a `SpellboundContext` across threads or calls.** Always create one per operation via `CreateDbContext()` (or have `RunDbWork` do it for you). EF Core contexts are explicitly not thread-safe.
- **No mutable static state in patch classes** unless guarded by a lock or an `Interlocked`/`ConcurrentDictionary`-style primitive. The patch classes themselves are instantiated once but their static methods are called from many threads.
- **Don't block tick threads on the database.** From a Harmony postfix on a hot path (kill, damage, level, regen tick), use `RunDbWork(...)` rather than `CreateDbContext()`. Slash commands are fine sync — the player is already waiting.
- **Read-modify-write goes in a transaction.** When you must read a row, mutate it, and save it back, wrap the work in `db.Database.BeginTransaction(IsolationLevel.Serializable)` AND bump the entity's `Version++` so the `[ConcurrencyCheck]` triggers `DbUpdateConcurrencyException` on a race. See `WorldStateService.ApplyTownStage` for the canonical pattern.
- **Awards and progress increments must be atomic at the DB layer.** Prefer a single `UPDATE ... WHERE AwardedAt IS NULL` (or `SELECT ... FOR UPDATE` inside a transaction) over read-modify-write in C#. The unique index on `AccountAchievement(AccountId, AchievementId)` is your safety net — catch `DbUpdateException` with the `IsUniqueViolation` shape and treat it as "already awarded, no-op." `AchievementService.TryAwardAtomic` is the only sanctioned write path; everything that grants an achievement should funnel through it.
- **Queue work onto the player's `ActionChain` when touching `WorldObject` state.** That's how `WorldStateService.DispatchLandblockReload` destroys + reloads the landblock; follow the same pattern for anything that mutates game state from a Harmony postfix that runs off the landblock thread.
- **Logging is fine from any thread.** `ModManager.Log` is safe.

## Style and conventions

- Match existing code: 4-space indent, file-scoped namespaces are fine but match the file you're editing.
- Add new globals to `GlobalUsings.cs` rather than per-file `using` statements when they're broadly useful. `ACE.Mods.Spellbound.Helpers` is already a global using — `SpellboundLog` and the `Player` messaging extensions are reachable everywhere without explicit imports.
- **Logging:** never call `ModManager.Log` directly. Use `SpellboundLog.Info` / `Warn` / `Error` — they own the `[Spellbound]` prefix so it never drifts.
- **Player messaging:** use `player.Tell(msg)` / `PlayerMessaging.TellAccount(accountId, msg)` / `PlayerMessaging.BroadcastWorld(msg)` instead of hand-building `GameMessageSystemChat` + `Session.Network.EnqueueSend`. All three are null-safe and offline-safe.
- **New event payloads:** override `Subject` to point to the primary player (Killer for kills, Caster for casts, Defender for damage-taken). Handlers rely on `e.AccountId` working off that — leaving `Subject` defaulted breaks the entry guard.
- New EF entities: derive from `BaseKeyedModel` (Id only) or `BaseNamedModel` (Id + Name). Register the key in `SpellboundContext.OnModelCreating`.
- New patch classes go under `EventHandlers/AchievementRules/<Trigger>Handler.cs` (gameplay hooks) or `CommandHandlers/` (slash commands). Inherit `SpellboundPatchBase`. New gameplay rules go under `EventHandlers/GameplayRules/`, code-driven custom achievements go under `EventHandlers/CustomAchievementRules/`; both kinds need only `[SpellboundEvent]` / `[CustomAchievement]`-tagged static methods (no `SpellboundPatchBase` subclassing — they don't apply Harmony patches themselves).
- Don't add comments that just describe what the code does. Comments earn their place by explaining *why* (a non-obvious constraint, a workaround, an upstream quirk).

## When the user asks you to add a feature

Default sequence:

1. Confirm it can be done without editing core projects. If not, surface that explicitly before writing code.
2. Identify the Harmony hook point (`[HarmonyPrefix]`, `[HarmonyPostfix]`, transpiler) by reading the relevant core class.
3. **Pick the trigger.** If it maps to an existing `SpellboundEventTrigger`, edit the matching `EventHandlers/AchievementRules/<Trigger>Handler.cs` (don't create a parallel handler). If it's a new trigger, add the enum value, add the canonical payload to `EventBus._payloadFor`, and create the new `<Trigger>Handler.cs` under `EventHandlers/AchievementRules/` with both publisher and subscriber.
4. Write the work in the right home:
   - Cross-cutting helpers (atomic award, town stage advance, filter matching) → `Services/`
   - Gameplay-mutating rule for an existing trigger → new file under `EventHandlers/GameplayRules/`
   - Code-driven achievement → new file under `EventHandlers/CustomAchievementRules/` with `[CustomAchievement]`
5. If DB schema changes: update the entity, update `OnModelCreating`, **write BOTH a new `Database/Spellbound/Updates/<YYYY-MM-DD-NNN>-<slug>.sql` for live deployments AND mirror the change into `Database/Spellbound/Baseline/CreateSpellboundDb.sql` in the same commit.** Call out the migration to the user.
6. If touching achievements: re-read the "Achievements: rules to enforce" section before writing.
7. If touching anything that runs off the main loop: re-read "Thread safety".

## Reference files to read before designing a new patch

Boot + base:

- `Source/ACE.Mods.Spellbound/Mod.cs` — patch auto-loading + EventBus / CustomAchievementRegistry boot wiring + Settings.json read for HarmonyDebug
- `Source/ACE.Mods.Spellbound/Base/SpellboundPatchBase.cs` — DB factory, settings access, `RunDbWork` / `RunDbWork<T>` ThreadPool primitives
- `Source/ACE.Mods.Spellbound/GlobalUsings.cs` — what's already imported everywhere

Per-trigger handlers (canonical templates):

- `EventHandlers/AchievementRules/PlayerOnKillHandler.cs` — postfix-only publisher + dispatcher
- `EventHandlers/AchievementRules/PlayerOnLevelHandler.cs` — prefix+postfix `__state` pattern, per-iteration event firing
- `EventHandlers/AchievementRules/PlayerOnDeathHandler.cs` — postfix-only publisher with PvP/PvE branching
- `EventHandlers/AchievementRules/PlayerPreCastHandler.cs` paired with `EventHandlers/GameplayRules/BlockNonItemEnchantmentSpells.cs` — mutable-payload + separate gameplay rule subscriber
- `EventHandlers/CustomAchievementRules/FirstCriticalKill.cs` — `[CustomAchievement]` evaluator template
- `EventHandlers/AchievementRules/NewCharacterApplyHandler.cs` — lifecycle-only Harmony patch (no EventBus); the on-character-create achievement re-walk

Services:

- `Services/AchievementService.cs` — atomic award, apply-to-characters, base-stat mutation (online + offline biota paths)
- `Services/WorldStateService.cs` — town stage mutation, transactional SQL import, landblock reload + audit comments
- `Services/RuleEvaluator.cs` — load-and-match loop for Achievement / WorldStateRule rows
- `Services/RuleMatcher.cs` — payload-aware (FilterType, Target) match; add a switch arm + helper for new payloads
- `Services/SpellboundDispatcher.cs` — standard three-step pipeline shared across all trigger handlers
- `Services/CustomAchievementRegistry.cs` — `[CustomAchievement]` discovery + dispatch

Events:

- `Model/Events/SpellboundEventArgs.cs` — payload base; defines `Subject` / `AccountId`
- `Model/Events/EventBus.cs` — payload-trigger map and dispatch
- `Model/Events/TriggerKeyedRegistry.cs` — shared scaffolding for EventBus + CustomAchievementRegistry

Helpers + commands:

- `Helpers/SpellboundLog.cs` — the only sanctioned log entry point
- `Helpers/PlayerMessaging.cs` — `Tell` / `TellAccount` / `BroadcastWorld` extensions on `Player`
- `CommandHandlers/Admin/SetTownStageCommandHandler.cs` — slash command that calls into a service
- `CommandHandlers/Admin/GrantAchievementCommandHandler.cs` — admin force-grant with idempotent re-walk semantics
- `CommandHandlers/Player/AchievementsCommandHandler.cs` — player-facing read with offline-thread safety pattern

## TODO list

The current punch list of work-in-progress items lives in [TODO.md](TODO.md). Check it (and update it) when starting or finishing meaningful work.

# ACE.Mods.Spellbound

Custom mod that turns this fork of [ACEmulator/ACE](https://github.com/ACEmulator/ACE) into the **Spellbound** server. Two features:

1. **Account-level achievements** — bonuses that persist across characters on the same account, including new ones rolled after the achievement was earned.
2. **Seasonal narrative town stages** — characters wipe between seasons, but accounts and achievements survive. Towns advance through stages (driven by event rules or by admin command), reloading the landblock from a per-stage SQL bundle.

The mod uses [Harmony](https://github.com/pardeike/Harmony) for runtime injection into the core ACE projects, so we almost never need to edit upstream code. **All custom code lives under this directory.**

## Folder map

```
Mod.cs                         ← entry point; auto-loads SpellboundPatchBase subclasses,
                                 boots EventBus + CustomAchievementRegistry
Base/SpellboundPatchBase.cs    ← every patch inherits this; provides DB primitives
CommandHandlers/               ← slash-command handlers (admin / player)
Config/Settings.cs             ← typed settings loaded from Settings.json
Data/SpellboundContext.cs      ← EF Core DbContext for our private MySQL DB
EventHandlers/
  AchievementRules/            ← <Trigger>Handler.cs — Harmony publisher + dispatcher subscriber
  GameplayRules/               ← independent rules that mutate event payloads
                                 (e.g. cancel a cast)
  CustomAchievementRules/      ← code-driven [CustomAchievement] evaluators
Helpers/                       ← cross-cutting utilities (SpellboundLog, PlayerMessaging)
Model/                         ← EF entities; Events/ holds EventBus + payload records
Services/
  AchievementService.cs        ← atomic award SQL, apply-to-characters
  WorldStateService.cs         ← town stage mutation + landblock reload
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

`Settings.json` carries local secrets (MySQL host / user / password) and absolute paths (e.g. the town-stages content directory). It's checked into git for new-clone bootstrap, but you almost certainly want your local edits to stay out of commits. Use git's `assume-unchanged` flag:

```
git update-index --assume-unchanged   ./Source/ACE.Mods.Spellbound/Settings.json
git update-index --no-assume-unchanged ./Source/ACE.Mods.Spellbound/Settings.json
```

(First line: stop tracking local edits. Second: opt back in.)

## Database

Spellbound owns its own MySQL database (`ace_custom_spellbound` by default per `Settings.json`); the upstream `ace_world` / `ace_shard` / `ace_auth` are unchanged.

There's no full EF Core migrations setup yet — schema changes ship as dated, hand-written SQL under `Database/Spellbound/Updates/` (e.g. `2026-04-23-001-concurrency-and-indexes.sql`). When you change an entity or `OnModelCreating`, add a matching migration script and run it manually against your DB.

## Where to read next

- [CLAUDE.md](../../CLAUDE.md) — design rules, threading contract, conventions, and the "how to add a feature" sequence. Required reading before non-trivial changes.
- [TODO.md](../../TODO.md) — living punch list of in-progress work and known gaps.

> **Keep CLAUDE.md current.** When you change a pattern — directory layout, dispatcher shape, threading rule, helper convention — fold the change into [CLAUDE.md](../../CLAUDE.md) in the same commit. If CLAUDE.md drifts from what the code actually does, the next contributor (human or AI) wastes time chasing stale rules, or worse, follows them. Treat doc updates as part of "done," not as cleanup for later.

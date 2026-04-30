# Spellbound TODO

Living punch list for `ACE.Mods.Spellbound`. Open items only — once something lands, the rule it encodes belongs in [CLAUDE.md](CLAUDE.md), not here. Update as you go: check items off, add new ones when discovered, remove ones that no longer apply.

## Achievement system

- [ ] **Wire the remaining `SpellboundEventTrigger` values.** Wired today: `Player_OnKill`, `Player_OnLevel`, `Player_OnDeath`, `Player_PreCast`. Roughly 22 enum values still unwired (regen events, portal entry/exit, loot, quests, item use, magic resist, evades, PK kill/death, account create/login, etc.). Bulk expansion deferred — pick this up one trigger at a time when an achievement requires it. Pattern: add payload under `Model/Events/Payloads/`, register in `EventBus._payloadFor`, drop a `<Trigger>Handler.cs` under `EventHandlers/AchievementRules/`.
- [ ] **Multiplier-style award types.** `ExperienceBonus`, `LuminanceBonus`, `FlatDamage`, `PercentDamage`, `FlatCritDamage`, `PercentCritDamage`, `ArmorLevel`, `AllResists` — `AchievementService.ApplyToCharacter` currently logs and skips them. Each needs a dedicated runtime calc-path Harmony hook (XP grant path, damage calc, etc.) since they're per-event multipliers, not one-shot stat mutations. Decide which calc paths to patch before picking this up.

## Season lifecycle

- [ ] **Name-snipe protection.** Once a character is deleted by `season-wipe.sql`, its name becomes claimable by anyone — including a previous-season griefer trying to take a former rival's handle. Need a "previous-season name reservation": a Spellbound-side `ReservedName(Name, AccountId, ExpiresAt)` table populated from the pre-wipe character roster, checked in a `CharacterHandler.CharacterCreateEx` Harmony prefix, with a configurable grace-period TTL.

## Website (low priority)

- [ ] Blazor web application that players log into with their game account credentials and view characters / achievements / stats / gear. Style: old Blizzard WoW Armory.
  - Leaderboards (per-achievement, per-account, season-aware).
  - Admin interface for editing achievements / world-state rules (much lower priority).
  - Extended account properties (Discord handle, etc.).

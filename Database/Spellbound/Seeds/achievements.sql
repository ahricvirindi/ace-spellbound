-- ============================================================================
-- Spellbound seed: canonical Achievement rows.
-- Re-runnable: uses INSERT IGNORE keyed off the explicit Id, so re-running is
-- a no-op for already-seeded rows. Tweak a row by editing it and running the
-- corresponding UPDATE manually — re-running this script will NOT overwrite
-- existing rows by design.
--
-- Stable Ids matter: code-driven achievement evaluators (see
-- EventHandlers/CustomAchievementRules/) reference rows by Id. Renumbering
-- breaks those references silently.
--
-- Enum reference (keep in sync with Model/Enumerations/):
--   SpellboundEventTrigger:       Player_OnKill = 117, Player_OnLevel = 106,
--                                 Player_OnDeath = 102
--   EventFilterType:              WeenieId = 1, CreatureType = 2,
--                                 ItemType = 3, QuestId = 4, Level = 5
--   AchievementAwardTypes:        Health = 1, Stamina = 2, Mana = 3,
--                                 Strength = 7, Endurance = 8, ... (see enum)
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 9001 — First Critical Kill.
-- Code-driven (see EventHandlers/CustomAchievementRules/FirstCriticalKill.cs);
-- the FilterType/Target on this row are unused — eligibility is decided by
-- the [CustomAchievement(9001, Player_OnKill)] evaluator. The row supplies
-- name + bonus type/value + AmountRequired only.
-- ----------------------------------------------------------------------------
INSERT IGNORE INTO `Achievement`
    (`Id`, `Name`, `EventTrigger`, `AwardDescription`, `FilterType`, `Target`, `AwardType`, `AwardValue`, `AmountRequired`)
VALUES
    (9001, 'First Critical Kill', 117, 'Reward for landing your first critical-hit killing blow.', 1, NULL, 1, 10, 1);

-- ----------------------------------------------------------------------------
-- 9002 — First Blood. Data-driven wildcard: any kill, once.
-- Demonstrates the (FilterType=WeenieId, Target=NULL) wildcard — RuleMatcher
-- treats a null/empty Target as "matches anything for this trigger."
-- ----------------------------------------------------------------------------
INSERT IGNORE INTO `Achievement`
    (`Id`, `Name`, `EventTrigger`, `AwardDescription`, `FilterType`, `Target`, `AwardType`, `AwardValue`, `AmountRequired`)
VALUES
    (9002, 'First Blood', 117, 'Awarded the first time you defeat any creature.', 1, NULL, 7, 1, 1);

-- ----------------------------------------------------------------------------
-- 9003 — Hunter. Data-driven counter: kill 100 of any creature.
-- AmountRequired = 100 means TryAwardAtomic increments Progress on each match
-- and only flips AwardedAt on the 100th kill.
-- ----------------------------------------------------------------------------
INSERT IGNORE INTO `Achievement`
    (`Id`, `Name`, `EventTrigger`, `AwardDescription`, `FilterType`, `Target`, `AwardType`, `AwardValue`, `AmountRequired`)
VALUES
    (9003, 'Hunter', 117, 'Defeat 100 creatures.', 1, NULL, 8, 1, 100);

-- ----------------------------------------------------------------------------
-- 9004 — Apprentice. Data-driven level threshold.
-- EventTrigger=Player_OnLevel(106), FilterType=Level(5), Target="5". Per-level
-- firing in PlayerOnLevelHandler means this matches the single 4→5 transition
-- even if the player jumps multiple levels in one XP grant.
-- ----------------------------------------------------------------------------
INSERT IGNORE INTO `Achievement`
    (`Id`, `Name`, `EventTrigger`, `AwardDescription`, `FilterType`, `Target`, `AwardType`, `AwardValue`, `AmountRequired`)
VALUES
    (9004, 'Apprentice', 106, 'Reach level 5.', 5, '5', 2, 5, 1);

-- ----------------------------------------------------------------------------
-- 9005 — Inevitable. Wildcard PvE death.
-- EventTrigger=Player_OnDeath(102), Target=NULL fires on any non-PvP death.
-- AwardType=Mana(3) is a small consolation prize. PK deaths skip this trigger.
-- ----------------------------------------------------------------------------
INSERT IGNORE INTO `Achievement`
    (`Id`, `Name`, `EventTrigger`, `AwardDescription`, `FilterType`, `Target`, `AwardType`, `AwardValue`, `AmountRequired`)
VALUES
    (9005, 'Inevitable', 102, 'You died. It happens.', 1, NULL, 3, 5, 1);

-- ============================================================================
-- Spellbound DEV-ONLY test data.
-- Apply manually to populate the web app pages with sample rows for visual /
-- smoke testing. NEVER apply to production.
--
-- Touches ONE database (ace_mod_spellbound). Assumes Virin and Alys already
-- exist in `ace_shard_spellbound.character` — created in-game. We READ their
-- character ids and account ids from the shard so the seed adapts to whatever
-- ACE assigned them, but we do NOT modify any shard rows.
--
-- Edit the @*_name vars in SECTION 1 below to point at different test
-- characters. Names are looked up case-insensitively (the shard's name
-- column inherits utf8mb4_general_ci) and filter out is_Deleted rows.
--
-- DB names are hardcoded to match the user's spellbound deployment. If you
-- run with stock ACE database names (`ace_shard`, `ace_mod_spellbound`) edit
-- the references below.
--
-- This file is INTENTIONALLY NOT mirrored into Operations/reset-spellbound-db.sql
-- — the reset script is "production-shaped" (schema + canonical seeds only),
-- while this file is "I want to see something on the page."
--
-- Idempotency: scoped DELETE + INSERT keyed on Virin's and Alys's character
-- ids and their account ids. Re-running rebuilds the test rows from scratch
-- without touching real data for any other character or account.
--
-- After running, expect:
--   * /who                  · Virin and Alys both online in Holtburg.
--   * /characters · "vir"    · Virin shows up.
--   * /character/Virin       · Level 80 archer, attributes / vitals / skills /
--                              equipped bow + leather, badge wall with a
--                              couple of achievements.
--   * /character/Alys        · Level 80 void mage, robe + wand + pendant.
--   * /leaderboards          · Total XP and Tusker Kills both populated.
-- ============================================================================


-- Disable MySQL Workbench's safe-update mode for the duration of this script.
-- The Leaderboards DELETE uses CharacterId which isn't the leading column of
-- any index on that table, so safe-update flags it as an unindexed delete and
-- aborts. We restore the previous value at the end of the script so this
-- doesn't leak into the user's broader session.
SET @prev_safe_updates := @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;


-- ============================================================================
-- SECTION 1 — resolve character ids + account ids from shard by name.
-- Read-only. The diagnostic SELECT below prints the resolved values so it's
-- obvious if either character isn't in the shard yet (the *_id and
-- *_account columns come back NULL — INSERTs later will fail loudly on
-- the NOT NULL constraints).
-- ============================================================================

SET @virin_name := 'Virin';
SET @alys_name  := 'Alys';

SET @virin_id      := (SELECT `id`         FROM `ace_shard_spellbound`.`character`
                        WHERE `name` = @virin_name AND `is_Deleted` = 0 LIMIT 1);
SET @virin_account := (SELECT `account_Id` FROM `ace_shard_spellbound`.`character`
                        WHERE `id`   = @virin_id);

SET @alys_id       := (SELECT `id`         FROM `ace_shard_spellbound`.`character`
                        WHERE `name` = @alys_name  AND `is_Deleted` = 0 LIMIT 1);
SET @alys_account  := (SELECT `account_Id` FROM `ace_shard_spellbound`.`character`
                        WHERE `id`   = @alys_id);

SELECT @virin_name    AS virin_name,
       @virin_id      AS virin_id,
       @virin_account AS virin_account,
       @alys_name     AS alys_name,
       @alys_id       AS alys_id,
       @alys_account  AS alys_account;


-- ============================================================================
-- SECTION 2 — ace_mod_spellbound.
-- ============================================================================

USE `ace_mod_spellbound`;


-- ----------------------------------------------------------------------------
-- 2a-prep. Ensure the Achievements catalog has the rows we reference below.
-- Self-contained INSERT IGNORE mirrors Seeds/achievements.sql ids 9001-9005;
-- harmless no-op if that seed was already applied. Without it, the web app's
-- badge-wall query (INNER JOIN AccountAchievements → Achievements) silently
-- drops every AccountAchievement that references a missing catalog row,
-- which is why a partially-applied reset can present as "no badges show up."
-- ----------------------------------------------------------------------------
INSERT IGNORE INTO `Achievements`
    (`Id`, `Name`, `EventTrigger`, `AwardDescription`, `FilterType`, `Target`, `AwardType`, `AwardValue`, `AmountRequired`)
VALUES
    (9001, 'First Critical Kill', 117, 'Reward for landing your first critical-hit killing blow.', 1, NULL, 1, 10, 1),
    (9002, 'First Blood',         117, 'Awarded the first time you defeat any creature.',          1, NULL, 7, 1,  1),
    (9003, 'Hunter',               117, 'Defeat 100 creatures.',                                    1, NULL, 8, 1,  100),
    (9004, 'Apprentice',           106, 'Reach level 5.',                                            5, '5',  2, 5,  1),
    (9005, 'Inevitable',           102, 'You died. It happens.',                                    1, NULL, 3, 5,  1);


-- ----------------------------------------------------------------------------
-- 2a. AccountAchievements: badge wall content. References Achievements rows
-- ensured above.
--   * Virin: First Blood + Apprentice awarded; Inevitable in progress.
--   * Alys:  First Blood + Apprentice awarded.
-- ----------------------------------------------------------------------------
DELETE FROM `AccountAchievements` WHERE `AccountId` IN (@virin_account, @alys_account);

INSERT INTO `AccountAchievements`
    (`AccountId`, `AchievementId`, `Progress`, `AwardedAt`, `Version`)
VALUES
    (@virin_account, 9002, 1, UTC_TIMESTAMP(6), 0),  -- Virin: First Blood
    (@virin_account, 9004, 1, UTC_TIMESTAMP(6), 0),  -- Virin: Apprentice
    (@virin_account, 9005, 0, NULL,             0),  -- Virin: Inevitable (in progress)
    (@alys_account,  9002, 1, UTC_TIMESTAMP(6), 0),  -- Alys: First Blood
    (@alys_account,  9004, 1, UTC_TIMESTAMP(6), 0);  -- Alys: Apprentice


-- ----------------------------------------------------------------------------
-- 2b. CharacterProfileSnapshots: stats + skills shown on /character/{Name}.
-- SkillsJson is a list of {Skill, State, Base, Current} matching
-- SnapshotService.SkillSnapshotEntry. State must be "Trained" or
-- "Specialized" — anything else is filtered out by the writer.
--
-- Virin: archer build — high Coord/Quick, specialized Missile Weapons.
-- Alys: void mage build — high Focus/Self, specialized Void/Life/Mana Conv.
-- ----------------------------------------------------------------------------
DELETE FROM `CharacterProfileSnapshots` WHERE `CharacterId` IN (@virin_id, @alys_id);

INSERT INTO `CharacterProfileSnapshots`
    (`CharacterId`, `AccountId`, `CharacterName`, `Level`, `TotalXp`, `UnassignedXp`,
     `Strength`, `Endurance`, `Coordination`, `Quickness`, `Focus`, `Self`,
     `HealthMax`, `StaminaMax`, `ManaMax`, `SkillsJson`, `SnapshottedAt`)
VALUES
    (@virin_id, @virin_account, @virin_name, 80, 700000000, 0,
     100, 100, 290, 290, 80, 100,
     200, 390, 120,
     '[{"Skill":"MissileWeapons","State":"Specialized","Base":390,"Current":390},{"Skill":"MissileDefense","State":"Specialized","Base":380,"Current":380},{"Skill":"MeleeDefense","State":"Trained","Base":280,"Current":280},{"Skill":"MagicDefense","State":"Trained","Base":270,"Current":270},{"Skill":"Run","State":"Trained","Base":390,"Current":390},{"Skill":"Healing","State":"Trained","Base":300,"Current":300},{"Skill":"AssessCreature","State":"Trained","Base":250,"Current":250}]',
     UTC_TIMESTAMP(6)),
    (@alys_id, @alys_account, @alys_name, 80, 720000000, 0,
     100, 100, 100, 100, 290, 290,
     150, 150, 430,
     '[{"Skill":"VoidMagic","State":"Specialized","Base":390,"Current":390},{"Skill":"LifeMagic","State":"Specialized","Base":390,"Current":390},{"Skill":"WarMagic","State":"Trained","Base":300,"Current":300},{"Skill":"ManaConversion","State":"Specialized","Base":380,"Current":380},{"Skill":"ArcaneLore","State":"Trained","Base":280,"Current":280},{"Skill":"MagicDefense","State":"Trained","Base":300,"Current":300},{"Skill":"ItemEnchantment","State":"Trained","Base":270,"Current":270},{"Skill":"CreatureEnchantment","State":"Trained","Base":270,"Current":270}]',
     UTC_TIMESTAMP(6));


-- ----------------------------------------------------------------------------
-- 2c. CharacterEquipmentSnapshots: equipped gear shown on /character/{Name}.
-- EquipmentJson is a list of EquippedItemEntry. Slot is the EquipMask enum
-- name (e.g. "MissileWeapon", "ChestArmor"). IconId 0 suppresses the icon
-- render (the markup skips <img> when IconId==0). Spell names follow the
-- dat's display-name slug rule so the spell-icon mapping just works for
-- common names (Heart Thirst V, Coordination Self V, etc.).
-- ----------------------------------------------------------------------------
DELETE FROM `CharacterEquipmentSnapshots` WHERE `CharacterId` IN (@virin_id, @alys_id);

INSERT INTO `CharacterEquipmentSnapshots`
    (`CharacterId`, `AccountId`, `CharacterName`, `EquipmentJson`, `SnapshottedAt`)
VALUES
    (@virin_id, @virin_account, @virin_name,
     '[{"Name":"Composite Bow","Slot":"MissileWeapon","IconId":0,"Workmanship":9,"Damage":35,"ArmorLevel":null,"Spellcraft":350,"MaxMana":3000,"Value":120000,"Spells":["Heart Thirst V","Coordination Self V","Quickness Self V"],"Requirements":["Specialized in Missile Weapons","Coordination 290+","Level 50+"]},{"Name":"Studded Leather Coat","Slot":"ChestArmor","IconId":0,"Workmanship":8,"Damage":null,"ArmorLevel":350,"Spellcraft":300,"MaxMana":2500,"Value":85000,"Spells":["Endurance Self V","Damage Reduction V"],"Requirements":["Coordination 250+","Level 40+"]},{"Name":"Olthoi Helm","Slot":"HeadWear","IconId":0,"Workmanship":7,"Damage":null,"ArmorLevel":250,"Spellcraft":250,"MaxMana":2000,"Value":45000,"Spells":["Coordination Self IV"],"Requirements":["Level 30+"]},{"Name":"Studded Gauntlets","Slot":"HandWear","IconId":0,"Workmanship":7,"Damage":null,"ArmorLevel":200,"Spellcraft":250,"MaxMana":2000,"Value":35000,"Spells":["Heart Thirst IV"],"Requirements":["Coordination 200+","Level 30+"]},{"Name":"Soft Boots","Slot":"FootWear","IconId":0,"Workmanship":7,"Damage":null,"ArmorLevel":200,"Spellcraft":250,"MaxMana":2000,"Value":35000,"Spells":["Run Self V"],"Requirements":["Level 30+"]}]',
     UTC_TIMESTAMP(6)),
    (@alys_id, @alys_account, @alys_name,
     '[{"Name":"Wand of Devastation","Slot":"Held","IconId":0,"Workmanship":9,"Damage":null,"ArmorLevel":null,"Spellcraft":380,"MaxMana":5000,"Value":150000,"Spells":["Spirit Drinker VI","Magic Yield V","Self Self V"],"Requirements":["Specialized in Void Magic","Self 290+","Level 60+"]},{"Name":"Robe of Stars","Slot":"ChestArmor","IconId":0,"Workmanship":8,"Damage":null,"ArmorLevel":50,"Spellcraft":380,"MaxMana":4000,"Value":90000,"Spells":["Self Self V","Focus Self V","Coordination Self V"],"Requirements":["Focus 250+","Level 50+"]},{"Name":"Pendant of Mana","Slot":"NeckWear","IconId":0,"Workmanship":7,"Damage":null,"ArmorLevel":null,"Spellcraft":300,"MaxMana":3000,"Value":60000,"Spells":["Mana Conversion V"],"Requirements":["Trained in Mana Conversion","Level 40+"]},{"Name":"Diamond Ring","Slot":"FingerWearLeft","IconId":0,"Workmanship":7,"Damage":null,"ArmorLevel":null,"Spellcraft":280,"MaxMana":2500,"Value":50000,"Spells":["Focus Self V"],"Requirements":["Level 30+"]}]',
     UTC_TIMESTAMP(6));


-- ----------------------------------------------------------------------------
-- 2d. OnlinePlayers: /who roster. Both characters parked in Holtburg.
-- Landblock raw uint = 0xA9B40000; LandblockName is what the snapshot writer
-- would have resolved via LandblockNaming.Resolve. Pre-seeded explicitly so
-- /who shows the friendly name without depending on the Zones row also being
-- present (it should be, via Seeds/zones.sql, but better to not couple).
-- ----------------------------------------------------------------------------
DELETE FROM `OnlinePlayers` WHERE `CharacterId` IN (@virin_id, @alys_id);

INSERT INTO `OnlinePlayers`
    (`CharacterId`, `AccountId`, `CharacterName`, `Level`, `Landblock`, `LandblockName`, `SeenAt`)
VALUES
    (@virin_id, @virin_account, @virin_name, 80, 0xA9B40000, 'Holtburg', UTC_TIMESTAMP(6)),
    (@alys_id,  @alys_account,  @alys_name,  80, 0xA9B40000, 'Holtburg', UTC_TIMESTAMP(6));


-- ----------------------------------------------------------------------------
-- 2e. Leaderboards: Tusker kill counts. Category 2 = KillsByCreature.
-- /leaderboards Total XP column reads from CharacterProfileSnapshots above
-- (no row needed here for that). Counts are arbitrary so Virin sorts first.
-- ----------------------------------------------------------------------------
DELETE FROM `Leaderboards` WHERE `CharacterId` IN (@virin_id, @alys_id);

INSERT INTO `Leaderboards`
    (`Category`, `Target`, `CharacterId`, `AccountId`, `CharacterName`, `Count`, `UpdatedAt`)
VALUES
    (2, 'Tusker', @virin_id, @virin_account, @virin_name, 47, UTC_TIMESTAMP(6)),
    (2, 'Tusker', @alys_id,  @alys_account,  @alys_name,  23, UTC_TIMESTAMP(6));


-- Restore the safe-update mode setting we found at the top of the script.
SET SQL_SAFE_UPDATES = @prev_safe_updates;

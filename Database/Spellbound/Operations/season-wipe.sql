-- ============================================================================
-- Spellbound season wipe.
--
-- Run by hand between seasons. ASSUMES:
--   1. The ACE server process is STOPPED. Wiping shard tables under a live
--      server will dereference cached biota objects and crash sessions.
--   2. The MySQL user has DELETE / TRUNCATE on the shard DB and the spellbound
--      DB.
--   3. You've already taken whatever backup you intend to keep of last season.
--
-- WHAT GETS WIPED:
--   - Shard DB:    every character + every biota + every character_properties_*
--                  and biota_properties_* row, plus house_permission. This
--                  destroys all player characters, their inventory, every
--                  player corpse, every dropped item, every allegiance, every
--                  house permission row.
--   - Spellbound:  AwardedCharacterAchievements (per-char idempotency rows
--                  reference now-deleted character GUIDs and would mis-skip
--                  the apply walk if a future GUID collides). Towns.Stage is
--                  reset to 0; rows themselves are kept.
--
-- WHAT SURVIVES:
--   - Auth DB:     account rows, passwords, access levels — fully untouched.
--   - Spellbound:  Achievement catalog, AccountAchievement awards (the whole
--                  point — bonuses persist across seasons), AccountVerification,
--                  WorldStateRule definitions.
--
-- AFTER RUNNING:
--   - Restart the ACE server.
--   - Players reconnect to the same accounts, see no characters, create new
--     ones. PlayerOnCreate (PlayerManager.AddOfflinePlayer postfix) fires for
--     each new char and re-applies every account achievement to the new biota.
--   - Towns.Stage starts back at 0; if your stage SQL files require a baseline
--     world.landblock_instance state, run that import first.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. Shard DB — character + biota cascade.
-- Replace `ace_shard` below if your shard DB is named differently.
-- ----------------------------------------------------------------------------
USE `ace_shard`;

SET FOREIGN_KEY_CHECKS = 0;

TRUNCATE TABLE `character_properties_contract_registry`;
TRUNCATE TABLE `character_properties_fill_comp_book`;
TRUNCATE TABLE `character_properties_friend_list`;
TRUNCATE TABLE `character_properties_quest_registry`;
TRUNCATE TABLE `character_properties_shortcut_bar`;
TRUNCATE TABLE `character_properties_spell_bar`;
TRUNCATE TABLE `character_properties_squelch`;
TRUNCATE TABLE `character_properties_title_book`;
TRUNCATE TABLE `character`;

TRUNCATE TABLE `biota_properties_allegiance`;
TRUNCATE TABLE `biota_properties_anim_part`;
TRUNCATE TABLE `biota_properties_attribute`;
TRUNCATE TABLE `biota_properties_attribute_2nd`;
TRUNCATE TABLE `biota_properties_body_part`;
TRUNCATE TABLE `biota_properties_book`;
TRUNCATE TABLE `biota_properties_book_page_data`;
TRUNCATE TABLE `biota_properties_bool`;
TRUNCATE TABLE `biota_properties_create_list`;
TRUNCATE TABLE `biota_properties_d_i_d`;
TRUNCATE TABLE `biota_properties_emote_action`;
TRUNCATE TABLE `biota_properties_emote`;
TRUNCATE TABLE `biota_properties_enchantment_registry`;
TRUNCATE TABLE `biota_properties_event_filter`;
TRUNCATE TABLE `biota_properties_float`;
TRUNCATE TABLE `biota_properties_generator`;
TRUNCATE TABLE `biota_properties_i_i_d`;
TRUNCATE TABLE `biota_properties_int`;
TRUNCATE TABLE `biota_properties_int64`;
TRUNCATE TABLE `biota_properties_palette`;
TRUNCATE TABLE `biota_properties_position`;
TRUNCATE TABLE `biota_properties_skill`;
TRUNCATE TABLE `biota_properties_spell_book`;
TRUNCATE TABLE `biota_properties_string`;
TRUNCATE TABLE `biota_properties_texture_map`;
TRUNCATE TABLE `biota`;

TRUNCATE TABLE `house_permission`;

SET FOREIGN_KEY_CHECKS = 1;

-- ----------------------------------------------------------------------------
-- 2. Spellbound DB — per-character idempotency cleared, towns reset to 0.
-- Replace `ace_custom_spellbound` if your Settings.json::MySql.Database differs.
-- ----------------------------------------------------------------------------
USE `ace_custom_spellbound`;

TRUNCATE TABLE `AwardedCharacterAchievements`;

UPDATE `Towns`
   SET `Stage`     = 0,
       `UpdatedAt` = UTC_TIMESTAMP(6),
       `Version`   = `Version` + 1;

-- ----------------------------------------------------------------------------
-- 3. Smoke checks. Run these after the wipe to confirm survivors are intact.
-- Expect: account rows untouched, AccountAchievements row count > 0 if you had
-- granted any, character row count = 0, biota row count = 0.
-- ----------------------------------------------------------------------------
SELECT 'auth.account count'         AS metric, COUNT(*) AS value FROM `ace_auth`.`account`
UNION ALL SELECT 'shard.character count',                     COUNT(*) FROM `ace_shard`.`character`
UNION ALL SELECT 'shard.biota count',                         COUNT(*) FROM `ace_shard`.`biota`
UNION ALL SELECT 'spellbound.AccountAchievements count',      COUNT(*) FROM `ace_custom_spellbound`.`AccountAchievements`
UNION ALL SELECT 'spellbound.AwardedCharacterAchievements count', COUNT(*) FROM `ace_custom_spellbound`.`AwardedCharacterAchievements`
UNION ALL SELECT 'spellbound.Towns at stage 0',               COUNT(*) FROM `ace_custom_spellbound`.`Towns` WHERE `Stage` = 0
UNION ALL SELECT 'spellbound.Towns above stage 0',            COUNT(*) FROM `ace_custom_spellbound`.`Towns` WHERE `Stage` > 0;

-- ============================================================================
-- Spellbound DB migration: add optional tele point to Zones.
-- Apply against the `ace_mod_spellbound` database (or whatever
-- Settings.json::MySql.Database is set to).
--
-- Why: the upstream `points_of_interest` table is keyed on portal weenies, so
-- /setpoi-style commands would have to write phantom portal weenies to the
-- world DB to record an admin-chosen position. Storing the tele point on the
-- Zone instead keeps the data in our DB, parallels the existing /zone
-- subcommand surface, and means a renamed zone keeps its tele point
-- automatically (no FK chase required).
--
-- Column shape mirrors the upstream Position constructor:
--   Position(uint cell, float posX, float posY, float posZ,
--            float rotX, float rotY, float rotZ, float rotW)
-- All 8 columns are nullable together — TeleCell IS NULL is the "no tele
-- point set" signal (see Zone.HasTele).
-- ============================================================================

ALTER TABLE `Zones`
    ADD COLUMN `TeleCell` INT UNSIGNED NULL AFTER `SetByAccountId`,
    ADD COLUMN `TelePosX` FLOAT NULL,
    ADD COLUMN `TelePosY` FLOAT NULL,
    ADD COLUMN `TelePosZ` FLOAT NULL,
    ADD COLUMN `TeleRotX` FLOAT NULL,
    ADD COLUMN `TeleRotY` FLOAT NULL,
    ADD COLUMN `TeleRotZ` FLOAT NULL,
    ADD COLUMN `TeleRotW` FLOAT NULL;

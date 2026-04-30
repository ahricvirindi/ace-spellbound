-- ============================================================================
-- Spellbound DB migration: enforce unique Zone.Name.
-- Apply against the `ace_custom_spellbound` database (or whatever
-- Settings.json::MySql.Database is set to).
--
-- Why: /zone tele <name> resolves a zone by its Name column. Without a unique
-- index two zones could share a name and the lookup would silently pick one.
-- The Name column inherits utf8mb4_general_ci from the table, so this index
-- is also case-insensitive — "Holtburg" and "holtburg" are the same key.
--
-- This statement WILL FAIL if existing rows already collide on Name. To clear
-- it, find the offenders and rename or delete them first:
--     SELECT `Name`, COUNT(*) FROM `Zones` GROUP BY `Name` HAVING COUNT(*) > 1;
-- ============================================================================

CREATE UNIQUE INDEX `IX_Zones_Name`
    ON `Zones` (`Name`);

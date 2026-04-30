-- ============================================================================
-- Spellbound DB migration: merge Towns + LandblockAliases into a unified Zones
-- table. Rename WorldStateRules.TownId -> ZoneId. Apply against the
-- `ace_custom_spellbound` database (or whatever Settings.json::MySql.Database
-- is set to).
--
-- Why: Towns and LandblockAliases were both (Landblock, Name) maps; only Towns
-- carried Stage state. With staging now applying to dungeons / wilderness /
-- anywhere narrative beats live (not just towns), the split is noise. Zones is
-- the unified entity — pure aliases are rows where Stage stays 0 with no
-- Content/zone-stages/<Name>/ directory; staged regions get a directory and
-- stage advances flip the Stage column the way Towns used to.
--
-- LandblockAliases rows for landblocks that aren't already in Towns are
-- migrated in (Stage = 0). Where both tables had a row for the same landblock,
-- the Town wins (because it carries Stage state); the alias name is dropped on
-- the floor — admins can re-/aliashere if they prefer the alias label.
-- ============================================================================

-- 1. Add the SetByAccountId column to Towns so absorbed alias rows can keep
--    their audit pointer.
ALTER TABLE `Towns`
    ADD COLUMN `SetByAccountId` INT UNSIGNED NULL AFTER `UpdatedAt`;

-- 2. Pull alias rows for any landblock that doesn't already have a Town. Stage
--    defaults to 0 and Version starts at 0; the existing UpdatedAt / Name /
--    SetByAccountId carry over.
INSERT IGNORE INTO `Towns` (`Name`, `Landblock`, `Stage`, `UpdatedAt`, `SetByAccountId`, `Version`)
SELECT a.`Name`, a.`Landblock`, 0, a.`UpdatedAt`, a.`SetByAccountId`, 0
  FROM `LandblockAliases` a
 WHERE NOT EXISTS (SELECT 1 FROM `Towns` t WHERE t.`Landblock` = a.`Landblock`);

-- 3. Drop the WorldStateRules FK before renaming the parent table — MySQL
--    keeps FK targets through RENAME, but the constraint NAME is now stale,
--    and we want to rebuild it under the new name anyway.
ALTER TABLE `WorldStateRules`
    DROP FOREIGN KEY `FK_WorldStateRules_Towns_TownId`;

-- 4. Rename Towns -> Zones, fix up the index name to match the new table.
ALTER TABLE `Towns` RENAME TO `Zones`;
ALTER TABLE `Zones` DROP INDEX `IX_Towns_Landblock`;
CREATE UNIQUE INDEX `IX_Zones_Landblock`
    ON `Zones` (`Landblock`);

-- 5. Rename WorldStateRules.TownId -> ZoneId, re-add the FK against Zones.
ALTER TABLE `WorldStateRules`
    CHANGE COLUMN `TownId` `ZoneId` INT NOT NULL;
ALTER TABLE `WorldStateRules`
    ADD CONSTRAINT `FK_WorldStateRules_Zones_ZoneId`
        FOREIGN KEY (`ZoneId`) REFERENCES `Zones` (`Id`)
        ON DELETE RESTRICT;

-- 6. Drop LandblockAliases — fully absorbed into Zones above.
DROP TABLE `LandblockAliases`;

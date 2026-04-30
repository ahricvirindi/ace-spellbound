-- ============================================================================
-- Spellbound DB migration: normalize WorldStateRules filter shape
-- Apply against `ace_mod_spellbound`.
--
-- WorldStateRules previously carried two typed filter columns
-- (`WeenieClassId`, `CreatureType`). After this migration they share the same
-- (FilterType, Target) shape that `Achievement` already uses, so a single
-- `RuleMatcher` can evaluate both rule kinds against an event subject.
--
-- Data migration:
--   - Rows with a non-null WeenieClassId  → FilterType=1 (WeenieId),
--                                            Target = WeenieClassId as string
--   - Rows with a non-null CreatureType   → FilterType=2 (CreatureType),
--                                            Target = CreatureType (numeric, as string)
--   - Rows with both filters set          → WeenieClassId wins (more specific);
--                                            old behavior was AND, see release note below.
--   - Rows with neither filter set        → FilterType=1, Target=NULL (wildcard)
--
-- Release note: the previous schema let one rule AND two filter columns
-- together. The new schema only carries a single (FilterType, Target) pair.
-- If any existing row truly relied on the AND semantics, split it into two
-- rows manually before running this migration.
-- ============================================================================

ALTER TABLE `WorldStateRules`
    ADD COLUMN `FilterType` INT NOT NULL DEFAULT 1 AFTER `EventTrigger`,
    ADD COLUMN `Target`     VARCHAR(200) NULL    AFTER `FilterType`;

UPDATE `WorldStateRules`
   SET `FilterType` = 1,
       `Target`     = CAST(`WeenieClassId` AS CHAR)
 WHERE `WeenieClassId` IS NOT NULL;

UPDATE `WorldStateRules`
   SET `FilterType` = 2,
       `Target`     = CAST(`CreatureType` AS CHAR)
 WHERE `WeenieClassId` IS NULL
   AND `CreatureType`  IS NOT NULL;

ALTER TABLE `WorldStateRules`
    DROP COLUMN `WeenieClassId`,
    DROP COLUMN `CreatureType`;

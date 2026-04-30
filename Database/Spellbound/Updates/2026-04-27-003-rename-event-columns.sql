-- ============================================================================
-- Spellbound DB migration: rename event-trigger / filter columns to neutral
-- (non-achievement-specific) names so both AchievementService and
-- WorldStateService can share the same vocabulary.
--
-- Apply against `ace_custom_spellbound`. Idempotent — checks for the old
-- column name before renaming, no-ops otherwise.
--
-- Renames:
--   Achievement.AchievementTrigger      -> Achievement.EventTrigger
--   Achievement.AchievementTargetType   -> Achievement.FilterType
--   WorldStateRules.Trigger             -> WorldStateRules.EventTrigger
-- ============================================================================

-- Achievement.AchievementTrigger -> EventTrigger
SET @col_exists = (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME   = 'Achievement'
      AND COLUMN_NAME  = 'AchievementTrigger'
);
SET @sql = IF(@col_exists > 0,
    'ALTER TABLE `Achievement` CHANGE COLUMN `AchievementTrigger` `EventTrigger` INT NOT NULL',
    'SELECT ''Achievement.AchievementTrigger already renamed or absent — skipping'' AS msg'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Achievement.AchievementTargetType -> FilterType
SET @col_exists = (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME   = 'Achievement'
      AND COLUMN_NAME  = 'AchievementTargetType'
);
SET @sql = IF(@col_exists > 0,
    'ALTER TABLE `Achievement` CHANGE COLUMN `AchievementTargetType` `FilterType` INT NOT NULL',
    'SELECT ''Achievement.AchievementTargetType already renamed or absent — skipping'' AS msg'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- WorldStateRules.Trigger -> EventTrigger
-- Also drop+recreate the trigger index since it referenced the old column name.
SET @col_exists = (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME   = 'WorldStateRules'
      AND COLUMN_NAME  = 'Trigger'
);
SET @sql = IF(@col_exists > 0,
    'ALTER TABLE `WorldStateRules` CHANGE COLUMN `Trigger` `EventTrigger` INT NOT NULL',
    'SELECT ''WorldStateRules.Trigger already renamed or absent — skipping'' AS msg'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Refresh the index name so it reflects the new column name (best-effort).
SET @idx_exists = (
    SELECT COUNT(*)
    FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME   = 'WorldStateRules'
      AND INDEX_NAME   = 'IX_WorldStateRules_Trigger'
);
SET @sql = IF(@idx_exists > 0,
    'ALTER TABLE `WorldStateRules` DROP INDEX `IX_WorldStateRules_Trigger`',
    'SELECT ''IX_WorldStateRules_Trigger absent — skipping drop'' AS msg'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Idempotent: only create if missing.
SET @idx_exists = (
    SELECT COUNT(*)
    FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME   = 'WorldStateRules'
      AND INDEX_NAME   = 'IX_WorldStateRules_EventTrigger'
);
SET @sql = IF(@idx_exists = 0,
    'CREATE INDEX `IX_WorldStateRules_EventTrigger` ON `WorldStateRules` (`EventTrigger`)',
    'SELECT ''IX_WorldStateRules_EventTrigger already exists — skipping'' AS msg'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

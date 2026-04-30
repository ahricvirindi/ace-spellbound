-- ============================================================================
-- Spellbound DB migration: rename Achievement.AchievemntTrigger -> AchievementTrigger
-- and renumber AchievementTriggers / AchievementAwardTypes enum values.
-- Apply against `ace_custom_spellbound`.
--
-- Background: the prior enum definitions had nearly every value set to `2`,
-- and the entity column was misspelled (`AchievemntTrigger`). The award path
-- was never wired up so no production rows are expected to exist; this script
-- still does the column rename idempotently and warns if rows are present.
-- ============================================================================

-- 1) Column rename: AchievemntTrigger -> AchievementTrigger (only if old name exists)
SET @col_exists = (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME   = 'Achievement'
      AND COLUMN_NAME  = 'AchievemntTrigger'
);

SET @sql = IF(@col_exists > 0,
    'ALTER TABLE `Achievement` CHANGE COLUMN `AchievemntTrigger` `AchievementTrigger` INT NOT NULL',
    'SELECT ''AchievemntTrigger column already renamed or absent — skipping'' AS msg'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 2) WARN if there are existing Achievement rows whose enum values were stored
--    under the broken enum (everything = 2). We don't auto-rewrite them — surface
--    a count so an operator can decide. Expected count on a fresh deploy: 0.
SELECT COUNT(*) AS legacy_achievement_rows
FROM `Achievement`;

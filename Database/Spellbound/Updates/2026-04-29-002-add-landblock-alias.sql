-- ============================================================================
-- Spellbound DB migration: landblock alias table
-- Apply against the `ace_custom_spellbound` database (or whatever
-- Settings.json::MySql.Database is set to).
--
-- Why: /who and any future location-aware UI need a human-readable name for
-- arbitrary landblocks. POI in the world DB only covers ~14 portal-target
-- landmarks; Town covers staged towns. This table is the catch-all that
-- admins fill in via /aliashere as they discover gaps. Resolution priority is
-- LandblockAlias -> Town.Name -> formatted hex.
--
-- Landblock format: matches Town.Landblock — full position id with the low
-- 16 bits set, formatted as "0xAABBFFFF". Centralized in Helpers/LandblockKey.
-- ============================================================================

CREATE TABLE IF NOT EXISTS `LandblockAliases` (
    `Id`              INT          NOT NULL AUTO_INCREMENT,
    `Landblock`       VARCHAR(50)  NOT NULL,
    `Name`            VARCHAR(200) NOT NULL,
    `SetByAccountId`  INT UNSIGNED NULL,
    `UpdatedAt`       DATETIME(6)  NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE UNIQUE INDEX `IX_LandblockAliases_Landblock`
    ON `LandblockAliases` (`Landblock`);

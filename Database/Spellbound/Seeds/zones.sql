-- ============================================================================
-- Spellbound seed: canonical Zone rows.
-- Re-runnable via INSERT IGNORE keyed on the unique Landblock index. Editing
-- an existing row's Stage / Name / etc. requires a manual UPDATE — re-running
-- this script will NOT overwrite existing rows.
--
-- Landblock format: hex string with optional 0x prefix (parsed by
-- WorldStateService.TryParseLandblockId). Use the canonical 8-char form
-- "AABBCCDD" or "0xAABBCCDD".
--
-- Each zone referenced here that has stage SQL must have a matching directory
-- at Content/zone-stages/<Name>/<stage>.sql before AdvanceZoneStage /
-- SetZoneStage will succeed for that stage. Zones with Stage = 0 and no stage
-- directory are pure aliases.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1 — Holtburg (placeholder landblock — replace with your campaign's actual
-- staging landblock before going live).
-- ----------------------------------------------------------------------------
INSERT IGNORE INTO `Zones`
    (`Id`, `Name`, `Landblock`, `Stage`, `UpdatedAt`, `Version`)
VALUES
    (1, 'Holtburg', '00000000', 0, UTC_TIMESTAMP(6), 0);

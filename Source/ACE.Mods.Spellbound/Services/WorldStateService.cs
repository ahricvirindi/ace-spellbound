using System.Data;
using System.Globalization;

using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Model;
using ACE.Server.Command.Handlers.Processors;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Services
{
    public static class WorldStateService
    {
        public static void SetZoneStage(int zoneId, int targetStage)
            => ApplyZoneStage(zoneId, targetStage, advanceOnly: false);

        public static void AdvanceZoneStage(int zoneId, int minStage)
            => ApplyZoneStage(zoneId, minStage, advanceOnly: true);

        private static void ApplyZoneStage(int zoneId, int targetStage, bool advanceOnly)
        {
            if (targetStage < 0 || targetStage > 10)
            {
                SpellboundLog.Warn($"WorldState: refusing stage {targetStage} for zone {zoneId} (outside 0..10).");
                return;
            }

            if (!SpellboundPatchBase.IsDbReady)
            {
                SpellboundLog.Warn($"WorldState: ApplyZoneStage(zone={zoneId}) dropped — Spellbound mod not yet started.");
                return;
            }

            // Step 1: short-lived lookup context. We re-fetch a tracked copy inside the
            // transaction below; this read is just for path resolution.
            Zone? zone;
            using (var lookupDb = SpellboundPatchBase.CreateDbContext())
            {
                zone = lookupDb.Zones.AsNoTracking().FirstOrDefault(x => x.Id == zoneId);
            }
            if (zone == null)
            {
                SpellboundLog.Warn($"WorldState: no Zone record for id {zoneId}.");
                return;
            }

            if (advanceOnly && zone.Stage >= targetStage)
            {
                SpellboundLog.Info($"WorldState: zone '{zone.Name}' already at stage {zone.Stage} (target {targetStage}); advance is a no-op.");
                return;
            }

            if (!TryResolveStageFile(zone, targetStage, out var stageFile))
                return;

            try
            {
                using var db = SpellboundPatchBase.CreateDbContext();
                using var tx = db.Database.BeginTransaction(IsolationLevel.Serializable);

                var trackedZone = db.Zones.FirstOrDefault(x => x.Id == zoneId);
                if (trackedZone == null)
                {
                    SpellboundLog.Info($"WorldState: zone {zoneId} disappeared between lookup and update.");
                    return;
                }

                if (advanceOnly && trackedZone.Stage >= targetStage)
                {
                    SpellboundLog.Info($"WorldState: zone '{trackedZone.Name}' raced ahead to stage {trackedZone.Stage}; advance no-op.");
                    return;
                }

                if (!TryImportStageSql(stageFile))
                {
                    SpellboundLog.Error($"WorldState: stage SQL failed for zone '{trackedZone.Name}' → {targetStage}; Zone.Stage NOT updated.");
                    return;
                }

                trackedZone.Stage = targetStage;
                trackedZone.UpdatedAt = DateTime.UtcNow;
                trackedZone.Version++;
                db.SaveChanges();
                tx.Commit();
            }
            catch (DbUpdateConcurrencyException)
            {
                SpellboundLog.Warn($"WorldState: concurrent stage update on zone {zoneId}; this advance lost the race.");
                return;
            }
            catch (Exception ex)
            {
                SpellboundLog.Error($"WorldState: ApplyZoneStage(zone={zoneId}, stage={targetStage}) failed: {ex}");
                return;
            }

            DispatchLandblockReload(zone, targetStage);
        }

        private static bool TryImportStageSql(string sqlFile)
        {
            string sqlCommands;
            try
            {
                sqlCommands = File.ReadAllText(sqlFile).Replace("\r\n", "\n");
                var lifestonedIdx = sqlCommands.IndexOf("/* Lifestoned Changelog:", StringComparison.Ordinal);
                if (lifestonedIdx != -1)
                    sqlCommands = sqlCommands.Substring(0, lifestonedIdx);
            }
            catch (Exception ex)
            {
                SpellboundLog.Error($"WorldState: cannot read stage SQL '{sqlFile}': {ex.Message}");
                return false;
            }

            using var ctx = new WorldDbContext();
            using var tx = ctx.Database.BeginTransaction();
            try
            {
                ctx.Database.ExecuteSqlRaw(sqlCommands);
                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { /* connection may already be aborted */ }
                SpellboundLog.Error($"WorldState: stage SQL '{sqlFile}' rolled back. {ex.Message}");
                return false;
            }
        }

        private static bool TryResolveStageFile(Zone zone, int stage, out string stageFile)
        {
            stageFile = string.Empty;
            var root = SpellboundPatchBase.Settings?.ZoneStagesDirectory ?? string.Empty;
            var contentFolder = Path.Combine(root, zone.Name);
            var di = new DirectoryInfo(contentFolder);
            if (!di.Exists)
            {
                SpellboundLog.Warn($"WorldState: no zone staging directory for '{zone.Name}' (expected {contentFolder}).");
                return false;
            }

            var found = di.GetFiles($"{stage}.sql").Select(x => x.FullName).FirstOrDefault();
            if (found == null)
            {
                SpellboundLog.Warn($"WorldState: no SQL file found at {contentFolder}\\{stage}.sql.");
                return false;
            }

            var rootFull = Path.GetFullPath(root);
            var foundFull = Path.GetFullPath(found);
            if (!foundFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                SpellboundLog.Error($"WorldState: stage file '{foundFull}' resolved outside of '{rootFull}'. Aborting.");
                return false;
            }

            stageFile = found;
            return true;
        }

        private static void DispatchLandblockReload(Zone zone, int newStage)
        {
            if (!TryParseLandblockId(zone.Landblock, out var landblockId))
            {
                SpellboundLog.Error(
                    $"WorldState: zone {zone.Id} has unparseable Landblock '{zone.Landblock}'. " +
                    "Stage updated in DB but landblock was not reloaded.");
                return;
            }

            var landblock = LandblockManager.GetLandblock(landblockId, loadAdjacents: false);
            if (landblock == null)
            {
                SpellboundLog.Info(
                    $"WorldState: landblock {zone.Landblock} not loaded; nothing to reload. " +
                    "Players entering it will see the new stage.");
                return;
            }

            var capturedLb = landblock;
            var zoneName = zone.Name;
            var chain = new ActionChain();

            chain.AddAction(capturedLb, () =>
            {
                var msg = new GameMessageSystemChat(
                    $"You feel the world shift. {zoneName} is changing... (stage {newStage})",
                    ChatMessageType.WorldBroadcast);
                capturedLb.EnqueueBroadcast(excludeList: null, adjacents: false, pos: null, maxRangeSq: null, msg);
            });
            chain.AddDelayForOneTick();

            chain.AddAction(capturedLb, () =>
            {
                capturedLb.DestroyAllNonPlayerObjects();
                DatabaseManager.World.ClearCachedInstancesByLandblock(capturedLb.Id.Landblock);
            });
            chain.AddDelayForOneTick();
            chain.AddAction(capturedLb, () =>
            {
                capturedLb.Init(true);
            });
            chain.EnqueueChain();
        }

        private static bool TryParseLandblockId(string hexId, out LandblockId id)
        {
            id = default;
            if (string.IsNullOrWhiteSpace(hexId))
                return false;

            var s = hexId.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);

            if (uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var raw))
            {
                id = new LandblockId(raw);
                return true;
            }
            return false;
        }
    }
}

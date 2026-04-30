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
        public static void SetTownStage(int townId, int targetStage)
            => ApplyTownStage(townId, targetStage, advanceOnly: false);

        public static void AdvanceTownStage(int townId, int minStage)
            => ApplyTownStage(townId, minStage, advanceOnly: true);

        private static void ApplyTownStage(int townId, int targetStage, bool advanceOnly)
        {
            if (targetStage < 0 || targetStage > 10)
            {
                SpellboundLog.Warn($"WorldState: refusing stage {targetStage} for town {townId} (outside 0..10).");
                return;
            }

            if (!SpellboundPatchBase.IsDbReady)
            {
                SpellboundLog.Warn($"WorldState: ApplyTownStage(town={townId}) dropped — Spellbound mod not yet started.");
                return;
            }

            // Step 1: short-lived lookup context. We re-fetch a tracked copy inside the
            // transaction below; this read is just for path resolution.
            Town? town;
            using (var lookupDb = SpellboundPatchBase.CreateDbContext())
            {
                town = lookupDb.Towns.AsNoTracking().FirstOrDefault(x => x.Id == townId);
            }
            if (town == null)
            {
                SpellboundLog.Warn($"WorldState: no Town record for id {townId}.");
                return;
            }

            if (advanceOnly && town.Stage >= targetStage)
            {
                SpellboundLog.Info($"WorldState: town '{town.Name}' already at stage {town.Stage} (target {targetStage}); advance is a no-op.");
                return;
            }

            if (!TryResolveStageFile(town, targetStage, out var stageFile))
                return;

            try
            {
                using var db = SpellboundPatchBase.CreateDbContext();
                using var tx = db.Database.BeginTransaction(IsolationLevel.Serializable);

                var trackedTown = db.Towns.FirstOrDefault(x => x.Id == townId);
                if (trackedTown == null)
                {
                    SpellboundLog.Info($"WorldState: town {townId} disappeared between lookup and update.");
                    return;
                }

                if (advanceOnly && trackedTown.Stage >= targetStage)
                {
                    SpellboundLog.Info($"WorldState: town '{trackedTown.Name}' raced ahead to stage {trackedTown.Stage}; advance no-op.");
                    return;
                }

                if (!TryImportStageSql(stageFile))
                {
                    SpellboundLog.Error($"WorldState: stage SQL failed for town '{trackedTown.Name}' → {targetStage}; Town.Stage NOT updated.");
                    return;
                }

                trackedTown.Stage = targetStage;
                trackedTown.UpdatedAt = DateTime.UtcNow;
                trackedTown.Version++;
                db.SaveChanges();
                tx.Commit();
            }
            catch (DbUpdateConcurrencyException)
            {
                SpellboundLog.Warn($"WorldState: concurrent stage update on town {townId}; this advance lost the race.");
                return;
            }
            catch (Exception ex)
            {
                SpellboundLog.Error($"WorldState: ApplyTownStage(town={townId}, stage={targetStage}) failed: {ex}");
                return;
            }

            DispatchLandblockReload(town, targetStage);
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

        private static bool TryResolveStageFile(Town town, int stage, out string stageFile)
        {
            stageFile = string.Empty;
            var root = SpellboundPatchBase.Settings?.TownStagesDirectory ?? string.Empty;
            var contentFolder = Path.Combine(root, town.Name);
            var di = new DirectoryInfo(contentFolder);
            if (!di.Exists)
            {
                SpellboundLog.Warn($"WorldState: no town staging directory for '{town.Name}' (expected {contentFolder}).");
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

        private static void DispatchLandblockReload(Town town, int newStage)
        {
            if (!TryParseLandblockId(town.Landblock, out var landblockId))
            {
                SpellboundLog.Error(
                    $"WorldState: town {town.Id} has unparseable Landblock '{town.Landblock}'. " +
                    "Stage updated in DB but landblock was not reloaded.");
                return;
            }

            var landblock = LandblockManager.GetLandblock(landblockId, loadAdjacents: false);
            if (landblock == null)
            {
                SpellboundLog.Info(
                    $"WorldState: landblock {town.Landblock} not loaded; nothing to reload. " +
                    "Players entering it will see the new stage.");
                return;
            }

            var capturedLb = landblock;
            var townName = town.Name;
            var chain = new ActionChain();

            chain.AddAction(capturedLb, () =>
            {
                var msg = new GameMessageSystemChat(
                    $"You feel the world shift. {townName} is changing... (stage {newStage})",
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

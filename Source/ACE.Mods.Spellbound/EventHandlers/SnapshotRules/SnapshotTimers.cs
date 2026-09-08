using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Services;

namespace ACE.Mods.Spellbound.EventHandlers.SnapshotRules
{
    // Periodic snapshot timers. Started from OnStartSuccess (after Settings
    // are populated and IsDbReady is true) and run until process exit.
    //
    // Cadences are configurable in Settings.json under "Snapshots" — read
    // once at boot. See Config/Settings.cs:SnapshotSettings.
    //
    // Login/logout patches keep the OnlinePlayers table tight between rebuilds.
    // Profile snapshots are also written on logout, so this timer just covers
    // the long-lived-online case.
    [HarmonyPatch]
    public sealed class SnapshotTimers : SpellboundPatchBase
    {
        public SnapshotTimers(Mod mod, string settingsName) : base(mod, settingsName) { }

        // Initial delay between mod boot and the first timer fire. Hardcoded
        // short because the goal is just "wait for the mod to fully settle"
        // — exposing this in Settings.json wasn't worth the surface area.
        // Roster fires first (snappier UX for the /who page); profile is
        // staggered so both don't both hit MySQL on the same tick at boot.
        private static readonly TimeSpan RosterStartupDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ProfileStartupDelay = TimeSpan.FromMinutes(1);

        private static int _started;
        private static Timer? _rosterTimer;
        private static Timer? _profileTimer;

        public override Task OnStartSuccess()
        {
            var t = base.OnStartSuccess();

            // Idempotent — multiple SpellboundPatchBase subclasses may share
            // a discovery sweep, but only one of them is *us*. Belt + braces
            // in case OnStartSuccess is ever invoked more than once.
            if (Interlocked.Exchange(ref _started, 1) == 1) return t;

            var snap = Settings?.Snapshots ?? new Config.SnapshotSettings();
            var rosterPeriod = TimeSpan.FromSeconds(Math.Max(10, snap.RosterIntervalSeconds));
            var profilePeriod = TimeSpan.FromSeconds(Math.Max(10, snap.ProfileIntervalSeconds));

            _rosterTimer = new Timer(_ => RebuildRoster(),
                state: null,
                dueTime: RosterStartupDelay,
                period: rosterPeriod);

            _profileTimer = new Timer(_ => SnapshotAllProfiles(),
                state: null,
                dueTime: ProfileStartupDelay,
                period: profilePeriod);

            SpellboundLog.Info(
                $"Snapshot timers started: roster every {rosterPeriod.TotalSeconds:0}s, profiles every {profilePeriod.TotalSeconds:0}s.");
            return t;
        }

        private static void RebuildRoster()
        {
            var online = PlayerManager.GetAllOnline();
            RunDbWork(db => SnapshotService.RebuildRoster(db, online));
        }

        private static void SnapshotAllProfiles()
        {
            var online = PlayerManager.GetAllOnline();
            // Capture profile + equipment rows on this thread (PlayerManager-
            // thread or timer-thread; safe to read attributes / EquippedObjects
            // off live Player objects). Then push the writes through RunDbWork
            // as a single batch. Each capture is wrapped individually so a
            // single bad player doesn't lose snapshots for the rest.
            var profileRows = new List<Model.CharacterProfileSnapshot>(online.Count);
            var equipmentRows = new List<Model.CharacterEquipmentSnapshot>(online.Count);
            foreach (var p in online)
            {
                if (p?.Account == null) continue;
                try
                {
                    profileRows.Add(SnapshotService.BuildProfileRow(p));
                }
                catch (Exception ex)
                {
                    SpellboundLog.Warn($"Skipped profile snapshot for {p.Name}: {ex.Message}");
                }
                try
                {
                    equipmentRows.Add(SnapshotService.BuildEquipmentRow(p));
                }
                catch (Exception ex)
                {
                    SpellboundLog.Warn($"Skipped equipment snapshot for {p.Name}: {ex.Message}");
                }
            }

            if (profileRows.Count == 0 && equipmentRows.Count == 0) return;

            RunDbWork(db =>
            {
                foreach (var row in profileRows)
                    SnapshotService.WriteProfile(db, row);
                foreach (var row in equipmentRows)
                    SnapshotService.WriteEquipment(db, row);
            });
        }
    }
}

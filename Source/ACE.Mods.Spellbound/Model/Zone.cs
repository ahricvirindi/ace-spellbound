using ACE.Mods.Spellbound.Model.Base;
using System.ComponentModel.DataAnnotations;

namespace ACE.Mods.Spellbound.Model
{
    /// <summary>
    /// A named landblock that may also carry seasonal-stage state. Replaces the
    /// older Town + LandblockAlias split — both were (Landblock, Name) maps and
    /// only Town had Stage/Version. Rows whose Stage stays at 0 with no matching
    /// Content/zone-stages/&lt;Name&gt;/ directory are pure aliases used by /who and
    /// LandblockNaming.Resolve. Rows with a stage directory get advanced through
    /// stages by WorldStateService (event-driven or via /zone stage).
    ///
    /// Optional tele point: TeleCell + the 7 float columns mirror the upstream
    /// Position constructor (cell, posX/Y/Z, rotX/Y/Z/W) so /zone tele can
    /// teleport an admin straight to a recorded landing spot. HasTele is the
    /// "is the tele point set?" predicate; all 8 fields are nullable together.
    /// </summary>
    public class Zone : BaseNamedModel
    {
        public string Landblock { get; set; } = string.Empty;
        public int Stage { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Account that last set the Name (via /zone name). Null for seed rows /
        // SQL-imported entries. Audit-only — not used in any decision logic.
        public uint? SetByAccountId { get; set; }

        public uint? TeleCell { get; set; }
        public float? TelePosX { get; set; }
        public float? TelePosY { get; set; }
        public float? TelePosZ { get; set; }
        public float? TeleRotX { get; set; }
        public float? TeleRotY { get; set; }
        public float? TeleRotZ { get; set; }
        public float? TeleRotW { get; set; }

        public bool HasTele => TeleCell.HasValue;

        [ConcurrencyCheck]
        public int Version { get; set; }
    }
}

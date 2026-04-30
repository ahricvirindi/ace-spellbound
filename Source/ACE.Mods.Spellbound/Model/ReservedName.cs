using ACE.Mods.Spellbound.Model.Base;

namespace ACE.Mods.Spellbound.Model
{
    /// <summary>
    /// A character name reserved to a specific account across season wipes.
    /// Populated by the manual season-wipe procedure (a snapshot of every
    /// (character.name, character.account_Id) pair just before TRUNCATE
    /// character) so the original account keeps its handles after the wipe
    /// destroys the row in the shard DB. Reservations are permanent — there
    /// is no expiration. Enforcement happens via a Harmony prefix on
    /// CharacterHandler.CharacterCreateEx.
    /// </summary>
    public class ReservedName : BaseKeyedModel
    {
        public string Name { get; set; } = string.Empty;
        public uint AccountId { get; set; }
        public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
    }
}

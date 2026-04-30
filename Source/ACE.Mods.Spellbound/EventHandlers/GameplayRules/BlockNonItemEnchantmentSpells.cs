using ACE.Mods.Spellbound.Model.Enumerations;
using ACE.Mods.Spellbound.Model.Events;
using ACE.Mods.Spellbound.Model.Events.Payloads;

namespace ACE.Mods.Spellbound.EventHandlers.GameplayRules
{
    public static class BlockNonItemEnchantmentSpells
    {
        [SpellboundEvent(SpellboundEventTrigger.Player_PreCast)]
        public static void Apply(PlayerPreCastEvent e)
        {
            if (e.IsWeaponSpell) return;
            if (e.Caster.IsAdmin) return;
            if (e.Spell.School == MagicSchool.ItemEnchantment) return;

            e.CancelCast = true;
            e.Caster.Tell(
                "The energies from your casting nearly coalesce and then fizzle to nothing.  Something unnatural blocks incantations of that type.",
                ChatMessageType.Magic);
        }
    }
}

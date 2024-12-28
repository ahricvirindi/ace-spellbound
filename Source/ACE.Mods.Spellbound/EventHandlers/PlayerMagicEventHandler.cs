using ACE.Mods.Spellbound.Base;
using ACE.Server.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ACE.Server.WorldObjects.Player;

namespace ACE.Mods.Spellbound.EventHandlers
{
    [HarmonyPatch]
    public class PlayerMagicEventHandler : SpellboundPatchBase
    {
        public PlayerMagicEventHandler(Mod mod, string settingsName) : base(mod, settingsName)
        {

        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.GetCastingPreCheckStatus), [typeof(Server.Entity.Spell), typeof(uint), typeof(bool)])]
        public static void OnSpellFizzleCheck(Server.Entity.Spell spell, uint magicSkill, bool isWeaponSpell, ref Player __instance, ref CastingPreCheckStatus __result) {
            ModManager.Log("Spellbound.OnSpellFizzleCheck()");

            if (isWeaponSpell || __instance.IsAdmin)
            {
                return;
            }

            if (spell.School != MagicSchool.ItemEnchantment)
            {
                __instance.Session.Network.EnqueueSend(new GameMessageSystemChat($"The energies from your casting nearly coalesce and then fizzle to nothing.  Something unnatural blocks incantations of that type.", ChatMessageType.Magic));
                __result = CastingPreCheckStatus.CastFailed;
            }
        }
    }
}

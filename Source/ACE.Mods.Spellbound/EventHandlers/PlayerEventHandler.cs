using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Spellbound.EventHandlers
{
    public class PlayerEventHandler
    {
        public static void OnCreate()
        {
            ModManager.Log("Spellbound.OnCreate()");
        }

        public void OnLogin()
        {
            ModManager.Log("Spellbound.OnLogin()");
        }

        public void OnDeath()
        {
            ModManager.Log("Spellbound.OnDeath()");
        }

        public void OnPortalEntry()
        {
            ModManager.Log("Spellbound.OnPortalEntry()");
        }

        public void OnPortalExit()
        {
            ModManager.Log("Spellbound.OnPortalExit()");
        }

        public void OnLevel()
        {
            ModManager.Log("Spellbound.OnLevel()");
        }

        public void OnDamageTaken()
        {
            ModManager.Log("Spellbound.OnDamageTaken()");
        }

        public void OnDamageGiven()
        {
            ModManager.Log("Spellbound.OnDamageTaken()");
        }

        public void OnCritDamageTaken()
        {
            ModManager.Log("Spellbound.OnCritDamageTaken()");
        }

        public void OnCritDamageGiven()
        {
            ModManager.Log("Spellbound.OnCritDamageTaken()");
        }

        public void OnMeleeEvade()
        {
            ModManager.Log("Spellbound.OnMeleeEvade()");
        }

        public void OnMagicResist()
        {
            ModManager.Log("Spellbound.OnMagicResist()");
        }

        public void OnLifeRegen()
        {
            ModManager.Log("Spellbound.OnLifeRegen()");
        }

        public void OnManaRegen()
        {
            ModManager.Log("Spellbound.OnManaRegen()");
        }

        public void OnStaminaRegen()
        {
            ModManager.Log("Spellbound.OnStaminaRegen()");
        }

        public void OnKill()
        {
            ModManager.Log("Spellbound.OnKill()");
        }

        public void OnDamageGivenCalculating()
        {
            ModManager.Log("Spellbound.OnKill()");
        }

        public void OnDamageTakenCalculating()
        {
            ModManager.Log("Spellbound.OnKill()");
        }

        public void OnExperienceAwarding()
        {
            ModManager.Log("Spellbound.OnKill()");
        }

        public void OnLuminanceAwarding()
        {
            ModManager.Log("Spellbound.OnKill()");
        }
    }
}

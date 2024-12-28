using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Spellbound.Model.Enumerations
{
    public enum AchievementTriggers
    {
        Account_OnCreate = 1,
        Account_OnLogin = 2,
        Player_OnCreate = 2,
        Player_OnLogin = 2,
        Player_OnDeath = 2,
        Player_OnPKDeath = 2,
        Player_OnPortaEntry = 2,
        Player_OnPortaExit = 2,
        Player_OnLevel = 2,
        Player_OnDamageTaken = 2,
        Player_OnDamageGiven = 2,
        Player_OnCritDamageTaken = 2,
        Player_OnCritDamageGiven = 2,
        Player_OnMeleeEvade = 2,
        Player_OnMagicResist = 2,
        Player_OnLifeRegen = 2,
        Player_OnManaRegen = 2,
        Player_OnStaminaRegen = 2,
        Player_OnKill = 2,
        Player_OnPKKill = 2,
        Player_OnExperienceAwarded = 2,
        Player_OnLuminanceAwarded = 2,
        Quest_OnStart = 2,
        Quest_OnIncrement = 2,
        Quest_OnComplete = 2,
        Player_OnLoot = 26,
        Player_OnMissileEvade = 2
    }
}

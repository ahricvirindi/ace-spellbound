namespace ACE.Mods.Spellbound.Model.Enumerations
{
    public enum SpellboundEventTrigger
    {
        Account_OnCreate = 1,
        Account_OnLogin = 2,

        Player_OnCreate = 100,
        Player_OnLogin = 101,
        Player_OnDeath = 102,
        Player_OnPKDeath = 103,
        Player_OnPortalEntry = 104,
        Player_OnPortalExit = 105,
        Player_OnLevel = 106,
        Player_OnDamageTaken = 107,
        Player_OnDamageGiven = 108,
        Player_OnCritDamageTaken = 109,
        Player_OnCritDamageGiven = 110,
        Player_OnMeleeEvade = 111,
        Player_OnMissileEvade = 112,
        Player_OnMagicResist = 113,
        Player_OnLifeRegen = 114,
        Player_OnManaRegen = 115,
        Player_OnStaminaRegen = 116,
        Player_OnKill = 117,
        Player_OnPKKill = 118,
        Player_OnExperienceAwarded = 119,
        Player_OnLuminanceAwarded = 120,
        Player_FromCorpse_OnLoot = 121,
        Player_FromContainer_OnLoot = 122,
        Player_PreCast = 123,
        Player_OnQuestStart = 124,
        Player_OnQuestIncrement = 125,
        Player_OnQuestComplete = 126,
        Player_OnItemUse = 127,
        Player_OnAchievement = 128,

        Creature_OnDeath = 300
    }
}

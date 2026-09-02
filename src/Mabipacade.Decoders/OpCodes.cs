namespace Mabipacade.Decoders;

public enum OpCodes : uint
{
    CombatAction          = 0x00007924,
    CombatActionEnd       = 0x00007925,
    CombatActionPack      = 0x00007926,
    PlayerSkillPrepareStart    = 0x00006984,
    PlayerSkillPrepareReady    = 0x00006985,
    PlayerSkillPostCastAck1    = 0x00006988,
    PlayerSkillPostCastAck2    = 0x00006989,
    PlayerSkillStop            = 0x0000698B,
    PlayerSkillPrepareProgress = 0x00006993,
    EntityAppear        = 0x0000520C,
    EntityDisappear     = 0x0000520D,
    EntitiesAppear      = 0x00005334,
    EntitiesDisappear   = 0x00005335,
    IsNowDead           = 0x000053FC,
    StatUpdatePrivate   = 0x00007530,
    StatUpdatePublic    = 0x00007532,
    EntityRelated       = 0x00007534,
    CharacterConditionUpdate = 0x0000A028,
    Chat                = 0x0000526C,
    Effect              = 0x00009091,
    EffectDelayed       = 0x00009095,
    SharpMind           = 0x0000A41E,
    PartyWindowUpdate   = 0x0000A43C,
    EquipmentChanged    = 0x000059E6,
    MissionRoomActors   = 0x000186A6
}

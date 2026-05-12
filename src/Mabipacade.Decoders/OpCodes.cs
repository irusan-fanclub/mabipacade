namespace Mabipacade.Decoders;

public enum OpCodes : ushort
{
    CombatAction          = 0x7924,
    CombatActionEnd       = 0x7925,
    CombatActionPack      = 0x7926,
    PlayerSkillPrepareStart    = 0x6984,
    PlayerSkillPrepareReady    = 0x6985,
    PlayerSkillPostCastAck1    = 0x6988,
    PlayerSkillPostCastAck2    = 0x6989,
    PlayerSkillStop            = 0x698B,
    PlayerSkillPrepareProgress = 0x6993,
    EntityAppear        = 0x520C,
    EntityDisappear     = 0x520D,
    EntitiesAppear      = 0x5334,
    EntitiesDisappear   = 0x5335,
    IsNowDead           = 0x53FC,
    StatUpdatePrivate   = 0x7530,
    StatUpdatePublic    = 0x7532,
    EntityRelated       = 0x7534,
    ConditionUpdate2    = 0xA028,
    Chat                = 0x526C,
    Effect              = 0x9091,
    EffectDelayed       = 0x9095,
    SharpMind           = 0xA41E,
    PartyWindowUpdate   = 0xA43C,
    EquipmentChanged    = 0x59E6
}

using Mabipacade.Core.Pipeline;
using Mabipacade.Decoders.Combat;
using Mabipacade.Decoders.Entity;
using Mabipacade.Decoders.Misc;
using Mabipacade.Decoders.Movement;
using Mabipacade.Decoders.Pet;
using Mabipacade.Decoders.Prop;
using Mabipacade.Decoders.Skills;
using Mabipacade.Decoders.Stats;
using Mabipacade.Decoders.Ui;
using Mabipacade.Decoders.World;

namespace Mabipacade.Decoders;

public static class DefaultDecoders
{
    public static void RegisterAll(DecoderRegistry registry)
    {
        // Combat
        registry.Register(new CombatActionDecoder());
        registry.Register(new CombatActionEndDecoder());
        registry.Register(new CombatActionPackDecoder());
        registry.Register(new SetCombatTargetDecoder());
        registry.Register(new SetFinisherDecoder());
        registry.Register(new SetFinisher2Decoder());
        registry.Register(new CombatTargetUpdateDecoder());
        registry.Register(new CombatPrepareDecoder());
        registry.Register(new CombatSetAimRDecoder());
        registry.Register(new CombatUsedSkillDecoder());
        registry.Register(new CombatAttackRDecoder());
        registry.Register(new RemoveDeathScreenDecoder());

        // Skills
        registry.Register(new PlayerSkillPrepareStartDecoder());
        registry.Register(new PlayerSkillPrepareReadyDecoder());
        registry.Register(new PlayerSkillPostCastAck1Decoder());
        registry.Register(new PlayerSkillPostCastAck2Decoder());
        registry.Register(new PlayerSkillStopDecoder());
        registry.Register(new PlayerSkillPrepareProgressDecoder());

        // Entity
        registry.Register(new EntityAppearDecoder());
        registry.Register(new EntityDisappearDecoder());
        registry.Register(new EntitiesAppearDecoder());
        registry.Register(new EntitiesDisappearDecoder());
        registry.Register(new IsNowDeadDecoder());

        // Stats / body
        registry.Register(new StatUpdatePrivateDecoder());
        registry.Register(new StatUpdatePublicDecoder());
        registry.Register(new StatUpdateUnk7533Decoder());
        registry.Register(new EntityRelatedDecoder());
        registry.Register(new CreatureBodyUpdateDecoder());
        registry.Register(new ConditionUpdate2Decoder());

        // Items / equipment
        registry.Register(new EquipmentChangedDecoder());
        registry.Register(new UnequipmentDecoder());
        registry.Register(new EquipUnk59E0Decoder());
        registry.Register(new EquipUnk59E1Decoder());
        registry.Register(new ItemUpdateDecoder());
        registry.Register(new ItemDurabilityUpdateDecoder());
        registry.Register(new ItemUnk5BCFDecoder());

        // Misc
        registry.Register(new ChatDecoder());
        registry.Register(new EffectDecoder());
        registry.Register(new EffectDelayedDecoder());
        registry.Register(new SharpMindDecoder());
        registry.Register(new PartyWindowUpdateDecoder());

        // Pet
        registry.Register(new PetRegisterDecoder());
        registry.Register(new PetUnregisterDecoder());
        registry.Register(new SummonPetRDecoder());
        registry.Register(new TelePetRDecoder());
        registry.Register(new GetPetAiRDecoder());
        registry.Register(new SkillInfoDecoder());
        registry.Register(new PetSkillListDecoder());
        registry.Register(new PetStat55Decoder());
        registry.Register(new PetEquipSlotInitDecoder());
        registry.Register(new PetSystemFieldDecoder());
        registry.Register(new PetSummonAckDecoder());
        registry.Register(new PetSyncAckDecoder());
        registry.Register(new PetFarewellDecoder());
        registry.Register(new PetPrpDecoder());
        registry.Register(new PetPrpDetailDecoder());
        registry.Register(new PetKeepingTickDecoder());
        registry.Register(new PetUnk9098Decoder());
        registry.Register(new PetUnk9097Decoder());
        // 32-bit "category" opcodes (upper bytes set) — buff KV + pet resource.
        registry.Register(new BuffStateUpdateDecoder());
        registry.Register(new PetCapacityDecoder());
        registry.Register(new PetResourceSyncDecoder());

        // Movement (full 32-bit opcodes)
        registry.Register(new WalkingDecoder());
        registry.Register(new RunningDecoder());
        registry.Register(new PetMovementSyncDecoder());

        // World / region / motion / stance
        registry.Register(new NoticeDecoder());
        registry.Register(new ChangeStanceDecoder());
        registry.Register(new ChangeStanceResDecoder());
        registry.Register(new UseMotionDecoder());
        registry.Register(new MotionCancelDecoder());
        registry.Register(new CharacterLockDecoder());
        registry.Register(new CharacterUnlockDecoder());
        registry.Register(new PointsUpdateDecoder());
        registry.Register(new DisappearDecoder());
        registry.Register(new EnterRegionRequestRDecoder());
        registry.Register(new EnterDynamicRegionDecoder());
        registry.Register(new RemoveDynamicRegionDecoder());

        // Prop
        registry.Register(new PropAppearsDecoder());
        registry.Register(new PropDisappearsDecoder());
        registry.Register(new PropUpdateDecoder());

        // UI / URL / guild
        registry.Register(new UrlUpdateChronicleDecoder());
        registry.Register(new UrlUpdateAdvertiseDecoder());
        registry.Register(new UrlUpdateGuestbookDecoder());
        registry.Register(new UrlUpdatePvpDecoder());
        registry.Register(new UrlUpdateDungeonBoardDecoder());
        registry.Register(new UrlUpdateReserved1Decoder());
        registry.Register(new UrlUpdateReserved2Decoder());
        registry.Register(new GuildBattlegroundStateDecoder());
        registry.Register(new RegionStatDecoder());
    }
}

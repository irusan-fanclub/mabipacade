using Mabipacade.Core.Pipeline;
using Mabipacade.Decoders.Combat;
using Mabipacade.Decoders.Entity;
using Mabipacade.Decoders.Misc;
using Mabipacade.Decoders.Skills;
using Mabipacade.Decoders.Stats;

namespace Mabipacade.Decoders;

public static class DefaultDecoders
{
    public static void RegisterAll(DecoderRegistry registry)
    {
        registry.Register(new CombatActionDecoder());
        registry.Register(new CombatActionEndDecoder());
        registry.Register(new CombatActionPackDecoder());

        registry.Register(new PlayerSkillPrepareStartDecoder());
        registry.Register(new PlayerSkillPrepareReadyDecoder());
        registry.Register(new PlayerSkillPostCastAck1Decoder());
        registry.Register(new PlayerSkillPostCastAck2Decoder());
        registry.Register(new PlayerSkillStopDecoder());
        registry.Register(new PlayerSkillPrepareProgressDecoder());

        registry.Register(new EntityAppearDecoder());
        registry.Register(new EntityDisappearDecoder());
        registry.Register(new EntitiesAppearDecoder());
        registry.Register(new EntitiesDisappearDecoder());
        registry.Register(new IsNowDeadDecoder());

        registry.Register(new StatUpdatePrivateDecoder());
        registry.Register(new StatUpdatePublicDecoder());
        registry.Register(new EntityRelatedDecoder());
        registry.Register(new ConditionUpdate2Decoder());

        registry.Register(new ChatDecoder());
        registry.Register(new EffectDecoder());
        registry.Register(new EffectDelayedDecoder());
        registry.Register(new SharpMindDecoder());
        registry.Register(new PartyWindowUpdateDecoder());
        registry.Register(new EquipmentChangedDecoder());
    }
}

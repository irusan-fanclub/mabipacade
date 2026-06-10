using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Ui;

/// <summary>
/// 0xA90E GuildBattlegroundState — { String name, Byte, String state, Byte }.
/// e.g. ("Senmag_Guild_BattleGround", 1, "closed", 1). Not in Aura.
/// </summary>
public sealed record GuildBattlegroundState(string Name, byte Flag1, string State, byte Flag2);

public sealed class GuildBattlegroundStateDecoder : IPacketDecoder
{
    public uint Op => 0x0000A90E;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;

        var name = e.Count >= 1 && e[0].Type == MessageElemType.String ? e[0].AsString() : "";
        var flag1 = e.Count >= 2 && e[1].Type == MessageElemType.Byte ? e[1].AsByte() : (byte)0;
        var state = e.Count >= 3 && e[2].Type == MessageElemType.String ? e[2].AsString() : "";
        var flag2 = e.Count >= 4 && e[3].Type == MessageElemType.Byte ? e[3].AsByte() : (byte)0;

        return new GuildBattlegroundState(name, flag1, state, flag2);
    }
}

using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Ui;

namespace Mabipacade.Decoders.Tests.Ui;

public class UrlUpdateDecodersTests
{
    private static DecoderInput Strings(uint op, params string[] values)
    {
        var elems = new MessageElem[values.Length];
        for (var i = 0; i < values.Length; i++)
            elems[i] = MessageElem.String(values[i]);
        return new DecoderInput(DateTime.UtcNow, Direction.Inbound, op, 0UL, elems);
    }

    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x65A2, new UrlUpdateChronicleDecoder().Op);
        Assert.Equal((uint)0x65A3, new UrlUpdateAdvertiseDecoder().Op);
        Assert.Equal((uint)0x65A4, new UrlUpdateGuestbookDecoder().Op);
        Assert.Equal((uint)0x65A5, new UrlUpdatePvpDecoder().Op);
        Assert.Equal((uint)0x65A6, new UrlUpdateDungeonBoardDecoder().Op);
        Assert.Equal((uint)0x6652, new UrlUpdateReserved1Decoder().Op);
        Assert.Equal((uint)0x6653, new UrlUpdateReserved2Decoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var input = Strings(0x65A2,
            "http://tw-chronicle-mabinogi.beanfun.com/chronicle/Chronicle.aspx",
            "http://tw-chronicle-mabinogi.beanfun.com/chronicle/chronicleoption.aspx",
            "http://tw-chronicle-mabinogi.beanfun.com/chronicle/ranking.aspx");

        var result = (UrlUpdateChronicle)new UrlUpdateChronicleDecoder().Decode(input);

        Assert.Equal(3, result.Urls.Count);
        Assert.Equal("http://tw-chronicle-mabinogi.beanfun.com/chronicle/Chronicle.aspx", result.Urls[0]);
    }

    [Fact]
    public void Decodes_DistinctTypePerOpcode()
    {
        Assert.IsType<UrlUpdateAdvertise>(new UrlUpdateAdvertiseDecoder().Decode(Strings(0x65A3, "a")));
        Assert.IsType<UrlUpdateGuestbook>(new UrlUpdateGuestbookDecoder().Decode(Strings(0x65A4, "a")));
        Assert.IsType<UrlUpdatePvp>(new UrlUpdatePvpDecoder().Decode(Strings(0x65A5, "a")));
        Assert.IsType<UrlUpdateDungeonBoard>(new UrlUpdateDungeonBoardDecoder().Decode(Strings(0x65A6, "a")));
    }

    [Fact]
    public void Decodes_ReservedSlots_TwoEmptyStrings()
    {
        var r1 = (UrlUpdateReserved1)new UrlUpdateReserved1Decoder().Decode(Strings(0x6652, "", ""));
        Assert.Equal(new[] { "", "" }, r1.Urls);

        var r2 = (UrlUpdateReserved2)new UrlUpdateReserved2Decoder().Decode(Strings(0x6653, "", ""));
        Assert.Equal(new[] { "", "" }, r2.Urls);
    }
}

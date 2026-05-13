using Mabipacade.DebugUi.Models;

namespace Mabipacade.DebugUi.Tests.Models;

public class HexDumpLineTests
{
    [Fact]
    public void From_SplitsBytesIntoSixteenPerLine()
    {
        var bytes = new byte[20];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i + 0x40);
        var lines = HexDumpLine.From(bytes).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(0, lines[0].Offset);
        Assert.Equal(16, lines[1].Offset);
        Assert.Contains("40 41 42", lines[0].HexBytes);
        Assert.Contains("@AB", lines[0].Ascii);
    }

    [Fact]
    public void From_NonPrintable_RendersAsDot()
    {
        var lines = HexDumpLine.From(new byte[] { 0x00, 0x1F, 0x7F, 0x80 }).ToList();
        Assert.Single(lines);
        Assert.Equal("....", lines[0].Ascii);
    }
}

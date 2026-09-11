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

    [Fact]
    public void From_NoHighlight_PutsEverythingInPre()
    {
        var lines = HexDumpLine.From(new byte[] { 0x40, 0x41, 0x42 }).ToList();
        Assert.Equal("40 41 42", lines[0].HexPre);
        Assert.False(lines[0].HasHit);
        Assert.Equal("", lines[0].HexPost);
    }

    [Fact]
    public void From_Highlight_SplitsTheRowAroundTheRange()
    {
        var bytes = Enumerable.Range(0, 8).Select(i => (byte)i).ToArray();
        var line = HexDumpLine.From(bytes, highlightOffset: 2, highlightLength: 3).Single();

        Assert.Equal("00 01 ", line.HexPre);
        Assert.Equal("02 03 04", line.HexHit);
        Assert.Equal(" 05 06 07", line.HexPost);
        Assert.Equal(line.HexBytes, line.HexPre + line.HexHit + line.HexPost);
    }

    [Fact]
    public void From_Highlight_SpanningLines_MarksEachTouchedRow()
    {
        var bytes = Enumerable.Range(0, 40).Select(i => (byte)i).ToArray();
        // Bytes 14..21 cross the boundary between row 0 and row 1.
        var lines = HexDumpLine.From(bytes, highlightOffset: 14, highlightLength: 8).ToList();

        Assert.Equal(3, lines.Count);
        Assert.Equal("0E 0F", lines[0].HexHit);                  // tail of row 0
        Assert.Equal("10 11 12 13 14 15", lines[1].HexHit);      // head of row 1
        Assert.Equal("", lines[1].HexPre);
        Assert.False(lines[2].HasHit);                           // row 2 untouched
        Assert.Equal(lines[2].HexBytes, lines[2].HexPre);
    }
}

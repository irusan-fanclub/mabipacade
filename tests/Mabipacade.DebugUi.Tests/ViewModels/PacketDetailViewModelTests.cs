using Mabipacade.Core.Model;
using Mabipacade.DebugUi.ViewModels;

namespace Mabipacade.DebugUi.Tests.ViewModels;

public class PacketDetailViewModelTests
{
    [Fact]
    public void SelectedRow_Null_HasEmptyDecoded()
    {
        var vm = new PacketDetailViewModel { SelectedRow = null };
        Assert.Equal("no packet selected", vm.DecodedJson);
        Assert.Empty(vm.HexLines);
        Assert.Empty(vm.ElemNodes);
    }

    [Fact]
    public void SelectedRow_WithDecoded_RendersJson()
    {
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x00006984, 1UL,
            new[] { MessageElem.Short(42) },
            Decoded: new { skillId = 59000 });
        var vm = new PacketDetailViewModel { SelectedRow = new PacketRowVm(packet) };
        Assert.Contains("skillId", vm.DecodedJson);
        Assert.Contains("59000", vm.DecodedJson);
        Assert.Single(vm.ElemNodes);
    }

    [Fact]
    public void SelectedRow_WithDecoded_BuildsACollapsibleTree()
    {
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x00005209, 1UL,
            Array.Empty<MessageElem>(),
            Decoded: new { name = "蘑菇嫩煎雞", items = new[] { new { id = 1 }, new { id = 2 } } });
        var vm = new PacketDetailViewModel { SelectedRow = new PacketRowVm(packet) };

        var root = Assert.Single(vm.DecodedNodes);
        Assert.True(root.IsExpanded);   // top level open, everything below closed

        var items = root.Children.Single(n => n.Name == "items");
        Assert.Equal("items  [2]", items.Summary);
        Assert.False(items.ChildrenMaterialised);
    }

    [Fact]
    public void SelectedRow_NoDecoded_HasNoTree()
    {
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x0000FFFF, 0UL,
            Array.Empty<MessageElem>(), null);
        var vm = new PacketDetailViewModel { SelectedRow = new PacketRowVm(packet) };
        Assert.Empty(vm.DecodedNodes);
    }

    [Fact]
    public void BuildPacketJson_EmitsTheFullEnvelope_WithTextVerbatim()
    {
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x0000526C, 1UL,
            new[] { MessageElem.String("雷楠") },
            Decoded: new { sender = "", message = "雷楠" });
        var vm = new PacketDetailViewModel { SelectedRow = new PacketRowVm(packet) };

        var json = vm.BuildPacketJson()!;
        Assert.Contains("\"kind\": \"packet\"", json);   // indented for reading
        Assert.Contains("雷楠", json);
        Assert.DoesNotContain("\\u", json);
        Assert.Equal("packet-0x0000526C.json", vm.SuggestedExportFileName);
    }

    [Fact]
    public void BuildPacketJson_IsNull_WithoutASelection()
    {
        Assert.Null(new PacketDetailViewModel { SelectedRow = null }.BuildPacketJson());
    }

    [Fact]
    public void SelectedRow_NoDecoded_ReportsNoDecoder()
    {
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x0000FFFF, 0UL,
            Array.Empty<MessageElem>(), null);
        var vm = new PacketDetailViewModel { SelectedRow = new PacketRowVm(packet) };
        Assert.Contains("no decoder registered", vm.DecodedJson);
    }

    // Body with the elem-stream prefix and two elems:
    // Byte(42) at offset 3 (2 bytes), Short(1) at offset 5 (3 bytes).
    private static readonly byte[] TwoElemBody =
        { 0x00, 0x02, 0x00, 0x01, 0x2A, 0x02, 0x00, 0x01 };

    private static PacketRowVm RowWithBody()
    {
        Mabipacade.Core.Pipeline.MessageElemReader.TryRead(TwoElemBody, out var elems);
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x00006984, 1UL,
            elems, Decoded: null) { Body = TwoElemBody };
        return new PacketRowVm(packet);
    }

    [Fact]
    public void SelectedRow_WithBody_GivesElemsTheirByteOffsets()
    {
        var vm = new PacketDetailViewModel { SelectedRow = RowWithBody() };

        Assert.Equal(2, vm.ElemNodes.Count);
        Assert.Equal(3, vm.ElemNodes[0].Offset);
        Assert.Equal(2, vm.ElemNodes[0].Length);
        Assert.Equal(5, vm.ElemNodes[1].Offset);
        Assert.Equal(3, vm.ElemNodes[1].Length);
        Assert.Equal("0005", vm.ElemNodes[1].OffsetLabel);
    }

    [Fact]
    public void SelectedRow_WithoutBody_LeavesElemOffsetsEmpty()
    {
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x00006984, 1UL,
            new[] { MessageElem.Short(42) }, Decoded: null);
        var vm = new PacketDetailViewModel { SelectedRow = new PacketRowVm(packet) };

        Assert.Null(vm.ElemNodes[0].Offset);
        Assert.Equal("", vm.ElemNodes[0].OffsetLabel);
    }

    [Fact]
    public void HighlightHexRange_MarksTheLines_AndReportsTheFirst()
    {
        var vm = new PacketDetailViewModel { SelectedRow = RowWithBody() };

        int first = vm.HighlightHexRange(5, 3);

        Assert.Equal(0, first);         // 8-byte body → single hex line
        var line = Assert.Single(vm.HexLines);
        Assert.Equal("02 00 01", line.HexHit);
    }

    [Fact]
    public void HighlightHexRange_WithoutBody_ReturnsMinusOne()
    {
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x00006984, 1UL,
            Array.Empty<MessageElem>(), null);
        var vm = new PacketDetailViewModel { SelectedRow = new PacketRowVm(packet) };
        Assert.Equal(-1, vm.HighlightHexRange(0, 1));
    }

    [Fact]
    public void BuildPacketSlimJson_HasOnlyTheSpecimenFields_Indented()
    {
        var vm = new PacketDetailViewModel { SelectedRow = RowWithBody() };

        var json = vm.BuildPacketSlimJson()!;

        Assert.Contains("\"opDec\": 27012", json);   // 0x6984, indented for reading
        Assert.Contains("\"eid\": \"1\"", json);
        Assert.Contains("\"elems\"", json);
        Assert.DoesNotContain("\"kind\"", json);
        Assert.DoesNotContain("\"decoded\"", json);
        Assert.DoesNotContain("\"type\"", json);

        Assert.Null(new PacketDetailViewModel().BuildPacketSlimJson());
    }

    [Fact]
    public void PcapExport_SuggestsOpNamedFile_AndKnowsWhenItCannotRun()
    {
        var withBody = new PacketDetailViewModel { SelectedRow = RowWithBody() };
        Assert.True(withBody.CanExportPcap);
        Assert.Equal("packet-0x00006984.pcapng", withBody.SuggestedPcapExportFileName);

        var bare = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x00006984, 1UL,
            Array.Empty<MessageElem>(), null);
        var withoutBody = new PacketDetailViewModel { SelectedRow = new PacketRowVm(bare) };
        Assert.False(withoutBody.CanExportPcap);

        Assert.False(new PacketDetailViewModel().CanExportPcap);
    }

    [Fact]
    public void ExportPacketPcap_WritesAReadableCapture()
    {
        var vm = new PacketDetailViewModel { SelectedRow = RowWithBody() };
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"mabipacade-ui-test-{Guid.NewGuid():N}.pcapng");
        try
        {
            vm.ExportPacketPcap(path);
            var bytes = System.IO.File.ReadAllBytes(path);
            Assert.True(bytes.Length > 0);
            // pcapng section header block magic.
            Assert.Equal(new byte[] { 0x0A, 0x0D, 0x0D, 0x0A }, bytes[..4]);
        }
        finally
        {
            try { System.IO.File.Delete(path); } catch (System.IO.IOException) { }
        }
    }

    [Fact]
    public void SelectedRow_WithBody_PopulatesHex()
    {
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10 };
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x00006984, 0UL,
            Array.Empty<MessageElem>(), Decoded: null) { Body = bytes };
        var vm = new PacketDetailViewModel { SelectedRow = new PacketRowVm(packet) };
        Assert.Equal(2, vm.HexLines.Count);     // 16 bytes per line; 17 bytes → 2 lines
    }
}

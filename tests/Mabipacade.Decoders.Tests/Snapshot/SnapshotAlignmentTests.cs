using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Snapshot;

namespace Mabipacade.Decoders.Tests.Snapshot;

/// <summary>
/// Checks each section against the element ranges established in
/// research/packet-0x5209/alignment-0805.md for this exact capture. The body has
/// no length prefixes, so a section's span is the only thing that keeps the next
/// one aligned — these boundaries are the parser's real contract.
/// </summary>
public class SnapshotAlignmentTests
{
    private static CharacterSnapshot Decode()
    {
        var input = new DecoderInput(
            DateTime.UnixEpoch, Direction.Inbound, 0x5209, 0, SnapshotFixture.Elems);
        return (CharacterSnapshot)new ChannelCharacterInfoDecoder().Decode(input);
    }

    /// <summary>Section name, first element, element count — straight from the analysis.</summary>
    public static TheoryData<string, int, int> ExpectedSpans => new()
    {
        { "CParameter", 2, 242 },                        // 2-243
        { "CTitleMgr", 244, 1095 },                      // 244-1338, = 6 + 3×363
        { "CMateMgr", 1339, 4 },                         // 1339-1342
        { "CJobComponent", 1343, 1 },
        { "COptionWeaponComponent", 1344, 67 },          // 1344-1410
        { "feature:0x659:SCMO", 1411, 1 },
        { "feature:0x1A4:InventorySize", 1412, 2 },      // 1412-1413
        { "Items", 1414, 9439 },                         // 1414-10852, 39% of the packet
        { "CKeyword", 10853, 649 },                      // 10853-11501
        { "CSkillMgr", 11502, 480 },                     // 11502-11981
        { "CBannerMgr", 11982, 2 },
        { "CPVPMgr", 11984, 18 },                        // 11984-12001
        { "CConditionMgr", 12002, 104 },                 // 12002-12105
        { "CGuildComponent", 12106, 14 },                // 12106-12119
        { "CArbeitMgr", 12120, 23 },                     // 12120-12142
        { "CSummonSlave", 12143, 4 },
        { "CSummonMaster", 12147, 1 },
        { "CTransformMgr", 12148, 5 },                   // 12148-12152
        { "CPetMgr", 12153, 12 },                        // 12153-12164
        { "CHouseComponent", 12165, 1 },
        { "CTameMgr", 12166, 5 },                        // 12166-12170
        { "CVehicle", 12171, 7 },                        // 12171-12177
        { "CShowdownComponent", 12178, 4 },              // 12178-12181
        { "CTransportComponent", 12182, 2 },
        { "CAviationComponent", 12184, 1 },
        { "CSkiingComponent", 12185, 1 },
        { "CFarmingComponent", 12186, 1 },
        { "CEventComponent", 12187, 5 },                 // 12187-12191
        { "CHeartStickerComponent", 12192, 2 },
        { "CJoustComponent", 12194, 11 },                // 12194-12204
        { "CAchievement", 12205, 328 },                  // 12205-12532
        { "CPrivateFarmComponent", 12533, 1 },
        { "CFamilyComponent", 12534, 6 },                // 12534-12539
        { "CDemiGodComponent", 12540, 1 },
        { "CCommerceComponent", 12541, 4 },              // 12541-12544
        { "CTalentComponent", 12545, 131 },              // 12545-12675
        { "CShapeShiftComponent", 12676, 529 },          // 12676-13204
        { "feature:0x395", 13205, 2 },
        { "CRockPaperScissorsComponent", 13207, 6 },     // 13207-13212
        { "CRegisterComponent", 13213, 41 },             // 13213-13253
        { "CNewEgoWeaponComponent", 13254, 108 },        // 13254-13361
        { "CMagigraphComponent", 13362, 27 },            // 13362-13388
        { "CMultiClassComponent", 13389, 56 },           // 13389-13444
        { "CPrivateIslandNPCComponent", 13445, 1 },
        { "CAstrologistComponent", 13446, 289 },         // 13446-13734
        { "CRoyalAlchemistComponent", 13735, 2 },
        { "CMusicBuffSharingComponent", 13737, 16 },     // 13737-13752
        { "unattributed", 13753, 46 },                   // 13753-13798, split points unresolved
        { "CQuestCtrl", 13799, 10155 },                  // 13799-23953, 42% of the packet
        { "tail", 23954, 22 },                           // 23954-23975
    };

    [Fact]
    public void WholePacket_ParsesToTheLastElement()
    {
        // The property the layout demands: no length prefixes anywhere, so
        // landing exactly on the final element is the proof that every one of
        // the 43 sections consumed the right amount.
        var snapshot = Decode();
        Assert.Null(snapshot.Error);
        Assert.Equal(23976, snapshot.ParsedThrough);
        Assert.True(snapshot.IsComplete);
    }

    [Fact]
    public void Quests_MatchTheDocumentedShape()
    {
        var quests = Decode().Body.Quests!;

        Assert.Equal(116, quests.Count);
        // Seven quest types were observed; type 27's 36 records are uniform.
        Assert.Equal(7, quests.Select(q => q.Description.QuestType).Distinct().Count());
        Assert.Equal(36, quests.Count(q => q.Description.QuestType == 27));
        // Feature 0x636 adds six fields to every type-25 objective.
        Assert.Equal(27, quests.Count(q => q.Description.QuestType == 25));
        Assert.Contains(quests, q => q.Description.Name == "塔拉任務");
    }

    [Fact]
    public void Tail_MatchesTheDocumentedValues()
    {
        var tail = Decode().Body.Tail!;

        // The extra-storage loop ran once and then hit its sentinel — the single
        // strongest piece of evidence that the tail is aligned.
        Assert.Single(tail.Storage);
        Assert.Equal(72u, tail.Storage[0].PocketId);
        Assert.Equal(63859081304000UL, tail.Storage[0].Value);

        Assert.Equal(44u, tail.Feature47CId);
        Assert.True(tail.Feature4AFFlag);
        Assert.Equal(314u, tail.Feature4AFValue);
        Assert.Equal(63921250800000UL, tail.PrTime);   // 2026-08-02 07:00
        // Three elements the client never reads.
        Assert.Equal(3, tail.Residue.Count);
    }

    [Theory]
    [MemberData(nameof(ExpectedSpans))]
    public void Section_OccupiesTheDocumentedSpan(string name, int start, int length)
    {
        var snapshot = Decode();
        var section = snapshot.Sections.SingleOrDefault(s => s.Name == name);

        Assert.True(section is not null,
            $"section '{name}' was never reached; parsing stopped at element " +
            $"{snapshot.ParsedThrough} ({snapshot.Error})");
        Assert.Equal(start, section!.Start);
        Assert.Equal(length, section.Length);
    }

    [Fact]
    public void Header_MatchesTheCapture()
    {
        var snapshot = Decode();
        Assert.Equal((byte)1, snapshot.Result);
        Assert.Equal(4503599630207674UL, snapshot.CharacterId);
    }

    [Fact]
    public void Items_AreAllReadAndOwnedByTheSnapshotCharacter()
    {
        var snapshot = Decode();
        var items = snapshot.Body.Items!.Items;

        Assert.Equal(804, items.Count);
        // Every record ends with the holder's entity id; all 804 carry the
        // snapshot's own character id, which is what closes the container.
        Assert.All(items, i => Assert.Equal(snapshot.CharacterId, i.OwnerId));
        // The two blobs have the fixed sizes the analysis states: 0x50 and 0x90.
        Assert.All(items, i => Assert.Equal(80, i.Core.Length));
        Assert.All(items, i => Assert.Equal(144, i.Extended.Length));
        // Attachments are 0x28 each.
        Assert.All(items, i => Assert.All(i.Attachments, a => Assert.Equal(40, a.Length)));
    }

    [Fact]
    public void Items_ShowTheDocumentedConditionalBranches()
    {
        var items = Decode().Body.Items!.Items;

        // Hit counts from 08b-item-record.md's condition table.
        Assert.Equal(116, items.Count(i => i.QuestTip is not null));
        Assert.Equal(3, items.Count(i => i.Interstitial is not null));
        Assert.Equal(13, items.Count(i => i.Extras.Count > 0));
        Assert.Equal(745, items.Count(i => i.Attachments.Count == 0));
        Assert.Equal(31, items.Max(i => i.Attachments.Count));
    }

    [Fact]
    public void Items_DecodeSensibleCoreFields()
    {
        var items = Decode().Body.Items!.Items;

        // Offsets into the core blob come from MabiNotes, not from the
        // reference analysis, so they are pinned by plausibility: real item ids,
        // bag ids, and grid coordinates that fit a 9x10 bag.
        Assert.Equal(2309u, items[0].ItemId);
        Assert.Equal(2u, items[0].RecordType);
        Assert.Equal(8u, items[0].PosX);
        Assert.Equal(1u, items[0].PosY);

        Assert.Equal(455, items.Select(i => i.ItemId).Distinct().Count());
        Assert.All(items, i => Assert.InRange(i.ItemId, 1u, 6_000_000u));
    }

    [Fact]
    public void Titles_MatchTheDocumentedCountsAndFlags()
    {
        var titles = Decode().Body.Titles!;

        Assert.Equal(363, titles.Titles.Count);
        Assert.Equal(16057u, titles.StyleTitleId);
        Assert.Equal(18131u, titles.StyleSubTitleId);
        Assert.Equal(16057u, titles.EquippedTitleId);   // property bag MCDT1
        Assert.Equal(18131u, titles.EquippedSubTitleId); // property bag MCDT2

        Assert.Equal(133, titles.Titles.Count(t => t.Flag == 0));
        Assert.Equal(226, titles.Titles.Count(t => t.Flag == 1));
        Assert.Equal(4, titles.Titles.Count(t => t.Flag == 3));
        // Only the four expiring titles carry a timestamp.
        Assert.Equal(4, titles.Titles.Count(t => t.ExpiresAt != 0));
        Assert.All(titles.Titles.Where(t => t.ExpiresAt != 0), t => Assert.Equal((byte)3, t.Flag));
    }

    [Fact]
    public void SmallHeadSections_MatchTheCapture()
    {
        var body = Decode().Body;

        Assert.Equal(0UL, body.Mate!.MateId);        // unmarried
        Assert.Equal((byte)103, body.JobId);
        Assert.Equal(0u, body.Scmo);
        Assert.Equal(9u, body.InventorySize!.Width);
        Assert.Equal(10u, body.InventorySize.Height);

        // COptionWeapon's three lists: 15 slots, 7 ids, 5 levels.
        Assert.Equal(15, body.OptionWeapon!.Slots.Count);
        Assert.Equal(7, body.OptionWeapon.Ids.Count);
        Assert.Equal(5, body.OptionWeapon.Levels.Count);
    }
}

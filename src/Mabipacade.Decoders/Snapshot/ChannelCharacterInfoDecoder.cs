using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Snapshot.Sections;

namespace Mabipacade.Decoders.Snapshot;

/// <summary>
/// Decoder for <c>0x5209 NET_CHARACTER_DATA_REPLY</c> 🅲 (MabiNotes/Aura call it
/// ChannelCharacterInfoRequestR), the owner-only full character snapshot.
///
/// Layout follows the reference analysis in mabi_it_workspace
/// <c>research/packet-0x5209/</c>, derived from Client.exe
/// (TimeDateStamp 6A6861C9) for TW G28S1 and aligned against a 23,976-element
/// capture.
///
/// The body carries no length prefixes: the client hands one message cursor
/// down the component chain and each component returns it advanced. This parser
/// mirrors that, so sections are read strictly in order and a section that
/// fails to line up stops the parse rather than corrupting the rest.
/// </summary>
public sealed class ChannelCharacterInfoDecoder : IPacketDecoder
{
    public uint Op => 0x00005209;

    /// <summary><c>result == 1</c> is the only value carrying a body.</summary>
    private const byte ResultSuccess = 1;

    /// <summary>
    /// How far past the last identified component to look for the quest log. The
    /// unattributed span is 46 elements on the reference capture; the allowance
    /// is generous enough for another character's components to differ while
    /// still failing loudly rather than scanning the rest of the packet.
    /// </summary>
    private const int UnattributedSearchLimit = 512;

    public object Decode(DecoderInput input)
    {
        var c = new ElemCursor(input.Elems);
        var sections = new List<SnapshotSection>();

        // Header, read by CAccount::OnMessage (0x14243A100) before the cursor is
        // handed to CCharacter::ProcessDataMessage.
        byte result;
        ulong characterId;
        try
        {
            result = c.U8();
            characterId = c.U64();
        }
        catch (SnapshotFormatException ex)
        {
            return new CharacterSnapshot(0, 0, new SnapshotBody(), sections,
                c.Index, input.Elems.Count, ex.Message);
        }

        // 0x66 is a failure the client is told to ignore without complaint, so
        // it is not reported as an error here either.
        if (result != ResultSuccess)
            return new CharacterSnapshot(result, characterId, new SnapshotBody(), sections,
                c.Index, input.Elems.Count,
                result == 0x66 ? null : $"result {result}");

        var body = new SnapshotBody();
        string? error = null;
        try
        {
            // Order is CCharacter::ProcessDataMessage's component chain. Each
            // reader leaves the cursor on the next component's first element.
            body.Parameter = ReadSection(c, sections, "CParameter", CParameterSection.Read);
            body.Titles = ReadSection(c, sections, "CTitleMgr", HeadSections.ReadTitleMgr);
            body.Mate = ReadSection(c, sections, "CMateMgr", HeadSections.ReadMateMgr);
            body.JobId = ReadSection(c, sections, "CJobComponent", HeadSections.ReadJob);
            body.OptionWeapon = ReadSection(c, sections, "COptionWeaponComponent", HeadSections.ReadOptionWeapon);
            body.Scmo = ReadSection(c, sections, "feature:0x659:SCMO", HeadSections.ReadScmo);
            body.InventorySize = ReadSection(c, sections, "feature:0x1A4:InventorySize", HeadSections.ReadInventorySize);
            body.Items = ReadSection(c, sections, "Items", ItemSection.Read);

            body.Keywords = ReadSection(c, sections, "CKeyword", MidSections.ReadKeywords);
            body.Skills = ReadSection(c, sections, "CSkillMgr", MidSections.ReadSkillMgr);
            ReadSection(c, sections, "CBannerMgr", MidSections.ReadBanner);
            ReadSection(c, sections, "CPVPMgr", MidSections.ReadPvp);
            body.Conditions = ReadSection(c, sections, "CConditionMgr", MidSections.ReadConditions);
            body.Guild = ReadSection(c, sections, "CGuildComponent", MidSections.ReadGuild);
            ReadSection(c, sections, "CArbeitMgr", MidSections.ReadArbeit);
            ReadSection(c, sections, "CSummonSlave", MidSections.ReadSummonSlave);
            ReadSection(c, sections, "CSummonMaster", MidSections.ReadSummonMaster);
            ReadSection(c, sections, "CTransformMgr", MidSections.ReadTransform);
            body.Pet = ReadSection(c, sections, "CPetMgr", MidSections.ReadPet);
            ReadSection(c, sections, "CHouseComponent", MidSections.ReadHouse);
            ReadSection(c, sections, "CTameMgr", MidSections.ReadTame);
            ReadSection(c, sections, "CVehicle", MidSections.ReadVehicle);
            ReadSection(c, sections, "CShowdownComponent", MidSections.ReadShowdown);
            ReadSection(c, sections, "CTransportComponent", MidSections.ReadTransport);
            ReadSection(c, sections, "CAviationComponent", MidSections.ReadSingleByte);
            ReadSection(c, sections, "CSkiingComponent", MidSections.ReadSingleByte);
            ReadSection(c, sections, "CFarmingComponent", MidSections.ReadFarming);
            ReadSection(c, sections, "CEventComponent", MidSections.ReadEvent);
            ReadSection(c, sections, "CHeartStickerComponent", MidSections.ReadHeartSticker);
            ReadSection(c, sections, "CJoustComponent", MidSections.ReadJoust);
            body.Achievements = ReadSection(c, sections, "CAchievement", MidSections.ReadAchievements);
            body.PrivateFarm = ReadSection(c, sections, "CPrivateFarmComponent", MidSections.ReadPrivateFarm);
            body.Family = ReadSection(c, sections, "CFamilyComponent", MidSections.ReadFamily);
            ReadSection(c, sections, "CDemiGodComponent", MidSections.ReadDemiGod);
            ReadSection(c, sections, "CCommerceComponent", MidSections.ReadCommerce);
            body.Talent = ReadSection(c, sections, "CTalentComponent", MidSections.ReadTalent);
            body.ShapeShifts = ReadSection(c, sections, "CShapeShiftComponent", MidSections.ReadShapeShift);
            ReadSection(c, sections, "feature:0x395", MidSections.ReadFeature0x395);
            ReadSection(c, sections, "CRockPaperScissorsComponent", MidSections.ReadRockPaperScissors);
            ReadSection(c, sections, "CRegisterComponent", MidSections.ReadRegister);
            body.EgoWeapons = ReadSection(c, sections, "CNewEgoWeaponComponent", MidSections.ReadNewEgoWeapon);
            ReadSection(c, sections, "CMagigraphComponent", static cur =>
            {
                // No field structure was established for this one; consume the
                // documented span so the components after it stay aligned.
                cur.Skip(MidSections.MagigraphLength);
                return 0;
            });
            body.MultiClass = ReadSection(c, sections, "CMultiClassComponent", MidSections.ReadMultiClass);
            ReadSection(c, sections, "CPrivateIslandNPCComponent", MidSections.ReadPrivateIslandNpc);
            body.Astrologist = ReadSection(c, sections, "CAstrologistComponent", MidSections.ReadAstrologist);
            ReadSection(c, sections, "CRoyalAlchemistComponent", MidSections.ReadRoyalAlchemist);
            body.MusicBuffSharing = ReadSection(c, sections, "CMusicBuffSharingComponent", MidSections.ReadMusicBuffSharing);

            // The one gap sequential reading cannot cross. Consumed as an opaque
            // span located by the quest log's anchor rather than by a length.
            ReadSection(c, sections, "unattributed", static cur =>
            {
                cur.Skip(QuestSection.FindQuestCtrl(cur, UnattributedSearchLimit));
                return 0;
            });

            body.Quests = ReadSection(c, sections, "CQuestCtrl", QuestSection.Read);
            body.Tail = ReadSection(c, sections, "tail", TailSection.Read);
        }
        catch (SnapshotFormatException ex)
        {
            error = ex.Message;
        }

        return new CharacterSnapshot(result, characterId, body, sections,
            c.Index, input.Elems.Count,
            error ?? (c.Index == input.Elems.Count ? null : "tail not parsed yet"));
    }

    /// <summary>Runs one section's reader and records the span it consumed.</summary>
    private static T ReadSection<T>(ElemCursor c, List<SnapshotSection> sections,
        string name, Func<ElemCursor, T> read)
    {
        int start = c.Index;
        var value = read(c);
        sections.Add(new SnapshotSection(name, start, c.Index - start,
            c.Slice(start, c.Index - start)));
        return value;
    }
}

using System.IO;
using System.Text;
using Mabipacade.DebugUi.Models;
using Mabipacade.DebugUi.Services;

namespace Mabipacade.DebugUi.Tests.Services;

public class NameResolverServiceTests
{
    [Fact]
    public void ResolvePaths_FlatLayout_PrefersDirectFiles()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"mp-nrs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            File.WriteAllText(Path.Combine(temp, "SkillInfo.xml"), "");
            File.WriteAllText(Path.Combine(temp, "SkillInfo.taiwan.txt"), "");

            var (xml, txt) = NameResolverService.ResolvePaths(temp);
            Assert.Equal(Path.Combine(temp, "SkillInfo.xml"), xml);
            Assert.Equal(Path.Combine(temp, "SkillInfo.taiwan.txt"), txt);
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public void ResolvePaths_ExtractedLayout_LooksInDataSubdirs()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"mp-nrs-{Guid.NewGuid():N}");
        var skillDir = Path.Combine(temp, "data", "db", "Skill");
        var xmlDir = Path.Combine(temp, "data", "local", "xml");
        Directory.CreateDirectory(skillDir);
        Directory.CreateDirectory(xmlDir);
        try
        {
            File.WriteAllText(Path.Combine(skillDir, "SkillInfo.xml"), "");
            File.WriteAllText(Path.Combine(xmlDir, "SkillInfo.taiwan.txt"), "");

            var (xml, txt) = NameResolverService.ResolvePaths(temp);
            Assert.Equal(Path.Combine(skillDir, "SkillInfo.xml"), xml);
            Assert.Equal(Path.Combine(xmlDir, "SkillInfo.taiwan.txt"), txt);
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public void Reload_ExtractedLayout_ActuallyLoadsSkills()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"mp-nrs-{Guid.NewGuid():N}");
        var skillDir = Path.Combine(temp, "data", "db", "Skill");
        var xmlDir = Path.Combine(temp, "data", "local", "xml");
        Directory.CreateDirectory(skillDir);
        Directory.CreateDirectory(xmlDir);
        try
        {
            using (var sw = new StreamWriter(Path.Combine(skillDir, "SkillInfo.xml"), false, Encoding.Unicode))
            {
                sw.WriteLine("<SkillRoot>");
                sw.WriteLine("<Skill SkillID='59000' SkillEngName='FinalHit' SkillLocalName='_LT[xml.skillinfo.59000]'/>");
                sw.WriteLine("</SkillRoot>");
            }
            using (var sw = new StreamWriter(Path.Combine(xmlDir, "SkillInfo.taiwan.txt"), false, new UTF8Encoding(true)))
            {
                sw.WriteLine("xml.skillinfo.59000\t終結一擊");
            }

            var svc = new NameResolverService();
            svc.Reload(new DebugUiSettings { XmlDataDirectory = temp });

            Assert.Equal("終結一擊", svc.Current.TryResolveSkill(59000));
        }
        finally { Directory.Delete(temp, true); }
    }
}

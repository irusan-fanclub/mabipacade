using System.IO;
using System.Text;
using Mabipacade.DebugUi.Resolution;

namespace Mabipacade.DebugUi.Tests.Resolution;

public class SkillInfoLoaderTests
{
    [Fact]
    public void LoadLocalization_ParsesKeyValuePairs()
    {
        var path = Path.GetTempFileName();
        try
        {
            // UTF-8 with BOM
            using (var sw = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                sw.WriteLine("xml.skillinfo.271\t冰風暴");
                sw.WriteLine("xml.skillinfo.273\t冰矛");
                sw.WriteLine("");
                sw.WriteLine("xml.skillinfo.500\tFinal Hit");
            }
            var map = SkillInfoLoader.LoadLocalization(path);
            Assert.Equal("冰風暴", map["xml.skillinfo.271"]);
            Assert.Equal("冰矛", map["xml.skillinfo.273"]);
            Assert.Equal("Final Hit", map["xml.skillinfo.500"]);
            Assert.False(map.ContainsKey(""));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseSkillXml_LastOccurrenceWins_NotMaxSeason()
    {
        var path = Path.GetTempFileName();
        try
        {
            // UTF-16 with BOM
            using (var sw = new StreamWriter(path, false, Encoding.Unicode))
            {
                sw.WriteLine("<?xml version='1.0' encoding='UTF-16' ?>");
                sw.WriteLine("<SkillRoot>");
                sw.WriteLine("<Skill SkillID='271' Season='3' SkillEngName='Icebolt' SkillLocalName='_LT[xml.skillinfo.273]'/>");
                sw.WriteLine("<Skill SkillID='271' Season='4' SkillEngName='IceboltV4' SkillLocalName='_LT[xml.skillinfo.999]'/>");
                sw.WriteLine("<Skill SkillID='271' Season='2' SkillEngName='IceboltV2' SkillLocalName='_LT[xml.skillinfo.111]'/>");
                sw.WriteLine("</SkillRoot>");
            }
            var loc = new Dictionary<string, string>
            {
                ["xml.skillinfo.273"] = "Icebolt-S3",
                ["xml.skillinfo.999"] = "Icebolt-S4",
                ["xml.skillinfo.111"] = "Icebolt-S2",
            };
            var map = SkillInfoLoader.ParseSkillXml(path, loc);
            Assert.Single(map);
            // Last in file order is Season=2, NOT max Season=4
            Assert.Equal("IceboltV2", map[271].EnglishName);
            Assert.Equal("Icebolt-S2", map[271].LocalName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseSkillXml_ResolvesLocalName_FallsBackToRefWhenMissing()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var sw = new StreamWriter(path, false, Encoding.Unicode))
            {
                sw.WriteLine("<SkillRoot>");
                sw.WriteLine("<Skill SkillID='999' SkillEngName='Mystery' SkillLocalName='_LT[xml.skillinfo.unknown]'/>");
                sw.WriteLine("</SkillRoot>");
            }
            var map = SkillInfoLoader.ParseSkillXml(path, new Dictionary<string, string>());
            Assert.Equal("_LT[xml.skillinfo.unknown]", map[999].LocalName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_MissingFiles_ReturnsEmpty()
    {
        var map = SkillInfoLoader.Load("nonexistent.xml", "nonexistent.txt");
        Assert.Empty(map);
    }
}

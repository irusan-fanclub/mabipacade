using System.IO;
using Mabipacade.DebugUi.Models;
using Mabipacade.DebugUi.Services;

namespace Mabipacade.DebugUi.Tests.Services;

public class SettingsServiceTests
{
    [Fact]
    public void Load_MissingFile_ReturnsDefault()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"mp-st-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var svc = new SettingsService(temp);
            var s = svc.Load();
            Assert.Equal(1000, s.WindowWidth);
            Assert.Equal(600, s.WindowHeight);
            Assert.Null(s.LastPcapPath);
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public void SaveThenLoad_RoundTrip()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"mp-st-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var svc = new SettingsService(temp);
            svc.Save(new DebugUiSettings { WindowWidth = 1200, WindowHeight = 800, LastPcapPath = "C:/x.pcap" });
            var s = svc.Load();
            Assert.Equal(1200, s.WindowWidth);
            Assert.Equal(800, s.WindowHeight);
            Assert.Equal("C:/x.pcap", s.LastPcapPath);
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public void Save_WritesAtomically_NoStaleTmpFile()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"mp-st-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var svc = new SettingsService(temp);
            svc.Save(new DebugUiSettings { WindowWidth = 1, WindowHeight = 1, LastPcapPath = null });
            Assert.True(File.Exists(Path.Combine(temp, "settings.json")));
            Assert.False(File.Exists(Path.Combine(temp, "settings.json.tmp")));
        }
        finally { Directory.Delete(temp, true); }
    }
}

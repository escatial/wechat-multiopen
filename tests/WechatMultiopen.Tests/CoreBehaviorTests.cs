using System.Text.Json;
using System.Text.Json.Serialization;
using WechatMultiopen.Cli;
using WechatMultiopen.Config;
using WechatMultiopen.Core;
using WechatMultiopen.Models;
using Xunit;

namespace WechatMultiopen.Tests;

public sealed class WildcardMatchTests
{
    [Theory]
    [InlineData("*_WeChat_App_Instance_Identity_Mutex_Name", "1234_WeChat_App_Instance_Identity_Mutex_Name", true)]
    [InlineData("*_WeChat_App_Instance_Identity_Mutex_Name", "ABCD_WeChat_App_Instance_Identity_Mutex_Name", true)]
    [InlineData("*_WeChat_Win_User_Identity_Mutex_Name", "_WeChat_Win_User_Identity_Mutex_Name", true)]
    [InlineData("*XWeChat_XWEB_Instance_Identity_Mutex_Name", "1E100XWeChat_XWEB_Instance_Identity_Mutex_Name", true)]
    [InlineData("*_WeChat_App_Instance_Identity_Mutex_Name", "1234_WeChat_App_Instance_Identity_Other", false)]
    [InlineData("*_WeChat_App_Instance_Identity_Mutex_Name", "1234_WeChat_Win_User_Identity_Mutex_Name", false)]
    [InlineData("WeChat_App_Mutex", "wechat_app_mutex", true)]
    [InlineData("WeChat_App_Mutex", "WeChat_App_Mutex_", false)]
    [InlineData("abc*", "abcdef", true)]
    [InlineData("abc*", "xabcdef", false)]
    public void MatchesWildcardPatterns(string pattern, string value, bool expected)
        => Assert.Equal(expected, HandleCloserEngine.Matches(pattern, value));

    [Fact]
    public void NullValueNeverMatches()
        => Assert.False(HandleCloserEngine.Matches("*", null));
}

public sealed class ParseCountTests
{
    [Fact]
    public void DefaultsToTwo() => Assert.Equal(2, CliApp.ParseCount(Array.Empty<string>()));

    [Theory]
    [InlineData("-n", "3", 3)]
    [InlineData("--count", "4", 4)]
    [InlineData("-N", "2", 2)]
    public void ParsesValidCounts(string flag, string value, int expected)
        => Assert.Equal(expected, CliApp.ParseCount(new[] { flag, value }));

    [Theory]
    [InlineData("-n", "1")]
    [InlineData("-n", "5")]
    [InlineData("-n", "abc")]
    public void RejectsOutOfRangeOrNonNumericCounts(params string[] args)
        => Assert.Throws<CliArgumentException>(() => CliApp.ParseCount(args));

    [Fact]
    public void TrailingFlagFallsBackToDefault()
    {
        string[] args = { "-n" };
        Assert.Equal(2, CliApp.ParseCount(args));
    }
}

public sealed class FeatureTableLoaderTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory().FullName;

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); }
        catch { /* 临时目录删除失败不影响测试结论 */ }
    }

    [Fact]
    public void MissingExternalFileFallsBackToEmbedded()
    {
        var table = FeatureTableLoader.Load(Path.Combine(_tempDir, "absent.json"));
        Assert.Equal("1.0", table.SchemaVersion);
        Assert.Contains(table.Branches, x => x.Branch == WeChatBranch.V4 && x.Mutexes.Length > 0);
    }

    [Fact]
    public void CorruptExternalFileFallsBackToEmbedded()
    {
        var path = Path.Combine(_tempDir, "corrupt.json");
        File.WriteAllText(path, "{ not json");
        var table = FeatureTableLoader.Load(path);
        Assert.Contains(table.Branches, x => x.ExeNames.Contains("Weixin.exe"));
    }

    [Fact]
    public void ValidExternalFileOverridesEmbedded()
    {
        var path = Path.Combine(_tempDir, "external.json");
        File.WriteAllText(path, """
            {"schemaVersion":"1.0","branches":[{"branch":"V4","exeNames":["Weixin.exe"],"mutexes":[{"namePattern":"*_Test_Marker_Mutex","handleType":"Mutant"}]}]}
            """);
        var table = FeatureTableLoader.Load(path);
        var v4 = Assert.Single(table.Branches);
        Assert.Equal("*_Test_Marker_Mutex", Assert.Single(v4.Mutexes).NamePattern);
    }
}

public sealed class FeatureTemplateTests
{
    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void PrefersIdentityNamedMutantsAndDeduplicates()
    {
        string[] names = { "{7C0A}_WeChat_Win_User_Identity_Mutex_Name", "{7C0A}_WeChat_Win_User_Identity_Mutex_Name", "Tencent_Some_Other_Mutex" };
        var json = DoctorService.BuildFeatureTemplate(WeChatBranch.V4, "Weixin.exe", names);
        Assert.NotNull(json);
        Assert.Contains("*{7C0A}_WeChat_Win_User_Identity_Mutex_Name", json);
        Assert.Contains("Weixin.exe", json);
        Assert.DoesNotContain("Tencent_Some_Other_Mutex", json);
    }

    [Fact]
    public void FallsBackToAllMutantsWhenNoIdentityNamed()
    {
        string[] names = { "PlainMutex" };
        var json = DoctorService.BuildFeatureTemplate(WeChatBranch.V3, "WeChat.exe", names);
        Assert.NotNull(json);
        Assert.Contains("*PlainMutex", json);
    }

    [Fact]
    public void StripsKernelObjectPathPrefixInPattern()
    {
        string[] names = { @"\Sessions\1\BaseNamedObjects\XWeChat_XWEB_Instance_Identity_Mutex_Name" };
        var json = DoctorService.BuildFeatureTemplate(WeChatBranch.V4, "Weixin.exe", names);
        Assert.NotNull(json);
        Assert.Contains("*XWeChat_XWEB_Instance_Identity_Mutex_Name", json);
        Assert.DoesNotContain("BaseNamedObjects", json);
        Assert.Contains("\"schemaVersion\"", json);
    }

    [Fact]
    public void EmptyInputReturnsNull()
        => Assert.Null(DoctorService.BuildFeatureTemplate(WeChatBranch.V4, "Weixin.exe", Array.Empty<string>()));

    [Fact]
    public void TemplateParsesBackIntoFeatureTableAndCapsAtTenPatterns()
    {
        var names = Enumerable.Range(1, 15).Select(i => $"Identity_{i}_Mutex").ToArray();
        var json = DoctorService.BuildFeatureTemplate(WeChatBranch.V4, "Weixin.exe", names)!;        var table = JsonSerializer.Deserialize<FeatureTable>(json, ParseOptions);
        Assert.NotNull(table);
        var branch = Assert.Single(table!.Branches);
        Assert.Equal(WeChatBranch.V4, branch.Branch);
        Assert.Equal("Weixin.exe", Assert.Single(branch.ExeNames));
        Assert.Equal(10, branch.Mutexes.Length);
        Assert.All(branch.Mutexes, x => Assert.StartsWith("*", x.NamePattern));
    }
}

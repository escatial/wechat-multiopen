using WechatMultiopen.Config;
using WechatMultiopen.Models;
using Xunit;

namespace WechatMultiopen.Tests;

public sealed class FeatureTableTests
{
    [Fact]
    public void EmbeddedTableContainsV3Feature()
    {
        var table = FeatureTableLoader.Load();
        var v3 = Assert.Single(table.Branches, x => x.Branch == WeChatBranch.V3);
        Assert.Contains(v3.Mutexes, x => x.HandleType == "Mutant");
    }

    [Fact]
    public void V4ContainsConfirmedXwebFeature()
    {
        var table = FeatureTableLoader.Load();
        var v4 = Assert.Single(table.Branches, x => x.Branch == WeChatBranch.V4);
        Assert.Contains(v4.Mutexes, x => x.HandleType == "Mutant" && x.NamePattern == "*XWeChat_XWEB_Instance_Identity_Mutex_Name");
    }
}

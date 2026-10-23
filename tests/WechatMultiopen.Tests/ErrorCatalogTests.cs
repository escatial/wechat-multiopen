using WechatMultiopen.Diagnostics;
using WechatMultiopen.Models;
using Xunit;

namespace WechatMultiopen.Tests;

public sealed class ErrorCatalogTests
{
    [Fact]
    public void EveryDefinedErrorHasChineseGuidance()
    {
        foreach (var code in Enum.GetValues<ErrorCode>().Where(x => x != ErrorCode.Success))
        {
            var info = ErrorCatalog.Get(code);
            Assert.False(string.IsNullOrWhiteSpace(info.Reason));
            Assert.False(string.IsNullOrWhiteSpace(info.Advice));
        }
    }
}

namespace WechatMultiopen.Infrastructure;

public static class AppInfo
{
    public static string Version =>
        typeof(AppInfo).Assembly.GetName().Version is { } version ? version.ToString(3) : "unknown";
}

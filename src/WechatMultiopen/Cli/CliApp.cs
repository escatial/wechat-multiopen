using WechatMultiopen.Diagnostics;
using WechatMultiopen.Infrastructure;
using WechatMultiopen.Models;

namespace WechatMultiopen.Cli;

public sealed class CliApp
{
    private readonly ServiceSet _services;
    public CliApp(ServiceSet services) => _services = services;

    public int Run(string[] args)
    {
        if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
        {
            PrintHelp();
            return 0;
        }

        if (args.Contains("--version", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"wechat-multiopen {Infrastructure.AppInfo.Version}");
            var detected = Core.WeChatDetector.Detect(GetValue(args, "-p", "--path"));
            Console.WriteLine(detected is null
                ? "微信：未检测到"
                : $"微信：{detected.ExePath}（{detected.FileVersion}）");
            return 0;
        }

        if (args.Contains("--doctor", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(_services.Doctor.Run(GetValue(args, "-p", "--path"), args.Contains("--verbose", StringComparer.OrdinalIgnoreCase)));
            return 0;
        }

        if (args.Contains("--log", StringComparer.OrdinalIgnoreCase)) _services.Logger.Enable();
        int count;
        try
        {
            count = ParseCount(args);
        }
        catch (CliArgumentException ex)
        {
            Console.Error.WriteLine($"[{ErrorCode.InvalidPath}] {ex.Message}\n建议：请使用 --help 查看正确用法。");
            return (int)ErrorCode.InvalidPath;
        }
        var path = GetValue(args, "-p", "--path");
        if (path is not null && !File.Exists(path))
        {
            PrintError(ErrorCatalog.Get(ErrorCode.InvalidPath));
            return (int)ErrorCode.InvalidPath;
        }

        var report = _services.Orchestrator.RunAsync(count, path).GetAwaiter().GetResult();
        Console.WriteLine($"已启动 {report.Succeeded}/{report.Requested} 个实例。\n微信：{report.WeChat?.ExePath}");
        foreach (var error in report.Errors) Console.Error.WriteLine($"错误：{error}");
        if (_services.Logger.Path.Length > 0) Console.WriteLine($"日志：{_services.Logger.Path}");
        return report.Succeeded == report.Requested ? 0 : (int)ErrorCode.LaunchFailed;
    }

    internal static int ParseCount(string[] args)
    {
        var value = GetValue(args, "-n", "--count");
        if (value is null) return 2;
        if (!int.TryParse(value, out var count) || count is < 2 or > 4)
        {
            throw new CliArgumentException("实例数量必须是 2 到 4 之间的整数。");
        }
        return count;
    }

    private static string? GetValue(string[] args, string shortName, string longName)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(shortName, StringComparison.OrdinalIgnoreCase) || args[i].Equals(longName, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }

    private static void PrintError(ErrorInfo error) => Console.Error.WriteLine($"[{error.Code}] {error.Reason}\n建议：{error.Advice}");

    private static void PrintHelp()
    {
        Console.WriteLine("wechat-multiopen.exe [-n 2..4] [-p <微信路径>] [--log] [--doctor] [--version] [--help]");
        Console.WriteLine("无参数启动图形界面。默认启动 2 个实例。\n");
        Console.WriteLine(Compliance.Notice);
    }
}

public sealed class CliArgumentException(string message) : Exception(message);

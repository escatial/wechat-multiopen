using Microsoft.Win32;
using System.Diagnostics;
using WechatMultiopen.Config;
using WechatMultiopen.Models;

namespace WechatMultiopen.Core;

public sealed class WeChatDetector
{
    private readonly FeatureTable _features;

    public WeChatDetector(FeatureTable features) => _features = features;

    public static WeChatInfo? Detect(string? pathOverride)
    {
        if (!string.IsNullOrWhiteSpace(pathOverride))
        {
            if (!File.Exists(pathOverride)) return null;
            var fullPath = Path.GetFullPath(pathOverride);
            var name = Path.GetFileName(fullPath);
            if (!name.Equals("WeChat.exe", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("Weixin.exe", StringComparison.OrdinalIgnoreCase)) return null;
            return BuildInfo(fullPath);
        }

        foreach (var path in RegistryPaths().Concat(CommonPaths()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(path)) return BuildInfo(path);
        }
        return null;
    }

    public static IReadOnlyList<int> FindRunningProcesses(WeChatInfo target)
    {
        var processName = Path.GetFileNameWithoutExtension(target.ExePath);
        return Process.GetProcessesByName(processName)
            .Where(p => IsTargetPath(p, target.ExePath))
            .Select(p => p.Id)
            .ToArray();
    }

    public BranchFeature? GetFeature(WeChatInfo target)
        => _features.Branches.FirstOrDefault(x =>
            x.Branch == target.Branch &&
            x.Mutexes.Length > 0 &&
            x.ExeNames.Any(n => n.Equals(target.ExeName, StringComparison.OrdinalIgnoreCase)));

    private static WeChatInfo BuildInfo(string path)
    {
        var exeName = Path.GetFileName(path);
        var branch = exeName.Equals("Weixin.exe", StringComparison.OrdinalIgnoreCase) ? WeChatBranch.V4 : WeChatBranch.V3;
        var version = FileVersionInfo.GetVersionInfo(path).FileVersion ?? "unknown";
        return new WeChatInfo(path, version, branch);
    }

    private static IEnumerable<string> RegistryPaths()
    {
        foreach (var subKey in new[] { @"Software\Tencent\WeChat", @"Software\Tencent\Weixin" })
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey);
            var value = key?.GetValue("InstallPath") as string;
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return Path.Combine(value, subKey.EndsWith("Weixin", StringComparison.OrdinalIgnoreCase) ? "Weixin.exe" : "WeChat.exe");
            }
        }
    }

    private static IEnumerable<string> CommonPaths()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };
        foreach (var root in roots.Where(Directory.Exists))
        {
            yield return Path.Combine(root, "Tencent", "WeChat", "WeChat.exe");
            yield return Path.Combine(root, "Tencent", "Weixin", "Weixin.exe");
        }
    }

    private static bool IsTargetPath(Process process, string targetPath)
    {
        try { return string.Equals(process.MainModule?.FileName, targetPath, StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
}

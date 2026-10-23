using System.Security.Principal;
using System.Diagnostics;
using WechatMultiopen.Config;
using WechatMultiopen.Models;
using WechatMultiopen.Native;

namespace WechatMultiopen.Core;

public sealed class DoctorService
{
    private readonly WeChatDetector _detector;
    private readonly FeatureTable _features;
    private readonly NtApiNative _native;

    public DoctorService(WeChatDetector detector, FeatureTable features, NtApiNative native)
    {
        _detector = detector;
        _features = features;
        _native = native;
    }

    public string Run(string? pathOverride, bool verbose = false)
    {
        var admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        var target = WeChatDetector.Detect(pathOverride);
        var feature = target is null ? null : _detector.GetFeature(target);
        var lines = new List<string>
        {
            $"管理员权限：{(admin ? "是" : "否")}",
            target is null ? "微信：未检测到" : $"微信：{target.ExePath}",
            target is null ? "版本：未知" : $"版本：{target.FileVersion} ({target.Branch})",
            feature is null ? "特征：未找到对应分支" : $"特征：{feature.Mutexes.Length} 条配置"
        };
        try { lines.Add($"系统句柄枚举：{NtApiNative.EnumerateHandles().Count} 个"); }
        catch (Exception ex) { lines.Add($"系统句柄枚举：失败（{ex.Message}）"); }
        if (verbose && target is not null)
        {
            lines.Add("命名对象侦察：");
            lines.AddRange(ProbeNamedObjects(target));
        }
        lines.Add(feature is null
            ? "提示：当前版本没有已确认的互斥体特征，启动多开会被阻止。"
            : "提示：当前版本已加载已确认的互斥体特征，可尝试启动多开。其他微信版本仍需单独侦察。");
        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<string> ProbeNamedObjects(WeChatInfo target)
    {
        var pids = WeChatDetector.FindRunningProcesses(target);
        if (pids.Count == 0) return new[] { "未找到正在运行的目标进程。" };

        var result = new List<string>();
        IReadOnlyList<NtApiNative.HandleEntry> handles;
        try { handles = NtApiNative.EnumerateHandles(); }
        catch (Exception ex) { return new[] { $"枚举失败：{ex.Message}" }; }

        foreach (var pid in pids)
        {
            var sourceProcess = NtApiNative.OpenProcess(pid);
            if (sourceProcess == IntPtr.Zero)
            {
                result.Add($"PID {pid}：无法打开进程句柄。");
                continue;
            }

            try
            {
                foreach (var entry in handles.Where(x => x.ProcessId == pid))
                {
                    if (!NtApiNative.Duplicate(sourceProcess, entry.Handle, Process.GetCurrentProcess().Handle,
                            out var copy, NtApiNative.DuplicateSameAccess)) continue;
                    try
                    {
                        var type = NtApiNative.NormalizeKernelName(NtApiNative.QueryObjectString(
                            copy, NtApiNative.ObjectTypeInformation, TimeSpan.FromMilliseconds(500)));
                        if (type is not ("Mutant" or "Event" or "Section" or "Semaphore")) continue;
                        var name = NtApiNative.NormalizeKernelName(NtApiNative.QueryObjectString(
                            copy, NtApiNative.ObjectNameInformation, TimeSpan.FromMilliseconds(500)));
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            var line = $"PID {pid} | {type} | {name}";
                            // Keep WeChat identity objects visible even when a process owns many handles.
                            if (name.Contains("wechat", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("weixin", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("identity", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("xweb", StringComparison.OrdinalIgnoreCase))
                            {
                                result.Insert(0, line);
                            }
                            else result.Add(line);
                        }
                        if (result.Count >= 2000) return result;
                    }
                    finally { CloseHandle(copy); }
                }
            }
            finally { CloseHandle(sourceProcess); }
        }

        return result.Count == 0 ? new[] { "未发现可查询的命名同步对象。" } : result.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}

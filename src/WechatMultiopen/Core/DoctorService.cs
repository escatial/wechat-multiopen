using System.Security.Principal;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using WechatMultiopen.Config;
using WechatMultiopen.Models;
using WechatMultiopen.Native;

namespace WechatMultiopen.Core;

public sealed class DoctorService
{
    private static readonly JsonSerializerOptions TemplateJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
        if (verbose && target is not null) AppendProbeSection(lines, target);
        lines.Add(feature is null
            ? "提示：当前版本没有已确认的互斥体特征，启动多开会被阻止。"
            : "提示：当前版本已加载已确认的互斥体特征，可尝试启动多开。其他微信版本仍需单独侦察。");
        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendProbeSection(List<string> lines, WeChatInfo target)
    {
        lines.Add("命名对象侦察：");
        var probe = ProbeNamedObjects(target);
        lines.AddRange(probe.Lines);

        var template = BuildFeatureTemplate(target.Branch, target.ExeName, probe.MutantNames);
        if (template is null)
        {
            lines.Add("未发现命名 Mutant，无法生成特征模板。");
            return;
        }
        lines.Add(string.Empty);
        lines.Add($"特征模板（多开失效时，确认新版本特征后可保存为 {FeatureTableLoader.ExternalPath}，程序将优先读取该文件）：");
        lines.Add(template);
    }

    internal static string? BuildFeatureTemplate(WeChatBranch branch, string exeName, IReadOnlyList<string> mutantNames)
    {
        var distinct = mutantNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinct.Length == 0) return null;

        var identity = distinct.Where(IsIdentityNamed).ToArray();
        var selected = (identity.Length > 0 ? identity : distinct).Take(10).ToArray();
        var table = new FeatureTable(
            "1.0",
            new[] { new BranchFeature(branch, new[] { exeName }, selected.Select(n => new MutexFeature(ToPattern(n))).ToArray()) });
        return JsonSerializer.Serialize(table, TemplateJsonOptions);
    }

    // 内核对象名带 \Sessions\<n>\BaseNamedObjects\ 前缀，剥离后与内置特征表保持同一写法。
    private static string ToPattern(string name)
    {
        const string marker = "BaseNamedObjects\\";
        var markerIndex = name.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return markerIndex >= 0 ? "*" + name[(markerIndex + marker.Length)..] : "*" + name;
    }

    private static bool IsIdentityNamed(string name) =>
        name.Contains("wechat", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("weixin", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("identity", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("xweb", StringComparison.OrdinalIgnoreCase);

    private static readonly string[] NoTargetProcessLine = { "未找到正在运行的目标进程。" };
    private static readonly string[] NoNamedObjectsLine = { "未发现可查询的命名同步对象。" };

    private static ProbeResult ProbeNamedObjects(WeChatInfo target)
    {
        var pids = WeChatDetector.FindRunningProcesses(target);
        if (pids.Count == 0) return new ProbeResult(NoTargetProcessLine, Array.Empty<string>());
        var result = new List<string>();
        var mutants = new List<string>();
        IReadOnlyList<NtApiNative.HandleEntry> handles;
        try { handles = NtApiNative.EnumerateHandles(); }
        catch (Exception ex) { return new ProbeResult(new List<string> { $"枚举失败：{ex.Message}" }, Array.Empty<string>()); }

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
                        if (type == "Mutant" && !string.IsNullOrWhiteSpace(name)) mutants.Add(name);
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            var line = $"PID {pid} | {type} | {name}";
                            // Keep WeChat identity objects visible even when a process owns many handles.
                            if (IsIdentityNamed(name)) result.Insert(0, line);
                            else result.Add(line);
                        }
                        if (result.Count >= 2000) return new ProbeResult(result, mutants);
                    }
                    finally { CloseHandle(copy); }
                }
            }
            finally { CloseHandle(sourceProcess); }
        }

        if (result.Count == 0) return new ProbeResult(NoNamedObjectsLine, mutants);
        return new ProbeResult(result.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray(), mutants);
    }

    private sealed record ProbeResult(IReadOnlyList<string> Lines, IReadOnlyList<string> MutantNames);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}

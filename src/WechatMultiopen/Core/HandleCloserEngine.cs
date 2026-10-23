using WechatMultiopen.Infrastructure;
using WechatMultiopen.Models;
using WechatMultiopen.Native;
using System.Diagnostics;

namespace WechatMultiopen.Core;

public sealed class HandleCloserEngine
{
    private readonly NtApiNative _native;
    private readonly FileLogger _logger;

    public HandleCloserEngine(NtApiNative native, FeatureTable features, FileLogger logger)
    {
        _native = native;
        _logger = logger;
    }

    public CloseResult CloseMutexes(IReadOnlyList<int> processIds, BranchFeature feature)
    {
        if (processIds.Count == 0 || feature.Mutexes.Length == 0)
            return new CloseResult(0, 0, Array.Empty<HandleFailure>());

        IReadOnlyList<NtApiNative.HandleEntry> handles;
        try { handles = NtApiNative.EnumerateHandles(); }
        catch (Exception ex)
        {
            _logger.Error("handles", ex.Message);
            throw;
        }

        var candidates = 0;
        var closed = 0;
        var failures = new List<HandleFailure>();
        foreach (var entry in handles.Where(x => processIds.Contains(x.ProcessId)))
        {
            var sourceProcess = NtApiNative.OpenProcess(entry.ProcessId);
            if (sourceProcess == IntPtr.Zero)
            {
                failures.Add(new(entry.ProcessId, entry.Handle, "OpenProcessFailed"));
                continue;
            }

            try
            {
                if (!NtApiNative.Duplicate(sourceProcess, entry.Handle, ProcessHandle(), out var copy, NtApiNative.DuplicateSameAccess))
                {
                    failures.Add(new(entry.ProcessId, entry.Handle, "DuplicateFailed"));
                    continue;
                }

                try
                {
                    var type = NtApiNative.NormalizeKernelName(NtApiNative.QueryObjectString(copy, NtApiNative.ObjectTypeInformation, TimeSpan.FromSeconds(2)));
                    if (!feature.Mutexes.Any(x => x.HandleType.Equals(type, StringComparison.OrdinalIgnoreCase))) continue;
                    var name = NtApiNative.NormalizeKernelName(NtApiNative.QueryObjectString(copy, NtApiNative.ObjectNameInformation, TimeSpan.FromSeconds(2)));
                    var matched = feature.Mutexes.Any(x => x.HandleType.Equals(type, StringComparison.OrdinalIgnoreCase) && Matches(x.NamePattern, name));
                    if (!matched) continue;

                    candidates++;
                    if (NtApiNative.Duplicate(sourceProcess, entry.Handle, ProcessHandle(), out var closedCopy,
                        NtApiNative.DuplicateCloseSource | NtApiNative.DuplicateSameAccess))
                    {
                        CloseHandle(closedCopy);
                        closed++;
                        _logger.Info("handles", $"closed pid={entry.ProcessId}");
                    }
                    else failures.Add(new(entry.ProcessId, entry.Handle, "CloseSourceFailed"));
                }
                finally { CloseHandle(copy); }
            }
            finally { CloseHandle(sourceProcess); }
        }

        return new CloseResult(candidates, closed, failures);
    }

    internal static bool Matches(string pattern, string? value)
    {
        if (value is null) return false;
        if (!pattern.Contains('*')) return value.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        var parts = pattern.Split('*', StringSplitOptions.None);
        var index = 0;
        foreach (var part in parts)
        {
            if (part.Length == 0) continue;
            var found = value.IndexOf(part, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return false;
            index = found + part.Length;
        }
        return !pattern.StartsWith('*') ? value.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase) : true;
    }

    private static IntPtr ProcessHandle() => Process.GetCurrentProcess().Handle;

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}

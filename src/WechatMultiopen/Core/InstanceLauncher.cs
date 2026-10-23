using System.Diagnostics;
using WechatMultiopen.Infrastructure;
using WechatMultiopen.Models;

namespace WechatMultiopen.Core;

public sealed class InstanceLauncher
{
    private static readonly TimeSpan StabilityWindow = TimeSpan.FromSeconds(3);

    private readonly FileLogger _logger;
    public InstanceLauncher(FileLogger logger) => _logger = logger;

    public async Task<LaunchResult> LaunchAsync(string exePath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
        if (process is null) return new(false, null, "ProcessStartFailed");
        _logger.Info("launch", $"started pid={process.Id}");
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited) return new(false, process.Id, "ExitedImmediately");
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                // 互斥体可能被微信在窗口出现后重建并导致实例随即闪退，需观察一小段才计入成功。
                var stableUntil = DateTime.UtcNow + StabilityWindow;
                while (DateTime.UtcNow < stableUntil)
                {
                    await Task.Delay(500, cancellationToken);
                    process.Refresh();
                    if (process.HasExited)
                    {
                        _logger.Warn("launch", $"pid={process.Id} exited after window");
                        return new(false, process.Id, "ExitedAfterWindow");
                    }
                }
                return new(true, process.Id, null);
            }
            await Task.Delay(500, cancellationToken);
        }
        return new(false, process.Id, "WindowTimeout");
    }
}

using System.Diagnostics;
using WechatMultiopen.Infrastructure;
using WechatMultiopen.Models;

namespace WechatMultiopen.Core;

public sealed class InstanceLauncher
{
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
            if (process.MainWindowHandle != IntPtr.Zero) return new(true, process.Id, null);
            await Task.Delay(500, cancellationToken);
        }
        return new(false, process.Id, "WindowTimeout");
    }
}

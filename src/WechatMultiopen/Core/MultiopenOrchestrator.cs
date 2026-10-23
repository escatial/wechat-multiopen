using WechatMultiopen.Config;
using WechatMultiopen.Diagnostics;
using WechatMultiopen.Infrastructure;
using WechatMultiopen.Models;

namespace WechatMultiopen.Core;

public sealed class MultiopenOrchestrator
{
    private readonly WeChatDetector _detector;
    private readonly HandleCloserEngine _closer;
    private readonly InstanceLauncher _launcher;
    private readonly FileLogger _logger;

    public MultiopenOrchestrator(WeChatDetector detector, HandleCloserEngine closer, InstanceLauncher launcher, FeatureTable features, FileLogger logger)
    {
        _detector = detector;
        _closer = closer;
        _launcher = launcher;
        _logger = logger;
    }

    public async Task<OperationReport> RunAsync(int count, string? pathOverride, CancellationToken cancellationToken = default)
    {
        if (count is < 2 or > 4) throw new ArgumentOutOfRangeException(nameof(count));
        var target = WeChatDetector.Detect(pathOverride) ?? throw new InvalidOperationException(ErrorCatalog.Get(ErrorCode.WeChatNotFound).Reason);
        var feature = _detector.GetFeature(target) ?? throw new InvalidOperationException(ErrorCatalog.Get(ErrorCode.UnsupportedVersion).Reason);
        var errors = new List<string>();
        var succeeded = 0;

        for (var i = 0; i < count; i++)
        {
            var pids = WeChatDetector.FindRunningProcesses(target);
            _logger.Info("close", $"iteration={i + 1} target={target.ExePath} pids={string.Join(',', pids)} featureCount={feature.Mutexes.Length}");
            var close = _closer.CloseMutexes(pids, feature);
            _logger.Info("close", $"candidates={close.Candidates} closed={close.Closed} failures={close.Failures.Count}");
            var matchedAnyCandidate = close.Candidates > 0;
            if (close.Closed == 0)
            {
                await Task.Delay(100, cancellationToken);
                var retryPids = WeChatDetector.FindRunningProcesses(target);
                var retryClose = _closer.CloseMutexes(retryPids, feature);
                _logger.Info("close", $"retry candidates={retryClose.Candidates} closed={retryClose.Closed} failures={retryClose.Failures.Count}");
                matchedAnyCandidate |= retryClose.Candidates > 0;
            }

            var launch = await _launcher.LaunchAsync(target.ExePath, TimeSpan.FromSeconds(25), cancellationToken);
            if (!launch.Success)
            {
                var retryClose = _closer.CloseMutexes(WeChatDetector.FindRunningProcesses(target), feature);
                matchedAnyCandidate |= retryClose.Candidates > 0;
                var retry = retryClose.Closed > 0
                    ? await _launcher.LaunchAsync(target.ExePath, TimeSpan.FromSeconds(25), cancellationToken)
                    : launch;
                if (!retry.Success)
                {
                    // 已有实例却一条特征都没匹配上，说明是版本特征未适配，而不是启动本身的问题。
                    errors.Add(retry.FailReason == "WindowTimeout"
                        ? ErrorCatalog.Get(ErrorCode.WindowTimeout).Reason
                        : pids.Count > 0 && !matchedAnyCandidate
                            ? ErrorCatalog.Get(ErrorCode.MutexNotClosed).Reason
                            : ErrorCatalog.Get(ErrorCode.LaunchFailed).Reason);
                    break;
                }
                succeeded++;
                continue;
            }
            succeeded++;
        }

        return new OperationReport(count, succeeded, target, errors);
    }
}

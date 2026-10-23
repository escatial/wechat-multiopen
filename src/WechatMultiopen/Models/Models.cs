namespace WechatMultiopen.Models;

public enum WeChatBranch { V3, V4 }

public sealed record WeChatInfo(string ExePath, string FileVersion, WeChatBranch Branch)
{
    public string ExeName => Path.GetFileName(ExePath);
}

public sealed record MutexFeature(string NamePattern, string HandleType = "Mutant", string? Comment = null);
public sealed record BranchFeature(WeChatBranch Branch, string[] ExeNames, MutexFeature[] Mutexes, string? MinVersion = null, string? MaxVersion = null);
public sealed record FeatureTable(string SchemaVersion, BranchFeature[] Branches);

public sealed record HandleFailure(int Pid, IntPtr Handle, string Reason);
public sealed record CloseResult(int Candidates, int Closed, IReadOnlyList<HandleFailure> Failures)
{
    public bool HasConfirmedMatch => Candidates > 0;
}

public sealed record LaunchResult(bool Success, int? ProcessId, string? FailReason);
public sealed record OperationReport(int Requested, int Succeeded, WeChatInfo? WeChat, IReadOnlyList<string> Errors);

public sealed record ErrorInfo(ErrorCode Code, string Reason, string Advice);
public enum ErrorCode
{
    Success = 0,
    WeChatNotFound = 101,
    UnsupportedVersion = 102,
    InvalidPath = 103,
    ElevationDenied = 201,
    HandleEnumerationFailed = 301,
    MutexNotClosed = 302,
    ObjectQueryFailed = 303,
    LaunchFailed = 401,
    WindowTimeout = 402,
    Unknown = 500
}

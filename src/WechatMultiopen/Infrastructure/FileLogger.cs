namespace WechatMultiopen.Infrastructure;

public sealed class FileLogger
{
    private const long MaxLogBytes = 1024 * 1024;
    private const int KeepArchives = 3;

    private readonly object _gate = new();
    private bool _enabled;
    private string _path = string.Empty;

    public string Path => _path;

    public void Enable()
    {
        var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "wechat-multiopen");
        Directory.CreateDirectory(dir);
        _path = System.IO.Path.Combine(dir, "multiopen.log");
        RotateIfNeeded(dir);
        _enabled = true;
    }

    public void Info(string step, string message) => Write("INFO", step, message);
    public void Warn(string step, string message) => Write("WARN", step, message);
    public void Error(string step, string message) => Write("ERROR", step, message);

    private void RotateIfNeeded(string dir)
    {
        try
        {
            if (File.Exists(_path) && new FileInfo(_path).Length > MaxLogBytes)
            {
                var archive = System.IO.Path.Combine(dir, $"multiopen-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                File.Move(_path, archive);
            }
            foreach (var stale in Directory.GetFiles(dir, "multiopen-*.log")
                         .OrderByDescending(File.GetLastWriteTimeUtc)
                         .Skip(KeepArchives))
            {
                File.Delete(stale);
            }
        }
        catch
        {
            // 轮转失败不应阻断主流程，日志继续追加。
        }
    }

    private void Write(string level, string step, string message)
    {
        if (!_enabled) return;
        lock (_gate)
        {
            File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{step}] {message}{Environment.NewLine}");
        }
    }
}

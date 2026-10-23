namespace WechatMultiopen.Infrastructure;

public sealed class FileLogger
{
    private readonly object _gate = new();
    private bool _enabled;
    private string _path = string.Empty;

    public string Path => _path;

    public void Enable()
    {
        var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "wechat-multiopen");
        Directory.CreateDirectory(dir);
        _path = System.IO.Path.Combine(dir, "multiopen.log");
        _enabled = true;
    }

    public void Info(string step, string message) => Write("INFO", step, message);
    public void Warn(string step, string message) => Write("WARN", step, message);
    public void Error(string step, string message) => Write("ERROR", step, message);

    private void Write(string level, string step, string message)
    {
        if (!_enabled) return;
        lock (_gate)
        {
            File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{step}] {message}{Environment.NewLine}");
        }
    }
}

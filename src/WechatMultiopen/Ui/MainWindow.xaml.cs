using System.Windows;
using System.Windows.Input;
using WechatMultiopen.Infrastructure;

namespace WechatMultiopen.Ui;

public partial class MainWindow : Window
{
    private readonly ServiceSet _services;

    public MainWindow(ServiceSet services)
    {
        InitializeComponent();
        _services = services;
        _services.Logger.Enable();
        _services.Logger.Info("gui", "GUI started");
        var target = Core.WeChatDetector.Detect(null);
        WechatText.Text = target is null ? "未检测到微信，请确认已安装。" : $"微信：{target.ExePath}\n版本：{target.FileVersion} ({target.Branch})";
        StatusText.Text = Compliance.Notice;
        CountTextBox.Focus();
        CountTextBox.SelectAll();
    }

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        LaunchButton.IsEnabled = false;
        DoctorButton.IsEnabled = false;
        try
        {
            if (!TryGetCount(out var count)) return;
            var report = await _services.Orchestrator.RunAsync(count, null);
            StatusText.Text = $"已启动 {report.Succeeded}/{report.Requested} 个实例。" +
                              (report.Errors.Count == 0 ? string.Empty : $"\n{string.Join("\n", report.Errors)}");
            _services.Logger.Info("gui", $"completed succeeded={report.Succeeded} requested={report.Requested} errors={report.Errors.Count}");
        }
        catch (Exception ex)
        {
            var error = Diagnostics.ErrorCatalog.FromException(ex);
            StatusText.Text = $"[{error.Code}] {error.Reason}\n建议：{error.Advice}";
            _services.Logger.Error("gui", $"failed code={error.Code} message={ex.Message}");
        }
        finally { LaunchButton.IsEnabled = true; DoctorButton.IsEnabled = true; }
    }

    private void DoctorButton_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = _services.Doctor.Run(null);
    }

    private void IncreaseButton_Click(object sender, RoutedEventArgs e) => AdjustCount(1);

    private void DecreaseButton_Click(object sender, RoutedEventArgs e) => AdjustCount(-1);

    private void CountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(c => !char.IsDigit(c));
    }

    private void CountTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Up)
        {
            AdjustCount(1);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            AdjustCount(-1);
            e.Handled = true;
        }
    }

    private void CountTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!TryGetCount(out var count))
        {
            CountTextBox.Text = "2";
            return;
        }
        CountTextBox.Text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void AdjustCount(int delta)
    {
        _ = int.TryParse(CountTextBox.Text, out var current);
        var next = Math.Clamp(current + delta, 2, 4);
        CountTextBox.Text = next.ToString(System.Globalization.CultureInfo.InvariantCulture);
        CountTextBox.CaretIndex = CountTextBox.Text.Length;
    }

    private bool TryGetCount(out int count)
    {
        if (int.TryParse(CountTextBox.Text, out count) && count is >= 2 and <= 4) return true;
        count = 0;
        StatusText.Text = "实例数量必须是 2 到 4 之间的整数。";
        CountTextBox.Focus();
        CountTextBox.SelectAll();
        return false;
    }
}

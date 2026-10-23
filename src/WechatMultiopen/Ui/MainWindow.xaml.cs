using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WechatMultiopen.Core;
using WechatMultiopen.Infrastructure;
using WechatMultiopen.Models;

namespace WechatMultiopen.Ui;

public partial class MainWindow : Window
{
    private readonly ServiceSet _services;
    private string? _overridePath;

    public MainWindow(ServiceSet services)
    {
        InitializeComponent();
        _services = services;
        _services.Logger.Enable();
        _services.Logger.Info("gui", "GUI started");
        Title += $" v{AppInfo.Version}";
        VersionText.Text = $"v{AppInfo.Version}";
        RefreshTargetInfo();
        CountTextBox.Focus();
        CountTextBox.SelectAll();
    }

    private void RefreshTargetInfo()
    {
        var target = WeChatDetector.Detect(_overridePath);
        if (target is null)
        {
            WechatText.Text = _overridePath is null
                ? "未检测到微信，请确认已安装，或点击「更换路径」选择 WeChat.exe / Weixin.exe。"
                : $"路径无效：{_overridePath}";
            VersionChip.Visibility = Visibility.Collapsed;
            RunningChip.Visibility = Visibility.Collapsed;
            return;
        }
        var running = WeChatDetector.FindRunningProcesses(target).Count;
        WechatText.Text = target.ExePath;
        VersionChip.Visibility = Visibility.Visible;
        VersionChipText.Text = $"{target.FileVersion} ({target.Branch})";
        RunningChip.Visibility = Visibility.Visible;
        RunningChipText.Text = running > 0 ? $"已运行 {running} 个实例" : "当前未运行";
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择微信主程序",
            Filter = "微信主程序|WeChat.exe;Weixin.exe",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        _overridePath = dialog.FileName;
        _services.Logger.Info("gui", $"path override={_overridePath}");
        RefreshTargetInfo();
    }

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        LaunchButton.IsEnabled = false;
        DoctorButton.IsEnabled = false;
        BrowseButton.IsEnabled = false;
        LaunchButton.Content = "启动中…";
        try
        {
            if (!TryGetCount(out var count)) return;
            var report = await _services.Orchestrator.RunAsync(count, _overridePath);
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
        finally
        {
            LaunchButton.Content = "启动实例";
            LaunchButton.IsEnabled = true;
            DoctorButton.IsEnabled = true;
            BrowseButton.IsEnabled = true;
            RefreshTargetInfo();
        }
    }

    private void DoctorButton_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = _services.Doctor.Run(_overridePath);
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

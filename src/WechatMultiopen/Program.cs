using System.Windows;
using WechatMultiopen.Cli;
using WechatMultiopen.Core;
using WechatMultiopen.Diagnostics;
using WechatMultiopen.Infrastructure;
using WechatMultiopen.Native;
using WechatMultiopen.Ui;

namespace WechatMultiopen;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0) ConsoleHost.AttachToParentOrCreate();
        try
        {
            if (args.Length == 0)
            {
                var app = new App();
                app.InitializeComponent();
                app.Run(new MainWindow(BuildServices()));
                return 0;
            }

            return new CliApp(BuildServices()).Run(args);
        }
        catch (Exception ex)
        {
            var error = ErrorCatalog.FromException(ex);
            if (args.Length == 0)
            {
                MessageBox.Show($"[{error.Code}] {error.Reason}\n建议：{error.Advice}", "微信多开助手", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else Console.Error.WriteLine($"[{error.Code}] {error.Reason}\n建议：{error.Advice}");
            return (int)error.Code;
        }
    }

    private static ServiceSet BuildServices()
    {
        var logger = new FileLogger();
        var features = Config.FeatureTableLoader.Load();
        var native = new NtApiNative();
        var detector = new WeChatDetector(features);
        var closer = new HandleCloserEngine(native, features, logger);
        var launcher = new InstanceLauncher(logger);
        var doctor = new DoctorService(detector, features, native);
        var orchestrator = new MultiopenOrchestrator(detector, closer, launcher, features, logger);
        return new ServiceSet(orchestrator, doctor, detector, logger);
    }
}

public sealed record ServiceSet(
    MultiopenOrchestrator Orchestrator,
    DoctorService Doctor,
    WeChatDetector Detector,
    FileLogger Logger);

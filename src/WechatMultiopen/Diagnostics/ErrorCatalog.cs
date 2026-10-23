using WechatMultiopen.Models;
using WechatMultiopen.Cli;

namespace WechatMultiopen.Diagnostics;

public static class ErrorCatalog
{
    public static ErrorInfo Get(ErrorCode code) => code switch
    {
        ErrorCode.WeChatNotFound => new(code, "未检测到微信主程序。", "请确认微信已安装，或使用 -p 指定 WeChat.exe/Weixin.exe 的完整路径。"),
        ErrorCode.UnsupportedVersion => new(code, "当前微信版本暂不支持。", "请先运行 --doctor；确认版本特征后再更新特征配置。"),
        ErrorCode.InvalidPath => new(code, "指定的微信路径无效。", "请确认文件存在且路径指向 WeChat.exe 或 Weixin.exe。"),
        ErrorCode.ElevationDenied => new(code, "工具需要管理员权限才能操作进程句柄。", "请在 UAC 中允许，或右键使用管理员身份运行。"),
        ErrorCode.HandleEnumerationFailed => new(code, "系统句柄枚举失败。", "请运行 --doctor 检查权限与安全软件拦截情况。"),
        ErrorCode.MutexNotClosed => new(code, "未能确认或关闭微信多开对象。", "当前版本特征可能未适配，请保留日志并运行 --doctor。"),
        ErrorCode.ObjectQueryFailed => new(code, "部分对象名称查询失败。", "请重试；如果持续失败，请检查安全软件拦截。"),
        ErrorCode.LaunchFailed => new(code, "微信实例启动后立即退出。", "请关闭安全软件实时防护后重试，或减少实例数量。"),
        ErrorCode.WindowTimeout => new(code, "微信窗口在限定时间内未就绪。", "请重试或减少实例数量，并确认微信没有被系统拦截。"),
        _ => new(ErrorCode.Unknown, "发生未分类错误。", "请查看日志文件并反馈错误码。")
    };

    public static ErrorInfo FromException(Exception exception) => exception switch
    {
        CliArgumentException => new(ErrorCode.InvalidPath, exception.Message, "请使用 --help 查看正确用法。"),
        ArgumentOutOfRangeException => new(ErrorCode.InvalidPath, "命令参数超出允许范围。", "实例数量必须是 2 到 4 之间的整数。"),
        UnauthorizedAccessException => Get(ErrorCode.ElevationDenied),
        FileNotFoundException => Get(ErrorCode.InvalidPath),
        _ => Get(ErrorCode.Unknown)
    };
}

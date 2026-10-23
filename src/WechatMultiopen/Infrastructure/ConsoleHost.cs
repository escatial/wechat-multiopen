using System.Runtime.InteropServices;

namespace WechatMultiopen.Infrastructure;

public static class ConsoleHost
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    public static void AttachToParentOrCreate()
    {
        if (!AttachConsole(AttachParentProcess)) AllocConsole();
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
}

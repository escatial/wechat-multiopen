using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WechatMultiopen.Native;

public sealed class NtApiNative
{
    public const uint ProcessDupHandle = 0x0040;
    public const uint DuplicateCloseSource = 0x00000001;
    public const uint DuplicateSameAccess = 0x00000002;
    public const int SystemExtendedHandleInformation = 64;
    public const int ObjectNameInformation = 1;
    public const int ObjectTypeInformation = 2;
    public const uint StatusInfoLengthMismatch = 0xC0000004;

    public static IReadOnlyList<HandleEntry> EnumerateHandles()
    {
        var size = 1024 * 1024;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                var status = NtQuerySystemInformation(SystemExtendedHandleInformation, buffer, size, out var length);
                if (status == 0)
                {
                    var count = Marshal.ReadIntPtr(buffer).ToInt64();
                    var offset = IntPtr.Size * 2;
                    var itemSize = IntPtr.Size == 8 ? 40 : 24;
                    var result = new List<HandleEntry>((int)Math.Min(count, 100000));
                    for (long i = 0; i < count && offset + itemSize <= size; i++, offset += itemSize)
                    {
                        var objectPtr = Marshal.ReadIntPtr(buffer, offset);
                        var pid = Marshal.ReadIntPtr(buffer, offset + IntPtr.Size).ToInt64();
                        var handle = Marshal.ReadIntPtr(buffer, offset + IntPtr.Size * 2);
                        var access = unchecked((uint)Marshal.ReadInt32(buffer, offset + IntPtr.Size * 3));
                        result.Add(new HandleEntry((int)pid, handle, access, objectPtr));
                    }
                    return result;
                }

                if (status != StatusInfoLengthMismatch) throw new Win32Exception(unchecked((int)status));
                size = Math.Max(size * 3 / 2, length + 4096);
                if (size > 256 * 1024 * 1024) throw new InvalidOperationException("系统句柄表超过安全上限");
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new InvalidOperationException("句柄表扩容重试次数已耗尽");
    }

    public static IntPtr OpenProcess(int pid) => OpenProcess(ProcessDupHandle, false, pid);

    public static bool Duplicate(IntPtr sourceProcess, IntPtr sourceHandle, IntPtr targetProcess, out IntPtr targetHandle, uint options)
        => DuplicateHandle(sourceProcess, sourceHandle, targetProcess, out targetHandle, 0, false, options);

    public static string? QueryObjectString(IntPtr handle, int informationClass, TimeSpan timeout)
    {
        var task = Task.Run(() => QueryObjectStringCore(handle, informationClass));
        return task.Wait(timeout) ? task.Result : null;
    }

    public static string? NormalizeKernelName(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Replace('/', '\\');

    private static string? QueryObjectStringCore(IntPtr handle, int informationClass)
    {
        var size = 4096;
        for (var i = 0; i < 4; i++)
        {
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                var status = NtQueryObject(handle, informationClass, buffer, size, out var length);
                if (status == 0)
                {
                    var textLength = Marshal.ReadInt16(buffer);
                    var textBuffer = Marshal.ReadIntPtr(buffer, IntPtr.Size == 8 ? 8 : 4);
                    return textBuffer == IntPtr.Zero || textLength == 0
                        ? null
                        : Marshal.PtrToStringUni(textBuffer, textLength / 2);
                }
                if (status != StatusInfoLengthMismatch) return null;
                size = Math.Max(size * 2, length + 256);
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        return null;
    }

    [DllImport("ntdll.dll")]
    private static extern uint NtQuerySystemInformation(int informationClass, IntPtr buffer, int length, out int returnLength);

    [DllImport("ntdll.dll")]
    private static extern uint NtQueryObject(IntPtr handle, int informationClass, IntPtr buffer, int length, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DuplicateHandle(IntPtr sourceProcess, IntPtr sourceHandle, IntPtr targetProcess,
        out IntPtr targetHandle, uint desiredAccess, bool inheritHandle, uint options);

    public readonly record struct HandleEntry(int ProcessId, IntPtr Handle, uint GrantedAccess, IntPtr ObjectAddress);
}

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Tada.RuntimeProbe;

internal static class NativeModulePath
{
    internal enum VolumeName : uint { Dos = 0, Nt = 2 }

    internal static bool IsDevicePath(string path) => path.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase);

    internal static string ToWin32DevicePath(string path)
    {
        // GLOBALROOT opens the NT path directly. Extended Win32 paths bypass separator normalization.
        return @"\\?\GLOBALROOT\" + string.Join('\\', path.Split('\\', StringSplitOptions.RemoveEmptyEntries));
    }

    internal static string GetDosPath(FileStream module)
    {
        const string extended = @"\\?\";
        const string extendedUnc = @"\\?\UNC\";
        string path = GetPathByHandle(module, VolumeName.Dos);
        if (path.StartsWith(extendedUnc, StringComparison.OrdinalIgnoreCase))
            path = @"\\" + path[extendedUnc.Length..];
        else if (path.StartsWith(extended, StringComparison.Ordinal))
            path = path[extended.Length..];
        if (!Path.IsPathFullyQualified(path))
            throw new IOException($"Native module did not resolve to a fully qualified Win32 path: {path}");
        return path;
    }

    internal static string GetPathByHandle(FileStream module, VolumeName volumeName)
    {
        // .NET exposes the path used to open the stream. Windows resolves the final path
        // from the same handle the observer hashes, including the volume name.
        SafeFileHandle handle = module.SafeFileHandle;
        var buffer = new StringBuilder(512);
        while (true)
        {
            uint length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, volumeName);
            if (length == 0)
            {
                var error = new Win32Exception(Marshal.GetLastPInvokeError());
                throw new IOException($"Cannot resolve the path of {module.Name}: {error.Message}", error);
            }
            if (length < buffer.Capacity) return buffer.ToString();
            buffer.EnsureCapacity(checked((int)length + 1));
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint capacity, VolumeName volumeName);
}

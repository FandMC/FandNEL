using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using Serilog;

namespace FandNEL;

/// <summary>在读取账号和启动器资源前迁入旧数据，只删除与新位置内容一致的旧文件。</summary>
internal static class ApplicationDataMigration
{
    private static string LegacyDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FandNEL");

    public static void ImportIfMissing(string destinationDirectory)
    {
        var sourceDirectory = LegacyDirectory;
        var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationDirectory));
        sourceDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDirectory));
        if (!Directory.Exists(sourceDirectory)
            || string.Equals(sourceDirectory, destination, StringComparison.OrdinalIgnoreCase))
            return;

        if (IsInside(sourceDirectory, destination) || IsInside(destination, sourceDirectory))
            throw new IOException("旧数据目录与程序目录互相包含，无法安全迁移。");
        RejectLink(sourceDirectory);
        RejectLink(destination);
        foreach (var name in new[] { "users.json", "cppusers.json" })
            ImportEntry(Path.Combine(sourceDirectory, name), Path.Combine(destination, name), isDirectory: false);

        var launcherDirectory = Path.Combine(sourceDirectory, "launcher");
        if (!Directory.Exists(launcherDirectory)) return;
        RejectLink(launcherDirectory);
        foreach (var name in new[] { ".game_cache", "resources" })
            ImportEntry(Path.Combine(launcherDirectory, name), Path.Combine(destination, name), isDirectory: true);
    }

    /// <summary>仅在唯一账号仓库成功读取 users.json 后清理已停用的 DPAPI 文件。</summary>
    public static void RemoveObsoleteAccountFiles(string applicationDirectory)
    {
        if (!File.Exists(Path.Combine(applicationDirectory, "users.json"))) return;
        foreach (var directory in new[] { Path.GetFullPath(applicationDirectory), Path.GetFullPath(LegacyDirectory) }
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = Path.Combine(directory, "accounts.json.dpapi");
            var lockPath = path + ".lock";
            if (!File.Exists(path) && !File.Exists(lockPath)) continue;
            try
            {
                RejectLink(directory);
                if (File.Exists(path)) RejectLink(path);
                if (File.Exists(lockPath)) RejectLink(lockPath);
                // 与旧版加密仓库使用同一锁文件，不能在它写入时清理。
                using (var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    if (File.Exists(path))
                    {
                        ClearReadOnly(path);
                        File.Delete(path);
                    }
                }
                ClearReadOnly(lockPath);
                File.Delete(lockPath);
                Log.Information("已删除停用的 accounts.json.dpapi 及其锁文件。");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.Warning("废弃账号文件暂时无法删除（{ErrorType}），下次启动重试。", exception.GetType().Name);
            }
        }
    }

    private static void ImportEntry(string source, string destination, bool isDirectory)
    {
        if (!(isDirectory ? Directory.Exists(source) : File.Exists(source)))
            return;
        RejectLink(source);
        // 新位置已有内容时直接核对，不能覆盖用户已更新的账号或缓存。
        if (!Path.Exists(destination)) CopyEntry(source, destination, isDirectory);

        var removed = isDirectory
            ? RemoveMatchingDirectory(source, destination)
            : RemoveMatchingFile(source, destination);
        var name = Path.GetFileName(destination);
        if (removed)
            Log.Information("旧版数据 {Name} 已迁入程序目录，原位置对应数据已清理。", name);
        else
            Log.Warning("旧版数据 {Name} 存在内容不同、目标缺失或被占用的文件，已保留这些原文件。", name);
    }

    private static void CopyEntry(string source, string destination, bool isDirectory)
    {
        var temporary = destination + $".{Guid.NewGuid():N}.importing";
        var name = Path.GetFileName(destination);
        Log.Information("正在复制旧版数据 {Name} 到程序目录。", name);
        try
        {
            RejectLink(source);
            if (isDirectory)
            {
                CopyDirectory(source, temporary);
                Directory.Move(temporary, destination);
            }
            else
            {
                CopyFile(source, temporary);
                File.Move(temporary, destination);
            }
        }
        catch (IOException) when (Path.Exists(destination))
        {
            // 另一个实例已经完成复制；始终保留先到的新位置数据。
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"无法将旧版 {name} 复制到程序目录，请检查目录权限和可用空间。原数据未修改。", exception);
        }
        finally
        {
            try
            {
                // 暂存路径使用本次操作生成的唯一名称，不接触原目录和正式目标。
                if (isDirectory && Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
                else if (File.Exists(temporary)) File.Delete(temporary);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.Warning("未能清理 {Name} 的迁移暂存文件（{ErrorType}）。", name, exception.GetType().Name);
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            RejectLink(entry.FullName);
            var target = Path.Combine(destination, entry.Name);
            if (entry is DirectoryInfo)
                CopyDirectory(entry.FullName, target);
            else
                CopyFile(entry.FullName, target);
        }
    }

    private static void CopyFile(string source, string destination)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
        output.Flush(flushToDisk: true);
    }

    private static bool RemoveMatchingDirectory(string source, string destination)
    {
        try
        {
            if (!Directory.Exists(source)) return true;
            if (!Directory.Exists(destination)) return false;
            RejectLink(source);
            RejectLink(destination);
            var removedAll = true;
            foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
            {
                var target = Path.Combine(destination, entry.Name);
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    removedAll = false;
                else if (entry is DirectoryInfo)
                    removedAll &= RemoveMatchingDirectory(entry.FullName, target);
                else
                    removedAll &= RemoveMatchingFile(entry.FullName, target);
            }
            // 只删除空目录；复制期间新建或变动的文件不能被递归删除。
            if (!removedAll) return false;
            Directory.Delete(source, recursive: false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool RemoveMatchingFile(string source, string destination)
    {
        try
        {
            if (!File.Exists(source)) return true;
            if (!File.Exists(destination)) return false;
            RejectLink(source);
            RejectLink(destination);
            ClearReadOnly(source);
            // 同一源句柄完成核对和删除，禁止其他实例在核对后替换源路径。
            // 目标先打开、最后关闭，确保旧文件删除前新文件一直有效。
            using var output = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sourceHandle = CreateFileW(source, GenericRead | DeleteAccess, FileShare.Read,
                IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);
            if (sourceHandle.IsInvalid) return false;
            using var input = new FileStream(sourceHandle, FileAccess.Read);
            if (input.Length != output.Length
                || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(input), SHA256.HashData(output)))
                return false;
            byte deleteFile = 1;
            return SetFileInformationByHandle(sourceHandle, FileDispositionInfo, ref deleteFile, sizeof(byte));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private const uint GenericRead = 0x80000000;
    private const uint DeleteAccess = 0x00010000;
    private const int FileDispositionInfo = 4;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, FileShare shareMode,
        IntPtr securityAttributes, FileMode creationDisposition, FileAttributes flagsAndAttributes, IntPtr templateFile);

    // FILE_DISPOSITION_INFO.DeleteFile 是单字节 BOOLEAN，不是四字节 Win32 BOOL。
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int informationClass,
        ref byte deleteFile, uint bufferSize);

    private static bool IsInside(string parent, string candidate) =>
        candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void ClearReadOnly(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("旧版数据中存在目录链接或文件链接，无法自动复制。");
    }
}

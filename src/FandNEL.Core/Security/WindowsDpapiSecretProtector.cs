using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace FandNEL.Core.Security;

/// <summary>
/// Protects data with Windows DPAPI for the current Windows user.
/// No external package is required; the implementation calls CryptProtectData directly.
/// </summary>
public sealed class WindowsDpapiSecretProtector : ISecretProtector
{
    public byte[] Protect(ReadOnlySpan<byte> plaintext)
    {
        EnsureWindows();
        var input = new DataBlob(plaintext);

        try
        {
            if (!CryptProtectData(ref input.Blob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output))
            {
                throw CreateWin32Exception(nameof(CryptProtectData));
            }

            return CopyAndFree(output);
        }
        finally
        {
            input.Dispose();
        }
    }

    public byte[] Unprotect(ReadOnlySpan<byte> ciphertext)
    {
        EnsureWindows();
        var input = new DataBlob(ciphertext);

        try
        {
            if (!CryptUnprotectData(ref input.Blob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output))
            {
                throw CreateWin32Exception(nameof(CryptUnprotectData));
            }

            return CopyAndFree(output);
        }
        finally
        {
            input.Dispose();
        }
    }

    private static byte[] CopyAndFree(NativeDataBlob output)
    {
        try
        {
            if (output.cbData == 0)
            {
                return [];
            }

            var result = new byte[output.cbData];
            Marshal.Copy(output.pbData, result, 0, result.Length);
            return result;
        }
        finally
        {
            if (output.pbData != IntPtr.Zero)
            {
                ZeroMemory(output.pbData, output.cbData);
                LocalFree(output.pbData);
            }
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows DPAPI is only available on Windows.");
        }
    }

    private static Win32Exception CreateWin32Exception(string operation) =>
        new(Marshal.GetLastWin32Error(), $"{operation} failed.");

    private sealed class DataBlob : IDisposable
    {
        private IntPtr _buffer;

        public DataBlob(ReadOnlySpan<byte> bytes)
        {
            _buffer = Marshal.AllocHGlobal(bytes.Length == 0 ? 1 : bytes.Length);
            if (!bytes.IsEmpty)
            {
                var copy = bytes.ToArray();
                try
                {
                    Marshal.Copy(copy, 0, _buffer, copy.Length);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(copy);
                }
            }

            Blob = new NativeDataBlob
            {
                cbData = bytes.Length,
                pbData = _buffer
            };
        }

        public NativeDataBlob Blob;

        public void Dispose()
        {
            var buffer = Interlocked.Exchange(ref _buffer, IntPtr.Zero);
            if (buffer != IntPtr.Zero)
            {
                ZeroMemory(buffer, Blob.cbData);
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeDataBlob
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref NativeDataBlob pDataIn,
        [MarshalAs(UnmanagedType.LPWStr)] string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out NativeDataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref NativeDataBlob pDataIn,
        IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out NativeDataBlob pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern void RtlZeroMemory(IntPtr destination, nuint length);

    private static void ZeroMemory(IntPtr address, int length)
    {
        if (length > 0)
        {
            RtlZeroMemory(address, (nuint)length);
        }
    }
}

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HalfSwordTrainer;

internal sealed class Memory : IDisposable
{
    private readonly SafeProcessHandle handle;
    public Process Process { get; }
    public long Base { get; }
    public int Size { get; }

    public Memory(bool writable = false)
    {
        var matches = Process.GetProcessesByName("HalfSwordUE5-Win64-Shipping");
        if (matches.Length != 1)
            throw new InvalidOperationException($"Expected one running Half Sword game; found {matches.Length}.");
        Process = matches[0];
        var module = Process.MainModule ?? throw new InvalidOperationException("Cannot locate game module.");
        Base = module.BaseAddress;
        Size = module.ModuleMemorySize;
        handle = OpenProcess(0x1000u | 0x10u | (writable ? 0x28u : 0), false, Process.Id);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot open game. Run at the same elevation as the game.");
    }

    public byte[] Read(long address, int size)
    {
        var bytes = new byte[size];
        if (!ReadProcessMemory(handle, (nint)address, bytes, (nuint)size, out var read) || read != (nuint)size)
        {
            int error = Marshal.GetLastWin32Error();
            if (error is 299 or 487 or 998 && !Process.HasExited)
                throw new GameTransitionException($"Game memory changed while reading 0x{address:X}; waiting for loading.");
            throw new Win32Exception(error, $"Cannot read 0x{address:X} ({size} bytes).");
        }
        return bytes;
    }

    public bool TryRead(long address, int size, out byte[] bytes)
    {
        bytes = new byte[size];
        return address > 0x10000 && ReadProcessMemory(handle, (nint)address, bytes, (nuint)size, out var read) && read == (nuint)size;
    }

    public T Read<T>(long address) where T : unmanaged =>
        MemoryMarshal.Read<T>(Read(address, Marshal.SizeOf<T>()));

    public void Write(long address, ReadOnlySpan<byte> bytes)
    {
        var buffer = bytes.ToArray();
        if (!WriteProcessMemory(handle, (nint)address, buffer, (nuint)buffer.Length, out var written) || written != (nuint)buffer.Length)
        {
            int error = Marshal.GetLastWin32Error();
            if (error is 299 or 487 or 998 && !Process.HasExited)
                throw new GameTransitionException($"Game memory changed while writing 0x{address:X}; waiting for loading.");
            throw new Win32Exception(error, $"Cannot write 0x{address:X}.");
        }
    }

    public IEnumerable<(string Name, long Address, int Size)> Sections()
    {
        var pe = Base + Read<int>(Base + 0x3c);
        int count = Read<ushort>(pe + 6);
        long section = pe + 24 + Read<ushort>(pe + 20);
        for (int i = 0; i < count; i++, section += 40)
        {
            var data = Read(section, 40);
            yield return (System.Text.Encoding.ASCII.GetString(data, 0, 8).TrimEnd('\0'),
                Base + BitConverter.ToUInt32(data, 12), BitConverter.ToInt32(data, 8));
        }
    }

    public void Dispose() { handle.Dispose(); Process.Dispose(); }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(SafeProcessHandle process, nint address, [Out] byte[] buffer, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(SafeProcessHandle process, nint address, byte[] buffer, nuint size, out nuint written);
}

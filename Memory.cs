using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HalfSwordTrainer;

internal readonly record struct GameProcessIdentity(int Id, DateTime StartedUtc);

internal sealed class Memory : IDisposable
{
    private readonly SafeProcessHandle handle;
    public Process Process { get; }
    public long Base { get; }
    public int Size { get; }
    public GameProcessIdentity Identity { get; }

    public Memory(bool writable = false)
    {
        var matches = Process.GetProcessesByName("HalfSwordUE5-Win64-Shipping");
        if (matches.Length == 0) throw new GameUnavailableException();
        if (matches.Length != 1)
        {
            foreach (var match in matches) match.Dispose();
            throw new InvalidOperationException($"Expected one running Half Sword game; found {matches.Length}.");
        }
        Process = matches[0];
        try
        {
            if (Process.HasExited) throw new GameUnavailableException();
            Identity = new(Process.Id, Process.StartTime.ToUniversalTime());
            var module = Process.MainModule ?? throw new InvalidOperationException("Cannot locate game module.");
            Base = module.BaseAddress;
            Size = module.ModuleMemorySize;
            handle = OpenProcess(0x1000u | 0x10u | (writable ? 0x28u : 0), false, Process.Id);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                if (Process.HasExited) throw new GameUnavailableException();
                throw new Win32Exception(error, "Cannot open game. Run at the same elevation as the game.");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            bool exited = Process.HasExited;
            Process.Dispose();
            if (exited) throw new GameUnavailableException();
            throw;
        }
    }

    public byte[] Read(long address, int size)
    {
        if (Process.HasExited) throw new GameUnavailableException();
        var bytes = new byte[size];
        if (!ReadProcessMemory(handle, (nint)address, bytes, (nuint)size, out var read) || read != (nuint)size)
        {
            int error = Marshal.GetLastWin32Error();
            if (Process.HasExited) throw new GameUnavailableException();
            if (error is 299 or 487 or 998 && !Process.HasExited)
                throw new GameTransitionException($"Game memory changed while reading 0x{address:X}; waiting for loading.");
            throw new Win32Exception(error, $"Cannot read 0x{address:X} ({size} bytes).");
        }
        return bytes;
    }

    public bool TryRead(long address, int size, out byte[] bytes)
    {
        if (Process.HasExited) throw new GameUnavailableException();
        bytes = new byte[size];
        return address > 0x10000 && ReadProcessMemory(handle, (nint)address, bytes, (nuint)size, out var read) && read == (nuint)size;
    }

    public T Read<T>(long address) where T : unmanaged =>
        MemoryMarshal.Read<T>(Read(address, Marshal.SizeOf<T>()));

    public void Write(long address, ReadOnlySpan<byte> bytes)
    {
        if (Process.HasExited) throw new GameUnavailableException();
        var buffer = bytes.ToArray();
        if (!WriteProcessMemory(handle, (nint)address, buffer, (nuint)buffer.Length, out var written) || written != (nuint)buffer.Length)
        {
            int error = Marshal.GetLastWin32Error();
            if (Process.HasExited) throw new GameUnavailableException();
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

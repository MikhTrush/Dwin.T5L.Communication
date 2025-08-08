using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Dwin.T5L.Communication;

public class DwinMemory : IDwinMemory
{
    //Everything in memory should be in big-endian format, but we will convert it to little-endian if necessary
    public Memory<UInt16> Memory { get; } = new UInt16[0xFFFF];
    private readonly int MaxMessageLength = 128;
    public readonly object obj;
    public readonly bool IsLittleEndian = BitConverter.IsLittleEndian;

    public DwinMemory(object locker)
    {
        obj = locker ?? throw new ArgumentNullException(nameof(locker));
    }

    public IDwinMemory CreateDwinMemory(object locker)
    {
        return new DwinMemory(locker);
    }

    public void WriteDataToMemoryBEbytes(UInt16 addr, byte[] data)
    {
        if (data.Length / 2 + addr >= Memory.Length)
            throw new ArgumentOutOfRangeException(nameof(data));
        if (data.Length % 2 != 0)
            throw new ArgumentException("Data length must be even.");

        lock (obj)
        {
            // Копируем байты из data в выделенную память mem начиная с addr
            var bytesSpan = MemoryMarshal.AsBytes(Memory.Slice(addr, data.Length / 2).Span);
            data.CopyTo(bytesSpan);
            //At the end, it's bigendian, so we need to convert it to little-endian if necessary
        }
    }

    public void WriteUInt16AsBEbytesToMemory(UInt16 addr, UInt16 data)
    {
        if (addr >= Memory.Length)
            throw new ArgumentOutOfRangeException(nameof(addr));
        if (IsLittleEndian)
        {
            var bytes = BitConverter.GetBytes(data);
            (bytes[0], bytes[1]) = (bytes[1], bytes[0]); // Swap bytes for little-endian
            data = BitConverter.ToUInt16(bytes, 0);
        }
        lock (obj)
        {
            Memory.Span[addr] = data;
        }
    }

    public void WriteUInt16AsBEbytesArrayToMemory(UInt16 addr, UInt16[] data)
    {
        if (data.Length + addr >= Memory.Length)
            throw new ArgumentOutOfRangeException(nameof(data));
        if (IsLittleEndian)
        {
            // Convert to little-endian if necessary
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (ushort)((data[i] >> 8) | (data[i] << 8));
            }
        }
        lock (obj)
        {
            data.AsSpan().CopyTo(Memory.Slice(addr, data.Length).Span);
        }
    }

    public void WriteStringToMemory(string message, UInt16 address, Encoding enc)
    {
        if (message.Length > MaxMessageLength)
        {
            message = message.Substring(0, MaxMessageLength);
        }
        // Convert the string to bytes and write to memory
        var messageBytes = enc.GetBytes(message);
        if (messageBytes.Length % 2 != 0)
        {
            // If the length is odd, add a padding byte
            Array.Resize(ref messageBytes, messageBytes.Length + 1);
        }
        var bytesSpan = MemoryMarshal.AsBytes(Memory.Slice(address, messageBytes.Length / 2).Span);
        lock (obj)
        {
            bytesSpan.Clear(); // Clear the memory before writing
            messageBytes.CopyTo(bytesSpan);
        }
    }

    public byte[] ReadDataFromMemoryAsBytes(UInt16 addr, UInt16 length)
    {
        lock (obj)
        {
            return MemoryMarshal.AsBytes(Memory.Slice(addr, length).Span).ToArray();
        }
    }

    public ushort ReadUInt16FromMemory(UInt16 addr)
    {
        if (addr >= Memory.Length)
            throw new ArgumentOutOfRangeException(nameof(addr));
        //Memory is in big-endian format, so we need to convert it to little-endian if necessary

        lock (obj)
        {
            if (!IsLittleEndian)
            {
                return Memory.Span[addr];
            }
            var me = MemoryMarshal.AsBytes(Memory.Slice(addr, 1).Span);
            return BitConverter.ToUInt16([me[1], me[0]], 0);
        }
    }

    public ushort[] ReadUInt16ArrayFromMemory(UInt16 addr, int length)
    {
        if (length + addr >= Memory.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        lock (obj)
        {
            var s = Memory.Slice(addr, length).Span.ToArray();
            if (!IsLittleEndian)
                return s;

            // Convert from big-endian to little-endian if necessary
            for (int i = 0; i < s.Length; i++)
            {
                s[i] = (ushort)((s[i] >> 8) | (s[i] << 8));
            }
            return s;
        }
    }

    public string ReadStringFromMemory(UInt16 addr, UInt16 length, Encoding enc)
    {
        if (addr + length >= Memory.Length)
            throw new ArgumentOutOfRangeException(nameof(addr));
        lock (obj)
        {
            var bytes = MemoryMarshal.AsBytes(Memory.Slice(addr, length).Span).ToArray();
            return enc.GetString(bytes).TrimEnd('\0'); // Trim null characters
        }
    }

    private void Log(string str)
    {
        Console.WriteLine(str);
    }

}

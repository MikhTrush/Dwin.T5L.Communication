using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dwin.T5L.Communication;

public static class Crc16
{
    // Таблица предварительно вычисленных значений CRC(lookup table)
    private static readonly UInt16[] CrcTable = new UInt16[256];
    private const UInt16 Polynomial = 0xA001; // Реверсированный полином 0x8005

    // Статический конструктор для заполнения таблицы
    static Crc16()
    {
        for (UInt16 i = 0; i < 256; i++)
        {
            UInt16 crc = i;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x0001) != 0)
                    crc = (UInt16)((crc >> 1) ^ Polynomial);
                else
                    crc >>= 1;
            }
            CrcTable[i] = crc;
        }
    }

    // Вычисление CRC-16 Modbus с использованием lookup table
    public static UInt16 ComputeCheckSum(byte[] bytes)
    {
        UInt16 crc = 0xFFFF; // Начальное значение

        foreach (byte b in bytes)
        {
            crc = (UInt16)((crc >> 8) ^ CrcTable[(crc ^ b) & 0xFF]);
        }

        return crc;
    }

    public static UInt16 ComputeCheckSum(ReadOnlySpan<byte> bytes)
    {
        UInt16 crc = 0xFFFF;
        foreach (byte b in bytes)
        {
            crc = (UInt16)((crc >> 8) ^ CrcTable[(crc ^ b) & 0xFF]);
        }
        return crc;
    }

    public static byte[] GetChecksumBytes(byte[] data)
    {
        UInt16 crc = ComputeCheckSum(data);
        return [(byte)(crc & 0xFF), (byte)(crc >> 8)];
    }

    public static byte[] GetChecksumBytes(ReadOnlySpan<byte> data)
    {
        UInt16 crc = ComputeCheckSum(data);
        return [(byte)(crc & 0xFF), (byte)(crc >> 8)];
    }

    public static bool IsCrcCorrect(byte[] receivedData, UInt16 receivedCrc)
    {
        return ComputeCheckSum(receivedData) == receivedCrc;
    }

    public static bool IsCrcCorrect(ReadOnlySpan<byte> receivedData, UInt16 receivedCrc)
    {
        return ComputeCheckSum(receivedData) == receivedCrc;
    }

    public static bool IsCrcCorrect(byte[] receivedData, byte[] crc)
    {
        if (crc.Length != 2)
            throw new ArgumentException("Crc length should be 2");
        return ComputeCheckSum(receivedData) == (UInt16)(crc[1] << 8 | crc[0]);
    }
}
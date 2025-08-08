using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;

namespace Dwin.T5L.Communication;

public class DwinProtocol : IDwinProtocol
{
    public const byte Header1 = 0x5A;
    public const byte Header2 = 0xA5;

    const int HeaderLength = 2;
    const int LengthOffset = 2;

    public const byte WriteCmd = 0x82;
    public const byte ReadCmd = 0x83;

    private const int MinAnswerLength = 8;
    private static readonly byte[] WriteAnswerBytes = [0x4F, 0x4B];
    private readonly int MinDataLength = 5;

    public void SendWritingCommand(UInt16 address, byte[] data, SerialPort serialPort)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data), "Data buffer for writing command cannot be null.");
        if (serialPort == null)
            throw new ArgumentNullException(nameof(serialPort), "Serial port for writing command cannot be null.");
        serialPort.DiscardOutBuffer();
        var command = ConstructCommand(WriteCmd, address, data.AsSpan());
        serialPort.Write(command, 0, command.Length);
    }

    public void SendReadingCommand(UInt16 address, byte length, SerialPort serialPort)
    {
        if (serialPort == null)
            throw new ArgumentNullException(nameof(serialPort), "Serial port for reading command cannot be null.");
        if (length > 128)
            throw new ArgumentOutOfRangeException(nameof(length), "Read length must be less than or equal to 128.");
        length = (byte)(length * 2);
        serialPort.DiscardOutBuffer();
        var command = ConstructCommand(ReadCmd, address, [length]);
        serialPort.Write(command, 0, command.Length);
    }

    private static byte[] ConstructCommand(byte command, UInt16 address, ReadOnlySpan<byte> data)
    {
        int totalLength = 8 + data.Length;
        Span<byte> sequence = totalLength <= 256 ? stackalloc byte[totalLength] : new byte[totalLength];
        sequence[0] = Header1;
        sequence[1] = Header2;
        sequence[2] = (byte)(data.Length + 5);
        sequence[3] = command;
        sequence[4] = (byte)(address >> 8);
        sequence[5] = (byte)(address & 0xFF);
        data.CopyTo(sequence.Slice(6));
        // CRC calculation directly on the span
        var crcSpan = sequence.Slice(3, totalLength - 5);
        byte[] crcBytes = Crc16.GetChecksumBytes(crcSpan);
        sequence[^2] = crcBytes[0];
        sequence[^1] = crcBytes[1];
        // Copy to array only once for return
        return sequence.ToArray();
    }

    public void ProcessAnswer(byte[] answer, Action<ushort, byte[]> WriteDataToMemory)
    {
        if (answer == null)
            throw new ArgumentNullException(nameof(answer), "Answer buffer cannot be null.");
        if (WriteDataToMemory == null)
            throw new ArgumentNullException(nameof(WriteDataToMemory), "WriteDataToMemory delegate cannot be null.");
        if (answer.Length < MinAnswerLength)
            throw new ArgumentException("Ответ слишком короткий (answer too short).", nameof(answer));
        ReadOnlySpan<byte> packet = GetPacket(answer); //
        CheckCrc(packet);

        byte cmd = packet[3];

        if (cmd != ReadCmd && cmd != WriteCmd)
            throw new InvalidDataException($"Команда ответа не совпадает (unexpected response command): {cmd:X2}");
        if (cmd == WriteCmd)
        {
            if (!TryProcessWriteAnswer(packet))
                throw new InvalidDataException("Ответ на запись не соответствует ожидаемому (write answer does not match expected).");
            return;
        }
        // Note: WriteDataToMemoryBEbytes must be thread-safe if used in multi-threaded scenarios.
        if (!TryProcessReadAnswer(packet, WriteDataToMemory))
            throw new InvalidDataException("Ответ на чтение не соответствует ожидаемому (read answer does not match expected).");
    }

    private static void CheckCrc(ReadOnlySpan<byte> packet)
    {
        // CRC: последние 2 байта
        ReadOnlySpan<byte> crcSpan = packet.Slice(packet.Length - 2, 2);
        UInt16 receivedCrc = MemoryMarshal.Read<UInt16>(crcSpan);

        // Проверка CRC без аллокаций
        if (!Crc16.IsCrcCorrect(packet.Slice(3, packet.Length - 5), receivedCrc))
            throw new InvalidDataException("CRC не совпадает.");
    }

    // This method is allocation-free: it returns a ReadOnlySpan<byte> over the original array.
    private static ReadOnlySpan<byte> GetPacket(byte[] answer)
    {
        int headerIndex = GetHeaderIndex(answer);

        int length = answer[headerIndex + 2];
        int totalLength = length + 3;

        if (answer.Length - headerIndex < totalLength)
            throw new InvalidDataException($"Данных меньше, чем указано в длине (data less than specified length): {answer.Length - headerIndex} < {totalLength}.");
        ReadOnlySpan<byte> packet = answer.AsSpan(headerIndex, totalLength);
        if (packet.Length < 7)
            throw new InvalidDataException($"Пакет слишком короткий для проверки CRC и команд (packet too short for CRC/command check): {packet.Length} < 7.");
        return packet;
    }

    // This method allocates only when calling WriteDataToMemoryBEbytes, due to the delegate signature requiring a byte[].
    // If WriteDataToMemoryBEbytes could accept a Span<byte>, this allocation could be avoided.
    private static bool TryProcessReadAnswer(ReadOnlySpan<byte> packet, Action<ushort, byte[]> WriteDataToMemory)
    {
        // Чтение данных
        if (packet.Length < 9)
            return false;

        // Получение адреса без аллокаций
        UInt16 addr = (UInt16)(packet[4] << 8 | packet[5]);


        byte wordcount = packet[6]; // it is actually word count
        if (7 + wordcount * 2 > packet.Length - 2)
            return false;
        // Only allocation: ToArray() for WriteDataToMemoryBEbytes
        WriteDataToMemory(addr, packet.Slice(7, wordcount).ToArray());
        return true;
    }

    private static bool TryProcessWriteAnswer(ReadOnlySpan<byte> packet)
    {
        return (packet[4] == WriteAnswerBytes[0]) && (packet[5] == WriteAnswerBytes[1]);
    }

    private static int GetHeaderIndex(byte[] answer)
    {
        int headerIndex = -1;
        for (int i = 1; i < answer.Length; i++)
        {
            if (answer[i - 1] == Header1 && answer[i] == Header2)
            {
                headerIndex = i - 1;
                break;
            }
        }
        if (headerIndex == -1 || answer.Length - headerIndex < 8)
            throw new InvalidDataException("Заголовок не найден или данных недостаточно (header not found or insufficient data).");
        return headerIndex;
    }

    /// <summary>
    /// Waits synchronously for a full DWIN message from the serial port.
    /// 
    /// This method scans the incoming serial data for the DWIN header (0x5A 0xA5),
    /// then reads the length byte to determine the total message size, and waits
    /// until the entire message is received or a timeout occurs. If a timeout is reached,
    /// it retries up to <paramref name="maxRetries"/> times before throwing a <see cref="TimeoutException"/>.
    /// 
    /// <para>
    /// Improvements:
    /// <list type="bullet">
    /// <item>Efficiently searches only new data for the header to avoid redundant scanning.</item>
    /// <item>Uses a <see cref="Stopwatch"/> for precise timeout measurement.</item>
    /// <item>Performs basic length validation to avoid processing invalid messages.</item>
    /// <item>Uses short sleeps (1 ms) to minimize latency while waiting for data.</item>
    /// <item>Retries the entire process up to <paramref name="maxRetries"/> times if a timeout occurs.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="port">The <see cref="SerialPort"/> to read from. Must be open and configured.</param>
    /// <param name="timeoutMs">Timeout in milliseconds for each attempt to receive a full message.</param>
    /// <param name="maxRetries">Number of times to retry receiving a message before giving up.</param>
    /// <returns>The full DWIN message as a <see cref="byte[]"/> (including header and all data).</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="port"/> is null.</exception>
    /// <exception cref="TimeoutException">Thrown if no valid message is received after all retries.</exception>
    public byte[] WaitForDwinMessage(SerialPort port, int timeoutMs = 500, int maxRetries = 2)
    {
        if (port == null)
            throw new ArgumentNullException(nameof(port));


        int retries = 0;
        var stopwatch = new Stopwatch();

        while (retries <= maxRetries)
        {
            stopwatch.Restart();
            var buffer = new List<byte>(256);
            bool headerFound = false;
            int headerIndex = -1;

            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                if (port.BytesToRead > 0)
                {
                    byte[] temp = new byte[port.BytesToRead];
                    int bytesRead = port.Read(temp, 0, temp.Length);
                    buffer.AddRange(temp.Take(bytesRead));

                    // Only search new data for header to improve efficiency
                    int searchStart = Math.Max(0, buffer.Count - bytesRead - 1);
                    for (int i = searchStart; i < buffer.Count - 1; i++)
                    {
                        if (buffer[i] == Header1 && buffer[i + 1] == Header2)
                        {
                            headerFound = true;
                            headerIndex = i;
                            break;
                        }
                    }

                    if (headerFound)
                    {
                        if (buffer.Count > headerIndex + LengthOffset)
                        {
                            int length = buffer[headerIndex + LengthOffset];
                            if (length < MinDataLength) // Basic length validation
                                break;

                            int totalLength = HeaderLength + 1 + length; // header + length + data

                            while (buffer.Count - headerIndex < totalLength &&
                                   stopwatch.ElapsedMilliseconds < timeoutMs)
                            {
                                if (port.BytesToRead > 0)
                                {
                                    byte[] moreBytes = new byte[port.BytesToRead];
                                    int moreBytesRead = port.Read(moreBytes, 0, moreBytes.Length);
                                    buffer.AddRange(moreBytes.Take(moreBytesRead));
                                }
                                else
                                {
                                    Thread.Sleep(1); // Shorter sleep
                                }
                            }

                            if (buffer.Count - headerIndex >= totalLength)
                            {
                                return buffer.Skip(headerIndex).Take(totalLength).ToArray();
                            }
                        }
                        break; // Header found but message incomplete, retry
                    }
                }
                else
                {
                    Thread.Sleep(1); // Shorter sleep
                }
            }
            retries++;
        }
        throw new TimeoutException($"Timeout waiting for DWIN message after {maxRetries + 1} attempts.");
    }
}

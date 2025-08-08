using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Dwin.T5L.Communication;

public interface IDwinProtocol
{
    /// <summary>
    /// Sends command to write data to display memory
    /// </summary>
    /// <param name="address"></param>
    /// <param name="data"></param>
    /// <param name="serialPort"></param>
    public void SendWritingCommand(UInt16 address, byte[] data, SerialPort serialPort);

    /// <summary>
    /// Sends command to read data from display memory
    /// </summary>
    /// <param name="address">Address, where to start reading</param>
    /// <param name="length">How much to read</param>
    /// <param name="serialPort"></param>
    public void SendReadingCommand(UInt16 address, byte length, SerialPort serialPort);
    
    /// <summary>
    /// Applies 
    /// </summary>
    /// <param name="answer">Received Data</param>
    /// <param name="WriteDataToMemory">Action to perform on correct data</param>
    public void ProcessAnswer(byte[] answer, Action<ushort, byte[]> WriteDataToMemory);
   

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
    public byte[] WaitForDwinMessage(SerialPort port, int timeoutMs = 500, int maxRetries = 2);
}

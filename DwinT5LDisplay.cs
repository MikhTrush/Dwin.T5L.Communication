using System.IO.Ports;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace Dwin.T5L.Communication;

/// <summary>
/// Provides a high-level interface for interacting with a DWIN HMI display based on the T5L chip.
/// This class encapsulates the underlying protocol communication and memory management.
/// </summary>
public class DwinT5LDisplay : IDisposable
{
    private readonly SerialPort _serialPort;
    private readonly IDwinProtocol _protocol;
    private readonly IDwinMemory _memory;
    private bool _isDisposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="DwinT5LDisplay"/> class.
    /// </summary>
    /// <param name="serialPort">An open SerialPort connected to the DWIN display.</param>
    /// <param name="protocol">The protocol handler. If null, a default <see cref="DwinDisplayProtocol"/> is used.</param>
    /// <param name="memory">The memory handler. If null, a default <see cref="DwinDisplayMemory"/> is used.</param>
    public DwinT5LDisplay(SerialPort serialPort, IDwinProtocol? protocol = null, IDwinMemory? memory = null)
    {
        _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));
        if (!_serialPort.IsOpen)
            throw new ArgumentException("SerialPort must be open.", nameof(serialPort));

        var locker = new object();

        _protocol = protocol ?? new DwinProtocol();
        _memory = memory ?? new DefaultDwinMemoryFactory().CreateDwinMemory(locker); // Provide a lock object

        // Consider: Automatically start a background sync loop here if desired,
        // or let the user manage reads/writes explicitly via methods.
    }

    /// <summary>
    /// Sets a 16-bit unsigned integer value at the specified display memory address.
    /// This method handles endianness and sends the data to the display.
    /// </summary>
    /// <param name="address">The memory address on the display.</param>
    /// <param name="value">The UInt16 value to set.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the DwinDisplay instance has been disposed.</exception>
    /// <exception cref="TimeoutException">Thrown if the display does not respond in time.</exception>
    /// <exception cref="InvalidDataException">Thrown if the display returns an error or unexpected response.</exception>
    public void WriteVariable(UInt16 address, UInt16 value)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        // 1. Use _memory to prepare the data (handle endianness)
        _memory.WriteUInt16AsBEbytesToMemory(address, value);
        // 2. Read the prepared data from _memory
        byte[] dataToSend = _memory.ReadDataFromMemoryAsBytes(address, 1); // 1 UInt16 = 2 bytes = 1 word
        // 3. Use _protocol to send the write command via _serialPort
        _protocol.SendWritingCommand(address, dataToSend, _serialPort);
        // 4. (Optional but recommended) Wait for the display's ACK/NACK using _protocol
        var response = _protocol.WaitForDwinMessage(_serialPort);
        _protocol.ProcessAnswer(response, _memory.WriteDataToMemoryBEbytes); // Process potential errors in ACK
    }

    public void WriteVariablesRange(UInt16 address, UInt16[] values)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        // 1. Use _memory to prepare the data (handle endianness)
        _memory.WriteUInt16AsBEbytesArrayToMemory(address, values);

        byte[] dataToSend = _memory.ReadDataFromMemoryAsBytes(address, (UInt16)(values.Length * 2));

        _protocol.SendWritingCommand(address, dataToSend, _serialPort);

        var response = _protocol.WaitForDwinMessage(_serialPort);

        _protocol.ProcessAnswer(response, _memory.WriteDataToMemoryBEbytes);
    }

    /// <summary>
    /// Sets a string value at the specified display memory address.
    /// </summary>
    /// <param name="address">The memory address on the display.</param>
    /// <param name="text">The string to set.</param>
    /// <param name="encoding">The text encoding to use (default UTF-8).</param>
    public void WriteText(UInt16 address, string text, Encoding? encoding = null)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        encoding ??= Encoding.BigEndianUnicode;
        // 1. Use _memory to prepare/store the string
        _memory.WriteStringToMemory(text, address, encoding); // "Library" is just for internal logging if present
        // 2. Read the prepared byte data
        // Calculate length needed (considering padding for even byte count)
        var byteCount = encoding.GetByteCount(text);
        if (byteCount % 2 != 0) byteCount++; // Ensure even length
        var wordLength = (UInt16)(byteCount / 2);
        byte[] dataToSend = _memory.ReadDataFromMemoryAsBytes(address, wordLength);
        // 3. Use _protocol to send the write command
        _protocol.SendWritingCommand(address, dataToSend, _serialPort);
        // 4. (Optional) Wait/process ACK
        var response = _protocol.WaitForDwinMessage(_serialPort);
        _protocol.ProcessAnswer(response, _memory.WriteDataToMemoryBEbytes);
    }

    /// <summary>
    /// Switches the display to the specified page.
    /// </summary>
    /// <param name="pageId">The ID of the page to switch to.</param>
    public void GoToPage(UInt16 pageId)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        // Similar logic: Prepare data in _memory, send via _protocol
        _memory.WriteUInt16AsBEbytesToMemory(DwinDisplayConstants.PageNumberAddress, pageId); // Assume you define common addresses like PageNumberAddress
        byte[] dataToSend = _memory.ReadDataFromMemoryAsBytes(DwinDisplayConstants.PageNumberAddress, 1);
        _protocol.SendWritingCommand(DwinDisplayConstants.PageNumberAddress, dataToSend, _serialPort);
        var response = _protocol.WaitForDwinMessage(_serialPort);
        _protocol.ProcessAnswer(response, _memory.WriteDataToMemoryBEbytes);
    }

    public void GoToPageBack()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        
    }

    /// <summary>
    /// Reads a 16-bit unsigned integer value from the specified display memory address.
    /// </summary>
    /// <param name="address">The memory address on the display.</param>
    /// <returns>The UInt16 value read from the display.</returns>
    public UInt16 ReadVariable(UInt16 address)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        // 1. Use _protocol to send a read command via _serialPort
        _protocol.SendReadingCommand(address, 1, _serialPort); // Read 1 word (UInt16)
        // 2. Wait for the response using _protocol
        var response = _protocol.WaitForDwinMessage(_serialPort);
        // 3. Use _protocol to process the response, directing data into _memory
        _protocol.ProcessAnswer(response, _memory.WriteDataToMemoryBEbytes);
        // 4. Read the value from _memory and return it
        return _memory.ReadUInt16FromMemory(address);
    }

    public UInt16[] ReadVariablesRange(UInt16 address, byte length)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _protocol.SendReadingCommand(address, length, _serialPort);

        var response = _protocol.WaitForDwinMessage(_serialPort);

        _protocol.ProcessAnswer(response, _memory.WriteDataToMemoryBEbytes);

        return _memory.ReadUInt16ArrayFromMemory(address, length);
    }

    public string ReadText(UInt16 address, byte length, Encoding? encoding = null)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        encoding ??= Encoding.BigEndianUnicode;

        _protocol.SendReadingCommand(address, length, _serialPort);
        var response = _protocol.WaitForDwinMessage(_serialPort);

        _protocol.ProcessAnswer(response, (u, b) => { _memory.WriteDataToMemoryBEbytes(u, b); });
        var result = _memory.ReadStringFromMemory(address, length, encoding);

        return result;
    }
    // Consider adding asynchronous versions (e.g., SetVariableAsync) if needed for responsiveness in certain applications.

    #region IDisposable
    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                // Note: Be careful about disposing _serialPort if it was passed in.
                // Common pattern is that the creator of SerialPort disposes it.
                // _serialPort?.Dispose(); // Maybe not, see note above.
            }

            // Free unmanaged resources (if any)

            _isDisposed = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
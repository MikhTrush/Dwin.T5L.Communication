using System.IO.Ports;
using System.Text;

namespace Dwin.T5L.Communication;

public class DwinDisplayNoMem : IDwinT5LDisplay, IDisposable
{
    private readonly SerialPort _serialPort;
    private readonly IDwinProtocol _protocol;
    private bool _isDisposed;
    private int _timeout = 100;
    private int _retries = 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="DwinT5LDisplay"/> class.
    /// </summary>
    /// <param name="serialPort">An open SerialPort connected to the DWIN display.</param>
    /// <param name="protocol">The protocol handler. If null, a default <see cref="DwinDisplayProtocol"/> is used.</param>
    public DwinDisplayNoMem(SerialPort serialPort, IDwinProtocol? protocol = null)
        : this(serialPort, 200, 3)
    {

    }

    public DwinDisplayNoMem(SerialPort serialPort, int timeout, int retries, IDwinProtocol? protocol = null)
    {
        _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));

        if (!_serialPort.IsOpen)
        {
            _serialPort.Open();
        }

        _timeout = timeout;
        _retries = retries;
        _protocol = protocol ?? new DwinProtocol(_timeout, _retries);
    }

    private Action<ushort, byte[]> EmptyAction { get; set; } = (a, b) => { };

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
        byte[] data = [(byte)(value >> 8), (byte)value];

        _protocol.SendWritingCommand(address, data, _serialPort);

        var response = _protocol.WaitForDwinMessage(_serialPort);

        _protocol.ProcessAnswer(response, EmptyAction); // Process potential errors in ACK
    }

    public void WriteVariablesRange(UInt16 address, UInt16[] values)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        byte[] data = new byte[values.Length * 2];

        for (int i = 0; i < values.Length; i++)
        {
            data[i * 2] = (byte)(values[i] >> 8);
            data[i * 2 + 1] = (byte)values[i];
        }

        _protocol.SendWritingCommand(address, data, _serialPort);

        var response = _protocol.WaitForDwinMessage(_serialPort);

        _protocol.ProcessAnswer(response, EmptyAction);
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

        var data = encoding.GetBytes(text);
        var byteCount = encoding.GetByteCount(text);
        if (byteCount % 2 != 0) byteCount++; // Ensure even length

        _protocol.SendWritingCommand(address, data, _serialPort);

        var response = _protocol.WaitForDwinMessage(_serialPort);
        _protocol.ProcessAnswer(response, EmptyAction);
    }

    /// <summary>
    /// Switches the display to the specified page.
    /// </summary>
    /// <param name="pageId">The ID of the page to switch to.</param>
    public void GoToPage(UInt16 pageId)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        byte[] dataToSend = [0x5a, 0x1, (byte)(pageId >> 8), (byte)pageId];

        _protocol.SendWritingCommand(DwinDisplayConstants.PageNumberAddress, dataToSend, _serialPort);
        var response = _protocol.WaitForDwinMessage(_serialPort);
        _protocol.ProcessAnswer(response, EmptyAction);
    }


    public void GoToPreviousPage()
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

        _protocol.SendReadingCommand(address, 1, _serialPort);

        var response = _protocol.WaitForDwinMessage(_serialPort);

        ushort ans = 0;

        _protocol.ProcessAnswer(response, (u, b) => { ans = (ushort)((b[0] << 8) + b[1]); });

        return ans;
    }

    public UInt16[] ReadVariablesRange(UInt16 address, byte length)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _protocol.SendReadingCommand(address, length, _serialPort);

        var response = _protocol.WaitForDwinMessage(_serialPort);

        ushort[] ans = new ushort[length];

        _protocol.ProcessAnswer(response, (u, b) =>
        {
            for (int i = 0; i < length && (2 * i + 1) < b.Length; i++)
            {
                ans[i] = (ushort)((b[2 * i] << 8) + b[2 * i + 1]);
            }
        });

        return ans;
    }

    public string ReadText(UInt16 address, byte length, Encoding? encoding = null)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        encoding ??= Encoding.BigEndianUnicode;

        _protocol.SendReadingCommand(address, length, _serialPort);
        var response = _protocol.WaitForDwinMessage(_serialPort);

        byte[] bytes = [];

        _protocol.ProcessAnswer(response, (u, b) => bytes = b);

        return encoding.GetString(bytes);
    }

    public void ClearMemory(UInt16 address, ushort length)
    {
        ushort[] arr = [.. Enumerable.Repeat<ushort>(0, 120)];

        for (ushort i = address; i < (ushort)(length + address); i += 120)
        {
            WriteVariablesRange(i, arr);
        }
    }

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

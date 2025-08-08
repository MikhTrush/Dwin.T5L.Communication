using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Dwin.T5L.Communication;

public interface IDwinMemory
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="addr"></param>
    /// <param name="data"></param>
    public void WriteDataToMemoryBEbytes(UInt16 addr, byte[] data);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="addr"></param>
    /// <param name="data"></param>
    public void WriteUInt16AsBEbytesToMemory(UInt16 addr, UInt16 data);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="addr"></param>
    /// <param name="data"></param>
    public void WriteUInt16AsBEbytesArrayToMemory(UInt16 addr, UInt16[] data);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="address"></param>
    /// <param name="type"></param>
    /// <param name="enc"></param>
    public void WriteStringToMemory(string message, UInt16 address, Encoding enc);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="addr"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public byte[] ReadDataFromMemoryAsBytes(UInt16 addr, UInt16 length);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="addr"></param>
    /// <returns></returns>
    public ushort ReadUInt16FromMemory(UInt16 addr);

    /// <summary>
    /// Reads an array of values from memory
    /// </summary>
    /// <param name="addr"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public ushort[] ReadUInt16ArrayFromMemory(UInt16 addr, int length);

    /// <summary>
    /// Reads string with specified encoding
    /// </summary>
    /// <param name="addr"></param>
    /// <param name="length"></param>
    /// <param name="enc"></param>
    /// <returns></returns>
    public string ReadStringFromMemory(UInt16 addr, UInt16 length, Encoding enc);
}

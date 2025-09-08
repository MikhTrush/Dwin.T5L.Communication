using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dwin.T5L.Communication
{
    public interface IDwinT5LDisplay
    {
        public void WriteVariable(UInt16 address, UInt16 value);

        public void WriteVariablesRange(UInt16 address, UInt16[] values);

        /// <summary>
        /// Sets a string value at the specified display memory address.
        /// </summary>
        /// <param name="address">The memory address on the display.</param>
        /// <param name="text">The string to set.</param>
        /// <param name="encoding">The text encoding to use (default UTF-8).</param>
        public void WriteText(UInt16 address, string text, Encoding? encoding = null);

        /// <summary>
        /// Switches the display to the specified page.
        /// </summary>
        /// <param name="pageId">The ID of the page to switch to.</param>
        public void GoToPage(UInt16 pageId);

        public void GoToPreviousPage();

        /// <summary>
        /// Reads a 16-bit unsigned integer value from the specified display memory address.
        /// </summary>
        /// <param name="address">The memory address on the display.</param>
        /// <returns>The UInt16 value read from the display.</returns>
        public UInt16 ReadVariable(UInt16 address);

        public UInt16[] ReadVariablesRange(UInt16 address, byte length);

        public string ReadText(UInt16 address, byte length, Encoding? encoding = null);
      
    }
}

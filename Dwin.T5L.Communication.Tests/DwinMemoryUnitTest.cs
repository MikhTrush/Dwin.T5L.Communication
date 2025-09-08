using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dwin.T5L.Communication;
using Xunit;

namespace Dwin.T5L.Communication.Tests
{
    public class DwinMemoryUnitTest
    {
        DwinMemory dm;

        public DwinMemoryUnitTest()
        {
            dm = new(new object());
        }

        [Theory]
        [InlineData("string", "string")]
        [InlineData("Руссиан текст", "Руссиан текст")]
        public void WritesAndReadsString(string incoming, string result)
        {
            dm.WriteStringToMemory(incoming, 0x5000, Encoding.BigEndianUnicode);
            var res = dm.ReadStringFromMemory(0x5000, (UInt16)(incoming.Length * 2), Encoding.BigEndianUnicode);
            Assert.Equal(res, result);
        }
    }
}

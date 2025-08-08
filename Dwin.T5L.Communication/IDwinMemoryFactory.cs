using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dwin.T5L.Communication
{
    public interface IDwinMemoryFactory
    {
        public IDwinMemory CreateDwinMemory(object locker);
    }
}

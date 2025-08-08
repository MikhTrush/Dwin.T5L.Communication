using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dwin.T5L.Communication
{
    public class DefaultDwinMemoryFactory : IDwinMemoryFactory
    {
        public IDwinMemory CreateDwinMemory(object locker) {
            return new DwinMemory(locker);
        }
    }
}

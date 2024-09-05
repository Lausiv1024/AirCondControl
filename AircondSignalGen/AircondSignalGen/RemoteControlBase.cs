using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AircondSignalGen
{
    public abstract class RemoteControlBase
    {
        public abstract byte[] GetCurrentSignal();
    }
}

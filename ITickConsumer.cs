using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Runt
{
    internal interface ITickConsumer
    {
        DateTime Tick();
    }
}

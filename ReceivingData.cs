using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Runt
{
    internal class ReceivingData
    {
        readonly Runt Runt;
        readonly Queue<(byte[] data, int length)> DataToReceive = new();
        private ManualResetEvent WaitForDataAvailable = new ManualResetEvent(false);

        public ReceivingData(Runt runt)
        {
            Runt = runt;
        }

        public Span<byte> Receive(Span<byte> buffer)
        {
            // wait until data available
            WaitForDataAvailable.WaitOne();
            byte[] data;
            int length;
            lock (DataToReceive)
            {
                (data, length) = DataToReceive.Dequeue();

                // reset wait handle if data no longer available
                if (DataToReceive.Count == 0)
                    WaitForDataAvailable.Reset();
            }
            // received data, copy into buffer
            data.AsSpan(0, length).CopyTo(buffer);
            // return data to array pool
            Runt.Pool.Return(data);

            // return buffer truncated to the length of the data
            return buffer[..length];
        }

        public void PushData(byte[] data, int length)
        {
            lock (DataToReceive)
            {
                DataToReceive.Enqueue((data, length));
                WaitForDataAvailable.Set();
            }
        }

        public bool DataAvailable()
        {
            lock (DataToReceive)
            {
                return DataToReceive.Count > 0;
            }
        }
    }
}

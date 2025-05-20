using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Runt
{
    public class Connection
    {
        public readonly IPEndPoint RemoteEndPoint;
        public readonly IPEndPoint LocalEndPoint;
        public bool IsConnected { get; private set; }

        internal SendingData SendingData;
        private ReceivingData ReceivingData;
        internal ReceiveBuffer ReceiveBuffer;

        internal Connection(Runt runt, IPEndPoint destination, Sender sender)
        {
            SendingData = new(runt, destination, sender);
            ReceivingData = new(runt);
            ReceiveBuffer = new(ReceivingData);
        }
        

        public void SendReliable(ReadOnlySpan<byte> data)
        {
            SendingData.SendReliable(data);
        }

        public void SendUnreliable(ReadOnlySpan<byte> data)
        {
            SendingData.SendUnreliable(data);
        }

        // This is a blocking call
        public Span<byte> Receive(Span<byte> buffer)
        {
            return ReceivingData.Receive(buffer);
        }

        public void Disconnect()
        {

        }

        public bool DataAvailable => ReceivingData.DataAvailable();

    }
}

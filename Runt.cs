using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Runt
{
    public class Runt
    {
        public const int MaxSegmentSize = 1472;
        public const int MaxPayloadSize = 1468;
        public const uint MaxReceiveWindow = 100;
        internal ArrayPool<byte> Pool = ArrayPool<byte>.Create(MaxSegmentSize, 10);

        private Dictionary<(IPAddress, int), Connection> RemoteAddressToConnection = new();
        public readonly List<Connection> Connections;

        private Sender Sender;
        

        public Runt(Socket socket)
        {
            
        }

        public Connection AcceptConnection()
        {
            return null;
        }

        public void CloseAllConnections()
        {

        }

        // internal methods

        internal void ManageReceivedData(Span<byte> data, IPAddress address, int port)
        {
            if (RemoteAddressToConnection.ContainsKey((address, port)))
            {
                Connection connection = RemoteAddressToConnection[(address, port)];
                Packet? packet = Packet.Interpret(this, data);
                if (packet != null)
                {
                    // Valid packet
                    Packet pack = (Packet)packet;
                    // push the data to the receive buffer
                    uint outboundAck = connection.ReceiveBuffer.PushPacket(pack);
                    // tell the sender that data has been received
                    Sender.ReportReceivedPacket(pack, connection, outboundAck);
                }
            }
            else
            {
                // new connection
            }
        }

        internal void ReceiveFailed()
        {

        }
    }
}

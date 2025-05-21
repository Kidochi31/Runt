using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Text;

namespace Runt
{
    internal class Sender
    {
        readonly byte[] SendBuffer = new byte[Runt.MaxSegmentSize];
        readonly Socket Socket;

        public Sender(Runt runt, Socket socket)
        {
            Socket = socket;
        }

        public void SendPacket(Packet? packet, IPEndPoint destination)
        {
            lock (Socket)
            {
                Span<byte> sendData = Packet.WritePacket(packet, SendBuffer);
                Socket.SendTo(sendData, destination);
            }
        }
    }
}

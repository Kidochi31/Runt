using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Runt
{
    internal class Receiver
    {
        Socket Socket;
        byte[] ReceiveData = new byte[Runt.MaxSegmentSize];
        EndPoint RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        Runt Runt;

        public Receiver(Socket socket, Runt runt)
        {
            Socket = socket;
            socket.ReceiveTimeout = Timeout.Infinite;
            Runt = runt;
        }

        public void StartReceiving()
        {
            try
            {
                while (true)
                {
                    int bytes = Socket.ReceiveFrom(ReceiveData, ref RemoteEndPoint);
                    IPEndPoint ipRemoteEP = (IPEndPoint)RemoteEndPoint;
                    Runt.ManageReceivedData(ReceiveData.AsSpan(0, bytes), ipRemoteEP.Address, ipRemoteEP.Port);
                }
            }
            catch (Exception)
            {
                Runt.ReceiveFailed();
                return;
            }
        }
    }
}

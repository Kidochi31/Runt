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
        readonly List<SendingData> NextSends = new();
        readonly List<Connection> Connections = new();
        readonly byte[] SendBuffer = new byte[Runt.MaxSegmentSize];
        readonly Socket Socket;

        readonly Timer Timer;
        bool InTick = false;
        object TickLock = new();
        DateTime NextTick = DateTime.MaxValue;

        public Sender(Runt runt, Socket socket)
        {
            Socket = socket;
            Timer = new Timer(o => Tick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        private void Tick()
        {
            lock (TickLock)
            {
                InTick = true;
                foreach(SendingData s in NextSends)
                {
                    s.Tick();
                }
                //Tick will not be able to report
                UpdateTimer(); // update the timer at the end of every tick
                InTick = false;
            }
        }

        public void ReportReceivedPacket(Packet packet, Connection connection, uint outboundAck)
        {
            uint seq = packet.Seq;
            uint ack = packet.Ack;
            if (seq == 0) return; // unreliable, contains no ack info

            if (ack == 0) return; // connection closed


            connection.SendingData.ReportOutboundAck(outboundAck);
            connection.SendingData.AckUpTo(ack);
            
        }

        public void ReportUpdatedNextTickTime(SendingData sendingData, DateTime newTickTime)
        {
            // Don't report if in the middle of a tick
            if (InTick)
                return;
            lock (TickLock)
            {
                if (newTickTime < NextTick)
                {
                    NextTick = newTickTime;
                    NextSends.Clear();
                    NextSends.Add(sendingData);
                    ResetTimer();
                }
                else if (newTickTime == NextTick && !NextSends.Contains(sendingData))
                {
                    NextSends.Add(sendingData);
                }
                else if (NextSends.Contains(sendingData)) // newTickTime > NextTick
                {
                    if(NextSends.Count == 0) UpdateTimer();
                    else NextSends.Remove(sendingData);
                }
                // else newTickTime > NextTick && !NextSends.Contains(sendingData) -> ignore
            }
        }

        private void UpdateTimer()
        {
            NextSends.Clear();

            NextTick = DateTime.MaxValue;
            foreach (Connection connection in Connections)
            {
                if (connection.SendingData.NextTickTime > NextTick)
                {
                    continue;
                }
                else if (connection.SendingData.NextTickTime < NextTick)
                {
                    NextSends.Clear();
                    NextTick = connection.SendingData.NextTickTime;
                    NextSends.Add(connection.SendingData);
                }
                else //if connection.SendingData.NextTickTime == NextTick
                {
                    NextSends.Add(connection.SendingData);
                }
            }
            ResetTimer();
        }

        private void ResetTimer()
        {
            if (NextTick == DateTime.MaxValue) Timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            else Timer.Change(NextTick < DateTime.Now ? TimeSpan.Zero : NextTick - DateTime.Now, Timeout.InfiniteTimeSpan);
        }

        public void SendPacket(Packet? packet, IPEndPoint destination)
        {
            lock (Socket)
            {
                Span<byte> sendData = Packet.WritePacket(packet, SendBuffer);
                Socket.SendTo(sendData, destination);
            }
        }

        public void AddConnection(Connection connection)
        {
            lock (TickLock)
            {
                Connections.Add(connection);

                if (connection.SendingData.NextTickTime < NextTick)
                {
                    NextTick = connection.SendingData.NextTickTime;
                    NextSends.Clear();
                    NextSends.Add(connection.SendingData);
                    ResetTimer();
                }
                else if (connection.SendingData.NextTickTime == NextTick)
                {
                    NextSends.Add(connection.SendingData);
                }
            }
        }

        public void RemoveConnection(Connection connection)
        {
            lock (TickLock)
            {
                Connections.Remove(connection);

                if (NextSends.Contains(connection.SendingData))
                {
                    if (NextSends.Count == 0) UpdateTimer();
                    else NextSends.Remove(connection.SendingData);
                }
            }
        }
    }
}

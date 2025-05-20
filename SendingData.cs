using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Runt
{
    internal class SendingData
    {
        readonly IPEndPoint Destination;
        readonly Runt Runt;
        readonly Sender Sender;
        readonly List<(byte[] data, int length, DateTime sendTime, int expiryCount, DateTime expiryTime)> DataToSend = new();

        public uint UnackedSeq = 1;
        public uint StartedSendingSeq = 0;
        private uint OutboundAck = 1;

        const float RoundTripTimeWeight = 0.7f;
        TimeSpan RoundTripTime = new TimeSpan(0, 0, 5);

        TimeSpan DefaultTickTime = new TimeSpan(0, 0, 10);
        public DateTime NextTickTime = DateTime.MaxValue;
        DateTime LastPacketSent = DateTime.MinValue;


        int? ExpiryIndex = null;
        const int MaxExpiry = 10;
        float CongestionWindow = 10;

        public SendingData(Runt runt, IPEndPoint destination, Sender sender)
        {
            Runt = runt;
            Destination = destination;
            Sender = sender;
        }

        public void Tick()
        {
            // this is called after default tick seconds or when an expiry time has been reached
            if (ExpiryIndex == null)
            {
                Sender.SendPacket(null, Destination); // send an empty packet
                LastPacketSent = DateTime.Now;
            }
            else
            {
                lock (DataToSend)
                {
                    int index = (int)ExpiryIndex;
                    (byte[] data, int length, DateTime sendTime, int expiryCount, DateTime expiryTime) = DataToSend[index];
                    if (expiryCount >= MaxExpiry)
                    {
                        // report send failure => end connection

                    }
                    else
                    {
                        // resend, increment number of expiries
                        Sender.SendPacket(new Packet(data, length, GetSeqOfIndex(index), OutboundAck), Destination);
                        DataToSend[index] = (data, length, DateTime.Now, expiryCount + 1, GetExpiryTime());
                        LastPacketSent = DateTime.Now;

                        // retransmission means packet didn't send -> halve congestion window
                        CongestionWindow = CongestionWindow / 2;
                    }
                }
            }
            UpdateNextTickTime();
        }

        public void SendReliable(ReadOnlySpan<byte> data)
        {
            byte[] storage = Runt.Pool.Rent(data.Length);
            data.CopyTo(storage);

            lock (DataToSend)
            {
                int index = DataToSend.Count;
                DataToSend.Add((storage, data.Length, DateTime.MaxValue, 0, DateTime.MaxValue));
            }

            SendNewDataUpToWindow();
            UpdateNextTickTime();
        }

        public void SendUnreliable(ReadOnlySpan<byte> data)
        {
            byte[] storage = Runt.Pool.Rent(data.Length);
            data.CopyTo(storage);
            Sender.SendPacket(new Packet(storage, data.Length), Destination);
            LastPacketSent = DateTime.Now;
            UpdateNextTickTime();
        }

        public void AckUpTo(uint seq)
        {
            lock (DataToSend)
            {
                uint numberAcked = GetNumberAckedBy(seq);
                if (numberAcked > DataToSend.Count)
                    return;
                for (int i = 0; i < numberAcked; i++)
                {
                    TimeSpan rtt = DateTime.Now - DataToSend[0].sendTime;
                    RoundTripTime = RoundTripTime * RoundTripTimeWeight + rtt * (1 - RoundTripTimeWeight);
                    DataToSend.RemoveAt(0);
                }
                // increase congestion window by 1 each RTT
                CongestionWindow += ((float)numberAcked / DataToSend.Count);

                DataToSend.RemoveRange(0, (int)numberAcked);
                UnackedSeq += numberAcked;
            }
            SendNewDataUpToWindow();
            UpdateNextTickTime();
        }

        public void ReportOutboundAck(uint ack)
        {
            OutboundAck = ack;
        }

        private uint GetNumberAckedBy(uint ack) => unchecked(ack - UnackedSeq);

        private uint GetSeqOfIndex(int index) => unchecked((uint)index + UnackedSeq);

        private int GetIndexOfSeq(uint seq) => unchecked((int)(seq - UnackedSeq));

        private DateTime GetExpiryTime() => DateTime.Now + RoundTripTime * 1.5;

        private void UpdateNextTickTime()
        {
            DateTime OldNextTickTime = NextTickTime;
            
            (ExpiryIndex, NextTickTime) = GetMinimumTickTime();

            if (OldNextTickTime != NextTickTime)
                Sender.ReportUpdatedNextTickTime();
        }

        private (int? index, DateTime tick) GetMinimumTickTime()
        {
            DateTime tickTime = DateTime.MaxValue;
            int? index = null;
            for(int i = 0; i < DataToSend.Count; i++)
            {
                if(tickTime > DataToSend[i].expiryTime)
                {
                    tickTime = DataToSend[i].expiryTime;
                    index = i;
                }
            }

            if (tickTime > LastPacketSent + DefaultTickTime)
            {
                tickTime = LastPacketSent + DefaultTickTime;
                index = null;
            }
            return (index, tickTime);
        }

        private void SendNewDataUpToWindow()
        {
            lock (DataToSend)
            {
                // send data up to the congestion window
                for (int i = GetIndexOfSeq(StartedSendingSeq) + 1; i < CongestionWindow; i++)
                {
                    (byte[] data, int length, DateTime sendTime, int expiryCount, DateTime expiryTime) = DataToSend[i];
                    Sender.SendPacket(new Packet(data, length, GetSeqOfIndex(i), OutboundAck), Destination);
                    DataToSend[i] = (data, length, DateTime.Now, 0, GetExpiryTime());
                    LastPacketSent = DateTime.Now;
                    unchecked { StartedSendingSeq++; }
                }
            }
        }
    }
}

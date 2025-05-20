using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Runt
{
    internal class ReceiveBuffer
    {
        public uint LeastUnackedSeq = 1;

        readonly ReceivingData ReceivingData;
        readonly (byte[]? data, int length)[] DataToReceive = new (byte[]? data, int length)[Runt.MaxReceiveWindow];


        public ReceiveBuffer(ReceivingData receivingData)
        {
            ReceivingData = receivingData;
        }

        public uint PushPacket(Packet packet)
        {
            if (packet.Reliable)
                PushReliableData(packet.Data, packet.DataLength, packet.Seq);
            else
                PushUnreliableData(packet.Data, packet.DataLength);

            return LeastUnackedSeq;
        }

        private void PushUnreliableData(byte[] data, int length)
        {
            // immediately push unreliable data
            ReceivingData.PushData(data, length);
        }

        private void PushReliableData(byte[] data, int length, uint seq)
        {
            if (SeqValid(seq))
            {
                lock (DataToReceive)
                {
                    if (DataToReceive[RelativeSeq(seq)].data is null)
                    {
                        DataToReceive[RelativeSeq(seq)] = (data, length);
                        PushAllAvailableData();
                    }
                }
            }
            else
            {
                // ignore the packet
            }
        }

        private void PushAllAvailableData()
        {
            if (DataToReceive[0].data is null)
                return;
            uint dataPushed = 0;
            for(;dataPushed < DataToReceive.Length; dataPushed++)
            {
                if (DataToReceive[dataPushed].data is null)
                    break;
                ReceivingData.PushData(DataToReceive[dataPushed].data, DataToReceive[dataPushed].length);
            }
            // move the rest of the data forward
            for(uint i = dataPushed; i < DataToReceive.Length; i++)
            {
                DataToReceive[i - dataPushed] = DataToReceive[i];
            }
            // change the least unacked seq to represent the new 0th index
            unchecked { LeastUnackedSeq += dataPushed; }
        }

        private bool SeqValid(uint seq)
        {
            unchecked
            {
                return seq - LeastUnackedSeq <= Runt.MaxReceiveWindow;
            }
        }

        private uint RelativeSeq(uint seq)
        {
            unchecked
            {
                return seq - LeastUnackedSeq;
            }
        }
    }
}

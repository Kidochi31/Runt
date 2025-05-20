using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Runt
{
    internal struct Packet
    {
        public byte[] Data;
        public uint Seq;
        public uint Ack;
        public int DataLength;
        public bool Reliable;

        public Packet(byte[] data, int dataLength, uint seq, uint ack)
        {
            Data = data;
            Seq = seq;
            Ack = ack;
            DataLength = dataLength;
            Reliable = true;
        }

        public Packet(byte[] data, int dataLength)
        {
            Data = data;
            Seq = 0;
            Ack = 0;
            DataLength = dataLength;
            Reliable = false;
        }

        public static Span<byte> WritePacket(Packet? packet, Span<byte> span)
        {
            if(packet is null)
                return span[0..1];

            Packet pack = (Packet)packet;

            Span<byte> originalSpan = span;
            if (pack.Reliable)
            {
                BinaryPrimitives.WriteUInt16BigEndian(span, (ushort)pack.Seq);
                span = span[2..];
                BinaryPrimitives.WriteUInt16BigEndian(span, (ushort)pack.Ack);
                span = span[2..];
                pack.Data.CopyTo(span[..pack.DataLength]);
                return span[..(pack.DataLength + 4)];
            }
            else
            {
                BinaryPrimitives.WriteUInt16BigEndian(span, 0);
                span = span[2..];
                pack.Data.CopyTo(span[..pack.DataLength]);
                return span[..(pack.DataLength + 2)];
            }
        }

        public static Packet? Interpret(Runt runt, ReadOnlySpan<byte> packet)
        {
            Packet pack = new Packet();
            if(packet.Length < 2)
                return null;

            pack.Seq = BinaryPrimitives.ReadUInt16BigEndian(packet[0..2]);
            pack.Reliable = pack.Seq == 0;
            byte[] data;
            if (pack.Reliable)
            {
                pack.Ack = BinaryPrimitives.ReadUInt16BigEndian(packet[2..4]);
                pack.DataLength = packet.Length - 4;
                data = runt.Pool.Rent(pack.DataLength);
                packet[4..].CopyTo(data);
            }
            else
            {
                pack.DataLength = packet.Length - 2;
                data = runt.Pool.Rent(pack.DataLength);
                packet[2..].CopyTo(data);
            }

            return pack;
        }
    }
}

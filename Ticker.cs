using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Runt
{
    internal class Ticker
    {
        PriorityQueue<ITickConsumer, DateTime> ConsumerQueue = new();

        readonly Timer Timer;
        Lock QueueLock = new();
        DateTime NextTick = DateTime.MaxValue;

        public Ticker()
        {
            Timer = new Timer(o => Tick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        private void Tick()
        {
            lock (QueueLock)
            {
                ITickConsumer nextConsumer = ConsumerQueue.Dequeue();
                DateTime nextTickTime = nextConsumer.Tick();
                ConsumerQueue.Enqueue(nextConsumer, nextTickTime);
                UpdateTimer();
            }
        }

        public void ReportUpdatedNextTickTime(ITickConsumer consumer, DateTime nextTickTime)
        {
            lock (QueueLock)
            {
                ConsumerQueue.Remove(consumer, out _, out _);
                ConsumerQueue.Enqueue(consumer, nextTickTime);
                UpdateTimer();
            }
        }

        public void AddTickConsumer(ITickConsumer consumer, DateTime nextTickTime)
        {
            lock (QueueLock)
            {
                ConsumerQueue.Enqueue(consumer, nextTickTime);
                UpdateTimer();
            }
        }

        public void RemoveTickConsumer(ITickConsumer consumer)
        {
            lock (QueueLock)
            {
                ConsumerQueue.Remove(consumer, out _, out _);
                UpdateTimer();
            }
        }

        private void UpdateTimer()
        {
            if (ConsumerQueue.Count > 0)
            {
                ConsumerQueue.TryPeek(out _, out NextTick); // Set NextTick to next
            }
            else
            {
                NextTick = DateTime.MaxValue; // nothing -> wait
            }

            if (NextTick == DateTime.MaxValue)
            {
                Timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
            else
            {
                TimeSpan timeUntilNextTick = NextTick - DateTime.Now;
                Timer.Change(NextTick < DateTime.Now ? TimeSpan.Zero : timeUntilNextTick, Timeout.InfiniteTimeSpan);
            }
        }
    }
}

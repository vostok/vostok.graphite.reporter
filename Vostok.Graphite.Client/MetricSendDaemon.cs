﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Vostok.Commons.Synchronization;
using Vostok.Commons.Utilities;
using Vostok.Logging;

namespace Vostok.Graphite.Client
{
    internal class MetricSendDaemon : IMetricSendDaemon
    {
        private readonly IMetricSender sender;
        private readonly ISendPeriodProvider sendPeriodProvider;

        private const int NotStarted = 0;
        private const int Working = 1;
        private const int Disposed = 2;
        private readonly AtomicInt state;
        private readonly object initializationSync;

        private CancellationTokenSource senderRoutineCts;
        private Task senderRoutine;

        public MetricSendDaemon(
            IMetricSender sender,
            ISendPeriodProvider sendPeriodProvider)
        {
            this.sender = sender;
            this.sendPeriodProvider = sendPeriodProvider;

            state = new AtomicInt(NotStarted);
            initializationSync = new object();
        }

        public void Start()
        {
            if (state.TryIncreaseTo(Working))
            {
                lock (initializationSync)
                {
                    if (state == Working)
                    {
                        senderRoutineCts = new CancellationTokenSource();
                        senderRoutine = Task.Run(SenderRoutine);
                    }
                }
            }
        }

        private async Task SenderRoutine()
        {
            var sendPeriod = sendPeriodProvider.GetNext(true);
            var sw = new Stopwatch();

            while (state == Working)
            {
                await WaitForNextSend(sendPeriod);

                sw.Restart();
                var success = await sender.SendAsync();
                var elapsed = sw.Elapsed;

                sendPeriod = sendPeriodProvider.GetNext(success);
                sendPeriod = TimeSpanExtensions.Max(TimeSpan.Zero, sendPeriod - elapsed);
            }
        }

        private async Task WaitForNextSend(TimeSpan sendPeriod)
        {
            try
            {
                await Task.Delay(sendPeriod, senderRoutineCts.Token);
            }
            catch (OperationCanceledException)
            {}
        }

        public void Dispose()
        {
            if (state.TryIncreaseTo(Disposed))
            {
                lock (initializationSync)
                {
                    senderRoutineCts?.Cancel();
                    senderRoutine?.GetAwaiter().GetResult();
                    sender.SendAsync().GetAwaiter().GetResult();
                }
            }
        }
    }
}
﻿using System;
using Vostok.Commons.Extensions.UnitConvertions;

namespace Vostok.Graphite.Client
{
    public class GraphiteSinkConfig
    {
        public int MaxMetricBufferCapacity { get; set; } = 50000;
        public TimeSpan SendPeriod { get; set; } = 5.Seconds();
        public TimeSpan SendPeriodCap { get; set; } = 10.Minutes();
        public string GraphiteHost { get; set; }
        public int GraphitePort { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using CronoSeries.ABMath.ModelFramework.Data;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    [Serializable]
    public class OHLCAggregator : TimeSeriesTransformation
    {
        private MVTimeSeries combined;

        public OHLCAggregator()
        {
            Period = new TimeSpan(0, 15, 0);
        }

        [Category("Parameter")] [Description("Period over which to aggregate")] public TimeSpan Period { get; set; }

        [Category("Parameter")]
        [Description("Phase (relative to midnight Jan. 1st, 2001)")]
        public TimeSpan Phase { get; set; }

        public override int NumInputs()
        {
            return 1;
        }

        public override int NumOutputs()
        {
            return 1;
        }

        public override string GetInputName(int index)
        {
            if (index == 0)
                return "OHLC Multivariate TS";
            throw new SocketException();
        }

        public override object GetOutput(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            if (!IsValid || combined == null)
                return null;
            return combined;
        }

        public override string GetOutputName(int index)
        {
            if (index == 0)
                return "Aggregated Bars";
            throw new SocketException();
        }

        public override string GetDescription()
        {
            return "Aggregates OHLC bars";
        }

        public override string GetShortDescription()
        {
            return "OHLC Agg.";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}

        private DateTime PhaseAdjust(DateTime dt)
        {
            var baseTicks = new DateTime(2001, 1, 1).Ticks;
            var periodTicks = Period.Ticks;
            var diffTicks = dt.Ticks - baseTicks;
            var intervals = (long) Math.Floor(diffTicks / (double) periodTicks);
            return new DateTime(baseTicks + intervals * periodTicks + Phase.Ticks);
        }

        public override void Recompute()
        {
            IsValid = false;
            if (GetInputType(-1) != InputType.MultivariateTS)
                return;

            var series = GetInputBundle();
            if (series.Count != 4 && series.Count != 5)
                return; // something wrong!, needs open,high,low,close (and optional volume)

            var open = series[0];
            var high = series[1];
            var low = series[2];
            var close = series[3];
            var volume = series.Count == 5 ? series[4] : null;

            combined = new MVTimeSeries(series.Count);

            var accumulated = Vector<double>.Build.Dense(series.Count);

            accumulated[0] = open[0];
            accumulated[1] = high[0];
            accumulated[2] = low[0]; // low
            accumulated[3] = close[0];
            if (series.Count == 5)
                accumulated[4] = 0;

            var initialTime = PhaseAdjust(open.TimeStamp(0)); // initial time for current bar

            for (var t = 0; t < open.Count; ++t)
            {
                var intoBar = open.TimeStamp(t) - initialTime;
                if (intoBar >= Period)
                {
                    // dump the previous bar and reset things
                    combined.Add(initialTime + Period, accumulated.ToArray(), false);
                    accumulated = Vector<double>.Build.Dense(series.Count);
                    accumulated[0] = open[t];
                    accumulated[1] = high[t];
                    accumulated[2] = low[t];
                    accumulated[3] = close[t];
                    if (volume != null)
                        accumulated[4] = 0;
                    initialTime = PhaseAdjust(open.TimeStamp(t));
                }

                accumulated[3] = close[t];
                accumulated[1] = Math.Max(accumulated[1], high[t]);
                accumulated[2] = Math.Min(accumulated[2], low[t]);
                if (volume != null)
                    accumulated[4] += volume[t];
            }

            IsValid = true;
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> {typeof(MVTimeSeries)};
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> {typeof(MVTimeSeries)};
        }
    }
}
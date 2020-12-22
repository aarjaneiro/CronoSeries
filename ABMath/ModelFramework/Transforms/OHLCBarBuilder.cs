using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using CronoSeries.ABMath.ModelFramework.Data;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    /// <summary>
    /// This transform takes a univariate price series as input, and converts it to open-high-low-close values over the specified intervals.
    /// </summary>
    [Serializable]
    public class OHLCBarBuilder : TimeSeriesTransformation
    {
        [Category("Parameter"), Description("Period over which to aggregate")]
        public TimeSpan Period { get; set; }
        [Category("Parameter"), Description("Phase (relative to midnight Jan. 1st, 2001)")]
        public TimeSpan Phase { get; set; }

        [NonSerialized]
        private MVTimeSeries combined;

        public OHLCBarBuilder()
        {
            Period = new TimeSpan(0, 15, 0);
        }

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
                return "Univariate Price Series";
            throw new SocketException();
        }

        public override object GetOutput(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            if (!IsValid || combined==null)
                return null;
            return combined;
        }

        public override string GetOutputName(int index)
        {
            if (index == 0)
                return "Bars";
            throw new SocketException();
        }

        public override string GetDescription()
        {
            return "Builds bars from price info";
        }

        public override string GetShortDescription()
        {
            return "OHLC";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}

        private DateTime PhaseAdjust(DateTime dt)
        {
            long baseTicks = new DateTime(2001, 1, 1).Ticks;
            long periodTicks = Period.Ticks;
            long diffTicks = dt.Ticks - baseTicks;
            long intervals = (long)Math.Floor(diffTicks/(double) periodTicks);
            return new DateTime(baseTicks + intervals*periodTicks + Phase.Ticks);
        }

        public override void Recompute()
        {
            IsValid = false;
            if (GetInputType(-1) != InputType.UnivariateTS)
                return;

            var series = GetInput(0) as TimeSeries;

            combined = new MVTimeSeries(4);
            combined.Title = "Bars";
            combined.SubTitle = new string[4] {"Open", "High", "Low", "Close"};

            var accumulated = Vector<double>.Build.Dense(4);
            accumulated[0] = accumulated[1] = accumulated[2] = accumulated[3] = series[0];

            DateTime initialTime = PhaseAdjust(series.TimeStamp(0));  // initial time for current bar

            for (int t = 0; t < series.Count; ++t )
            {
                TimeSpan intoBar = series.TimeStamp(t) - initialTime;
                if (intoBar >= Period)
                {
                    // dump the previous bar and reset things
                    combined.Add(initialTime + Period, accumulated.ToArray(), false);
                    accumulated = Vector<double>.Build.Dense(4);
                    double tx = series[t];
                    accumulated[0] = tx;
                    accumulated[1] = tx;
                    accumulated[2] = tx;
                    accumulated[3] = tx;
                    initialTime = PhaseAdjust(series.TimeStamp(t));
                }
                accumulated[3] = series[t];
                accumulated[1] = Math.Max(accumulated[1], series[t]);
                accumulated[2] = Math.Min(accumulated[2], series[t]);
            }
            IsValid = true;
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> { typeof(TimeSeries) };
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> { typeof(MVTimeSeries) };
        }

    }
}

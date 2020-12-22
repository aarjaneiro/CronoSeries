using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using CronoSeries.ABMath.ModelFramework.Data;

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    [Serializable]
    public class FilterTransform : TimeSeriesTransformation
    {
        [Category("Parameter"), Description("Autoregressive Filter Coefficients")]
        public double[] arCoeffs { get; set; }
        [Category("Parameter"), Description("Moving Average Filter Coefficients")]
        public double[] maCoeffs { get; set; }
        [Category("Parameter"), Description("If non-zero, uses the specified time interval instead of just using successive data points")]
        public TimeSpan TimeInterval { get; set; }

        public FilterTransform()
        {
            arCoeffs = new[] {1.0};
            maCoeffs = new[] {1.0};
            TimeInterval = new TimeSpan(0);
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
                return "Input TS";
            throw new SocketException();
        }

        public override string GetOutputName(int index)
        {
            if (index == 0)
                return "Filtered TS";
            throw new SocketException();
        }

        public override string GetDescription()
        {
            return "Linear time-invariant filter";
        }

        public override string GetShortDescription()
        {
            return "LTI Filter";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}

        private TimeSeries ApplyFilterTo(TimeSeries ts)
        {
            var retval = new TimeSeries();

            int maOrder = maCoeffs.Length;
            int arOrder = arCoeffs.Length;

            double arSum = 0;
            for (int i = 1; i < arOrder; ++i)
                arSum += arCoeffs[i];
            double maSum = 0;
            for (int i = 0; i < maOrder; ++i)
                maSum += maCoeffs[i];

                // now go through and apply filter
                for (int t = 0; t < ts.Count; ++t)
                {
                    double tx = 0.0;
                    if (TimeInterval.Ticks == 0) // apply the filter to successive data points
                    {
                        // get MA part
                        for (int i = 0; i < maOrder; ++i)
                            tx += t >= i ? maCoeffs[i] * ts[t - i] : ts[0];
                        // get AR part
                        for (int i = 1; i < arOrder; ++i)
                            tx += t >= i ? arCoeffs[i] * retval[t - i] : ts[0];
                        tx /= (arSum + maSum);
                    }
                    else // apply the filter to data points at specified sampling interval
                    {
                        // get MA part
                        for (int i = 0; i < maOrder; ++i)
                            tx += maCoeffs[i] * ts.ValueAtTime(ts.TimeStamp(t) - new TimeSpan(i * TimeInterval.Ticks));
                        // get AR part
                        for (int i = 1; i < arOrder; ++i)
                            tx += arCoeffs[i] * retval.ValueAtTime(ts.TimeStamp(t) - new TimeSpan(i * TimeInterval.Ticks));
                        tx /= (arSum + maSum);
                    }
                    retval.Add(ts.TimeStamp(t), tx, false);
                }

            return retval;
        }

        public override void Recompute()
        {
            IsValid = false;
            if (maCoeffs == null || maCoeffs.Length == 0)
                return;
            if (arCoeffs == null || arCoeffs.Length == 0)
                return;

            var tsList = GetInputBundle();
            var results = new List<TimeSeries>();
            foreach (var ts in tsList)
                results.Add(ApplyFilterTo(ts));

            outputs = results;
            IsValid = true;
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket >= NumInputs())
                throw new SocketException();
            return new List<Type> { typeof(TimeSeries), typeof(MVTimeSeries) };
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket >= NumOutputs())
                throw new SocketException();
            return new List<Type> { typeof(TimeSeries), typeof(MVTimeSeries) };
        }
    }
}

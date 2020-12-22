#region License Info
//Component of Cronos Package, http://www.codeplex.com/cronos
//Copyright (C) 2009 Anthony Brockwell

//This program is free software; you can redistribute it and/or
//modify it under the terms of the GNU General Public License
//as published by the Free Software Foundation; either version 2
//of the License, or (at your option) any later version.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with this program; if not, write to the Free Software
//Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
#endregion


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using CronoSeries.ABMath.ModelFramework.Data;

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    /// <summary>
    /// This class samples either a univariate or multivariate time series
    /// </summary>
    [Serializable]
    [DefaultProperty("Transform")]
    public class SamplingTransformation : TimeSeriesTransformation, IExtraFunctionality
    {
        [NonSerialized]
        private TimeSeries tsOutput;
        [NonSerialized]
        private MVTimeSeries mvtsOutput;

        [Category("Parameter"), Description("Time to begin sampling")]
        public DateTime StartTime { get; set; }
        [Category("Parameter"), Description("Time to stop sampling")]
        public DateTime EndTime { get; set; }
        [Category("Parameter"), Description("List of lags (after each sample point) to get data")]
        public List<TimeSpan> SamplingBaseOffsets { get; set; }
        [Category("Parameter"), Description("Interval for sampling, set to 0 to take all available points")]
        public TimeSpan SamplingInterval { get; set; }
        [Category("Parameter"), Description("Set to true if sampler should skip weekends")]
        public bool SkipWeekends { get; set; }
        [Category("Parameter"), Description("List of days to be skipped")]
        public List<DateTime> SkipDates { get; set; }

        public SamplingTransformation()
        {
            // fill in default params
            StartTime = new DateTime(2005, 1, 1, 14, 0, 0);
            EndTime = DateTime.Now;
            SamplingInterval = new TimeSpan(1, 0, 0, 0);  // 1 day
            SkipWeekends = true;
            SamplingBaseOffsets = new List<TimeSpan>();
            SamplingBaseOffsets.Add(new TimeSpan(0));
            SkipDates = new List<DateTime>();
        }

        public override string GetDescription()
        {
            return "Sampler, can sample at regular time intervals, or simply restrict to a specified time range";
        }

        public override string GetShortDescription()
        {
            return "Sample";
        }

        //public override Icon GetIcon()
        //{
        //    var x = Images.ResourceManager.GetObject("SamplerIcon") as Icon;
        //    return x;
        //}

        public override int NumInputs()
        {
            return 1;
        }

        public override int NumOutputs()
        {
            return 1;
        }

        private bool IsWithinSkipIntervals(DateTime dt)
        {
            if (SkipDates == null)
                return false;
            foreach (var skipDate in SkipDates)
                if (dt.Date == skipDate.Date)
                    return true;
            return false;
        }

        private TimeSeries DoUnivariateSampling(TimeSeries ofTS)
        {
            var retval = new TimeSeries();
            retval.Title = ofTS.Title;

            if (SamplingInterval.Ticks == 0)
            {
                int i0, i1;
                bool gotHit;
                i0 = ofTS.IndexAtOrBefore(StartTime, out gotHit);
                if (!gotHit)
                    ++i0;
                i1 = ofTS.IndexAtOrBefore(EndTime, out gotHit);
                ++i1;
                if (i1 > ofTS.Count)
                    i1 = ofTS.Count;
                for (int t=i0 ; t<i1 ; ++t)
                    retval.Add(ofTS.TimeStamp(t), ofTS[t], false);
            }
            else for (var current = new DateTime(StartTime.Ticks); current < EndTime; current = new DateTime((current + SamplingInterval).Ticks))
                foreach (var sampleOffset in SamplingBaseOffsets)
                {
                    var adjusted = current + sampleOffset;
                    bool skipping = false;
                    if (SkipWeekends)
                        if (adjusted.DayOfWeek == DayOfWeek.Saturday || adjusted.DayOfWeek == DayOfWeek.Sunday)
                            skipping = true;
                    if (IsWithinSkipIntervals(adjusted))
                        skipping = true;

                    if (!skipping)
                    {
                        double sampled = ofTS.ValueAtTime(adjusted);
                        retval.Add(adjusted, sampled, false);
                    }
                }

            return retval;
        }

        public override object GetOutput(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            if (tsOutput != null)
                return tsOutput;
            return mvtsOutput;
        }

        public override string GetInputName(int index)
        {
            return "TimeSeries";
        }

        public override string GetOutputName(int index)
        {
            return "TimeSeries";
        }

        public override void Recompute()
        {
            IsValid = false;

            tsOutput = null;
            mvtsOutput = null;

            if (SkipDates == null)
                SkipDates = new List<DateTime>();

            var mvts = GetInput(0) as MVTimeSeries;
            var ts = GetInput(0) as TimeSeries;

            // make sure we have at least 2 data points in the time series to be sampled
            var tp = GetInputType(-1);
            if (tp != InputType.UnivariateTS && tp != InputType.MultivariateTS)
                return;

            if (tp == InputType.MultivariateTS)
            {
                // split it up
                List<TimeSeries> individuals = mvts.ExtractList();
                int nc = individuals.Count;
                var sampled = new List<TimeSeries>(nc);
                IsValid = true;
                for (int i = 0; i < nc; ++i)
                {
                    var res = DoUnivariateSampling(individuals[i]);
                    if (res.Count > 0)
                        sampled.Add(res);
                    else
                        IsValid = false;
                }
                if (IsValid)
                    mvtsOutput = new MVTimeSeries(sampled, false) {Title = mvts.Title};
            }
            if (tp == InputType.UnivariateTS)
            {
                tsOutput = DoUnivariateSampling(ts);
                IsValid = tsOutput.Count > 0;
            }
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> { typeof(TimeSeries), typeof(MVTimeSeries) };
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> { typeof(TimeSeries), typeof(MVTimeSeries) };
        }

        #region Auxiliary Functions

        public int NumAuxiliaryFunctions()
        {
            return 2;
        }

        public string AuxiliaryFunctionName(int index)
        {
            if (index == 0)
                return "ArrayCopy";
            if (index == 1)
                return "AutoRange";
            return null;
        }

        public string AuxiliaryFunctionHelp(int index)
        {
            if (index == 0)
                return "If SamplingBaseOffsets parameter contains more than one offset, then this function will copy samples to the clipboard, with each row containing all offsets relative to the base sampling point.";
            if (index == 1)
                return "Sets beginning and end times of the transformation to match those of the current input.";
            return null;
        }

        #endregion

    }
}

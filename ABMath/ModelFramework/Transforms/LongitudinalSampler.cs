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
    [Serializable]
    public class LongitudinalSampler : TimeSeriesTransformation, IExtraFunctionality
    {
        [NonSerialized] private Longitudinal longitudinal;

        public LongitudinalSampler()
        {
            // fill in default params
            StartTime = new DateTime(2005, 1, 1, 0, 0, 0);
            EndTime = DateTime.Now;
            SamplingInterval = new TimeSpan(1, 0, 0, 0); // 1 day
            SkipWeekends = true;
            SamplingBaseOffsets = new List<TimeSpan> {new TimeSpan(9, 30, 0), new TimeSpan(16, 0, 0)};
            SkipDates = new List<DateTime>();
        }

        [Category("Parameter")] [Description("Time to begin sampling")] public DateTime StartTime { get; set; }

        [Category("Parameter")] [Description("Time to stop sampling")] public DateTime EndTime { get; set; }

        [Category("Parameter")]
        [Description("List of lags (after each sample point) to get data")]
        public List<TimeSpan> SamplingBaseOffsets { get; set; }

        [Category("Parameter")]
        [Description("Interval for sampling, set to 0 to take all available points")]
        public TimeSpan SamplingInterval { get; set; }

        [Category("Parameter")]
        [Description("Set to true if sampler should skip weekends")]
        public bool SkipWeekends { get; set; }

        [Category("Parameter")]
        [Description("List of days to be skipped")]
        public List<DateTime> SkipDates { get; set; }

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
            return null;
        }

        public override string GetDescription()
        {
            return "Longitudinal Sampler";
        }

        public override string GetShortDescription()
        {
            return "Long. Sample";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //    //var x = Images.ResourceManager.GetObject("SamplerIcon") as Icon;
        //    //return x;
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

        private List<TimeSeries> DoUnivariateSampling(TimeSeries ofTS)
        {
            var retval = new List<TimeSeries>();

            for (var current = new DateTime(StartTime.Ticks);
                current < EndTime;
                current = new DateTime((current + SamplingInterval).Ticks))
            {
                var slice = new TimeSeries();
                foreach (var sampleOffset in SamplingBaseOffsets)
                {
                    var adjusted = current + sampleOffset;
                    var skipping = false;
                    if (SkipWeekends)
                        if (adjusted.DayOfWeek == DayOfWeek.Saturday || adjusted.DayOfWeek == DayOfWeek.Sunday)
                            skipping = true;
                    if (IsWithinSkipIntervals(adjusted))
                        skipping = true;

                    if (!skipping)
                    {
                        var sampled = ofTS.ValueAtTime(adjusted);
                        slice.Add(adjusted, sampled, false);
                    }
                }

                slice.Title = current.ToString();
                if (slice.Count > 0)
                    retval.Add(slice);
            }

            return retval;
        }

        public override object GetOutput(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return longitudinal;
        }

        public override string GetInputName(int index)
        {
            return "TimeSeries";
        }

        public override string GetOutputName(int index)
        {
            return "Long. Data";
        }

        public override void Recompute()
        {
            IsValid = false;

            longitudinal = null;

            if (SkipDates == null)
                SkipDates = new List<DateTime>();

            var ts = GetInput(0) as TimeSeries;
            if (ts == null)
                return;

            if (ts.Count < 2) return;
            if (SamplingBaseOffsets.Count < 1)
                return;

            longitudinal = new Longitudinal(DoUnivariateSampling(ts));
            IsValid = true;
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> {typeof(TimeSeries)};
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> {typeof(Longitudinal)};
        }
    }
}
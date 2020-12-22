/*
Derived from the Cronos Package, http://www.codeplex.com/cronos
Copyright (C) 2009 Anthony Brockwell

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
*/


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using CronoSeries.ABMath.Data;

namespace CronoSeries.ABMath.Transforms
{
    [Serializable]
    public class ExpSmoother : TimeSeriesTransformation
    {
        public ExpSmoother()
        {
            SmoothFactor = 0.9;
        }

        [Category("Parameter")] [Description("Smoothing Factor")] public double SmoothFactor { get; set; }

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
            return "Exponential smoother";
        }

        public override string GetShortDescription()
        {
            return "ExpSmooth";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}

        private TimeSeries ApplyFilterTo(TimeSeries ts)
        {
            var retval = new TimeSeries();

            // now go through and apply filter
            for (var t = 0; t < ts.Count; ++t)
            {
                var tx = 0.0;
                // get MA part
                tx += (1 - SmoothFactor) * ts[t];
                // get AR part
                tx += t >= 1 ? SmoothFactor * retval[t - 1] : 0;
                retval.Add(ts.TimeStamp(t), tx, false);
            }

            return retval;
        }

        public override void Recompute()
        {
            IsValid = false;

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
            return new List<Type> {typeof(TimeSeries), typeof(MVTimeSeries)};
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket >= NumOutputs())
                throw new SocketException();
            return new List<Type> {typeof(TimeSeries), typeof(MVTimeSeries)};
        }
    }
}
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
using System.Text;
using CronoSeries.ABMath.Data;

namespace CronoSeries.ABMath.Transforms
{
    [Serializable]
    public class MidpointTransformation : TimeSeriesTransformation
    {
        [NonSerialized] private TimeSeries midPoints;


        public MidpointTransformation()
        {
            SpreadLimit = 0.04;
        }

        [Category("Parameter")]
        [Description("Limit: when bid-ask spread is greater than this, no midpoint is recorded.")]
        public double SpreadLimit { get; set; }

        [Category("Parameter")] [Description("Name to Assign to Result")] public string AssignedName { get; set; }

        public override void Recompute()
        {
            IsValid = false;
            if (GetInputType(-1) != InputType.MultivariateTS)
                return;

            midPoints = null;
            var series = GetInputBundle();
            if (series.Count != 2)
                return; // something wrong!

            // now we should be able to do something
            var t0 = 0; // index of next one to check
            var t1 = 0;
            var T0 = series[0].Count;
            var T1 = series[1].Count;

            midPoints = new TimeSeries();
            if (string.IsNullOrEmpty(AssignedName))
                midPoints.Title = "Mid";
            else
                midPoints.Title = AssignedName;

            var done = false;
            while (!done)
            {
                var dt0 = t0 < T0 ? series[0].TimeStamp(t0) : DateTime.MaxValue;
                var dt1 = t1 < T1 ? series[1].TimeStamp(t1) : DateTime.MaxValue;
                if (dt0 == dt1)
                {
                    // got one!  only record midpoint if the spread is sufficiently small
                    var gap = Math.Abs(series[0][t0] - series[1][t1]);
                    if (gap < SpreadLimit) // less than 4 basis points
                        midPoints.Add(series[0].TimeStamp(t0), (series[0][t0] + series[1][t1]) / 2.0, true);
                    ++t0;
                    ++t1;
                }

                if (dt0 < dt1)
                    ++t0;
                if (dt0 > dt1)
                    ++t1;

                done = t0 == T0 && t1 == T1;
            }

            outputs = new List<TimeSeries>(1);
            outputs.Add(midPoints);
            IsValid = true;
        }

        public override string GetDescription()
        {
            return "Midpoint of two time series.";
        }

        public override string GetShortDescription()
        {
            return "Mid(X,Y)";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}

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
                return "Bid and Ask (2-D MVTS)";
            throw new SocketException();
        }

        public override string GetOutputName(int index)
        {
            if (index == 0)
                return "Mid-Price";
            throw new SocketException();
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
            return new List<Type> {typeof(TimeSeries)};
        }

        public override bool SetInput(int socket, object item, StringBuilder failMsg)
        {
            CheckInputsReady();
            if (socket >= NumInputs())
                throw new ArgumentException("Bad socket.");

            var mts = item as MVTimeSeries;
            if (mts == null || mts.Dimension != 2)
            {
                failMsg.Append(
                    "Input to mid-price transform must be a bivariate series containing bid and ask prices.");
                return false; // failure when tsInput item is not of class TimeSeries or MVTimeSeries
            }

            socketedInputs[socket] = item;

            if (AllInputsValid())
                Recompute();
            return true;
        }
    }
}
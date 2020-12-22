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
using CronoSeries.ABMath.ModelFramework.Data;

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    /// <summary>
    ///     This differencing transformation can operate on univariate, multivariate or longitudinal data.
    /// </summary>
    [Serializable]
    public class DifferenceTransformation : TimeSeriesTransformation
    {
        [NonSerialized] private TimeSeries differences;

        [NonSerialized] private Longitudinal longDifferences;

        [NonSerialized] private MVTimeSeries mvDifferences;

        public DifferenceTransformation()
        {
            Lag = 1;
            Spacing = 1;
            Phase = 0;
        }

        [Category("Parameter")] [Description("Lag at which to difference")] public int Lag { get; set; }

        [Category("Parameter")]
        [Description(
            "Time stamp mode: if true, the time stamp is at the beginning of the interval, if false, it's at the end.")]
        public bool LeftTimeStamps { get; set; }

        [Category("Parameter")]
        [Description("Padding: set to true to pad output with zeros")]
        public bool PadWithZeroes { get; set; }

        [Category("Parameter")] [Description("Spacing at which to sample input")] public int Spacing { get; set; }

        [Category("Parameter")] [Description("Offset used if spacing is greater than 1")] public int Phase { get; set; }


        public override string GetDescription()
        {
            return "Difference";
        }

        public override string GetShortDescription()
        {
            if (Lag == 1)
                return "Diff";
            return $"Diff_{Lag}";
        }

        //public override Icon GetIcon()
        //{
        //    var x = Images.ResourceManager.GetObject("DifferenceIcon") as Icon;
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

        public override object GetOutput(int socket)
        {
            if (differences != null)
                return differences;
            if (mvDifferences != null)
                return mvDifferences;
            return longDifferences;
        }

        public override string GetInputName(int index)
        {
            return "TimeSeries";
        }

        public override string GetOutputName(int index)
        {
            return "TimeSeries";
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> {typeof(TimeSeries), typeof(MVTimeSeries)};
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> {typeof(TimeSeries), typeof(MVTimeSeries)};
        }

        public override void Recompute()
        {
            var mts = GetInput(0) as MVTimeSeries;
            var uts = GetInput(0) as TimeSeries;
            var lts = GetInput(0) as Longitudinal;

            if (Spacing == 0)
                Spacing = 1; // just fix it here: this is for backwards-compatibility

            mvDifferences = null;
            differences = null;
            longDifferences = null;
            IsValid = false;

            if (mts != null)
            {
                mvDifferences = new MVTimeSeries(mts.Dimension) {Title = "Diffs"};
                for (var i = 0; i < mts.Dimension; ++i)
                    mvDifferences.SubTitle[i] = mts.SubTitle[i];
                var zerov = new double[mts.Dimension];

                if (!LeftTimeStamps && PadWithZeroes)
                    for (var t = 0; t < Lag; ++t)
                        if (t % Spacing == Phase)
                            mvDifferences.Add(mts.TimeStamp(t), zerov, false);

                for (var t = Phase; t < mts.Count; t += Spacing)
                    if (t >= Lag)
                    {
                        var diff = new double[mts.Dimension];
                        for (var j = 0; j < mts.Dimension; ++j)
                            diff[j] = mts[t][j] - mts[t - Lag][j];
                        var stamp = LeftTimeStamps ? mts.TimeStamp(t - Lag) : mts.TimeStamp(t);
                        mvDifferences.Add(stamp, diff, false);
                    }

                if (LeftTimeStamps && PadWithZeroes)
                    for (var t = mts.Count - Lag; t < mts.Count; ++t)
                        if (t % Spacing == Phase)
                            mvDifferences.Add(mts.TimeStamp(t), zerov, false);
            }

            if (uts != null)
            {
                differences = new TimeSeries {Title = $"Diff({uts.Title})"};

                if (!LeftTimeStamps && PadWithZeroes)
                    for (var t = 0; t < Lag; ++t)
                        if (t % Spacing == Phase)
                            differences.Add(uts.TimeStamp(t), 0, false);

                for (var t = Phase; t < uts.Count; t += Spacing)
                    if (t >= Lag)
                    {
                        var diff = uts[t] - uts[t - Lag];
                        var stamp = LeftTimeStamps ? uts.TimeStamp(t - Lag) : uts.TimeStamp(t);
                        differences.Add(stamp, diff, false);
                    }

                if (LeftTimeStamps && PadWithZeroes)
                    for (var t = uts.Count - Lag; t < uts.Count; ++t)
                        if (t % Spacing == Phase)
                            differences.Add(uts.TimeStamp(t), 0, false);
            }

            if (lts != null)
            {
                var segments = new List<TimeSeries>(lts.Count);
                for (var i = 0; i < lts.Count; ++i)
                {
                    uts = lts[i];
                    var du = new TimeSeries();
                    for (var t = Lag; t < uts.Count; ++t)
                    {
                        var diff = uts[t] - uts[t - Lag];
                        var stamp = LeftTimeStamps ? uts.TimeStamp(t - Lag) : uts.TimeStamp(t);
                        du.Add(stamp, diff, false);
                    }

                    segments.Add(du);
                }

                longDifferences = new Longitudinal(segments);
            }

            IsValid = true;
        }
    }
}
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
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    [Serializable]
    public class BollingerBandTransform : TimeSeriesTransformation
    {
        public BollingerBandTransform()
        {
            NumPeriods = 20;
            Width = 1.96;
        }

        [Category("Parameter")]
        [Description("Number of periods to use for rolling mean/std.dev.")]
        public int NumPeriods { get; set; }

        [Category("Parameter")]
        [Description("# of standard deviations from mean at which to put bands.")]
        public double Width { get; set; }

        public override int NumInputs()
        {
            return 1;
        }

        public override int NumOutputs()
        {
            return 2;
        }

        public override string GetInputName(int index)
        {
            return "Time Series";
        }

        public override string GetOutputName(int socket)
        {
            if (socket == 0)
                return "Series with Bands";
            if (socket == 1)
                return "Position Indicator";
            throw new SocketException();
        }

        public override object GetOutput(int socket)
        {
            if (socket == 0)
            {
                var subList = new List<TimeSeries> {outputs[0], outputs[1], outputs[2]};
                var bundle = new MVTimeSeries(subList, false);
                bundle.Title = "Bollinger";
                return bundle;
            }

            if (socket == 1)
                return outputs[3];
            throw new SocketException();
        }

        public override string GetDescription()
        {
            return "Bollinger band transformation from univariate to trivariate series";
        }

        public override string GetShortDescription()
        {
            return "Boll. Bands";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}

        public override void Recompute()
        {
            IsValid = false;

            var inputs = GetInputBundle();
            if (inputs.Count != 1)
                return;

            var input = inputs[0];

            var values = new TimeSeries();
            var lower = new TimeSeries();
            var upper = new TimeSeries();
            var indicators = new TimeSeries();

            var acc = new List<double>(); //new Accumulator();
            for (var t = 0; t < input.Count; ++t)
            {
                acc.Add(input[t]);
                if (acc.Count > NumPeriods)
                {
                    var v = Vector<double>.Build.DenseOfArray(acc.ToArray());
                    var sigma = v.PopulationStandardDeviation();
                    var mean = v.Mean();
                    acc.Remove(input[t - NumPeriods]);
                    values.Add(input.TimeStamp(t), input[t], false);
                    lower.Add(input.TimeStamp(t), mean - Width * sigma, false);
                    upper.Add(input.TimeStamp(t), mean + Width * sigma, false);
                    var sig = sigma;
                    indicators.Add(input.TimeStamp(t), sig != 0 ? (input[t] - mean) / sig : 0, false);
                }
            }

            values.Title = "Level";
            lower.Title = "Lower Band";
            upper.Title = "Upper Band";
            indicators.Title = "Indicator";

            outputs = new List<TimeSeries>(4) {values, lower, upper, indicators};

            IsValid = true;
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket >= NumInputs())
                throw new SocketException();
            return new List<Type> {typeof(TimeSeries)};
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket == 0)
                return new List<Type> {typeof(MVTimeSeries)};
            if (socket == 1)
                return new List<Type> {typeof(TimeSeries)};
            throw new SocketException();
        }
    }
}
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
    /// <summary>
    ///     This transformation takes 2 or more univariate or multivariate time series and binds them together into
    ///     a new multivariate time series.
    /// </summary>
    [Serializable]
    public class BindingTransformation : TimeSeriesTransformation
    {
        [NonSerialized] private MVTimeSeries mvtsOut;

        [Category("Parameter")]
        [Description("Number of inputs to be bound into a multivariate series.")]
        public int NumberOfInputs { get; set; }

        [Category("Parameter")]
        [Description("If true, will fill in missing values by sampling.")]
        public bool SampleMissing { get; set; }

        private void CheckParameters()
        {
            if (NumberOfInputs == 0)
                NumberOfInputs = 2;
        }

        public override int NumInputs()
        {
            CheckParameters();
            return NumberOfInputs;
        }


        public override int NumOutputs()
        {
            return 1;
        }

        public override object GetOutput(int socket)
        {
            if (socket == 0)
                return mvtsOut;
            throw new SocketException();
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket == 0)
                return new List<Type> {typeof(MVTimeSeries)};
            throw new SocketException();
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket < NumberOfInputs)
                return new List<Type> {typeof(TimeSeries), typeof(MVTimeSeries)};
            throw new SocketException();
        }

        public override string GetInputName(int index)
        {
            return "Input #" + (index + 1);
        }

        public override string GetOutputName(int index)
        {
            return "Multivariate Output";
        }

        public override string GetDescription()
        {
            return "Multivariate Binding";
        }

        public override string GetShortDescription()
        {
            return "MV Bind";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}

        public override void Recompute()
        {
            CheckParameters();
            var list1 = new List<TimeSeries>();
            IsValid = true;

            // otherwise we can bind them together
            var lists = new List<List<TimeSeries>>();
            for (var i = 0; i < NumInputs(); ++i)
            {
                var ts = GetInput(i);
                var mts = ts as MVTimeSeries;
                if (mts != null)
                {
                    lists.Add(mts.ExtractList());
                }
                else
                {
                    var uts = ts as TimeSeries;
                    if (uts != null)
                    {
                        var lts = new List<TimeSeries>();
                        lts.Add(uts);
                        lists.Add(lts);
                    }
                    else
                    {
                        IsValid = false;
                    }
                }
            }

            if (IsValid)
            {
                foreach (var lts in lists)
                foreach (var ts in lts)
                    list1.Add(ts);

                mvtsOut = new MVTimeSeries(list1, SampleMissing);
                mvtsOut.Title = "MV";
            }
        }
    }
}
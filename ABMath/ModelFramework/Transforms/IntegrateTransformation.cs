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
using System.Net.Sockets;
using CronoSeries.ABMath.ModelFramework.Data;

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    [Serializable]
    public class IntegrateTransformation : TimeSeriesTransformation
    {
        [NonSerialized]
        private TimeSeries integral;
        [NonSerialized]
        private MVTimeSeries mvIntegral;

        //[Category("Parameter"), Description("Set to true if integral should integrate as step-function (using time-gaps), false if it should just integrate as sum of dirac-delta spikes.")]
        //public double UseTimeIntervals { get; set; }

        public override string GetDescription()
        {
            return "Integral";
        }

        public override string GetShortDescription()
        {
            return "Integral";
        }

        //public override Icon GetIcon()
        //{
        //    var x = Images.ResourceManager.GetObject("IntegralIcon") as Icon;
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
            if (integral != null)
                return integral;
            return mvIntegral;
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
            var tp = GetInputType(-1);
            if (tp == InputType.MultivariateTS)
            {
                var mts = GetInput(0) as MVTimeSeries;
                if (mts == null)
                    return;
                mvIntegral = new MVTimeSeries(mts.Dimension) { Title = "MV" };
                for (int i = 0; i < mts.Dimension; ++i)
                    mvIntegral.SubTitle[i] = mts.SubTitle[i];
                integral = null;
                var sum = new double[mts.Dimension];
                for (int t = 0; t < mts.Count; ++t)
                {
                    for (int j = 0; j < mts.Dimension; ++j)
                        if (!double.IsNaN(mts[t][j]))
                           sum[j] += mts[t][j];
                    var temp = new double[mts.Dimension];
                    Array.Copy(sum, temp, mts.Dimension);
                    mvIntegral.Add(mts.TimeStamp(t), temp, false);
                }
            }
            if (tp == InputType.UnivariateTS)
            {
                var ts = GetInput(0) as TimeSeries;
                if (ts == null)
                    return;
                integral = new TimeSeries { Title = ts.Title };
                mvIntegral = null;
                double sum = 0;
                for (int t = 0; t < ts.Count; ++t)
                {
                    sum += ts[t];
                    integral.Add(ts.TimeStamp(t), sum, false);
                }
            }
            IsValid = true;
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

    }
}

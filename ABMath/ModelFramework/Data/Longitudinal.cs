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
using System.Text;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.ModelFramework.Data
{
    /// <summary>
    ///     This class represents longitudinal data, that is, a collection of time series.
    /// </summary>
    [Serializable]
    public class Longitudinal : IConnectable
    {
        private List<TimeSeries> Components;

        public Longitudinal()
        {
            Components = new List<TimeSeries>();
            MaxCount = 0;
        }

        public Longitudinal(IEnumerable<TimeSeries> initialComponents)
        {
            Components = new List<TimeSeries>();
            foreach (var ts in initialComponents)
            {
                Components.Add(ts);
                if (ts.Count > MaxCount)
                    MaxCount = ts.Count;
            }
        }

        public TimeSeries this[int idx] => Components[idx];

        public int MaxCount { get; protected set; }

        public int Count
        {
            get
            {
                if (Components == null) return 0;
                return Components.Count;
            }
        }

        public double SampleMean()
        {
            double sum = 0;
            var count = 0;
            foreach (var c in Components)
            {
                for (var t = 0; t < c.Count; ++t)
                    sum += c[t];
                count += c.Count;
            }

            return sum / count;
        }

        public Vector<double> SampleACF(int maxLag)
        {
            var retval = Vector<double>.Build.Dense(maxLag + 1);
            retval[0] = 1.0;
            var m = MaxCount;
            var mean = SampleMean();

            for (var h = 0; h <= maxLag; ++h)
            {
                var tc = m - h;
                double tx = 0;
                var localCount = 0;
                for (var i = 0; i < Count; ++i)
                for (var t = 0; t < tc; ++t)
                    if (t + h < Components[i].Count)
                    {
                        tx += (Components[i][t] - mean) * (Components[i][t + h] - mean);
                        ++localCount;
                    }

                if (localCount > 0)
                    tx /= localCount;
                else
                    tx = double.NaN;
                retval[h] = tx;
            }

            retval /= retval[0];
            return retval;
        }

        #region IConnectable Stuff

        private string toolTipText;

        public int NumInputs()
        {
            return 0;
        }

        public int NumOutputs()
        {
            return 1;
        }

        public string GetInputName(int socket)
        {
            throw new ArgumentException("There are no inputs.");
        }

        public string GetOutputName(int socket)
        {
            return "Long. Data";
        }

        public List<Type> GetAllowedInputTypesFor(int socket)
        {
            return new List<Type>();
        }

        public List<Type> GetOutputTypesFor(int socket)
        {
            return new List<Type> {typeof(Longitudinal)};
        }

        public bool InputIsFree(int socket)
        {
            return false;
        }

        public bool SetInput(int socket, object item, StringBuilder failMessage)
        {
            failMessage.AppendLine("There are no inputs.");
            return false;
        }

        public object GetOutput(int socket)
        {
            if (socket == 0)
                return this;
            throw new ArgumentException("Invalid socket");
        }

        public string GetDescription()
        {
            return "Longitudinal data";
        }

        public string GetShortDescription()
        {
            return "Long. Data";
        }

        //public Color GetBackgroundColor()
        //{
        //    return Color.Fuchsia;
        //}

        //public Icon GetIcon()
        //{
        //    return null;
        //}

        public string ToolTipText
        {
            get => toolTipText;
            set => toolTipText = value;
        }

        #endregion
    }
}
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
using System.IO;
using System.Text;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.Data
{
    [Serializable]
    public class MVTimeSeries : TimeSeriesBase<double[]>, ICopyable, IConnectable
    {
        //public Color GetBackgroundColor()
        //{
        //    return Color.GreenYellow;
        //}

        //public Icon GetIcon()
        //{
        //    return null;
        //}

        private string toolTipText;

        public MVTimeSeries()
        {
            Dimension = 1;
        }

        public MVTimeSeries(int dimension)
        {
            Dimension = dimension;
            SubTitle = new string[dimension];
        }


        /// <summary>
        ///     constructor that builds a single multivariate time series from multiple univariate time series
        /// </summary>
        /// <param name="components"></param>
        /// <param name="sampleMissing"></param>
        public MVTimeSeries(IList<TimeSeries> components, bool sampleMissing)
        {
            Dimension = components.Count;

            SubTitle = new string[Dimension];
            for (var i = 0; i < Dimension; ++i)
                SubTitle[i] = components[i].Title;

            var indices = new int[Dimension]; // these are the indices of the last one added (-1 if none yet)
            for (var i = 0; i < Dimension; ++i)
                indices[i] = -1;

            var done = false;
            while (!done)
            {
                // get timestamp for next point
                var minTime = DateTime.MaxValue;
                for (var i = 0; i < Dimension; ++i)
                    if (indices[i] < components[i].Count - 1)
                        if (components[i].TimeStamp(indices[i] + 1) < minTime)
                            minTime = components[i].TimeStamp(indices[i] + 1);

                // now construct the new point
                var newPt = new double[Dimension];
                for (var i = 0; i < Dimension; ++i)
                    if (indices[i] < components[i].Count - 1)
                        if (components[i].TimeStamp(indices[i] + 1) == minTime)
                        {
                            newPt[i] = components[i][indices[i] + 1];
                            ++indices[i];
                        }
                        else
                        {
                            newPt[i] = double.NaN;
                        }
                    else
                        newPt[i] = double.NaN;

                // add it
                if (sampleMissing)
                    for (var i = 0; i < Dimension; ++i)
                        if (double.IsNaN(newPt[i]))
                            newPt[i] = components[i].ValueAtTime(minTime);

                Add(minTime, newPt, false);

                // and determine if we are done
                done = true;
                for (var i = 0; i < Dimension; ++i)
                    done &= indices[i] >= components[i].Count - 1;
            }
        }

        public string[] SubTitle { get; set; }

        public int NumInputs()
        {
            return 0;
        }

        public int NumOutputs()
        {
            return 1;
        }

        public virtual List<Type> GetOutputTypesFor(int socket)
        {
            return new List<Type> {typeof(MVTimeSeries)};
        }

        public bool InputIsFree(int socket)
        {
            return false;
        }

        public bool SetInput(int socket, object item, StringBuilder failMsg)
        {
            return false; // cannot set input
        }

        public object GetOutput(int socket)
        {
            if (socket == 0)
                return this;
            throw new ApplicationException("MVTimeSeries only has output socket 0.");
        }

        public string GetInputName(int index)
        {
            return null;
        }

        public string GetOutputName(int index)
        {
            return "TimeSeries";
        }

        public List<Type> GetAllowedInputTypesFor(int socket)
        {
            return null;
        }

        public string GetDescription()
        {
            return string.Format("MV Time Series (Length={0})", Count);
        }

        public string GetShortDescription()
        {
            if (Title == null)
                return string.Format("MVTS({0})", Count);
            return string.Format("{0}{1}({2})", Title, Environment.NewLine, Count);
        }

        public string ToolTipText
        {
            get => toolTipText;
            set => toolTipText = value;
        }

        public string CreateFullString(int detailLevel)
        {
            var sb = new StringBuilder(16384);

            // create header
            if (detailLevel > 0)
            {
                if (detailLevel > 1)
                    sb.AppendFormat("Date\t");
                for (var i = 0; i < Dimension; ++i)
                {
                    if (i != 0)
                        sb.Append("\t");
                    if (SubTitle[i] != null)
                        sb.AppendFormat(SubTitle[i]);
                }

                sb.AppendLine();
            }

            // now copy data
            for (var t = 0; t < Count; ++t)
            {
                var timeStamp = TimeStamp(t);
                if (detailLevel > 1)
                    sb.AppendFormat("{0}\t", timeStamp.ToString("MM/dd/yyyy HH:mm:ss.ffff"));
                for (var i = 0; i < Dimension; ++i)
                {
                    if (i != 0)
                        sb.Append("\t");
                    sb.AppendFormat("{0:0.000000}", values[t][i]);
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        public void ParseFromFullString(string imported)
        {
            values = new List<double[]>();
            times = new List<DateTime>();

            if (imported == null)
                return;
            if (imported.Length < 1)
                return;

            var sreader = new StringReader(imported);
            var collection = TimeSeries.GetTSFromReader(sreader, true);
            var mv = new MVTimeSeries(collection, false);

            values = mv.values;
            times = mv.times;
            Title = mv.Title;
            SubTitle = mv.SubTitle;
            Dimension = mv.Dimension;
        }

        /// <summary>
        ///     returns total number of NaNs (maximum is Count*Dimension)
        /// </summary>
        /// <returns></returns>
        public int NaNCount()
        {
            var count = 0;
            for (var t = 0; t < Count; ++t)
            for (var j = 0; j < Dimension; ++j)
                if (double.IsNaN(values[t][j]))
                    ++count;
            return count;
        }


        /// <summary>
        ///     converts the multivariate time series object into a list of newly created (univariate) timeseries objects
        /// </summary>
        /// <returns></returns>
        public List<TimeSeries> ExtractList()
        {
            var retval = new List<TimeSeries>(Dimension);
            for (var i = 0; i < Dimension; ++i)
                retval.Add(new TimeSeries());

            for (var i = 0; i < Dimension; ++i)
            {
                retval[i].Title = $"Comp.#{i + 1}";
                if (SubTitle != null)
                    if (SubTitle.Length > i)
                        retval[i].Title = SubTitle[i];
                for (var t = 0; t < Count; ++t)
                {
                    var tx = this[t][i];
                    if (!double.IsNaN(tx))
                        retval[i].Add(TimeStamp(t), tx, false);
                }
            }

            return retval;
        }

        public Vector<double> SampleMean()
        {
            var retval = Vector<double>.Build.Dense(Dimension);
            var numMissing = new int[Dimension];
            for (var t = 0; t < Count; ++t)
            for (var j = 0; j < Dimension; ++j)
                if (!double.IsNaN(values[t][j]))
                    retval[j] += values[t][j];
                else
                    ++numMissing[j];
            for (var j = 0; j < Dimension; ++j)
                if (Count - numMissing[j] > 0)
                    retval[j] *= 1.0 / (Count - numMissing[j]);
            return retval;
        }

        public Matrix<double>[] ComputeACF(int maxLag, bool normalize)
        {
            var acf = new Matrix<double>[maxLag + 1];

            int i, j, n = Count;
            if (n == 0)
                return null;

            var mean = SampleMean();

            // compute the sample autocovariance function.
            for (i = 0; i <= maxLag; ++i)
            {
                var total = Matrix<double>.Build.Dense(Dimension, Dimension);
                for (j = i; j < n; ++j)
                for (var k = 0; k < Dimension; ++k)
                for (var l = 0; l < Dimension; ++l)
                {
                    var tx = (values[j][k] - mean[k]) * (values[j - i][l] - mean[l]);
                    if (!double.IsNaN(tx))
                        total[k, l] += tx;
                }

                acf[i] = total * (1.0 / n);
            }

            // now normalize the sample ACFs
            if (normalize)
            {
                var diags = new double[Dimension];
                for (i = 0; i < Dimension; ++i)
                    diags[i] = Math.Sqrt(acf[0][i, i]);

                for (i = 0; i <= maxLag; ++i)
                for (j = 0; j < Dimension; ++j)
                for (var k = 0; k < Dimension; ++k)
                    acf[i][j, k] /= diags[j] * diags[k];
            }

            return acf;
        }

        public int NumAuxiliaryFunctions()
        {
            return 0;
        }

        public string AuxiliaryFunctionName(int index)
        {
            return null;
        }

        public bool AuxiliaryFunction(int index)
        {
            return false;
        }
    }
}
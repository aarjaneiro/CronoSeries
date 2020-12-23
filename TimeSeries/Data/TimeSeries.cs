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

namespace CronoSeries.TimeSeries.Data
{
    [Serializable]
    public class TimeSeries : TimeSeriesBase<double>, ICopyable, IConnectable
    {
        private string toolTipText;

        /// <summary>
        ///     Time series object is a list of times and a list of values.
        /// </summary>
        public TimeSeries()
        {
            Dimension = 1;
        }

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
            return new List<Type> {typeof(TimeSeries)};
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
            throw new ApplicationException("TimeSeriesBase only has output socket 0.");
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
            var sb = new StringBuilder(128);
            if (!string.IsNullOrEmpty(Title))
                sb.AppendFormat("{0}: ", Title);
            sb.AppendFormat("Time Series (Length {0})", Count);
            return sb.ToString();
        }

        public string GetShortDescription()
        {
            if (Title == null)
                return string.Format("TS({0})", Count);
            return string.Format("{0}{1}({2})", Title, Environment.NewLine, Count);
        }

        public string ToolTipText
        {
            get => toolTipText;
            set => toolTipText = value;
        }

        /// <summary>
        ///     creates string that contains all info
        /// </summary>
        /// <param name="detailLevel">0=just values, 1=headers, 2=headers + dates</param>
        /// <returns></returns>
        public string CreateFullString(int detailLevel)
        {
            var sb = new StringBuilder(16384);

            // create header
            if (detailLevel > 0)
            {
                if (detailLevel > 1)
                    sb.AppendFormat("Date\t");
                sb.Append(Title);
                sb.AppendLine();
            }

            // now copy data
            for (var t = 0; t < Count; ++t)
            {
                var timeStamp = TimeStamp(t);
                if (detailLevel > 1)
                    sb.AppendFormat("{0}\t", timeStamp.ToString("MM/dd/yyyy HH:mm:ss.ffff"));
                sb.AppendFormat("{0:0.000000}", values[t]);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public void ParseFromFullString(string imported)
        {
            values = new List<double>();
            times = new List<DateTime>();

            if (imported == null)
                return;
            if (imported.Length < 1)
                return;

            var sreader = new StringReader(imported);
            var collection = GetTSFromReader(sreader, true);
            if (collection != null)
                if (collection.Count == 1)
                {
                    values = collection[0].values;
                    times = collection[0].times;
                    Title = collection[0].Title;
                }
        }

        /// <summary>
        ///     A convenient setter only requiring dates and data as <see cref="List{T}" /> objects.
        /// </summary>
        /// <param name="dates">List of <see cref="DateTime" /></param>
        /// <param name="data">List of <see cref="double" /></param>
        /// <param name="title">Optional title for the Time Series</param>
        public void DataFromLists(List<DateTime> dates, List<double> data, string title = null)
        {
            values = data;
            times = dates;
            Title = title;
        }

        public void SubtractConstant(double k)
        {
            for (var i = 0; i < Count; ++i)
                values[i] -= k;
        }

        public int NaNCount()
        {
            var count = 0;
            for (var t = 0; t < Count; ++t)
                if (double.IsNaN(values[t]))
                    ++count;
            return count;
        }

        public void RemoveNaNs()
        {
            for (var t = 0; t < Count; ++t)
                if (double.IsNaN(values[t]))
                {
                    values.RemoveAt(t);
                    times.RemoveAt(t);
                    --t;
                }
        }

        public override string ToString()
        {
            return Title;
        }

        public double SampleMean()
        {
            double total = 0;
            foreach (var x in values)
                total += x;
            total /= Count;
            return total;
        }

        public double SampleVariance()
        {
            double total = 0;
            double ss = 0;
            foreach (var x in values)
            {
                total += x;
                ss += x * x;
            }

            total /= Count;
            return ss / Count - total * total;
        }

        public static Vector<double> GetPACFFrom(Vector<double> ACF)
        {
            var maxlag = ACF.Count - 1;
            if (maxlag <= 0)
                return null;

            // compute the sample PACF using the Durbin-Levinson algorithm (p.169 B&D)
            var PACF = Vector<double>.Build.Dense(maxlag + 1);
            var phis = Vector<double>.Build.Dense(maxlag + 1);
            var phis2 = Vector<double>.Build.Dense(maxlag + 1);
            double vi, phinn;
            phis[0] = ACF[1] / ACF[0];
            PACF[0] = 1.0;
            PACF[1] = phis[0];
            vi = ACF[0];
            vi = vi * (1 - phis[0] * phis[0]);
            for (var i = 2; i <= maxlag; ++i) // i=iteration number
            {
                for (var j = 0; j < i - 1; ++j)
                    phis2[j] = phis[i - j - 2];
                phinn = ACF[i];
                for (var j = 1; j < i; ++j)
                    phinn -= phis[j - 1] * ACF[i - j];
                phinn /= vi;
                for (var j = 0; j < i - 1; ++j)
                    phis[j] -= phinn * phis2[j];
                vi = vi * (1 - phinn * phinn);
                PACF[i] = phis[i - 1] = phinn;
            }

            return PACF;
        }

        /// <summary>
        ///     computes autocovariance (or autocorrelation if normalize==true) from lag 0 to lag maxlag
        /// </summary>
        /// <param name="maxLag">maximum lag to compute autocovariance at</param>
        /// <param name="normalize">if true, then return value is normalized (so it represents autocorrelations)</param>
        /// <returns></returns>
        public Vector<double> ComputeACF(int maxLag, bool normalize)
        {
            var acf = Vector<double>.Build.Dense(maxLag + 1);

            int i, j, n = Count;
            if (n == 0)
                return null;

            var mean = SampleMean();

            // compute the sample autocovariance function.
            for (i = 0; i <= maxLag; ++i)
            {
                double total = 0;
                for (j = i; j < n; ++j)
                    total += (values[j] - mean) * (values[j - i] - mean);
                acf[i] = total / n;
            }

            // now normalize the sample ACFs
            if (normalize)
            {
                if (acf[0] != 0.0)
                    acf *= 1.0 / acf[0];
                acf[0] = 1.0;
            }

            return acf;
        }

        public static bool IsNumeric(string s)
        {
            var numeric = true;
            var stripped = s.Trim();
            if (stripped.Length == 0)
                return false;
            char[] allowed = {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.'};
            var allowedList = new List<char>(allowed);
            if (!allowedList.Contains(stripped[0]))
                if (stripped[0] != '-' && stripped[0] != '+')
                    numeric = false;
            for (var i = 1; i < stripped.Length; ++i)
                if (!allowedList.Contains(stripped[i]))
                    numeric = false;
            return numeric;
        }

        public static List<TimeSeries> GetTSFromReader(TextReader sreader, bool ignoreDuplicates)
        {
            // load from opendialog.FileName
            int tsCount;
            string line;
            string[] headers;

            // start with header
            line = sreader.ReadLine();
            headers = line.Split(',', '\t');

            // Check to see if
            // (1) header is numeric
            var hasHeader = false;
            for (var i = 1; i < headers.Length; ++i)
                if (!IsNumeric(headers[i]))
                    hasHeader = true;
            // (2) first column is date/times
            if (hasHeader)
                line = sreader.ReadLine();
            if (line == null)
                return null;
            var hasDates = true;
            var pieces = line.Split(',', '\t');
            if (IsNumeric(pieces[0]))
                hasDates = false;
            var offset = hasDates ? 1 : 0;
            tsCount = pieces.Length - offset;

            var collection = new List<TimeSeries>();

            for (var i = 0; i < tsCount; ++i)
                collection.Add(new TimeSeries
                {
                    Title = hasHeader
                        ? headers[i + offset]
                        : $"TS#{i + 1}"
                });


            var dt = new DateTime(2001, 1, 1);
            var separators = new[] {',', '\t'};
            while (line != null)
            {
                pieces = line.Split(separators);
                var parsed = true;

                double value;
                if (hasDates)
                    try
                    {
                        dt = DateTime.Parse(pieces[0]);
                    }
                    catch
                    {
                        parsed = false;
                    }


                if (parsed)
                {
                    for (var i = offset; i < tsCount + offset; ++i)
                        if (pieces[i].Length > 0)
                        {
                            if (pieces[i] == "NaN")
                                value = double.NaN;
                            else
                                try
                                {
                                    value = double.Parse(pieces[i]);
                                }
                                catch
                                {
                                    value = double.NaN;
                                }

                            if (!double.IsNaN(value))
                            {
                                var lastTime = collection[i - offset].GetLastTime();
                                if (dt > lastTime)
                                {
                                    collection[i - offset].Add(dt, value, false);
                                }
                                else if (dt < lastTime)
                                {
                                    bool gotit;
                                    collection[i - offset].ValueAtTime(dt, out gotit);
                                    if (gotit)
                                        throw new ApplicationException($"Duplicate timestamp at {dt}");
                                    collection[i - offset].Add(dt, value, false);
                                }
                                else if (!ignoreDuplicates)
                                {
                                    throw new ApplicationException($"Duplicate timestamp at {dt}");
                                }
                            }
                        }
                }
                else
                {
                    break;
                }

                line = sreader.ReadLine();
                if (!hasDates)
                    dt = new DateTime(dt.AddDays(1).Ticks);
            }

            return collection;
        }


        /// <summary>
        ///     explicit conversion from a TimeSeries to a MVTimeSeries
        /// </summary>
        /// <param name="ts"></param>
        /// <returns></returns>
        public static explicit operator MVTimeSeries(TimeSeries ts)
        {
            var retval = new MVTimeSeries(1);
            for (var t = 0; t < ts.Count; ++t)
                retval.Add(ts.TimeStamp(t), new[] {ts[t]}, false);
            retval.Title = "MV";
            retval.SubTitle[0] = ts.Title;
            return retval;
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

        public double MaxValue()
        {
            var tx = double.MinValue;
            for (var t = 0; t < Count; ++t)
                if (this[t] > tx)
                    tx = this[t];
            return tx;
        }

        public double MinValue()
        {
            var tx = double.MaxValue;
            for (var t = 0; t < Count; ++t)
                if (this[t] < tx)
                    tx = this[t];
            return tx;
        }
    }
}
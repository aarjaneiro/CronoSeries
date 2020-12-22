using System;
using System.Collections.Generic;
using System.Net.Sockets;
using CronoSeries.ABMath.ModelFramework.Data;

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    [Serializable]
    public class MergeTransform : TimeSeriesTransformation
    {
        [NonSerialized] private object outputResult;

        public MergeTransform()
        {
            NumberOfInputs = 2;
        }

        public int NumberOfInputs { get; set; }

        public override int NumInputs()
        {
            return NumberOfInputs;
        }

        public override int NumOutputs()
        {
            return 1;
        }

        public override string GetInputName(int index)
        {
            return $"Time Series #{index + 1}";
        }

        public override string GetOutputName(int index)
        {
            return "Merged TS";
        }

        public override string GetDescription()
        {
            return "Merging transformation";
        }

        public override string GetShortDescription()
        {
            return "Merge";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}

        public override void Recompute()
        {
            IsValid = false;

            var ts = GetInput(0) as TimeSeries;
            var mts = GetInput(0) as MVTimeSeries;
            if (ts == null && mts == null)
                return; // not valid

            if (ts != null)
            {
                // merge univariate TS
                var result = new TimeSeries();
                for (var ii = 0; ii < NumberOfInputs; ++ii)
                {
                    var pts = GetInput(ii) as TimeSeries;
                    if (pts != null)
                        result.Add(pts, true);
                }

                outputResult = result;
                IsValid = true;
            }
            else if (mts != null)
            {
                // merge univariate TS
                var result = new MVTimeSeries(mts.Dimension);
                result.SubTitle = mts.SubTitle;
                for (var ii = 0; ii < NumberOfInputs; ++ii)
                {
                    var pts = GetInput(ii) as MVTimeSeries;
                    if (pts != null)
                        result.Add(pts, true);
                }

                outputResult = result;
                IsValid = true;
            }
        }

        public override object GetOutput(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return outputResult;
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket >= NumberOfInputs)
                throw new SocketException();
            return new List<Type> {typeof(TimeSeries), typeof(MVTimeSeries)};
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> {typeof(TimeSeries), typeof(MVTimeSeries)};
        }
    }
}
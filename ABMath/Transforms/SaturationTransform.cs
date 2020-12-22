using System;
using System.Collections.Generic;
using System.Net.Sockets;
using CronoSeries.ABMath.Data;

namespace CronoSeries.ABMath.Transforms
{
    [Serializable]
    public class SaturationTransform : TimeSeriesTransformation
    {
        public SaturationTransform()
        {
            Maximum = 1;
            Minimum = -1;
        }

        public double Maximum { get; set; }
        public double Minimum { get; set; }

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
            return "Time Series";
        }

        public override string GetOutputName(int index)
        {
            return "Thresholded Result";
        }

        public override string GetDescription()
        {
            return "Saturating Transformation";
        }

        public override string GetShortDescription()
        {
            return "Saturate";
        }

        //public override Icon GetIcon()
        //{
        //    var x = Images.ResourceManager.GetObject("ThresholdIcon") as Icon;
        //    return x;
        //}

        public override void Recompute()
        {
            var ins = GetInputBundle();
            outputs = new List<TimeSeries>();
            foreach (var ts in ins)
            {
                var ots = new TimeSeries {Title = ts.Title};
                for (var t = 0; t < ts.Count; ++t)
                    if (ts[t] > Maximum)
                        ots.Add(ts.TimeStamp(t), Maximum, false);
                    else if (ts[t] < Minimum)
                        ots.Add(ts.TimeStamp(t), Minimum, false);
                    else
                        ots.Add(ts.TimeStamp(t), ts[t], false);

                outputs.Add(ots);
            }

            IsValid = true;
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
    }
}
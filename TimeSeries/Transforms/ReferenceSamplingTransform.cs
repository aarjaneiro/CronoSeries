using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using CronoSeries.TimeSeries.Data;

namespace CronoSeries.TimeSeries.Transforms
{
    /// <summary>
    ///     this class allows you to sample one time series at the time points contained in another time series
    ///     output can be either the sampled series or a multivariate binding of the sampled and other time series
    /// </summary>
    [Serializable]
    public class ReferenceSamplingTransform : TimeSeriesTransformation
    {
        [NonSerialized] private MVTimeSeries mvtsOutput;

        [NonSerialized] private Data.TimeSeries tsOutput;

        public ReferenceSamplingTransform()
        {
            // fill in default params
            IncludeReferenceInOutput = false;
        }

        [Category("Parameter")]
        [Description("True if output should include reference series as well")]
        public bool IncludeReferenceInOutput { get; set; }

        public override string GetDescription()
        {
            return "Reference Sampler";
        }

        public override string GetShortDescription()
        {
            return "Ref. Sample";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}

        public override int NumInputs()
        {
            return 2;
        }

        public override int NumOutputs()
        {
            return 1;
        }

        public override object GetOutput(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            if (tsOutput != null)
                return tsOutput;
            return mvtsOutput;
        }

        public override string GetInputName(int index)
        {
            if (index == 0)
                return "TS to Sample";
            if (index == 1)
                return "TS with reference Times";
            throw new SocketException();
        }

        public override string GetOutputName(int index)
        {
            return "TimeSeries";
        }

        public override void Recompute()
        {
            IsValid = false;

            tsOutput = null;
            mvtsOutput = null;

            // make sure we have at least 2 data points in the time series to be sampled
            var tp = GetInputType(0);
            if (tp != InputType.UnivariateTS && tp != InputType.MultivariateTS)
                return;
            var rp = GetInputType(1);
            if (rp != InputType.UnivariateTS && rp != InputType.MultivariateTS)
                return;

            // first get the reference times
            var times = new List<DateTime>(1000);
            var input1 = GetInput(1);
            var mvref = input1 as MVTimeSeries;
            var uref = input1 as Data.TimeSeries;
            if (rp == InputType.UnivariateTS)
            {
                if (uref == null)
                    return;
                for (var t = 0; t < uref.Count; ++t)
                    times.Add(uref.TimeStamp(t));
            }
            else if (rp == InputType.MultivariateTS)
            {
                if (mvref == null)
                    return;
                for (var t = 0; t < mvref.Count; ++t)
                    times.Add(mvref.TimeStamp(t));
            }
            else
            {
                throw new ApplicationException("Invalid input type; this should not happen!");
            }

            var refDim = uref != null ? 1 : mvref.Dimension;

            // then sample to get the output
            if (tp == InputType.UnivariateTS)
            {
                var uts = GetInput(0) as Data.TimeSeries;
                if (uts == null)
                    return;

                // split it up
                if (!IncludeReferenceInOutput)
                {
                    tsOutput = new Data.TimeSeries();
                    for (var t = 0; t < times.Count; ++t)
                        tsOutput.Add(times[t], uts.ValueAtTime(times[t]), true);
                }
                else
                {
                    mvtsOutput = new MVTimeSeries(1 + refDim);
                    for (var t = 0; t < times.Count; ++t)
                    {
                        var val = new double[1 + refDim];
                        val[0] = uts.ValueAtTime(times[t]);
                        for (var i = 1; i <= refDim; ++i)
                            val[i] = mvref != null ? mvref[t][i - 1] : uref[t];
                        mvtsOutput.Add(times[t], val, true);
                    }
                }

                IsValid = true;
            }

            if (tp == InputType.MultivariateTS)
            {
                var mts = GetInput(0) as MVTimeSeries;
                if (mts == null)
                    return;

                // split it up
                if (!IncludeReferenceInOutput)
                {
                    mvtsOutput = new MVTimeSeries();
                    for (var t = 0; t < times.Count; ++t)
                        mvtsOutput.Add(times[t], mts.ValueAtTime(times[t]), true);
                }
                else
                {
                    var thisDim = mts.Dimension;
                    mvtsOutput = new MVTimeSeries(thisDim + refDim);
                    for (var t = 0; t < times.Count; ++t)
                    {
                        var val = new double[thisDim + refDim];
                        var dv = mts.ValueAtTime(times[t]);
                        for (var i = 0; i < thisDim; ++i)
                            val[i] = dv[i];
                        for (var i = 0; i < refDim; ++i)
                            val[i + thisDim] = mvref != null ? mvref[t][i] : uref[t];
                        mvtsOutput.Add(times[t], val, true);
                    }
                }

                IsValid = true;
            }
        }


        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket >= NumInputs())
                throw new SocketException();
            return new List<Type> {typeof(Data.TimeSeries), typeof(MVTimeSeries)};
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> {typeof(Data.TimeSeries), typeof(MVTimeSeries)};
        }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using CronoSeries.ABMath.ModelFramework.Data;
using CronoSeries.ABMath.ModelFramework.Models;

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    [Serializable]
    public class ForecastTransform : TimeSeriesTransformation, IExtraFunctionality
    {
        public ForecastTransform()
        {
            FutureTimes = new DateTime[10];
            for (var i = 0; i < 10; ++i)
                FutureTimes[i] = new DateTime(2011, 1, i + 1); // default
        }

        [Category("Parameter")]
        [Description("Times at which to generate forecasts")]
        public DateTime[] FutureTimes { get; set; }

        public int NumAuxiliaryFunctions()
        {
            return 1;
        }

        public string AuxiliaryFunctionName(int index)
        {
            if (index == 0)
                return "Specify Times";
            return null;
        }

        public string AuxiliaryFunctionHelp(int index)
        {
            if (index == 0)
                return "Specifies time points in the future at which forecasts will be generated.";
            return null;
        }

        public override int NumInputs()
        {
            return 2; // model and starting data
        }

        public override int NumOutputs()
        {
            return 1; // predictive means
        }

        public override string GetInputName(int index)
        {
            if (index == 0)
                return "Model";
            if (index == 1)
                return "Starting Data";
            throw new SocketException();
        }

        public override string GetOutputName(int index)
        {
            if (index == 0)
                return "Predictive Mean";
            throw new SocketException();
        }

        public override string GetDescription()
        {
            return "Forecasts";
        }

        public override string GetShortDescription()
        {
            return "Forecasts";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}

        public override bool SetInput(int socket, object item, StringBuilder failMsg)
        {
            CheckInputsReady();
            if (socket >= NumInputs())
                throw new ArgumentException("Bad socket.");

            if (socket == 0)
                if (item as TimeSeriesModel == null)
                {
                    if (failMsg != null)
                        failMsg.Append("The first input must be a model.");
                    return false;
                }

            if (socket == 1)
                if (item as TimeSeries == null)
                    return false;

            socketedInputs[socket] = item;

            if (AllInputsValid())
                Recompute();

            return true;
        }

        public override void Recompute()
        {
            outputs = new List<TimeSeries>();
            IsValid = false;

            if (FutureTimes == null)
                return;
            if (FutureTimes.Length == 0)
                return;

            var tsm = GetInput(0) as UnivariateTimeSeriesModel;
            var tsd = GetInput(1) as TimeSeries;

            if (tsm == null || tsd == null)
                return;

            var fdt = new List<DateTime>();
            foreach (var dt in FutureTimes)
                fdt.Add(dt);
            var forecasts = tsm.BuildForecasts(tsd, fdt) as TimeSeriesBase<DistributionSummary>;
            if (forecasts == null)
                return;

            var predictiveMeans = new TimeSeries();
            for (var t = 0; t < forecasts.Count; ++t)
                predictiveMeans.Add(forecasts.TimeStamp(t), forecasts[t].Mean, false);

            outputs.Add(predictiveMeans);
            IsValid = predictiveMeans.Count > 0;
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket == 0)
                return new List<Type> {typeof(Model)};
            if (socket == 1)
                return new List<Type> {typeof(TimeSeries), typeof(MVTimeSeries)};
            throw new SocketException();
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket >= NumOutputs())
                throw new SocketException();
            return new List<Type> {typeof(TimeSeries), typeof(MVTimeSeries)};
        }
    }
}
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using CronoSeries.ABMath.Data;

namespace CronoSeries.ABMath.Transforms
{
    [Serializable]
    public class HubTransform : TimeSeriesTransformation
    {
        public HubTransform()
        {
            IsValid = false;
        }

        public override int NumInputs()
        {
            return 1;
        }

        public override int NumOutputs()
        {
            return 1;
        }

        public override string GetInputName(int socket)
        {
            if (socket == 0)
                return "Input";
            throw new SocketException();
        }

        public override string GetOutputName(int socket)
        {
            if (socket >= 0 && socket < NumOutputs())
                return string.Format("Output #{0}", socket + 1);
            throw new SocketException();
        }

        public override string GetDescription()
        {
            return "Replicates the input to multiple outputs";
        }

        public override string GetShortDescription()
        {
            return "Hub";
        }

        //public override Icon GetIcon()
        //{
        //    var x = Images.ResourceManager.GetObject("HubIcon") as Icon;
        //    return x;
        //}

        public override void Recompute() // actually this does nothing since it's a pass through type of transform
        {
            IsValid = false;
            if (AllInputsValid())
                IsValid = true;
        }

        public override object GetOutput(int socket)
        {
            if (socket >= 0 && socket < NumOutputs())
                return IsValid ? GetInput(0) : null;
            throw new SocketException();
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket > 0)
                throw new SocketException();
            return new List<Type> {typeof(TimeSeries), typeof(MVTimeSeries)};
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket < 0 || socket >= NumOutputs())
                throw new SocketException();
            return new List<Type> {typeof(TimeSeries), typeof(MVTimeSeries)};
        }
    }
}
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using CronoSeries.TimeSeries.Data;

namespace CronoSeries.TimeSeries.Transforms
{
    [Serializable]
    public class RotarySwitchTransform : TimeSeriesTransformation, IExtraFunctionality
    {
        public RotarySwitchTransform()
        {
            NumberOfInputs = 2;
            CurrentSelection = 1;
            IsValid = false;
        }

        public int CurrentSelection { get; set; }
        public int NumberOfInputs { get; set; }

        public int NumAuxiliaryFunctions()
        {
            return 1;
        }

        public string AuxiliaryFunctionName(int index)
        {
            if (index == 0)
                return "Rotate";
            throw new ArgumentException();
        }

        public string AuxiliaryFunctionHelp(int index)
        {
            if (index == 0)
                return "Switches next input to be the current output";
            throw new ArgumentException();
        }

        public override int NumInputs()
        {
            return NumberOfInputs;
        }

        public override int NumOutputs()
        {
            return 1;
        }

        public override string GetInputName(int socket)
        {
            if (socket >= 0 && socket < NumberOfInputs)
                return string.Format("Input #{0}", socket + 1);
            throw new SocketException();
        }

        public override string GetOutputName(int socket)
        {
            if (socket == 0)
                return "Selected Output";
            throw new SocketException();
        }

        public override string GetDescription()
        {
            return "Passes one of the inputs through to the output";
        }

        public override string GetShortDescription()
        {
            return "Rotary";
        }

        //public override Icon GetIcon()
        //{
        //    var x = CurrentSelection == 1
        //                ? Images.ResourceManager.GetObject("RotaryIcon") as Icon
        //                : Images.ResourceManager.GetObject("RotaryIcon2") as Icon;
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
            if (socket == 0)
                return IsValid ? GetInput(CurrentSelection - 1) : null;
            throw new SocketException();
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
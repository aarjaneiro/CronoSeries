using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using CronoSeries.ABMath.ModelFramework.Data;

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    [Serializable]
    public class SplittingTransformation : TimeSeriesTransformation
    {
        public override int NumInputs()
        {
            return 1;  // is a multivariate input
        }

        public override int NumOutputs()
        {
            return GetTotalInputDimension();
        }

        public override string GetInputName(int index)
        {
            return "MV Input";
        }

        public override string GetOutputName(int index)
        {
            var sb = new StringBuilder(128);
            sb.AppendFormat("Component #{0}", index + 1);
            if (outputs != null)
                if (outputs.Count > index)
                    sb.AppendFormat(" [{0}]", outputs[index].Title);
            return sb.ToString();
        }

        public override string GetDescription()
        {
            return "Multivariate Splitter";
        }

        public override string GetShortDescription()
        {
            return "MV Split";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}

        public override void Recompute()
        {
            outputs = GetInputBundle();
            IsValid = true;
        }

        public override object GetOutput(int socket)
        {
            if (outputs != null)
                if (outputs.Count > socket)
                    return outputs[socket];
            return null;
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> { typeof(MVTimeSeries) };
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket >= NumOutputs())
                throw new SocketException();
            return new List<Type> { typeof(TimeSeries) };
        }

    }
}

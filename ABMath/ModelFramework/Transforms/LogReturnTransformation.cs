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
using System.Net.Sockets;
using CronoSeries.ABMath.ModelFramework.Data;

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    [Serializable]
    public class LogReturnTransformation : TimeSeriesTransformation
    {
        public override void Recompute()
        {
            IsValid = false;
            var inputs = GetInputBundle();
            outputs = new List<TimeSeries>(inputs.Count);

            var failure = false;
            for (var i = 0; i < inputs.Count; ++i)
            {
                var lrs = new TimeSeries();
                for (var t = 1; t < inputs[i].Count; ++t)
                {
                    var x1 = inputs[i][t - 1];
                    var x2 = inputs[i][t];
                    if (x1 > 0 && x2 > 0)
                        lrs.Add(inputs[i].TimeStamp(t), Math.Log(x2) - Math.Log(x1), false);
                    else
                        failure = true;
                }

                lrs.Title = inputs[i].Title;
                outputs.Add(lrs);
            }

            multivariateOutputPrefix = "LR";
            IsValid = true;
            if (failure)
                Console.WriteLine("One or more values was non-positive, corresponding log-returns were left out.");
        }


        public override string GetDescription()
        {
            return "Log-Return Transform";
        }

        public override string GetShortDescription()
        {
            return "LR";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}

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
            return "TimeSeries";
        }

        public override string GetOutputName(int index)
        {
            return "TimeSeries";
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
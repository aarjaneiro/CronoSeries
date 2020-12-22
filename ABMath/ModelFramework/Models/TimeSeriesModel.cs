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
using System.Text;
using CronoSeries.ABMath.ModelFramework.Data;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.ModelFramework.Models
{
    [Serializable]
    public abstract class TimeSeriesModel : Model
    {
        public override Vector<double> Parameters { get; set; }

        public override int NumInputs()
        {
            return 1;
        }

        public override string GetInputName(int socket)
        {
            return "Model Input";
        }

        public override bool InputIsFree(int socket)
        {
            return TheData == null;
        }

        public override bool SetInput(int socket, object item, StringBuilder failMsg)
        {
            if (socket >= NumInputs())
                throw new SocketException();

            if (CheckDataValidity(item, failMsg))
            {
                TheData = item;
                OnDataConnection();
                LogLikelihood(null, 0.0, true); // forces residuals and all outputs to be filled in
                return true;
            }

            return false;
        }

        public override int NumOutputs()
        {
            return 2; // the model itself, and the residuals
        }

        public override object GetOutput(int socket)
        {
            if (socket == 0)
                return this;
            if (socket == 1)
                return Residuals;
            throw new SocketException();
        }

        public override string GetOutputName(int socket)
        {
            if (socket == 0)
                return "The Model";
            if (socket == 1)
                return "Standardized Residuals";
            throw new SocketException();
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> {typeof(TimeSeries), typeof(MVTimeSeries), typeof(Longitudinal)};
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket == 0)
                return new List<Type> {typeof(Model)};
            if (socket == 1)
                return new List<Type> {typeof(TimeSeries), typeof(MVTimeSeries)};
            throw new SocketException();
        }
    }
}
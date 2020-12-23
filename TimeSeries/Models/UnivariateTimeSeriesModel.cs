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
using CronoSeries.TimeSeries.Data;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.TimeSeries.Models
{
    /// <summary>
    ///     This class represents a model for a univariate time series or for
    ///     longitudinal data (a list of separate univariate time series)
    /// </summary>
    [Serializable]
    public abstract class UnivariateTimeSeriesModel : TimeSeriesModel
    {
        protected Longitudinal longitudinalValues;
        protected Data.TimeSeries values;

        protected bool DataIsLongitudinal()
        {
            return longitudinalValues != null;
        }

        protected override bool CheckDataValidity(object data, StringBuilder failMessage)
        {
            var priceList = data as Data.TimeSeries;
            var priceLists = data as Longitudinal;
            if (priceList == null && priceLists == null)
            {
                if (failMessage != null)
                    failMessage.AppendLine("Cannot cast input into a (univariate) TimeSeries or Longitudinal object.");
                return false;
            }

            return true;
        }

        protected override void OnDataConnection()
        {
            values = TheData as Data.TimeSeries;
            longitudinalValues = TheData as Longitudinal;

            if (values == null && longitudinalValues == null)
                throw new ApplicationException("Invalid data connection.");
        }

        public abstract Vector<double> ComputeACF(int maxLag, bool normalize);

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> {typeof(Data.TimeSeries)};
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket < base.NumInputs())
                return base.GetOutputTypesFor(socket);
            return new List<Type>
                {typeof(Data.TimeSeries)}; // all the outputs of a univariate model are other time series
        }
    }
}
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

namespace CronoSeries.TimeSeries.Models
{
    [Serializable]
    public abstract class MVTimeSeriesModel : TimeSeriesModel
    {
        protected int dimension;

        [NonSerialized] protected MVTimeSeries mvts;

        public int Dimension
        {
            get => dimension;
            protected set => dimension = value;
        }

        protected override bool CheckDataValidity(object data, StringBuilder failMessage)
        {
            mvts = data as MVTimeSeries;
            if (mvts == null)
                return false;
            if (mvts.Dimension != dimension)
                return false;
            if (!CanHandleNaNs())
                if (mvts.NaNCount() > 0)
                {
                    if (failMessage != null)
                        failMessage.AppendLine("Cannot use this model with data with NaNs.");
                    return false;
                }

            return true;
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> {typeof(MVTimeSeries)};
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
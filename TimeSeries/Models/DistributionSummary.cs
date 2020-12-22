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

namespace CronoSeries.TimeSeries.Models
{
    [Serializable]
    public class DistributionSummary
    {
        private double sigma;
        public double Mean { get; set; }

        public double Variance
        {
            get => sigma * sigma;
            set => sigma = Math.Sqrt(value);
        }

        public double StdDev
        {
            get => sigma;
            set => sigma = value;
        }

        public double Kurtosis { get; set; }
    }
}
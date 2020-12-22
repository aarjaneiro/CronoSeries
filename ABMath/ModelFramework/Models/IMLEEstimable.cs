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


using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.ModelFramework.Models
{
    public interface IMLEEstimable
    {
        void CarryOutPreMLEComputations();
        Vector<double> ParameterToCube(Vector<double> param);
        Vector<double> CubeToParameter(Vector<double> cube);
    }
}
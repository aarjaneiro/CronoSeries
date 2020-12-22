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
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.Miscellaneous
{
    public abstract class Optimizer
    {
        public delegate void OptimizationCallback(Vector<double> param, double functionValue,
            int percentComplete, bool finished);

        public delegate double TargetFunction(Vector<double> x);

        public OptimizationCallback Callback { get; set; }
        public int StartIteration { get; set; }

        public Vector<double> ArgMin { get; protected set; }

        public double Minimum { get; protected set; }

        public List<Evaluation> Evaluations { get; protected set; }

        public abstract void Minimize(TargetFunction targetFunction, List<Vector<double>> initialValues,
            int maxIterations);

        public struct Evaluation : IComparable<Evaluation>
        {
            public Vector<double> argument;
            public double value;
            public DateTime timeStamp;

            public Evaluation(Vector<double> argument, double value)
            {
                this.argument = argument;
                this.value = value;
                timeStamp = DateTime.Now;
            }

            public int CompareTo(Evaluation other)
            {
                return value.CompareTo(other.value);
            }
        }
    }
}
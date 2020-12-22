#region License Info

//Component of Cronos Package, http://www.codeplex.com/cronos
//Copyright (C) 2009 Anthony Brockwell

//This program is free software; you can redistribute it and/or
//modify it under the terms of the GNU General Public License
//as published by the Free Software Foundation; either version 2
//of the License, or (at your option) any later version.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with this program; if not, write to the Free Software
//Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

#endregion

using System;
using System.Collections.Generic;

namespace CronoSeries.ABMath.Miscellaneous
{
    /// <summary>
    ///     This class keeps track of a step function mapping R -> R.
    ///     It is assumed that the function only changes (has steps) in a region with compact support.
    /// </summary>
    public class StepFunction
    {
        public StepFunction(double[] args, double[] values)
        {
            Args = args;
            Values = values;
        }

        public double[] Args { get; protected set; }
        public double[] Values { get; protected set; }

        public bool IsEmpty => Args.Length == 0;

        public int Length => Args.Length;

        /// <summary>
        ///     returns index of last element at or before time t,
        ///     or returns 0 if no element at or before time t
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private int IndexBefore(double t)
        {
            var i = Array.BinarySearch(Args, t);
            if (i < 0)
                i = ~i - 1;
            return i;
        }

        public double MonotonicIncreasingInverse(double x)
        {
            var i = Array.BinarySearch(Values, x);
            if (i >= 0) // it's found!
                return Args[i];
            // otherwise it must be between points
            i = ~i;
            if (i == 0)
                return Args[0];
            if (i == Args.Length)
                return Args[Args.Length - 1];
            var i0 = i - 1;
            var i1 = i;
            var frac = (x - Values[i0]) / (Values[i1] - Values[i0]);
            if (x < 0 || x > 1)
                throw new ApplicationException("Interpolation failure: argument is not between the two found points.");
            var interpd = frac * Args[i1] + (1 - frac) * Args[i0];
            return interpd;
        }

        /// <summary>
        ///     returns value at time t (i.e. last change at a time <= t)
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public double ValueAtTime(double t)
        {
            if (IsEmpty)
                throw new ApplicationException("Invalid evaluation in StepFunction.");

            if (t < Args[0])
                return Values[0]; // extend first value to the left

            // otherwise we just find the greatest timestamp still less than or equal to t
            // using a simple binary search
            var i0 = IndexBefore(t);
            if (i0 == -1)
                i0 = 0;

            // now i0 is greatest index such that corresponding timestamp <= t
            return Values[i0];
        }

        public double InterpolatedValue(double t)
        {
            var i0 = IndexBefore(t);
            if (i0 == -1)
                return Values[0];
            if (Length < 2)
                return Values[0];
            if (i0 == Length - 1)
                return Values[Length - 1];
            var t0 = Args[i0];
            var t1 = Args[i0 + 1];
            var frac = (t - t0) / (t1 - t0);
            var retval = Values[i0] * (1 - frac) + Values[i0 + 1] * frac;
            return retval;
        }

        public static StepFunction operator *(StepFunction x, double multiplier)
        {
            var newargs = new double[x.Args.Length];
            Array.Copy(x.Args, newargs, x.Args.Length);
            var newvals = new double[x.Args.Length];
            for (var t = 0; t < x.Args.Length; ++t)
                newvals[t] = x.Values[t] * multiplier;
            return new StepFunction(newargs, newvals);
        }

        public static StepFunction operator +(StepFunction x, StepFunction y)
        {
            // combine two step functions
            var newargs = new List<double>(x.Args.Length + y.Args.Length);
            var newvals = new List<double>(x.Args.Length + y.Args.Length);

            var i0 = 0;
            var i1 = 0;

            var done = i0 == x.Args.Length && i1 == y.Args.Length;
            while (!done)
            {
                var t0 = i0 < x.Args.Length ? x.Args[i0] : double.MaxValue;
                var t1 = i1 < y.Args.Length ? y.Args[i1] : double.MaxValue;
                double newval;

                if (t0 == t1)
                {
                    // combine them
                    newargs.Add(t0);
                    newval = x.Values[i0] + y.Values[i1];
                    newvals.Add(newval);
                    ++i0;
                    ++i1;
                }
                else
                {
                    if (t0 < t1)
                    {
                        // add value at t0
                        newargs.Add(t0);
                        newval = x.Values[i0] + y.InterpolatedValue(t0);
                        newvals.Add(newval);
                        ++i0;
                    }
                    else
                    {
                        newargs.Add(t1);
                        newval = x.InterpolatedValue(t1) + y.Values[i1];
                        newvals.Add(newval);
                        ++i1;
                    }
                }

                done = i0 == x.Args.Length && i1 == y.Args.Length;
            }

            var allargs = newargs.ToArray();
            var allvals = newvals.ToArray();
            return new StepFunction(allargs, allvals);
        }
    }
}
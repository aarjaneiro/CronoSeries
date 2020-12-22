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

using System.Linq;
using CronoSeries.ABMath.Forms.IridiumExtensions;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.Miscellaneous
{
    /// <summary>
    ///     This class evaluates log-likelihood for a time series by summing up individual specified log-likelihoods.
    ///     This is a rather trivial summation operation.  What is useful here, however, is the consistency penalty.
    ///     The idea is to look for a kind of draw-down in the partial sums of log-likelihoods and compute a penalty factor
    ///     which is bad if the model is inconsistent with the data for a long sub-interval within the time range.
    /// </summary>
    public class LogLikelihoodPenalizer
    {
        private readonly Vector<double> components;

        /// <summary>
        ///     The constructor takes as argument a Vector containing sequential individual Components of log-likelihood.
        /// </summary>
        /// <param name="v"></param>
        public LogLikelihoodPenalizer(Vector<double> components)
        {
            this.components = components;
            LogLikelihood = components.Sum();
            ComputePenalty();
        }

        public double Penalty { get; private set; }
        public double LogLikelihood { get; }

        private void ComputePenalty()
        {
            var average = components.Average();

            // construct cumulative LL, adjusted by average so that the total is 0
            var cumulative = Vector<double>.Build.Dense(components.Count);
            for (var t = 0; t < components.Count; ++t)
                cumulative[t] = (t > 0 ? cumulative[t - 1] : 0) + components[t] - average;

            // now find draw-down in cumulative
            var maxDrawDown = cumulative.MaxDrawDown();

            // now we use the drawdown here as a penalty
            Penalty = maxDrawDown;
        }
    }
}
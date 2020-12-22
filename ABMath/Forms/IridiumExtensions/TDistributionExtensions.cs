using System;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.Forms.IridiumExtensions
{
    public static class TDistributionExtensions
    {
        /// <summary>
        ///     returns max. likelihood estimator of scale factor for t-dn, assuming mean of data is zero
        /// </summary>
        /// <param name="dn"></param>
        /// <param name="data">data to be used</param>
        /// <returns></returns>
        public static double MLEofSigma(this StudentT dn, Vector<double> data)
        {
            var sd2 = data.Variance();
            var n = data.Count;
            var dof = (int) dn.DegreesOfFreedom;

            // now use iterative recursion
            for (var iteration = 0; iteration < 20; ++iteration)
            {
                var sum = 0.0;
                for (var i = 0; i < n; ++i)
                    sum += sd2 * data[i] * data[i] / (sd2 * dof + data[i] * data[i]);
                sd2 = (dof + 1) * sum / n;
            }

            return Math.Sqrt(sd2);
        }
    }
}
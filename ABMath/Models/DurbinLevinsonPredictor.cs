using System;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.Models
{
    /// <summary>
    ///     this class is used to carry out Durbin-Levinson recursions for linear time series prediction.
    ///     it can use either a supplied vector containing the autocovariance, or a delegate that can compute it
    /// </summary>
    public class DurbinLevinsonPredictor
    {
        private readonly AutocovarianceFunction acvFunction;
        private readonly Vector<double> autocovariance;

        private readonly int maxN;
        private readonly double mean;
        private double[] a, olda, rs, xhat, values;
        private int curIndex;

        public DurbinLevinsonPredictor(double mean, AutocovarianceFunction acvFunction, int maxN)
        {
            this.maxN = maxN;
            this.mean = mean;
            autocovariance = null;
            this.acvFunction = acvFunction;

            LocalInitialize();
        }

        public DurbinLevinsonPredictor(double mean, Vector<double> autocovariance)
        {
            this.mean = mean;
            this.autocovariance = autocovariance;
            maxN = autocovariance.Count;

            LocalInitialize();
        }

        public double CurrentPredictor { get; protected set; }
        public double CurrentMSPE { get; protected set; }

        private void LocalInitialize()
        {
            xhat = new double[maxN];
            rs = new double[maxN];
            a = new double[maxN];
            olda = new double[maxN];
            values = new double[maxN];

            rs[0] = autocovariance != null ? autocovariance[0] : acvFunction(0);
            xhat[0] = mean;
            CurrentPredictor = mean; // best linear predictor based on no information
            CurrentMSPE = rs[0];

            curIndex = 0;
        }

        public void Register(double nextObservation)
        {
            if (curIndex >= maxN)
                throw new ApplicationException("Ran out of allocated ACVF in D-L predictor.");
            values[curIndex] = nextObservation;

            ++curIndex;
            var i = curIndex;
            for (var j = 0; j < maxN; ++j)
                olda[j] = a[j];

            // compute the new a vector
            double sum = 0;
            if (autocovariance != null)
            {
                for (var j = 1; j < i; ++j)
                    sum += olda[j - 1] * autocovariance[i - j];
                a[i - 1] = 1 / rs[i - 1] * (autocovariance[i] - sum);
            }
            else // we use the delegate instead
            {
                for (var j = 1; j < i; ++j)
                    sum += olda[j - 1] * acvFunction(i - j);
                a[i - 1] = 1 / rs[i - 1] * (acvFunction(i) - sum);
            }

            for (var k = 0; k < i - 1; ++k)
                a[k] = olda[k] - a[i - 1] * olda[i - 2 - k];

            // update nu
            rs[i] = rs[i - 1] * (1 - a[i - 1] * a[i - 1]);

            double tx = 0;
            for (var j = 0; j < i; ++j)
                tx += a[j] * (i - 1 - j < curIndex ? values[i - 1 - j] - mean : xhat[i - 1 - j] - mean);
            xhat[i] = tx + mean;
            CurrentPredictor = xhat[i];
            CurrentMSPE = rs[i];
        }
    }
}
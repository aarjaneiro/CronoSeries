using System;
using System.IO;
using System.Xml.Serialization;
using CronoSeries.ABMath.Miscellaneous;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.ModelFramework.Models
{
    /// <summary>
    /// This class keeps track of a univariate distribution by storing its quantiles in a matrix.
    /// </summary>
    [Serializable]
    public class DistributionApproximation
    {
        /// <summary>
        /// This matrix defines quantiles, with InvCDF(first column) = second column
        /// </summary>
        public Matrix<double> Quantiles 
        {   
            get { return quantiles; } 
            set { 
                quantiles = value;
                CDF = BuildCDF();
            } 
        }
        private Matrix<double> quantiles;

        /// <summary>
        /// The CDF is automatically computed when quantiles are set.
        /// </summary>
        public StepFunction CDF
        {
            get; 
            private set;
        }

        private StepFunction BuildCDF()
        {
            var args = new double[Quantiles.RowCount];
            var vals = new double[Quantiles.RowCount];
            for (int i = 0; i < Quantiles.RowCount; ++i)
            {
                args[i] = Quantiles[i, 0];
                vals[i] = Quantiles[i, 1];
            }
            return new StepFunction(args, vals);
        }

        public void LoadFromXmlArray(TextReader reader)
        {
            var serializer = new XmlSerializer(typeof(double[]));
            var o = serializer.Deserialize(reader) as double[];

            double resolution = 1.0 / o.Length;
            var quants = Matrix<double>.Build.Dense(o.Length, 2);
            for (int i = 0; i < o.Length; ++i)
            {
                quants[i, 1] = (i + 0.5) * resolution;
                quants[i, 0] = o[i];
            }

            Quantiles = quants;
        }

        /// <summary>
        /// The following function fills in the quantiles, using the Mean and Variance,
        /// assuming that the distribution is Gaussian. 
        /// </summary>
        /// <param name="spacing"></param>
        public void FillGaussianQuantiles(double spacing, double StdDev, double Mean)
        {
            var ndn = new Normal(0, 1);

            int hn = (int)Math.Floor(0.4999 / spacing);
            int n = 2 * hn + 3;
            var quants = Matrix<double>.Build.Dense(n, 2);
            for (int i = 0; i < hn; ++i)
            {
                quants[i + hn + 2, 0] = 0.5 + (i + 1) * spacing;
                quants[hn - i, 0] = 0.5 - (i + 1) * spacing;
            }
            quants[hn + 1, 0] = 0.5;
            for (int i = 0; i < n; ++i)
                quants[i, 1] = ndn.InverseCumulativeDistribution(quants[i, 0]) * StdDev + Mean;

            // now to make computations tractable, we give the distribution compact support
            double range = quants[n - 2, 1] - quants[1, 1];
            quants[0, 0] = 0;
            quants[0, 1] = quants[1, 1] - range;
            quants[n - 1, 0] = 1;
            quants[n - 1, 1] = quants[n - 2, 1] + range;

            Quantiles = quants;
        }

        /// <summary>
        /// Creates a new distribution summary which corresponds to a mixture distribution:
        ///   with probability p1 we get the original (this)
        ///   with probability (1-p1) we get the other distribution
        /// </summary>
        /// <param name="other">other distribution</param>
        /// <param name="p1">weight assigned to this distribution</param>
        /// <returns></returns>
        public DistributionApproximation MixWith(DistributionApproximation other, double p1, int histBins)
        {
            double p2 = 1.0 - p1;
            StepFunction sf1 = CDF;
            StepFunction sf2 = other.CDF;
            StepFunction mixed = sf1 * p1 + sf2 * p2;

            var retval = new DistributionApproximation();

            // compute quantiles by getting them from the mixed cdf
            var quants = Matrix<double>.Build.Dense(histBins + 1, 2);
            for (int i = 0; i <= histBins; ++i)
            {
                quants[i, 0] = i / (double)histBins;
                quants[i, 1] = mixed.MonotonicIncreasingInverse(quants[i, 0]);
            }
            retval.Quantiles = quants;
            return retval;
        }


        public DistributionSummary ConvoluteWith(DistributionSummary other)
        {
            return null;
        }

        public override string ToString()
        {
            return Quantiles.ToString();
        }
    }
}

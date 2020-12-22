using System;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.TimeSeries.Models
{
    public class MVDistributionSummary
    {
        public Vector<double> Mean { get; set; }

        public Matrix<double> Variance { get; set; }

        public Vector<double> Kurtosis { get; set; }

        public DistributionSummary GetMarginal(int component)
        {
            throw new NotImplementedException();
        }
    }
}
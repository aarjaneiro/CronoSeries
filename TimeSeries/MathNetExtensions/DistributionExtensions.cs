﻿using System;

namespace CronoSeries.TimeSeries.MathNetExtensions
{
    public static class NormalDistributionExtensions
    {
        private const double logOneOnRootTwoPi = -0.918938533204673;

        public static double LogProbabilityDensity(double x, double mu, double sigma)
        {
            var retval = logOneOnRootTwoPi - Math.Log(sigma)
                                           - (x - mu) * (x - mu) / (2 * sigma * sigma);
            return retval;
        }
    }
}
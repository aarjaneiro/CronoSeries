﻿using System;

namespace CronoSeries.ABMath.Forms.IridiumExtensions
{
    public static class NormalDistributionExtensions
    {
        private const double logOneOnRootTwoPi = -0.918938533204673;

        public static double LogProbabilityDensity(double x, double mu, double sigma) 
        {
            double retval = logOneOnRootTwoPi - Math.Log(sigma)
                            - (x - mu)*(x - mu)/(2*sigma*sigma);
            return retval;
        }
    }
}

using System;
using CronoSeries.ABMath.Forms.IridiumExtensions;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.Miscellaneous
{
    public class Regression
    {
        private readonly Matrix<double> augmentedExplanatory;
        private readonly Vector<double> dependent;
        private readonly IUnivariateDistribution stdNormal;

        public Regression(Vector<double> dependent, Matrix<double> explanatory, bool addConstant, bool getBetaHatOnly)
        {
            //stdNormal = new StandardDistribution();

            stdNormal = new Normal();

            this.dependent = dependent;
            if (addConstant)
            {
                augmentedExplanatory = Matrix<double>.Build.Dense(explanatory.RowCount, explanatory.ColumnCount + 1);
                for (var i = 0; i < explanatory.RowCount; ++i)
                {
                    augmentedExplanatory[i, 0] = 1.0;
                    for (var j = 0; j < explanatory.ColumnCount; ++j)
                        augmentedExplanatory[i, j + 1] = explanatory[i, j];
                }
            }
            else
            {
                augmentedExplanatory = explanatory;
            }

            Recompute(getBetaHatOnly);
        }

        public Regression(Vector<double> dependent, Matrix<double> explanatory, Vector<double> weights,
            bool addConstant, bool getBetaHatOnly)
        {
            // to perform weighted regression, we create modified versions of 
            //stdNormal = new StandardDistribution();

            stdNormal = new Normal();
            this.dependent = Vector<double>.Build.Dense(dependent.Count);
            for (var i = 0; i < dependent.Count; ++i)
                this.dependent[i] = dependent[i] * Math.Sqrt(weights[i]);

            augmentedExplanatory =
                Matrix<double>.Build.Dense(explanatory.RowCount, explanatory.ColumnCount + (addConstant ? 1 : 0));
            if (addConstant)
                for (var i = 0; i < explanatory.RowCount; ++i)
                {
                    augmentedExplanatory[i, 0] = 1.0 * Math.Sqrt(weights[i]);
                    for (var j = 0; j < explanatory.ColumnCount; ++j)
                        augmentedExplanatory[i, j + 1] = explanatory[i, j] * Math.Sqrt(weights[i]);
                }
            else
                for (var i = 0; i < explanatory.RowCount; ++i)
                for (var j = 0; j < explanatory.ColumnCount; ++j)
                    augmentedExplanatory[i, j] = explanatory[i, j] * Math.Sqrt(weights[i]);

            Recompute(getBetaHatOnly);
        }

        public double Sigma { get; protected set; }

        public Vector<double> BetaHat { get; protected set; }

        public Matrix<double> BetaHatCovariance { get; protected set; }

        public Vector<double> PValues { get; protected set; }

        private void Recompute(bool getBetaHatOnly)
        {
            var p = augmentedExplanatory.ColumnCount;
            var n = augmentedExplanatory.RowCount;

            var xt = augmentedExplanatory.Clone();
            xt.Transpose();
            var xty = (xt * dependent.ToColumnMatrix()).ToVector();
            var xtx = xt * augmentedExplanatory;

            var mxty = Matrix<double>.Build.Dense(xty.Count, 1);
            for (var i = 0; i < xty.Count; ++i)
                mxty[i, 0] = xty[i];

            BetaHat = Vector<double>.Build.Dense(p);

            //if (mxty.Norm2()==0)
            if (mxty.L2Norm() == 0)
                return;

            var bm = xtx.Solve(mxty);
            // .SolveRobust(mxty);

            for (var i = 0; i < p; ++i)
                BetaHat[i] = bm[i, 0];

            if (getBetaHatOnly)
                return;

            var fitted = (augmentedExplanatory * BetaHat.ToColumnMatrix()).ToVector();
            var resids = dependent - fitted;

            // now compute approximate p-values
            Sigma = Math.Sqrt(resids.Variance()) * n / (n - p);
            BetaHatCovariance = Sigma * Sigma * xtx.Inverse();
            PValues = Vector<double>.Build.Dense(augmentedExplanatory.ColumnCount);
            for (var i = 0; i < augmentedExplanatory.ColumnCount; ++i)
            {
                var x = Math.Abs(BetaHat[i]) / Math.Sqrt(BetaHatCovariance[i, i]);
                PValues[i] = 2 * (1 - stdNormal.CumulativeDistribution(x));
            }
        }
    }
}
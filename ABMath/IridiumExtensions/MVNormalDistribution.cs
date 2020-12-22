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
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using MathNet.Numerics.Random;

namespace ABMath.IridiumExtensions
{
    public class MVNormalDistribution
    {
        //private readonly StandardDistribution stdnormal;
        private readonly Normal stdnormal;

        //public RandomSource RandomSource
        public Random RandomSource
        {
            get { return stdnormal.RandomSource; }
            set { stdnormal.RandomSource = value; }
        }

        private Matrix<double> sigma;
        private Matrix<double> sqrtSigma;
        private Matrix<double> sqrtSigmaInverse;
        private Matrix<double> invSigma;
        private double detSigma;
        private Vector<double> mu;
        private int dimension;

        public Matrix<double> Sigma
        {
            get { return sigma; }
            set
            {
                sigma = value;
                ComputeCholeskyDecomp();
            }
        }

        public Vector<double> Mu
        {
            get { return mu; }
            set
            {
                mu = value;
                dimension = value.Count;
            }
        }

        public MVNormalDistribution() // constructor
        {
            //stdnormal = new StandardDistribution();
            stdnormal = new Normal();
        }


        private void ComputeCholeskyDecomp()
        {
            var cd = Sigma.Cholesky();
            cd.Solve(Sigma);
            sqrtSigma = cd.Factor;
            detSigma = Sigma.Determinant();
            if (detSigma != 0)
            {
                invSigma = Sigma.Inverse();
                sqrtSigmaInverse = sqrtSigma.Inverse();
            }
            else
            {
                invSigma = null;
                sqrtSigmaInverse = null;
            }
        }

        public double LogProbabilityDensity(Vector<double> x)
        {
            Matrix<double> tm1 = (x - mu).ToColumnMatrix();
            tm1.Transpose();
            Matrix<double> tm2 = (tm1*invSigma*(x - mu).ToColumnMatrix());
            double retval = -0.5*tm2[0, 0] - 0.5*Math.Log(detSigma) - dimension/2.0*Math.Log(2*Math.PI);
            return retval;
        }
        
        public Vector<double> NextVector()
        {
            var retval = Vector<double>.Build.Dense(dimension);
            for (int i = 0; i < dimension; ++i)
                retval[i] = stdnormal.RandomSource.NextDouble();
            retval = sqrtSigma.MultiplyBy(retval);
            for (int i = 0; i < dimension; ++i)
                retval[i] += mu[i];
            return retval;
        }

        public Vector<double> Standardize(Vector<double> v)
        {
            if (sqrtSigmaInverse == null)
                throw new ApplicationException("Cannot standardize a MV normal vector when its covariance matrix is singular.");
            return sqrtSigmaInverse.MultiplyBy(v);
        }
    }
 }


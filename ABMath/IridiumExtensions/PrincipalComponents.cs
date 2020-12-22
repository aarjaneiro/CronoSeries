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
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;

namespace ABMath.IridiumExtensions
{
    public class PrincipalComponents
    {
        private Matrix<double> sampleCov;
        private Evd<double> covDecomposition;

        public Vector<double> SortedEigenvalues
        {
            get; protected set;
        }

        public Matrix<double> SortedComponents
        {
            get; protected set;
        }

        public PrincipalComponents(Matrix<double> data)
        {
            Matrix<double> dt = data.Clone();
            dt.Transpose();
            sampleCov = dt*data*(1.0/data.RowCount);
            //covDecomposition = new EigenvalueDecomposition(sampleCov);
            covDecomposition = sampleCov.Evd();
            covDecomposition.Solve(sampleCov);

            var evs = new double[sampleCov.RowCount];
            var indices = new int[sampleCov.RowCount];
            for (int i = 0; i < sampleCov.RowCount; ++i)
            {
                evs[i] = covDecomposition.EigenValues[i].Real;
                indices[i] = i;
            }
            Array.Sort(evs, indices);

            var permutation = Matrix<double>.Build.Dense(sampleCov.RowCount, sampleCov.RowCount);
            for (int i=0 ;i<sampleCov.RowCount ; ++i)
                permutation[indices[i], i] = 1.0;

            Matrix<double> v = covDecomposition.EigenVectors;
            SortedComponents = v*permutation;
            SortedEigenvalues = Vector<double>.Build.DenseOfArray(evs);
        }
    }
}
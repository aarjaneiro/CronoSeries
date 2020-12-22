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

namespace CronoSeries.ABMath.ModelFramework.Models
{
    internal class StateSpaceModel
    {       
        public Matrix<double> F { get; private set;}    // state transition model
        public Matrix<double> B { get; private set; }   // control input model
        public Matrix<double> Q { get; private set; }   // covariance matrix of w
        public Matrix<double> H { get; private set; }   // observation model        
        public Matrix<double> R { get; private set; }   // covariance of observation noise 

        public int StateDimension { get; private set; }
        public int ControlDimension { get; private set; }
        public int ObservationDimension { get; private set; }


        /// <summary>
        /// Builds a state space model
        /// </summary>
        /// <param name="F">state transition model</param>
        /// <param name="B">control input model</param>
        /// <param name="Q">covariance matrix of state transition model noise</param>
        /// <param name="H">observation model</param>
        /// <param name="R">covariance matrix of observation noise</param>
        private StateSpaceModel(Matrix<double> F, Matrix<double> B, Matrix<double> Q, Matrix<double> H, Matrix<double> R)
        {
            this.F = F;
            this.R = R;
            this.H = H;
            this.Q = Q;
            this.B = B;

            if (F.RowCount != F.ColumnCount)
                throw new Exception("The state transition model must be a square matrix.");
            if (Q.RowCount != Q.ColumnCount)
                throw new Exception("The state noise correlation matrix must be a square matrix.");
            if (R.RowCount != R.ColumnCount)
                throw new Exception("The observation noise correlation matrix must be a square matrix.");

            if (B.RowCount != F.RowCount)
                throw new Exception(
                    "The control input model matrix needs to produce vectors in the state space. Check dimensions.");
            if (H.ColumnCount != F.RowCount)
                throw new Exception(
                    "The observation model matrix needs to take vectors from the state space. Check dimensions.");
            if (Q.RowCount != F.RowCount)
                throw new Exception(
                    "The state model matrix and the state noise correlation matrix have mismatched dimensions.");
            if (H.RowCount != R.RowCount)
                throw new Exception(
                    "The observation model matrix and the observation noise correlation matrix have mismatched dimensions.");

            StateDimension = F.RowCount;
            ControlDimension = B.ColumnCount;
            ObservationDimension = H.ColumnCount;
        }
    }
}
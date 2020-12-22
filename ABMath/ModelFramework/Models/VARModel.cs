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
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using CronoSeries.ABMath.Forms.IridiumExtensions;
using CronoSeries.ABMath.ModelFramework.Data;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.ModelFramework.Models
{
    /// <summary>
    /// vector autoregressive model, fittable by method of moments only (Yule-Walker eqns)
    /// </summary>
    [Serializable]
    public class VARModel : MVTimeSeriesModel, IMoMEstimable
    {
        protected Vector<double> mu;

        [NonSerialized] private MVTimeSeries oneStepPredictors = null;
        [NonSerialized] private MVTimeSeries oneStepPredictorsAtAvail = null;
        protected int order;
        protected Matrix<double>[] Phi;
        protected Matrix<double> Sigma;

        public VARModel(int order, int dimension)
        {
            this.order = order;
            Dimension = dimension;
            LocalInitializeParameters();
        }

        protected int NumParameters
        {
            get { return dimension + dimension*dimension*(order + 1); }
        }

        public override string Description
        {
            get
            {
                var sb = new StringBuilder(512);
                sb.AppendLine("Vector Autoregression:");
                sb.Append("X(t)");
                for (int i = 0; i < order; ++i)
                    sb.AppendFormat("- Phi({0})X(t-{0})", (i + 1));
                sb.Append(" = Z(t)");
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendFormat("Mean = {0}{1}", mu, Environment.NewLine);
                for (int i = 0; i < order; ++i)
                    sb.AppendFormat("Phi({0}) = {1:0.0000}{2}", (i + 1), Phi[i], Environment.NewLine);
                sb.AppendFormat("Var(Z(t)) = {0:0.0000}{1}", Sigma, Environment.NewLine);
                return sb.ToString();
            }
        }

        public override Vector<double> Parameters
        {
            get
            {
                // build vector from matrix parameters
                var parmVec = Vector<double>.Build.Dense(NumParameters);

                for (int i = 0; i < dimension; ++i)
                    parmVec[i] = mu[i];

                for (int p = 0; p < order; ++p)
                    for (int i = 0; i < dimension; ++i)
                        for (int j = 0; j < dimension; ++j)
                            parmVec[dimension + dimension*dimension*p + j*dimension + i] = Phi[p][i, j];

                for (int i = 0; i < dimension; ++i)
                    for (int j = 0; j < dimension; ++j)
                        parmVec[dimension + dimension*dimension*order + j*dimension + i] = Sigma[i, j];

                return parmVec;
            }

            set
            {
                // unbundle from the vector into all the parameters
                if (value.Count != NumParameters)
                    throw new ArgumentException("Parameter vector is incorrect length.");

                for (int i = 0; i < dimension; ++i)
                    mu[i] = value[i];

                for (int p = 0; p < order; ++p)
                    for (int i = 0; i < dimension; ++i)
                        for (int j = 0; j < dimension; ++j)
                            Phi[p][i, j] = value[dimension + dimension*dimension*p + j*dimension + i];

                for (int i = 0; i < dimension; ++i)
                    for (int j = 0; j < dimension; ++j)
                        Sigma[i, j] = value[dimension + dimension*dimension*order + j*dimension + i];
            }
        }

        #region IMoMEstimable Members

        /// <summary>
        /// For multivariate autoregressive models, we just solve the Yule-Walker equations to estimate parameters.
        /// This is conceptually straightforward (see any standard textbook), but requires a lot of linear algebra book-keeping.
        /// </summary>
        public void FitByMethodOfMoments()
        {
            Matrix<double>[] gammas = mvts.ComputeACF(order + 1, false);
            var gammasT = new Matrix<double>[gammas.Length];
            for (int i = 0; i < gammas.Length; ++i)
            {
                gammasT[i] = gammas[i].Clone();
                gammasT[i].Transpose();
            }

            // get sample mean (if parameters are not locked)
            var tempmu = mvts.SampleMean();
            for (int i = 0; i < Dimension; ++i)
                if (ParameterStates[i] == ParameterState.Locked)
                    tempmu[i] = mu[i];
            mu = tempmu;

            Matrix<double> big = gammas[0];
            Matrix<double> little = gammasT[1];
            for (int i = 1; i < order; ++i)
            {
                Matrix<double> rightPiece = gammas[1];
                for (int j = 1; j < i; ++j)
                    rightPiece = MatrixExtensionsI.CreateBlockMatrixVertically(gammas[j + 1], rightPiece);
                Matrix<double> rightPieceT = rightPiece.Clone();
                rightPieceT.Transpose();
                big = MatrixExtensionsI.CreateBlockMatrixFrom(big, rightPiece, rightPieceT, gammas[0]);
                little = MatrixExtensionsI.CreateBlockMatrixVertically(little, gammasT[i + 1]);
            }

            Matrix<double> allPhis = big.Solve(little);

            for (int i = 0; i < order; ++i)
            {
                Phi[i] = allPhis.SubMatrix(i*dimension, 0, (i + 1)*dimension, dimension);
                Phi[i].Transpose();
            }

            Sigma = gammas[0];
            for (int i = 0; i < order; ++i)
                Sigma -= Phi[i]*gammasT[i + 1];

            LogLikelihood(null, 0.0, true);
        }

        #endregion

        public override string GetShortDescription()
        {
            return $"VAR({order}){Environment.NewLine}{dimension}-dim";
        }

        public override string GetParameterName(int index)
        {
            int n = dimension + dimension*dimension*(order + 1);
            if (index >= n)
                throw new ArgumentException("Invalid parameter index.");
            if (index < dimension)
                return $"Mu({(index + 1)})";

            int p = (index - dimension)/(dimension*dimension);
            int ofs = (index - dimension)%(dimension*dimension);
            int i = ofs%dimension;
            int j = ofs/dimension;
            if (p < order)
                return $"Phi[{p}][{i},{j}]";
            return $"Sigma[{p}][{i},{j}]";
        }

        public override string GetParameterDescription(int index)
        {
            return "Parameter";
        }

        public override int NumOutputs()
        {
            return base.NumOutputs() + 2;
        }

        public override object GetOutput(int socket)
        {
            if (socket < base.NumOutputs())
                return base.GetOutput(socket);
            if (socket == base.NumOutputs())
                return oneStepPredictors;
            if (socket == base.NumOutputs() + 1)
                return oneStepPredictorsAtAvail;
            throw new SocketException();
        }

        public override string GetOutputName(int index)
        {
            if (index < base.NumOutputs())
                return base.GetOutputName(index);
            if (index == base.NumOutputs())
                return StandardOutputs.OneStepPredictor;
            if (index == base.NumOutputs() + 1)
                return StandardOutputs.OneStepPredictorAtAvail;
            throw new SocketException();
        }


        // Only used for MLE, so we don't need to fill this in.
        protected override bool CheckParameterValidity(Vector<double> param)
        {
            return true;
        }

        /// <summary>
        /// returns one-step predictor of value at time t, given info up to time t-1
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private Vector<double> OneStepPredictor(int t)
        {
            var pred = Matrix<double>.Build.Dense(dimension, 1);
            Matrix<double> vmu = mu.ToColumnMatrix();
            Matrix<double> tv;
            for (int i = 1; i <= order; ++i)
            {
                if (t - i >= 0)
                {
                    tv = Matrix<double>.Build.Dense(mvts[t - i].Length, dimension);
                    int row = -1;
                    for (int j = 0; j < mvts[t - i].Length; j++)
                    {
                        if (i % dimension == 0)
                        {
                            row++;
                        }
                        tv[row, i] = mvts[t - i][i];
                    }
                }
                    
                        //tv = new Matrix(mvts[t - i], dimension);
                else
                    tv = mu.ToColumnMatrix();
                pred += Phi[i - 1]*(tv - vmu);
            }
            return pred.ToVector() + mu;
        }

        public override double LogLikelihood(Vector<double> parameter, double penaltyFactor, bool fillOutputs)
        {
            if (mvts == null)
                return double.NaN;

            Vector<double> paramBak = Parameters;

            if (parameter != null)
                Parameters = parameter;

            var mvn = new MVNormalDistribution {Mu = mu, Sigma = Sigma};
            double logLike = 0.0;
            for (int t = 0; t < mvts.Count; ++t)
            {
                Vector<double> pred = OneStepPredictor(t);
                Vector<double> resid = Vector<double>.Build.DenseOfArray(mvts[t]) - pred;
                logLike += mvn.LogProbabilityDensity(resid);
            }

            if (fillOutputs)
            {
                GoodnessOfFit = logLike;

                oneStepPredictors = new MVTimeSeries(dimension) { Title = mvts.Title };
                oneStepPredictorsAtAvail = new MVTimeSeries(dimension) { Title = mvts.Title };
                var rs = new MVTimeSeries(dimension) { Title = mvts.Title };

                if (mvts.SubTitle != null)
                {
                    oneStepPredictors.SubTitle = new string[dimension];
                    oneStepPredictorsAtAvail.SubTitle = new string[dimension];
                    rs.SubTitle = new string[dimension];
                    for (int i = 0; i < dimension; ++i)
                        if (mvts.SubTitle[i] != null)
                        {
                            oneStepPredictors.SubTitle[i] = $"{mvts.SubTitle[i]}[Pred]";
                            oneStepPredictorsAtAvail.SubTitle[i] = $"{mvts.SubTitle[i]}[Pred.AA]";
                            rs.SubTitle[i] = $"{mvts.SubTitle[i]}[Resid]";
                        }
                }

                for (int t = 0; t < mvts.Count; ++t)
                {
                    Vector<double> pred = OneStepPredictor(t);
                    Vector<double> resid = Vector<double>.Build.DenseOfArray(mvts[t]) - pred;

                    rs.Add(mvts.TimeStamp(t), mvn.Standardize(resid).ToArray(), false);
                    oneStepPredictors.Add(mvts.TimeStamp(t), pred.ToArray(), false);
                    if (t > 0)
                        oneStepPredictorsAtAvail.Add(mvts.TimeStamp(t - 1), pred.ToArray(), false);
                }
                oneStepPredictorsAtAvail.Add(mvts.TimeStamp(mvts.Count - 1), OneStepPredictor(mvts.Count).ToArray(), false);

                Residuals = rs;
            }

            if (parameter != null)
               Parameters = paramBak;

            return logLike;
        }

        protected override Vector<double> ComputeConsequentialParameters(Vector<double> parameter)
        {
            return parameter;
        }

        public override object SimulateData(object inputs, int randomSeed)
        {
            throw new NotImplementedException();
        }

        public override object BuildForecasts(object otherData, object inputs)
        {
            throw new NotImplementedException();
        }

        private void LocalInitializeParameters()
        {
            mu = Vector<double>.Build.Dense(dimension);
            Phi = new Matrix<double>[order];
            for (int i = 0; i < order; ++i)
                Phi[i] = Matrix<double>.Build.Dense(dimension, dimension);
            Sigma = Matrix<double>.Build.DenseIdentity(dimension, dimension);

            ParameterStates = new ParameterState[NumParameters];
        }

        protected override void OnDataConnection()
        {
            // we don't need to do anything in particular
        }

        protected override void InitializeParameters()
        {
            LocalInitializeParameters();
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket < base.NumOutputs())
                return base.GetOutputTypesFor(socket);
            if (socket >= base.NumOutputs() + 2)
                throw new SocketException();
            return new List<Type> { typeof(MVTimeSeries) };
        }
    }
}
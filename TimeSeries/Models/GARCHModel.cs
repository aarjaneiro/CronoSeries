/*
Derived from the Cronos Package, http://www.codeplex.com/cronos
Copyright (C) 2009 Anthony Brockwell

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
*/


using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using CronoSeries.TimeSeries.MathNetExtensions;
using CronoSeries.TimeSeries.Miscellaneous;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;

//using MathNet.Numerics.RandomSources;

namespace CronoSeries.TimeSeries.Models
{
    [Serializable]
    public class GARCHModel : UnivariateTimeSeriesModel, IMLEEstimable
    {
        public enum GARCHType
        {
            Standard,
            EGARCH
        }

        private const double unitRootBarrier = 1e-6; // roots must be at least this this far from unit circle

        private readonly double log1onroot2pi = Math.Log(1.0 / Math.Sqrt(2 * Math.PI));
        private readonly double root2onpi = Math.Sqrt(2 / Math.PI);

        protected int dataOrder;
        protected int intrinsicOrder;

        private GARCHType modelType;

        [NonSerialized] private Data.TimeSeries predictiveStdDevAtAvail;

        /// <summary>
        ///     <remarks>
        ///         To depreciate, use other constructor with data when building a
        ///         new model.
        ///     </remarks>
        ///     Basic constructor for an GARCH(m,s) model.
        /// </summary>
        /// <param name="modelType">Type of GARCH model to user</param>
        /// <param name="dataOrder">m order</param>
        /// <param name="intrinsicOrder">s order</param>
        public GARCHModel(GARCHType modelType, int dataOrder, int intrinsicOrder)
        {
            this.modelType = modelType;
            this.dataOrder = dataOrder;
            this.intrinsicOrder = intrinsicOrder;
            LocalInitializeParameters();
        }

        /// <summary>
        ///     basic constructor for an GARCH(m,s) model.
        /// </summary>
        /// <param name="modelType">Type of GARCH model to user</param>
        /// <param name="dataOrder">m order</param>
        /// <param name="intrinsicOrder">s order</param>
        /// <param name="data">data to associate with this model</param>
        public GARCHModel(GARCHType modelType, int dataOrder, int intrinsicOrder, Data.TimeSeries data)
        {
            this.modelType = modelType;
            this.dataOrder = dataOrder;
            this.intrinsicOrder = intrinsicOrder;
            values = data;
            LocalInitializeParameters();
        }

        public override string Description
        {
            get
            {
                var sb = new StringBuilder(200);
                switch (modelType)
                {
                    case GARCHType.EGARCH:
                        sb.AppendFormat("EGARCH Model: {0}", Environment.NewLine);
                        sb.AppendFormat("  alpha = [");
                        for (var i = 0; i <= dataOrder; ++i)
                            sb.AppendFormat(" {0}", alpha(i, Parameters));
                        sb.AppendFormat("]{0}  gamma = [", Environment.NewLine);
                        for (var i = 1; i <= dataOrder; ++i)
                            sb.AppendFormat(" {0}", gamma(i, Parameters));
                        sb.AppendFormat("]{0}  beta =  [", Environment.NewLine);
                        for (var i = 1; i <= intrinsicOrder; ++i)
                            sb.AppendFormat(" {0}", beta(i, Parameters));
                        sb.AppendFormat("]{0}", Environment.NewLine);
                        break;

                    case GARCHType.Standard:
                        sb.AppendFormat("GARCH Model: {0}", Environment.NewLine);
                        sb.AppendLine();
                        sb.AppendLine("X(t) = Sqrt[s(t)] Z(t),  {Z(t)}~IIDN(0,1)");
                        sb.AppendFormat("s(t) = {0:0.000000} ", alpha(0, Parameters));
                        for (var i = 1; i <= dataOrder; ++i)
                        {
                            var val = alpha(i, Parameters);
                            sb.AppendFormat(" {2}{0:0.0000} X(t-{1:0})^2", Math.Abs(val), i, val >= 0 ? '+' : '-');
                        }

                        for (var i = 1; i <= intrinsicOrder; ++i)
                        {
                            var val = beta(i, Parameters);
                            sb.AppendFormat(" {2}{0:0.0000} s(t-{1:0})", val, i, val >= 0 ? '+' : '-');
                        }

                        sb.AppendLine();
                        break;
                }

                return sb.ToString();
            }
        }

        public Vector<double> ParameterToCube(Vector<double> parm)
        {
            if (parm.Count != Parameters.Count)
                throw new ArgumentException("Invalid param argument size.");
            var cube = Vector<double>.Build.Dense(Parameters.Count);

            switch (modelType)
            {
                case GARCHType.EGARCH:
                    // alpha and gamma parameters
                    cube[0] = InvLogit(parm[0], 0.01);
                    for (var i = 1; i <= dataOrder; ++i)
                    {
                        cube[i] = InvLogit(parm[i], 0.1); // alpha coeffs
                        cube[i + dataOrder + intrinsicOrder] = InvLogit(parm[i + dataOrder + intrinsicOrder], 0.1);
                    }

                    // then beta parameters
                    if (intrinsicOrder > 1)
                        throw new ApplicationException("Oops - can't handle order > 1 here.");

                    for (var i = 0; i < intrinsicOrder; ++i)
                        cube[i + 1 + dataOrder] = parm[i + 1 + dataOrder];

                    break;

                case GARCHType.Standard:
                    cube[0] = Math.Exp(-parm[0]);
                    cube[1] = parm[1];
                    if (cube[1] > 1 - unitRootBarrier)
                        cube[1] = 1 - unitRootBarrier;
                    for (var i = 2; i <= dataOrder + intrinsicOrder; ++i)
                        cube[i] = parm[i] / (parm[i - 1] / cube[i - 1] - parm[i - 1]);
                    break;

                default:
                    throw new ApplicationException("Invalid GARCH modelType.");
            }

            return cube;
        }

        public Vector<double> CubeToParameter(Vector<double> cube)
        {
            if (cube.Count != Parameters.Count)
                throw new ApplicationException("Invalid cube size.");

            var parm = Vector<double>.Build.Dense(Parameters.Count);
            switch (modelType)
            {
                case GARCHType.EGARCH:
                    // alpha and gamma parameters
                    parm[0] = Logit(cube[0], 0.01, 10.0);
                    for (var i = 1; i <= dataOrder; ++i)
                    {
                        parm[i] = Logit(cube[i], 0.1, 100.0); // alpha coeffs
                        parm[i + dataOrder + intrinsicOrder] = Logit(cube[i + dataOrder + intrinsicOrder], 0.1, 100.0);
                    }

                    // then beta parameters
                    if (intrinsicOrder > 1)
                        throw new ApplicationException("Oops - can't handle order > 1 here.");

                    for (var i = 0; i < intrinsicOrder; ++i)
                        parm[i + 1 + dataOrder] = cube[i + 1 + dataOrder];

                    break;

                case GARCHType.Standard:
                    parm[0] = -Math.Log(cube[0]);
                    parm[1] = cube[1] * (1 - unitRootBarrier);
                    for (var i = 2; i <= dataOrder + intrinsicOrder; ++i)
                        parm[i] = (parm[i - 1] / cube[i - 1] - parm[i - 1]) * cube[i];
                    break;

                default:
                    throw new ApplicationException("Invalid GARCH modelType.");
            }

            return parm;
        }

        public void CarryOutPreMLEComputations()
        {
            // nothing to do here: if there are some aspects of likelihood computation that can be reused with different parameters and the same data,
            // we would do that here
        }

        public override int NumOutputs()
        {
            return 3; // the model itself, and the residuals
        }

        public override object GetOutput(int socket)
        {
            if (socket == 0)
                return this;
            if (socket == 1)
                return Residuals;
            if (socket == 2)
                return predictiveStdDevAtAvail;
            throw new SocketException();
        }

        public override string GetOutputName(int socket)
        {
            if (socket < 2)
                return base.GetOutputName(socket);
            if (socket == 2)
                return "Pred. StdDev at Avail.";
            throw new SocketException();
        }


        protected static double alpha(int idx, Vector<double> parms) // index starts at 0
        {
            return parms[idx];
        }

        protected double beta(int idx, Vector<double> parms) // index starts at 1
        {
            return parms[idx + dataOrder];
        }

        protected double gamma(int idx, Vector<double> parms) // index starts at 1
        {
            return parms[idx + dataOrder + intrinsicOrder];
        }


        public override string GetParameterName(int index)
        {
            if (modelType == GARCHType.Standard)
                switch (index)
                {
                    case 0:
                        return "Alpha(0)";
                    default:
                        if (index <= dataOrder)
                            return $"Alpha({index})";
                        if (index <= dataOrder + intrinsicOrder)
                            return $"Gamma({index - dataOrder + 1})";
                        throw new ArgumentException("Invalid parameter index.");
                }

            return "Unknown";
        }

        public override string GetParameterDescription(int index)
        {
            return null;
        }

        protected override bool CheckParameterValidity(Vector<double> param)
        {
            var violation = false;
            switch (modelType)
            {
                case GARCHType.EGARCH:
                    // determine roots of the beta polynomial
                    var betaArray = new double[intrinsicOrder + 1];
                    betaArray[0] = 1.0;
                    for (var i = 1; i <= intrinsicOrder; ++i) betaArray[i] = -beta(i, param);

                    //var betaP = new Polynomial(betaArray);
                    var betaP = Vector<double>.Build.DenseOfArray(betaArray);
                    var roots = betaP.Roots();
                    foreach (var c in roots)
                        if (c.Magnitude < 1.0 + unitRootBarrier) // it's too close to the unit circle
                            violation = true;
                    break;

                case GARCHType.Standard:
                    // just make sure standard GARCH constraints apply
                    violation &= alpha(0, param) > 0;
                    double sum = 0;
                    for (var i = 1; i <= dataOrder; ++i)
                    {
                        violation &= alpha(i, param) >= 0;
                        sum += alpha(i, param);
                    }

                    for (var i = 1; i <= intrinsicOrder; ++i)
                    {
                        violation &= beta(i, param) >= 0;
                        sum += beta(i, param);
                    }

                    violation &= sum <= 1 - unitRootBarrier;
                    break;

                default:
                    throw new ApplicationException("Invalid GARCH modelType.");
            }

            return !violation;
        }

        protected double Logit(double p, double multiplier, double max)
        {
            var tx = Math.Log(p / (1 - p));
            tx *= multiplier;
            if (tx > max)
                tx = max;
            if (tx < -max)
                tx = -max;
            return tx;
        }

        protected double InvLogit(double x, double multiplier)
        {
            var tx = x / multiplier;
            return Math.Exp(tx) / (1 + Math.Exp(tx));
        }

        private double GetVariance(Vector<double> param)
        {
            double marginalVariance = 0;
            double sum;

            switch (modelType)
            {
                case GARCHType.EGARCH:
                    // find E(log(sigma_t^2)) first
                    sum = alpha(0, param);
                    for (var j = 1; j <= dataOrder; ++j)
                        sum += root2onpi * alpha(j, param);
                    var tx = 1.0;
                    for (var j = 1; j <= intrinsicOrder; ++j)
                        tx -= beta(j, param);
                    marginalVariance = sum / tx;
                    break;

                case GARCHType.Standard:
                    sum = 0;
                    for (var j = 1; j <= dataOrder; ++j)
                        sum += alpha(j, param);
                    for (var j = 1; j <= intrinsicOrder; ++j)
                        sum += beta(j, param);
                    marginalVariance = alpha(0, param) / (1 - sum);
                    break;
            }

            return marginalVariance;
        }

        private double GetConditionalSig2(int t, Data.TimeSeries localLogReturns, Vector<double> sigmaSquared,
            Vector<double> param, double marginalVariance)
        {
            double ls2;
            switch (modelType)
            {
                case GARCHType.EGARCH:
                    ls2 = alpha(0, param);
                    for (var i = 1; i <= dataOrder; ++i)
                        if (t - i >= 0)
                        {
                            var ztmi = localLogReturns[t - i] / Math.Exp(sigmaSquared[t - i] / 2.0);
                            if (ztmi > 5.0)
                                ztmi = 5;
                            if (ztmi < -5.0)
                                ztmi = -5;
                            ls2 += alpha(i, param) * (Math.Abs(ztmi) + gamma(i, param) * ztmi);
                        }
                        else
                        {
                            ls2 += alpha(i, param) * root2onpi * marginalVariance;
                        }

                    for (var i = 1; i <= intrinsicOrder; ++i)
                        if (t - i >= 0)
                            ls2 += beta(i, param) * sigmaSquared[t - i];
                        else
                            ls2 += beta(i, param) * marginalVariance;
                    if (ls2 < -80)
                        ls2 = -80;
                    if (ls2 > 80)
                        ls2 = 80;
                    sigmaSquared[t] = ls2;
                    return Math.Exp(ls2);

                case GARCHType.Standard:
                    ls2 = alpha(0, param);
                    for (var i = 1; i <= dataOrder; ++i)
                        if (t - i >= 0)
                        {
                            var ztmi = localLogReturns[t - i];
                            ls2 += alpha(i, param) * ztmi * ztmi;
                        }
                        else
                        {
                            ls2 += alpha(i, param) * marginalVariance;
                        }

                    for (var i = 1; i <= intrinsicOrder; ++i)
                        if (t - i >= 0)
                            ls2 += beta(i, param) * sigmaSquared[t - i];
                        else
                            ls2 += beta(i, param) * marginalVariance;
                    sigmaSquared[t] = ls2;
                    return ls2;
            }

            return 0;
        }

        /// <summary>
        ///     Returns conditional log-likelihood of logReturns[t], given the past.
        ///     It uses past values of sigmaSquared, and fills in the next value.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="sigmaSquared"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        protected double conditionalLL(int t, Vector<double> sigmaSquared, Vector<double> param,
            double marginalVariance)
        {
            var ss = GetConditionalSig2(t, values, sigmaSquared, param, marginalVariance);
            return log1onroot2pi - Math.Log(ss) / 2 - values[t] * values[t] / (2 * ss);
        }

        public override double LogLikelihood(Vector<double> parameter,
            double penaltyFactor, bool fillOutputs)
        {
            //var sigmaSquared = new MathNet.Numerics.LinearAlgebra.Vector(values.Count);
            //double mVar = GetVariance(parameter);
            //var allLLs = new MathNet.Numerics.LinearAlgebra.Vector(values.Count);
            //for (int t = 0; t < values.Count; ++t )
            //    allLLs[t] = conditionalLL(t, sigmaSquared, parameter, mVar);

            var pbak = Parameters;
            if (values == null)
                return double.NaN;
            if (parameter != null)
                Parameters = parameter;

            var sigmaSquared = Vector<double>.Build.Dense(values.Count);
            var mVar = GetVariance(Parameters);
            double logLikelihood = 0;
            var allLLs = Vector<double>.Build.Dense(values.Count);

            for (var t = 0; t < values.Count; ++t)
            {
                allLLs[t] = conditionalLL(t, sigmaSquared, Parameters, mVar);
                logLikelihood += allLLs[t];
            }

            if (fillOutputs)
            {
                var rts = new Data.TimeSeries {Title = $"{values.Title}[GARCH Res]"};
                predictiveStdDevAtAvail = new Data.TimeSeries {Title = $"{values.Title}Pred.StDev[AA]"};
                for (var t = 0; t < values.Count; ++t)
                {
                    var rx = values[t] / Math.Sqrt(sigmaSquared[t]);
                    rts.Add(values.TimeStamp(t), rx, false);
                    if (t > 0)
                        predictiveStdDevAtAvail.Add(values.TimeStamp(t - 1), Math.Sqrt(sigmaSquared[t]), false);
                }

                Residuals = rts;
                GoodnessOfFit = logLikelihood;
            }

            if (parameter != null)
                Parameters = pbak;

            var llp = new LogLikelihoodPenalizer(allLLs);
            return llp.LogLikelihood - llp.Penalty * penaltyFactor;
        }

        protected override Vector<double> ComputeConsequentialParameters(Vector<double> parameter)
        {
            // first make sure that we can handle the consequential params
            for (var i = 1; i < Parameters.Count; ++i)
                if (ParameterStates[i] == ParameterState.Consequential)
                    throw new ArgumentException("Invalid consequential parameters.");

            if (ParameterStates[0] != ParameterState.Consequential)
                return parameter;

            // if we get here, we just need to fill in the first parameter alpha_0,
            // which just scales the variance
            double target;
            double alpha_0;
            double sumbetas = 0;
            for (var i = 1; i <= intrinsicOrder; ++i)
                sumbetas += beta(i, parameter);
            double sumalphas = 0;
            for (var i = 1; i <= dataOrder; ++i)
                sumalphas += alpha(i, parameter);

            switch (modelType)
            {
                case GARCHType.EGARCH:
                    target = Math.Log(values.SampleVariance());
                    alpha_0 = target * (1 - sumbetas) - root2onpi * sumalphas;
                    break;

                case GARCHType.Standard:
                    target = values.SampleVariance();
                    alpha_0 = target * (1 - sumbetas - sumalphas);
                    break;

                default:
                    throw new ApplicationException("Invalid model type.");
            }

            var fixedParms = Vector<double>.Build.DenseOfVector(parameter);
            fixedParms[0] = alpha_0;
            return fixedParms;
        }

        public override string GetShortDescription()
        {
            return string.Format("GARCH{0}({1:0},{2:0})", Environment.NewLine, dataOrder, intrinsicOrder);
        }

        public override Data.TimeSeries SimulateData(List<DateTime> inputs, int simSeed)
        {
            var times = inputs;
            if (times == null)
                return null; // inputs should be a list of DateTimes

            var n = times.Count;
            var simulated = new Data.TimeSeries();

            var randomSource = new Palf(simSeed);
            var stdnormal = new Normal();
            stdnormal.RandomSource = randomSource;

            var mVar = GetVariance(Parameters);
            var ss =
                Vector<double>.Build.Dense(n);
            for (var i = 0; i < n; ++i)
            {
                var variance = GetConditionalSig2(i, simulated, ss, Parameters, mVar);
                var simLR = stdnormal.RandomSource.NextDouble() * Math.Sqrt(variance);
                simulated.Add(times[i], simLR, false);
            }

            simulated.Title = "Simulation";
            simulated.Description = $"Simulation from {Description}";
            return simulated;
        }

        public override object BuildForecasts(object otherData, object inputs)
        {
            Console.WriteLine("Forecasting not yet implemented for GARCH models.");
            return null;
        }

        private void LocalInitializeParameters()
        {
            // first initialize params
            Vector<double> tv;
            switch (modelType)
            {
                case GARCHType.EGARCH:
                    tv = Vector<double>.Build.Dense(1 + 2 * dataOrder + intrinsicOrder);
                    tv[0] = -0.2;
                    Parameters = tv;
                    break;
                case GARCHType.Standard:
                    tv = Vector<double>.Build.Dense(1 + dataOrder + intrinsicOrder);
                    tv[0] = 1e-4;
                    Parameters = tv;
                    break;
                default:
                    throw new ApplicationException("Invalid model modelType.");
            }

            // then set up default parameter states for estimation
            var pstates = new ParameterState[Parameters.Count];
            for (var i = 0; i < pstates.Length; ++i)
                pstates[i] = i > 0 ? ParameterState.Free : ParameterState.Consequential;
            ParameterStates = pstates;
        }


        protected override void InitializeParameters()
        {
            LocalInitializeParameters();
        }


        public override Vector<double> ComputeACF(int maxLag, bool normalize)
        {
            var retval = Vector<double>.Build.Dense(maxLag + 1);
            retval[0] = normalize ? 1.0 : GetVariance(Parameters);
            return retval;
        }
    }
}
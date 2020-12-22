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
using System.Numerics;
using System.Text;
using CronoSeries.ABMath.IridiumExtensions;
using CronoSeries.ABMath.Miscellaneous;
using CronoSeries.ABMath.ModelFramework.Data;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;

//using MathNet.Numerics.RandomSources;

namespace CronoSeries.ABMath.ModelFramework.Models
{
    [Serializable]
    public class ARMAModel : UnivariateTimeSeriesModel, IRealTimePredictable, IMLEEstimable
    {
        private const double UnitRootBarrier = 1e-3;

        private const double muScale = 10.0;

        protected MathNet.Numerics.LinearAlgebra.Vector<double> autocovariance;

        [NonSerialized] protected TimeSeries oneStepPredictors; // timestamped to align with what they are predicting

        [NonSerialized]
        protected TimeSeries oneStepPredictorsAtAvailability; // timestamped at the point the predictor is available

        [NonSerialized] protected TimeSeries oneStepPredStd; // also aligned with what they are predicting

        private MathNet.Numerics.LinearAlgebra.Vector<double> rtacvf;
        private int rtm;
        private int rtminwidth;
        private int rtn;
        private MathNet.Numerics.LinearAlgebra.Vector<double> rtrs;
        private Matrix<double> rtSubThetas;
        private MathNet.Numerics.LinearAlgebra.Vector<double> rtvalues;
        private MathNet.Numerics.LinearAlgebra.Vector<double> rtxhat;

        [NonSerialized] protected TimeSeries unstandardizedResiduals;

        /// <summary>
        ///     basic constructor for an ARMA(p,q) model with Gaussian innovations
        /// </summary>
        /// <param name="arOrder">autoregressive order</param>
        /// <param name="maOrder">moving average order</param>
        public ARMAModel(int arOrder, int maOrder)
        {
            AROrder = arOrder;
            MAOrder = maOrder;
            TailDegreesOfFreedom = 0;
            LocalInitializeParameters();
        }

        /// <summary>
        ///     constructor for an ARMA(p,q) model with Students T innovations
        /// </summary>
        /// <param name="arOrder">autoregressive order</param>
        /// <param name="maOrder">moving average order</param>
        /// <param name="tailDOF">degrees of freedom of T-distribution for innovations</param>
        public ARMAModel(int arOrder, int maOrder, int tailDOF)
        {
            AROrder = arOrder;
            MAOrder = maOrder;
            TailDegreesOfFreedom = tailDOF;
            LocalInitializeParameters();
        }

        public override string Description
        {
            get
            {
                var sb = new StringBuilder(1024);
                sb.AppendFormat("ARMA({0},{1}) Model:{2}", AROrder, MAOrder, Environment.NewLine);
                sb.AppendLine();
                sb.AppendFormat("X(t)");
                for (var p = 0; p < AROrder; ++p)
                {
                    var sgn = ARCoeff(p) < 0 ? '+' : '-';
                    sb.AppendFormat(" {0}{1:0.000}X(t-{2})", sgn, Math.Abs(ARCoeff(p)), p + 1);
                }

                sb.AppendFormat(" = Z(t)");
                for (var q = 0; q < MAOrder; ++q)
                {
                    var sgn = MACoeff(q) >= 0 ? '+' : '-';
                    sb.AppendFormat(" {0}{1:0.000}Z(t-{2})", sgn, Math.Abs(MACoeff(q)), q + 1);
                }

                sb.AppendLine();

                if (TailDegreesOfFreedom == 0)
                    sb.AppendFormat("Z(t) ~ N(0,{0:0.0000}^2) (iid){1}", Sigma, Environment.NewLine);
                else
                    sb.AppendFormat("Z(t) ~ {0:0.0000} x Students T({1} d.o.f.) (iid){2}", Sigma, TailDegreesOfFreedom,
                        Environment.NewLine);
                sb.AppendFormat("Mean     = {0:0.0000}{1}", Mu, Environment.NewLine);
                sb.AppendFormat("FracDiff = {0:0.0000}", FracDiff);

                return sb.ToString();
            }
        }

        public double FracDiff
        {
            get => Parameters[2];
            set => Parameters[2] = value;
        }

        public double Mu
        {
            get => Parameters[0];
            set => Parameters[0] = value;
        }

        public double Sigma
        {
            get => Parameters[1];
            set => Parameters[1] = value;
        }

        public bool IsShortMemory => Math.Abs(FracDiff) < 1e-10;

        public int AROrder { get; }

        public int MAOrder { get; }

        public int TailDegreesOfFreedom { get; }

        public void CarryOutPreMLEComputations()
        {
            // nothing to do here: if there are some aspects of likelihood computation that can be reused with different parameters and the same data,
            // we would do that here
        }

        public virtual MathNet.Numerics.LinearAlgebra.Vector<double> ParameterToCube(
            MathNet.Numerics.LinearAlgebra.Vector<double> param)
        {
            var cube = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(param.Count);

            cube[0] = Math.Exp(param[0] / muScale) / (1 + Math.Exp(param[0] / muScale)); // real to [0,1]
            cube[1] = param[1] / (1 + param[1]); // real+ to [0,1]
            cube[2] = param[2] + 0.5; // [-0.5,0.5] to [0,1]

            var backup = Parameters;
            Parameters = param;
            //Polynomial betaP = GetARPoly();
            //Polynomial betaQ = GetMAPoly();
            var betaP = GetARPoly();
            var betaQ = GetMAPoly();
            Parameters = backup;

            var arcube = betaP.MapToCube(UnitRootBarrier);
            var macube = betaQ.MapToCube(UnitRootBarrier);

            for (var i = 0; i < AROrder; ++i)
                cube[3 + i] = arcube[i];
            for (var i = 0; i < MAOrder; ++i)
                cube[3 + AROrder + i] = macube[i];

            return cube;
        }

        public virtual MathNet.Numerics.LinearAlgebra.Vector<double> CubeToParameter(
            MathNet.Numerics.LinearAlgebra.Vector<double> cube)
        {
            var param = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(3 + AROrder + MAOrder);
            param[0] = Math.Log(cube[0] / (1 - cube[0])) * muScale;
            param[1] = cube[1] / (1 - cube[1]);
            param[2] = cube[2] - 0.5;

            var arCube = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(AROrder);
            for (var i = 0; i < AROrder; ++i)
                arCube[i] = cube[3 + i];
            var maCube = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(MAOrder);
            for (var i = 0; i < MAOrder; ++i)
                maCube[i] = cube[3 + AROrder + i];

            var betaP =
                PolynomialExtensions.MapFromCube(arCube, UnitRootBarrier);
            var betaQ =
                PolynomialExtensions.MapFromCube(maCube, UnitRootBarrier);

            for (var i = 0; i < AROrder; ++i)
                param[3 + i] = -betaP[i + 1];

            for (var i = 0; i < MAOrder; ++i)
                param[3 + AROrder + i] = betaQ[i + 1];

            return param;
        }

        public virtual void ResetRealTimePrediction()
        {
            rtm = Math.Max(AROrder, MAOrder);
            rtacvf = ComputeACF(rtm + 1, false);

            // then apply the innovations algorithm to compute $theta_{n,j}$s
            rtminwidth = Math.Max(Math.Max(MAOrder, rtm - 1), 1);

            //var thetas = new Matrix(nobs, minwidth);
            rtSubThetas = Matrix<double>.Build.Dense(rtminwidth, rtminwidth);

            rtxhat = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(10000);
            rtrs = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(10000);
            rtvalues = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(10000);

            rtn = 0;
            rtxhat[0] = Mu;
            rtrs[0] = KappaFunction(1, 1, rtm, rtacvf);
        }

        // for initializing from a time series
        public virtual void Register(TimeSeries series)
        {
            for (var t = 0; t < series.Count; ++t)
                Register(series.TimeStamp(t), series[t]);
        }

        // for registering both a value and an explanatory var. (ARMAX or regression model)
        public virtual double Register(DateTime timeStamp, double value, double[] auxValues)
        {
            return Register(timeStamp, value); // for ARMA models, auxiliary is irrelevant
        }

        // for registering one item at a time
        public virtual double Register(DateTime timeStamp, double value)
        {
            // first find $\theta_{n,q},\ldots,\theta_{n,1}$
            double t1;
            double sum;

            rtvalues[rtn] = value;

            ++rtn;
            var n = rtn;
            var startPt = Math.Max(n - rtminwidth, 0);
            var n1M = (n - 1) % rtminwidth;

            for (var k = startPt; k < n; ++k)
            {
                sum = KappaFunction(n + 1, k + 1, rtm, rtacvf);
                for (var j = startPt; j < k; ++j)
                {
                    if (k - j - 1 < rtminwidth)
                        t1 = rtSubThetas[(k - 1) % rtminwidth, k - j - 1];
                    else
                        t1 = 0.0;
                    sum -= t1 * rtSubThetas[n1M, n - j - 1] * rtrs[j];
                }

                rtSubThetas[n1M, n - k - 1] = sum / rtrs[k];
            }

            // then find $r_n$
            rtrs[n] = KappaFunction(n + 1, n + 1, rtm, rtacvf);
            for (var j = Math.Max(n - rtminwidth, 0); j < n; ++j)
            {
                t1 = rtSubThetas[n1M, n - j - 1];
                rtrs[n] -= t1 * t1 * rtrs[j];
            }

            // and finally work out $\hat{X}_{n+1}$
            sum = 0.0;
            if (n < rtm)
            {
                for (var j = 1; j <= Math.Min(n, rtminwidth); ++j)
                    if (n - j < rtn)
                        sum += rtSubThetas[n1M, j - 1] * (rtvalues[n - j] - rtxhat[n - j]);
            }
            else
            {
                for (var j = 1; j <= AROrder; ++j)
                    if (n - j < rtn)
                        sum += ARCoeff(j - 1) * (rtvalues[n - j] - Mu);
                    else
                        sum += ARCoeff(j - 1) * (rtxhat[n - j] - Mu);
                for (var j = 1; j <= rtminwidth; ++j)
                    if (n - j < rtn)
                        sum += rtSubThetas[n1M, j - 1] * (rtvalues[n - j] - rtxhat[n - j]);
            }

            rtxhat[n] = sum + Mu;
            return rtxhat[n];
        }

        public virtual DistributionSummary GetCurrentPredictor(DateTime futureTime)
        {
            var ds = new DistributionSummary {Mean = rtxhat[rtn], Variance = rtrs[rtn] * Sigma * Sigma};
            // ds.FillGaussianQuantiles(0.01);
            return ds;
        }

        public override int NumOutputs()
        {
            return base.NumOutputs() + 4;
        }

        public override object GetOutput(int socket)
        {
            if (socket < base.NumOutputs())
                return base.GetOutput(socket);
            if (socket == base.NumOutputs())
                return unstandardizedResiduals;
            if (socket == base.NumOutputs() + 1)
                return oneStepPredictors;
            if (socket == base.NumOutputs() + 2)
                return oneStepPredStd;
            if (socket == base.NumOutputs() + 3)
                return oneStepPredictorsAtAvailability;
            throw new SocketException();
        }

        public override string GetOutputName(int index)
        {
            if (index < base.NumOutputs())
                return base.GetOutputName(index);
            if (index == base.NumOutputs())
                return StandardOutputs.Residuals;
            if (index == base.NumOutputs() + 1)
                return StandardOutputs.OneStepPredictor;
            if (index == base.NumOutputs() + 2)
                return StandardOutputs.OneStepPredictiveStdDev;
            if (index == base.NumOutputs() + 3)
                return StandardOutputs.OneStepPredictorAtAvail;
            throw new SocketException();
        }

        public override string GetParameterName(int index)
        {
            switch (index)
            {
                case 0:
                    return "Mu";
                case 1:
                    return "Sigma";
                case 2:
                    return "FracDiff";
                default:
                    if (index < 3 + AROrder)
                        return $"Phi({index - 2})";
                    if (index < 3 + AROrder + MAOrder)
                        return $"Theta({index - 2 - AROrder})";
                    throw new ArgumentException("Invalid parameter index.");
            }
        }

        public override string GetParameterDescription(int index)
        {
            return null;
        }

        public override string GetShortDescription()
        {
            return string.Format("ARMA{0}({1:0},{2:0})", Environment.NewLine, AROrder, MAOrder);
        }

        protected override bool CheckParameterValidity(MathNet.Numerics.LinearAlgebra.Vector<double> param)
        {
            var violation = false;

            var backup = Parameters;
            Parameters = param;
            //Polynomial betaP = GetARPoly();
            //Polynomial betaQ = GetMAPoly();
            var betaP = GetARPoly();
            var betaQ = GetMAPoly();
            Parameters = backup;

            // determine roots of the beta polynomial
            var roots = betaP.Roots();
            foreach (var c in roots)
                if (c.Magnitude < 1.0 + UnitRootBarrier) // it's too close to the unit circle
                    violation = true;

            roots = betaQ.Roots();
            foreach (var c in roots)
                if (c.Magnitude < 1.0 + UnitRootBarrier) // it's too close to the unit circle
                    violation = true;

            return !violation;
        }

        //public void SetARPolynomial(Polynomial p)
        public void SetARPolynomial(MathNet.Numerics.LinearAlgebra.Vector<double> p)
        {
            if (p.Count - 1 > AROrder)
                throw new ArgumentException("Invalid AR polynomial - incorrect order.");
            if (p[0] != 1.0)
                throw new ArgumentException("Invalid AR polynomial - first coefficient must be 1.0.");
            for (var i = 1; i <= AROrder; ++i)
                Parameters[2 + i] = -p[i];
        }

        //public void SetMAPolynomial(Polynomial p)
        public void SetMAPolynomial(MathNet.Numerics.LinearAlgebra.Vector<double> p)
        {
            if (p.Count - 1 > MAOrder)
                throw new ArgumentException("Invalid MA polynomial - incorrect order.");
            if (p[0] != 1.0)
                throw new ArgumentException("Invalid MA polynomial - first coefficient must be 1.0.");
            for (var i = 1; i <= MAOrder; ++i)
                Parameters[2 + i + AROrder] = p[i];
        }

        //public Polynomial GetMAPolynomial()
        public MathNet.Numerics.LinearAlgebra.Vector<double> GetMAPolynomial()
        {
            //var p = new Polynomial(MAOrder);
            var p = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(MAOrder + 1);
            p[0] = 1.0;
            for (var i = 1; i <= MAOrder; ++i)
                p[i] = MACoeff(i - 1);
            return p;
        }

        protected MathNet.Numerics.LinearAlgebra.Vector<double> GetLikelihoodsFromResiduals(double[] res,
            double[] pvars)
        {
            var nobs = res.Length;
            var alpha = Math.Log(2 * Math.PI * Sigma * Sigma) * 1 / 2.0;
            var allLLs = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(nobs);
            if (TailDegreesOfFreedom == 0)
            {
                for (var i = 0; i < nobs; ++i)
                    allLLs[i] = -Math.Log(pvars[i]) / 2 - res[i] * res[i] / (2 * Sigma * Sigma) - alpha;
            }
            else
            {
                var tdn = new StudentT(0, 1, TailDegreesOfFreedom);
                for (var i = 0; i < nobs; ++i)
                    allLLs[i] = Math.Log(tdn.Density(res[i] / Sigma)) - Math.Log(Sigma);
            }

            return allLLs;
        }

        public override double LogLikelihood(MathNet.Numerics.LinearAlgebra.Vector<double> parameter,
            double penaltyFactor, bool fillOutputs)
        {
            MathNet.Numerics.LinearAlgebra.Vector<double> allLLs = null;
            var pbak = Parameters; // save the current one

            if (values == null)
                return double.NaN;
            if (values.Count == 0)
                return double.NaN;

            if (parameter != null)
                Parameters = parameter;

            Residuals = null;
            double loglikelihood = 0;

            if (!DataIsLongitudinal())
            {
                autocovariance = ComputeACF(values.Count + 1, false);

                double[] rs;
                double[] forecs;
                var resids = ComputeSpecialResiduals(values, out rs, 1, out forecs);

                TimeSeries rts;
                TimeSeries unstdrts;

                if (fillOutputs)
                {
                    rts = new TimeSeries {Title = $"{values.Title}[ARMA Res]"};
                    unstdrts = new TimeSeries {Title = $"{values.Title}[ARMA Res]"};
                    oneStepPredictors = new TimeSeries {Title = $"{values.Title}[Predic]"};
                    oneStepPredStd = new TimeSeries {Title = $"{values.Title}[Pred. Stdev.]"};
                    oneStepPredictorsAtAvailability = new TimeSeries {Title = $"{values.Title}[Predic. AA]"};

                    for (var t = 0; t < values.Count; ++t)
                    {
                        rts.Add(values.TimeStamp(t), resids[t] / Sigma, false);
                        unstdrts.Add(values.TimeStamp(t), resids[t], false);
                        var stdev = Math.Sqrt(rs[t]);
                        var fx = values[t] - resids[t] * stdev;
                        oneStepPredictors.Add(values.TimeStamp(t), fx, false);
                        oneStepPredStd.Add(values.TimeStamp(t), stdev * Sigma, false);
                        fx = t < values.Count - 1 ? values[t + 1] - resids[t + 1] * Math.Sqrt(rs[t + 1]) : forecs[0];
                        oneStepPredictorsAtAvailability.Add(values.TimeStamp(t), fx, false);
                    }

                    Residuals = rts;
                    unstandardizedResiduals = unstdrts;
                }

                // now it is easy to compute the likelihood
                // this is a straightforward implementation of eqn (8.7.4) in Brockwell & Davis,
                // Time Series: Theory and Methods (2nd edition)
                allLLs = GetLikelihoodsFromResiduals(resids, rs);
                for (var t = 0; t < values.Count; ++t)
                    loglikelihood += allLLs[t];
            }
            else
            {
                // do nothing for now!
                return double.NaN;
            }

            if (fillOutputs)
                GoodnessOfFit = loglikelihood;

            if (parameter != null)
                Parameters = pbak; // then restore original

            var llp = new LogLikelihoodPenalizer(allLLs);
            return llp.LogLikelihood - llp.Penalty * penaltyFactor;
        }

        protected override MathNet.Numerics.LinearAlgebra.Vector<double> ComputeConsequentialParameters(
            MathNet.Numerics.LinearAlgebra.Vector<double> parameter)
        {
            // fill in mean and sigma
            var pbak = Parameters;
            Parameters = parameter;
            MathNet.Numerics.LinearAlgebra.Vector<double> newParms = null;

            double[] rs;
            double[] forecs;
            double[] res;

            if (!DataIsLongitudinal())
            {
                // case 1: standard univariate time series data
                if (ParameterStates[0] == ParameterState.Consequential)
                    Mu = values.SampleMean();

                if (ParameterStates[1] == ParameterState.Consequential)
                {
                    autocovariance = ComputeACF(values.Count + 1, false);
                    res = ComputeSpecialResiduals(values, out rs, 0, out forecs);

                    if (TailDegreesOfFreedom == 0) // i.e. if innovations are normal
                    {
                        // consequential sigma is just the standard deviation
                        double ss = 0;
                        for (var i = 0; i < res.Length; ++i)
                            ss += res[i] * res[i];
                        Sigma = Math.Sqrt(ss / res.Length);
                    }
                    else // for model with t-distribution innovations
                    {
                        var tdn = new StudentT(0, 1, TailDegreesOfFreedom);
                        var vres = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.DenseOfArray(res);
                        Sigma = tdn.MLEofSigma(vres);
                    }
                }

                newParms = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.DenseOfVector(Parameters);
            }
            else
            {
                // case 2: longitudinal data
                if (ParameterStates[0] == ParameterState.Consequential)
                    Mu = longitudinalValues.SampleMean();

                if (ParameterStates[1] == ParameterState.Consequential)
                {
                    // get all residuals
                    autocovariance = ComputeACF(longitudinalValues.MaxCount + 1, false);
                    double ss = 0;
                    var ssCount = 0;
                    for (var i = 0; i < longitudinalValues.Count; ++i)
                    {
                        res = ComputeSpecialResiduals(longitudinalValues[i], out rs, 0, out forecs);
                        for (var t = 0; t < res.Length; ++t)
                            ss += res[t] * res[t];
                        ssCount += res.Length;
                    }

                    Sigma = Math.Sqrt(ss / ssCount);
                }

                newParms = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.DenseOfVector(Parameters);
            }

            Parameters = pbak;

            return newParms;
        }

        public override object SimulateData(object inputs, int randomSeed)
        {
            var times = inputs as List<DateTime>;
            if (times == null)
                return null; // inputs should be a list of DateTimes

            // Simulation here uses the Durbin-Levinson recursions to simulate from
            // successive one-step predictive d-ns.
            // (This works for long-memory processes as well, unlike the obvious constructive approach for ARMA models.)

            var nn = times.Count;
            var acf = ComputeACF(nn + 1, false);
            var nu = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(nn);
            var olda = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(nn);
            var a = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(nn);
            var simd = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(nn);

            var rs = new Palf(randomSeed);
            var sd = new StudentT();
            sd.RandomSource = rs;

            nu[0] = acf[0]; // nu(0) = 1-step pred. variance of X(0)
            simd[0] = sd.RandomSource.NextDouble() * Math.Sqrt(nu[0]);

            for (var t = 1; t < nn; ++t)
            {
                for (var j = 0; j < nn; ++j)
                    olda[j] = a[j];

                // compute the new a MathNet.Numerics.LinearAlgebra.Vector
                var sum = 0.0;
                for (var j = 1; j < t; ++j)
                    sum += olda[j - 1] * acf[t - j];
                a[t - 1] = 1 / nu[t - 1] * (acf[t] - sum);
                for (var j = 0; j < t - 1; ++j)
                    a[j] = olda[j] - a[t - 1] * olda[t - 2 - j];

                // update nu
                nu[t] = nu[t - 1] * (1 - a[t - 1] * a[t - 1]);

                // compute xhat
                sum = 0.0;
                for (var j = 0; j < t; ++j)
                    sum += a[j] * simd[t - 1 - j];
                simd[t] = sd.RandomSource.NextDouble() * Math.Sqrt(nu[t]) + sum;
            }

            var simulated = new TimeSeries
            {
                Title = "Simul.",
                Description = $"Simulation from {Description}"
            };
            for (var i = 0; i < nn; ++i)
                simulated.Add(times[i], simd[i] + Mu, false);

            return simulated;
        }

        public override object BuildForecasts(object otherData, object inputs)
        {
            var futureTimes = inputs as List<DateTime>;
            if (futureTimes == null)
                return null; // need future times to forecast

            var startData = otherData as TimeSeries;
            if (startData == null)
                return null; // need data to forecast

            // Now we can forecast:
            // for ARMA models, we don't do any kind of analysis of the future times, we just
            // assume they are the next discrete time points in a typical ARMA model with
            // times t=1,2,3,...,n.  So all we care about is the number of inputs.

            var preds = GetForecasts(startData, futureTimes);

            return preds;
        }

        /// <summary>
        ///     returns the i'th autoregressive coefficient: 0 = Phi_1, 1 = Phi_2, etc.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public double ARCoeff(int i)
        {
            return Parameters[3 + i];
        }

        public double MACoeff(int i)
        {
            return Parameters[3 + AROrder + i];
        }

        //private Polynomial GetARPoly()
        private MathNet.Numerics.LinearAlgebra.Vector<double> GetARPoly()
        {
            //Console.WriteLine(arOrder);
            var p = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(AROrder + 1);
            //var p = new Polynomial(arOrder);
            p[0] = 1.0;
            for (var i = 0; i < AROrder; ++i)
                p[i + 1] = -ARCoeff(i);
            return p;
        }

        //private Polynomial GetMAPoly()
        private MathNet.Numerics.LinearAlgebra.Vector<double> GetMAPoly()
        {
            //var p = new Polynomial(maOrder);
            var p = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(MAOrder + 1);
            p[0] = 1.0;
            for (var i = 0; i < MAOrder; ++i)
                p[i + 1] = MACoeff(i);
            return p;
        }

        protected void LocalInitializeParameters()
        {
            Parameters =
                MathNet.Numerics.LinearAlgebra.Vector<double>.Build
                    .Dense(3 + AROrder + MAOrder); // all coeffs are initially zero
            Mu = 0;
            Sigma = 1;
            FracDiff = 0;

            ParameterStates = new ParameterState[Parameters.Count];
            ParameterStates[0] = ParameterState.Consequential; // mu follows
            ParameterStates[1] = ParameterState.Consequential; // sigma follows from the others
            ParameterStates[2] = ParameterState.Locked; // locked at 0 by default
            for (var i = 3; i < Parameters.Count; ++i)
                ParameterStates[i] = ParameterState.Free; // only AR and MA coefficients are free  
        }

        protected override void InitializeParameters()
        {
            LocalInitializeParameters();
        }

        private MathNet.Numerics.LinearAlgebra.Vector<double> ComputePsiCoefficients(int length)
        {
            var psis = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(length);

            if (IsShortMemory)
            {
                psis[0] = 1;
                for (var m = 1; m < length; ++m)
                {
                    psis[m] = m <= MAOrder ? MACoeff(m - 1) : 0;
                    for (var k = 1; k <= Math.Min(m, AROrder); ++k)
                        psis[m] += ARCoeff(k - 1) * psis[m - k];
                }

                return psis;
            }

            throw new NotImplementedException("Cannot yet compute psis for ARFIMAs.");
        }

        /// <summary>
        ///     This function computes predictive means (one-step, two-step, ...)
        ///     along with the predictive mean-squared error.  It
        ///     assumes that data is regularly spaced in time.
        /// </summary>
        /// <param name="startData">existing data that we assume comes from the model</param>
        /// <param name="futureTimes">times in the future</param>
        /// <returns></returns>
        private TimeSeriesBase<DistributionSummary> GetForecasts(TimeSeries startData, IList<DateTime> futureTimes)
        {
            // now do forecasting, using the standard Durbin-Levison Algorithm
            var nobs = startData.Count;
            var horizon = futureTimes.Count;

            // First we need to compute Gamma(0..m) if we haven't already got enough of it
            if (autocovariance == null || autocovariance.Count < nobs + horizon + 1)
                autocovariance = ComputeACF(nobs + horizon + 1, false);

            // Numerically stable approach: use innovations algorithm if possible
            double[] nus;
            double[] forecs;
            ComputeSpecialResiduals(startData, out nus, horizon, out forecs);

            //// now compute MSEs of 1...horizon step predictors
            //// compute psis in causal expansion,
            //// by phi(B) (1-B)^d psi(B) = theta(B) and match coefficients
            var psis = ComputePsiCoefficients(horizon);

            // Use approximation (B&D eqn (5.3.24)) as before to get predictive variances
            var localFmse = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(horizon);
            localFmse[0] = 1.0;
            for (var i = 1; i < horizon; ++i)
                localFmse[i] = localFmse[i - 1] + psis[i] * psis[i];
            localFmse = localFmse * Sigma * Sigma;

            var predictors = new TimeSeriesBase<DistributionSummary>();
            for (var i = 0; i < horizon; ++i)
            {
                var dn = new DistributionSummary();
                dn.Mean = forecs[i];
                dn.Variance = localFmse[i];
                // dn.FillGaussianQuantiles(0.04);
                predictors.Add(futureTimes[i], dn, false);
            }

            return predictors;
        }

        public override MathNet.Numerics.LinearAlgebra.Vector<double> ComputeACF(int maxLag, bool normalize)
        {
            int i, h, j;
            double tx;

            var p = AROrder;
            var q = MAOrder;

            while (p > 0 && ARCoeff(p - 1) == 0)
                --p;
            while (q > 0 && MACoeff(q - 1) == 0)
                --q;

            var acvf = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(maxLag + 1);

            if (IsShortMemory) // i.e. if it's not fractionally integrated
            {
                var psi = ComputePsiCoefficients(q + 1);

                // second step: solve Y-W for $\gamma(0),\ldots,\gamma(p)$
                var phistuff = Matrix<double>.Build.Dense(p + 1, p + 1);
                var psistuff = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(p + 1);

                for (i = 0; i <= p; ++i)
                for (j = 0; j <= p; ++j)
                {
                    tx = 1.0;
                    if (j > 0)
                        if (j <= p)
                            tx = -ARCoeff(j - 1);
                        else
                            tx = 0;
                    phistuff[i, i - j > 0 ? i - j : j - i] += tx;
                }

                for (i = 0; i <= p; ++i)
                {
                    psistuff[i] = 0;
                    for (j = i; j <= q; ++j)
                    {
                        tx = 1.0;
                        if (j > 0)
                            tx = MACoeff(j - 1); // was maPoly(j-1)
                        if (j > q)
                            tx = 0.0;
                        psistuff[i] += tx * psi[j - i];
                    }

                    psistuff[i] *= Sigma * Sigma;
                }

                var gammas = phistuff.Solve(psistuff.ToColumnMatrix());

                // copy into return array
                for (i = 0; i <= (p > maxLag ? maxLag : p); ++i)
                    acvf[i] = gammas[i, 0];

                // and then compute the rest recursively
                for (i = p + 1; i <= maxLag; ++i)
                {
                    for (j = 1, tx = 0.0; j <= p; ++j)
                        tx += ARCoeff(j - 1) * acvf[i - j];
                    if (i < (p > q + 1 ? p : q + 1))
                        for (j = i; j <= q; ++j)
                        {
                            var ty = 1.0;
                            if (j > 0)
                                ty = MACoeff(j - 1);
                            if (j > q)
                                ty = 0.0;
                            tx += Sigma * Sigma * ty * psi[j - i];
                        }

                    acvf[i] = tx;
                }
            } // end of normal fracDiff==0.0 case
            else
            {
                // fractionally differenced case
                var psis = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(q + 1);
                var zetas = Matrix<double>.Build.Dense(p + 1, 2);
                var tc = new Complex();
                var tc2 = new Complex();
                Complex tc3;
                int p2 = 0, l;
                for (i = 0; i < p; ++i)
                    if (Math.Abs(ARCoeff(i)) > 1e-10)
                        p2 = i + 1; // p2 is the effective AR order, is < p if phi_p

                // compute psis
                for (i = 0; i <= q; ++i)
                for (psis[i] = 0, j = i; j <= q; ++j)
                {
                    tx = j > 0 ? MACoeff(j - 1) : 1.0;
                    var tx2 = j > i ? MACoeff(j - 1 - i) : 1.0;
                    psis[i] += tx * tx2;
                }

                // get AR polynomial roots
                List<Complex> roots;
                //var trimmedAR = new Polynomial(p2);
                var trimmedAR = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(p2);
                trimmedAR[0] = 1.0;
                for (i = 1; i <= p2; ++i)
                    trimmedAR[i] = -ARCoeff(i - 1);
                roots = trimmedAR.Roots(); // could be an empty list!

                // transform to inverse roots
                for (i = 0; i < p2; ++i)
                    roots[i] = 1.0 / roots[i];

                // get zetas
                for (i = 0; i < p2; ++i)
                {
                    for (j = 0, tc = 1.0; j < p2; ++j)
                        tc *= 1.0 - roots[i] * roots[j];
                    for (j = 0; j < p2; ++j)
                        if (j != i)
                            tc *= roots[i] - roots[j];
                    tc = 1.0 / tc;
                    zetas[i, 0] = tc.Real;
                    zetas[i, 1] = tc.Imaginary;
                }

                // compute gamma
                if (p2 == 0)
                    for (l = -q; l <= q; ++l)
                    {
                        var al = l < 0 ? -l : l;
                        tx = GammaFunction(1 - 2 * FracDiff) * GammaFunction(FracDiff + l) /
                             (GammaFunction(FracDiff) * GammaFunction(1.0 - FracDiff) *
                              GammaFunction(1.0 - FracDiff + l));
                        acvf[0] += psis[al] * tx;
                        for (h = 1; h <= maxLag; ++h)
                        {
                            tx *= (1.0 - FracDiff - h + l) / (FracDiff - h + l);
                            acvf[h] += psis[al] * tx;
                        }

                        acvf = acvf * Sigma * Sigma;
                    }
                else
                    for (j = 0; j < p2; ++j)
                    {
                        var cMatrix = CFunctionsFor(FracDiff, roots[j].Real, roots[j].Imaginary,
                            p2, p2 + q, 2 * q + maxLag + 2);
                        for (l = -q; l <= q; ++l)
                        for (h = 0; h <= maxLag; ++h)
                        {
                            tc = new Complex(zetas[j, 0], zetas[j, 1]);
                            //tc.Real = zetas[j, 0];
                            //tc.Imag = zetas[j, 1];
                            tc2 = new Complex(cMatrix[h + q - l, 0], cMatrix[h + q - l, 1]);
                            //tc2.Real = cMatrix[h + q - l, 0];
                            //tc2.Imag = cMatrix[h + q - l, 1];
                            tc3 = Sigma * Sigma * psis[l < 0 ? -l : l] * tc * tc2;
                            acvf[h] += tc3.Real; // we know imag. parts will cancel!
                        }
                    }
            }

            if (normalize)
                acvf /= acvf[0];
            return acvf;
        }

        /// <summary>
        ///  This function uses the approach described
        ///  in Section 8.7 of B&D if fracdiff parm==0
        ///  to get one-step predictors and residuals.
        /// </summary>
        /// <param name="rs">double[] array of (one-step predictive MSEs / sigma^2),
        //     based on sigma when this routine was called </param>
        /// <returns>double[] array of residuals = [ (x_i - \hat{x}_i)/\sqrt{rs_i} ]</returns>
        protected double[] ComputeSpecialResiduals(TimeSeries startData, out double[] rs, int forecastHorizon,
            out double[] forecasts)
        {
            // If fracdiff!=0 then it uses the D-L algorithm
            // with ACF computed by Sowell's formula.

            // 1.  residuals are returned: retval[] =  [ (x_i - \hat{x}_i)/\sqrt{rs_{i-1}} ]
            // 2.  rs[...] are one-step predictive MSEs / sigma^2,
            //     based on sigma when this routine was called

            int i, j, k, n, nobs = startData.Count + forecastHorizon;
            var xhat = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(Math.Max(nobs + 1, 1));
            var localResiduals = new double[nobs];
            double sum;
            rs = new double[nobs + 1];

            if (IsShortMemory)
            {
                var m = Math.Max(AROrder, MAOrder);

                // first we need to compute Gamma(0..m)
                if (autocovariance.Count < m + 1)
                    throw new ApplicationException("Internal autocovariance not computed to enough lags.");

                // then apply the innovations algorithm to compute $theta_{n,j}$s
                var minwidth = Math.Max(Math.Max(MAOrder, m - 1), 1);

                //var thetas = new Matrix(nobs, minwidth);
                var subThetas = Matrix<double>.Build.Dense(minwidth, minwidth);

                xhat[0] = Mu;
                rs[0] = KappaFunction(1, 1, m, autocovariance);
                for (n = 1; n <= nobs; ++n)
                {
                    // first find $\theta_{n,q},\ldots,\theta_{n,1}$
                    double t1;
                    var startPt = Math.Max(n - minwidth, 0);
                    var n1M = (n - 1) % minwidth;

                    for (k = startPt; k < n; ++k)
                    {
                        sum = KappaFunction(n + 1, k + 1, m, autocovariance);
                        for (j = startPt; j < k; ++j)
                        {
                            if (k - j - 1 < minwidth)
                                t1 = subThetas[(k - 1) % minwidth, k - j - 1];
                            else
                                t1 = 0.0;
                            sum -= t1 * subThetas[n1M, n - j - 1] * rs[j];
                        }

                        subThetas[n1M, n - k - 1] = sum / rs[k];
                    }

                    // then find $r_n$
                    rs[n] = KappaFunction(n + 1, n + 1, m, autocovariance);
                    for (j = Math.Max(n - minwidth, 0); j < n; ++j)
                    {
                        t1 = subThetas[n1M, n - j - 1];
                        rs[n] -= t1 * t1 * rs[j];
                    }

                    // and finally work out $\hat{X}_{n+1}$
                    sum = 0.0;
                    if (n < m)
                    {
                        for (j = 1; j <= Math.Min(n, minwidth); ++j)
                            if (n - j < startData.Count)
                                sum += subThetas[n1M, j - 1] * (startData[n - j] - xhat[n - j]);
                    }
                    else
                    {
                        for (j = 1; j <= AROrder; ++j)
                            if (n - j < startData.Count)
                                sum += ARCoeff(j - 1) * (startData[n - j] - Mu);
                            else
                                sum += ARCoeff(j - 1) * (xhat[n - j] - Mu);
                        for (j = 1; j <= minwidth; ++j)
                            if (n - j < startData.Count)
                                sum += subThetas[n1M, j - 1] * (startData[n - j] - xhat[n - j]);
                    }

                    if (n < nobs)
                        xhat[n] = sum + Mu;
                }
            } // end of short memory case
            else
            {
                if (autocovariance.Count < nobs + 1)
                    throw new ApplicationException("Internal autocovariance not computed to enough lags.");

                var dl = new DurbinLevinsonPredictor(Mu, autocovariance);
                xhat[0] = dl.CurrentPredictor;
                rs[0] = dl.CurrentMSPE;
                for (i = 1; i <= nobs; ++i)
                {
                    if (i <= startData.Count)
                        dl.Register(startData[i - 1]);
                    else
                        dl.Register(xhat[i - 1]);
                    xhat[i] = dl.CurrentPredictor;
                    rs[i] = dl.CurrentMSPE;
                }

                for (i = 0; i < rs.Length; ++i)
                    rs[i] /= Sigma * Sigma;
            }

            // store the results
            for (n = 0; n < startData.Count; ++n)
                localResiduals[n] = (startData[n] - xhat[n]) / Math.Sqrt(rs[n]);

            if (forecastHorizon > 0)
            {
                forecasts = new double[forecastHorizon];
                for (n = startData.Count; n < nobs; ++n)
                    forecasts[n - startData.Count] = xhat[n];
            }
            else
            {
                forecasts = null;
            }

            return localResiduals;
        }

        /// <summary>
        ///     This method fits the model by using Yule-Walker estimation.  It can only handle a few particular orders for the
        ///     ARMA model now.
        ///     In other cases it throws an exception.
        /// </summary>
        /// <param name="acvf"></param>
        public void EstimateByYuleWalker(double sampleMean, MathNet.Numerics.LinearAlgebra.Vector<double> acvf)
        {
            double s1, s2, phiHat, theta1, theta2, thetaHat;
            const double epsilon = 1e-4;

            if (MAOrder == 0)
                // AR(p)
                switch (AROrder)
                {
                    case 0:
                        // WN
                        Mu = sampleMean;
                        Sigma = Math.Sqrt(acvf[0]);
                        FracDiff = 0.0;
                        return;
                    case 1:
                        // AR(1)
                        phiHat = acvf[1] / acvf[0];
                        if (phiHat > 1 - epsilon)
                            phiHat = 1 - epsilon;
                        if (phiHat < -1 + epsilon)
                            phiHat = -1 + epsilon;
                        Mu = sampleMean;
                        FracDiff = 0.0;
                        SetARPolynomial(
                            MathNet.Numerics.LinearAlgebra.Vector<double>.Build.DenseOfArray(new[] {1, -phiHat}));
                        //SetARPolynomial(new Polynomial(new[] { 1, -phiHat }));
                        Sigma = Math.Sqrt(acvf[0] * (1 - phiHat * phiHat));
                        return;
                }

            if (MAOrder == 1)
                switch (AROrder)
                {
                    case 0:
                        // MA(1)
                        Mu = sampleMean;
                        var xx = Math.Sqrt(acvf[0] * acvf[0] - 4 * acvf[1] * acvf[1]);
                        s1 = (acvf[0] + xx) / 2;
                        s2 = (acvf[0] - xx) / 2;
                        theta1 = acvf[1] / s1;
                        theta2 = acvf[1] / s2; // 2 possible solutions to the quadratic equation we get
                        thetaHat = Math.Abs(theta1) < Math.Abs(theta2)
                            ? theta1
                            : theta2; // so pick the one with the smaller magnitude value of theta
                        if (thetaHat > 1 - epsilon)
                            thetaHat = 1 - epsilon;
                        if (thetaHat < -1 + epsilon)
                            thetaHat = -1 + epsilon;
                        //SetMAPolynomial(new Polynomial(new[] {1, thetaHat}));
                        SetMAPolynomial(
                            MathNet.Numerics.LinearAlgebra.Vector<double>.Build.DenseOfArray(new[] {1, thetaHat}));
                        Sigma = Math.Sqrt(acvf[1] / thetaHat);
                        FracDiff = 0.0;
                        return;

                    case 1:
                        // ARMA(1,1)
                        Mu = sampleMean;
                        if (acvf[1] != 0)
                            phiHat = acvf[2] / acvf[1];
                        else
                            phiHat = 0.0;
                        if (phiHat > 1 - epsilon)
                            phiHat = 1 - epsilon;
                        if (phiHat < -1 + epsilon)
                            phiHat = -1 + epsilon;
                        var k = acvf[1] - phiHat * acvf[0];

                        // now solve for sigma^2
                        double qa = 1.0, qb = 2 * phiHat * k - acvf[0] * (1 - phiHat * phiHat), qc = k * k;
                        var q = qb * qb - 4 * qa * qc;
                        var sq = q >= 0 ? Math.Sqrt(q) : 0;
                        s1 = (-qb + sq) / (2 * qa);
                        s2 = (-qb - sq) / (2 * qa);
                        if (s1 <= 0)
                            s1 = s2;
                        if (s2 <= 0)
                            s2 = s1;
                        theta1 = k / s1;
                        theta2 = k / s2;
                        thetaHat = Math.Abs(theta1) < Math.Abs(theta2)
                            ? theta1
                            : theta2; // so pick the one with the smaller magnitude value of theta
                        Sigma = Math.Sqrt(Math.Abs(theta1) < Math.Abs(theta2) ? s1 : s2);
                        if (thetaHat > 1 - epsilon)
                            thetaHat = 1 - epsilon;
                        if (thetaHat < -1 + epsilon)
                            thetaHat = -1 + epsilon;
                        //SetMAPolynomial(new Polynomial(new[] { 1, thetaHat }));
                        //SetARPolynomial(new Polynomial(new[] {1, -phiHat}));
                        SetMAPolynomial(
                            MathNet.Numerics.LinearAlgebra.Vector<double>.Build.DenseOfArray(new[] {1, thetaHat}));
                        SetARPolynomial(
                            MathNet.Numerics.LinearAlgebra.Vector<double>.Build.DenseOfArray(new[] {1, -phiHat}));
                        FracDiff = 0.0;
                        if (double.IsNaN(thetaHat))
                            throw new ApplicationException("Invalid value of theta.");
                        return;
                }

            throw new IndexOutOfRangeException();
        }

        /// <summary>
        ///     returns matrix containing functions required in Doornik and Ooms (or Oornik and Dooms?) paper
        /// </summary>
        /// <param name="d"></param>
        /// <param name="rho_real"></param>
        /// <param name="rho_imag"></param>
        /// <param name="p"></param>
        /// <param name="h"></param>
        /// <param name="extent"></param>
        /// <returns></returns>
        private Matrix<double> CFunctionsFor(double d, double rho_real, double rho_imag,
                int p, int h, int extent)
            // returns MathNet.Numerics.LinearAlgebra.Vector C^*(d,h,rho)...C^*(d,h-extent,rho)
        {
            int i, j;
            double c0, c1, c0onc1;
            Complex tc;
            var tc2 = new Complex();
            var tc3 = new Complex();
            var rho = new Complex(rho_real, rho_imag);
            //rho.Real = rho_real;
            //rho.Imag = rho_imag;
            var result = Matrix<double>.Build.Dense(extent, 2);

            // Deal with numerical problems as in Doornik & Ooms
            int glen = 2 * (extent - 2) + 1, gmid = extent - 2;
            var gval = Matrix<double>.Build.Dense(glen, 2);
            double a, c;

            // Step 1: compute gval MathNet.Numerics.LinearAlgebra.Vector
            //    gval(j) = G(d+i,1-d+i,rho), i=j-gmid
            c = 1 - d + extent - 2;
            a = d + extent - 2;
            tc = HypergeometricFunction_2F1(a, c, rho_real, rho_imag);
            tc = new Complex(-1.0, tc.Imaginary);
            //tc.Real -= 1.0;
            tc /= rho; // fix as rho->0 !
            gval[glen - 1, 0] = tc.Real;
            gval[glen - 1, 1] = tc.Imaginary;

            // then the rest are computed recursively
            for (i = extent - 3, j = 2; i >= -extent + 2; --i, ++j)
            {
                c = 1 - d + i;
                a = d + i;
                //tc.Real = gval[glen - j + 1, 0];
                //tc.Imag = gval[glen - j + 1, 1];
                tc = new Complex(gval[glen - j + 1, 0], gval[glen - j + 1, 1]);
                tc = a / c * (1.0 + rho * tc);
                gval[glen - j, 0] = tc.Real;
                gval[glen - j, 1] = tc.Imaginary;
            }

            // Step 2: compute C function
            c0 = GammaFunction(1 - 2 * d) * GammaFunction(d + h);
            c1 = GammaFunction(1 - d + h) * GammaFunction(1 - d) * GammaFunction(d);
            c0onc1 = c0 / c1;
            tc2 = new Complex(gval[gmid + h, 0], gval[gmid + h, 1]);
            //tc2.Real = gval[gmid + h, 0];
            //tc2.Imaginary = gval[gmid + h, 1];
            tc3 = new Complex(gval[gmid - h, 0], gval[gmid - h, 1]);
            //tc3.Real = gval[gmid - h, 0];
            //tc3.Imaginary = gval[gmid - h, 1];
            tc = c0onc1 * (ComplexPower(rho, 2 * p) * tc2 + ComplexPower(rho, 2 * p - 1) + tc3);
            result[0, 0] = tc.Real;
            result[0, 1] = tc.Imaginary;
            for (i = 1; i < extent; ++i)
            {
                var lh = h - i;
                c0onc1 *= (1 - d + lh) / (d + lh);
                //tc2.Real = gval[gmid + h - i, 0];
                //tc2.Imag = gval[gmid + h - i, 1];
                tc2 = new Complex(gval[gmid - h + i, 0], gval[gmid - h + i, 1]);
                tc3 = new Complex(gval[gmid - h + i, 0], gval[gmid - h + i, 1]);
                //tc3.Real = gval[gmid - h + i, 0];
                //tc3.Imag = gval[gmid - h + i, 1];
                tc = c0onc1 * (ComplexPower(rho, 2 * p) * tc2 + ComplexPower(rho, 2 * p - 1) + tc3);
                result[i, 0] = tc.Real;
                result[i, 1] = tc.Imaginary;
            }

            return result;
        }

        private static Complex HypergeometricFunction_2F1(double a, double c, double rhoReal, double rhoImag)
        {
            // this one sums the series directly, assuming b=1
            int i, m;
            var magrho = Math.Sqrt(rhoReal * rhoReal + rhoImag * rhoImag);
            var rhostar = 1 - (1 - magrho) / 2.0;
            var chro = new Complex(rhoReal, rhoImag);
            const double tolerance = 0.0001;

            // determine how many terms to use
            var k = (int) (Math.Ceiling((Math.Abs(a) * magrho + Math.Abs(c) * rhostar) / (magrho - rhostar)) + 0.5);
            if (k < 2)
                k = 2;
            m = (int) (Math.Ceiling(Math.Log(tolerance) / Math.Log(rhostar)) + 0.5);
            if (m < 3)
                m = 3;

            Complex total = 1.0;
            var paonpc = 1.0;
            Complex chroprod = 1.0;
            for (i = 0; i < k + m; ++i)
            {
                paonpc *= (a + i) / (c + i);
                chroprod *= chro;
                total += paonpc * chroprod;
            }

            return total;
        }

        protected static double GammaFunction(double x)
        {
            var localx = x;
            var multFactor = 1.0;

            while (localx <= 0.0)
            {
                multFactor /= localx;
                localx += 1.0;
            }

            var tx = SpecialFunctions.GammaLn(localx);
            return Math.Exp(tx) * multFactor;
        }

        protected static Complex ComplexPower(Complex c, double pow)
        {
            var r = Math.Sqrt(Math.Pow(c.Magnitude, 2));
            var theta = c.Phase;
            var retval = Complex.FromPolarCoordinates(Math.Pow(r, pow), theta * pow);
            return retval;
        }

        /// <summary>
        ///     this is acvf of the modified X_t process (Ansley 1979)
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="m"></param>
        /// <returns></returns>
        private double KappaFunction(int i, int j, int m, MathNet.Numerics.LinearAlgebra.Vector<double> acvf)
        {
            int m1 = Math.Min(i, j), m2 = Math.Max(i, j), ti, r;
            double sum;

            if (m2 <= m)
                return acvf[Math.Abs(i - j)] / (Sigma * Sigma);
            if (m1 > m)
            {
                sum = 0.0;
                for (r = 0; r <= MAOrder; ++r)
                {
                    var t1 = r == 0 ? 1.0 : MACoeff(r - 1);
                    ti = r + Math.Abs(i - j);
                    double t2;
                    if (ti == 0)
                        t2 = 1.0;
                    else t2 = ti <= MAOrder ? MACoeff(ti - 1) : 0.0;
                    sum += t1 * t2;
                }

                return sum;
            }

            if (m1 <= m && m2 > m && m2 <= 2 * m)
            {
                sum = acvf[Math.Abs(i - j)];
                for (r = 1; r <= AROrder; ++r)
                {
                    ti = r - Math.Abs(i - j);
                    sum -= ARCoeff(r - 1) * acvf[Math.Abs(ti)];
                }

                return sum / (Sigma * Sigma);
            }

            return 0;
        }
    }
}
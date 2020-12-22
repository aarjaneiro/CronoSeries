#region License Info

//Component of Cronos Package, http://www.codeplex.com/cronos
//Copyright (C) 2009, 2010 Anthony Brockwell

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
using System.Numerics;
using System.Text;
using CronoSeries.ABMath.Miscellaneous;
using CronoSeries.ABMath.ModelFramework.Data;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;

//using MathNet.Numerics.RandomSources;

namespace CronoSeries.ABMath.ModelFramework.Models
{
    /// <summary>
    ///     This is really like an ARMA model in many respects, but you specify the square root of the variance (sigma),
    ///     and the autocorrelations at lags 1...maxLag, instead of specifying autoregressive and moving average polynomials.
    ///     This means that we have to fall back on the Durbin-Levinson recursions for likelihood computations, instead of
    ///     using more efficient recursions from Brockwell & Davis.
    /// </summary>
    [Serializable]
    public class ACFModel : UnivariateTimeSeriesModel, IMLEEstimable, IRealTimePredictable //, IExtraFunctionality
    {
        private const double muScale = 10.0;
        private int maxLag;

        [NonSerialized] protected TimeSeries oneStepPredictors; // timestamped to align with what they are predicting

        [NonSerialized] protected TimeSeries oneStepPredictorsAtAvailability;
        // timestamped at the point the predictor is available

        [NonSerialized] protected TimeSeries oneStepPredStd; // also aligned with what they are predicting
        [NonSerialized] protected TimeSeries unstandardizedResiduals;

        public ACFModel(int maxLag)
        {
            this.maxLag = maxLag;
            LocalInitializeParameters();
        }

        public override string Description
        {
            get
            {
                var sb = new StringBuilder(1024);
                sb.AppendLine("ACF Model:");
                sb.AppendLine();
                sb.AppendFormat("Mean     = {0:0.0000}{1}", Mu, Environment.NewLine);
                sb.AppendFormat("Sigma    = {0:0.0000}{1}", Sigma, Environment.NewLine);
                return sb.ToString();
            }
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

        public void CarryOutPreMLEComputations()
        {
            // nothing to do here: if there are some aspects of likelihood computation that can be reused with different parameters and the same data,
            // we would do that here
        }

        public double Rho(int lag)
        {
            return Parameters[lag + 2];
        }

        public void SetRho(int lag, double value)
        {
            Parameters[lag + 2] = value;
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
                default:
                    if (index < 2 + maxLag)
                        return $"Rho({index - 1})";
                    throw new ApplicationException("Invalid parameter index.");
            }
        }

        public override string GetParameterDescription(int index)
        {
            return null;
        }

        public override string GetShortDescription()
        {
            return string.Format("ACF Model({0})", maxLag);
        }


        protected override bool CheckParameterValidity(MathNet.Numerics.LinearAlgebra.Vector<double> param)
        {
            var violation = false;

            // simple check for now: really we should check to make sure that the specified autocorrelation is non-negative definite
            for (var i = 0; i < maxLag; ++i)
                violation |= Math.Abs(param[2 + i]) >= 1;

            return !violation;
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
                // do nothing for now!
            {
                return double.NaN;
            }

            if (fillOutputs)
                GoodnessOfFit = loglikelihood;

            if (parameter != null)
                Parameters = pbak; // then restore original

            var llp = new LogLikelihoodPenalizer(allLLs);
            return llp.LogLikelihood - llp.Penalty * penaltyFactor;
        }

        protected MathNet.Numerics.LinearAlgebra.Vector<double> GetLikelihoodsFromResiduals(double[] res,
            double[] pvars)
        {
            var nobs = res.Length;
            var alpha = Math.Log(2 * Math.PI * Sigma * Sigma) * 1 / 2.0;
            var allLLs = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(nobs);
            for (var i = 0; i < nobs; ++i)
                allLLs[i] = -Math.Log(pvars[i]) / 2 - res[i] * res[i] / (2 * Sigma * Sigma) - alpha;
            return allLLs;
        }


        protected override MathNet.Numerics.LinearAlgebra.Vector<double> ComputeConsequentialParameters(
            MathNet.Numerics.LinearAlgebra.Vector<double> parameter)
        {
            // fill in mean and sigma
            var pbak = Parameters;
            Parameters = parameter;
            MathNet.Numerics.LinearAlgebra.Vector<double> newParms;

            // case 1: standard univariate time series data
            if (ParameterStates[0] == ParameterState.Consequential)
                Mu = values.SampleMean();

            if (ParameterStates[1] == ParameterState.Consequential)
            {
                var vr = values.ComputeACF(0, false);
                Sigma = Math.Sqrt(vr[0]); // just match variance with the data
            }

            newParms = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.DenseOfVector(Parameters);
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
            var sd = new Normal(); //new StandardDistribution(rs);
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

        protected void LocalInitializeParameters()
        {
            Parameters =
                MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(2 + maxLag); // all coeffs are initially zero
            Mu = 0;
            Sigma = 1;

            ParameterStates = new ParameterState[Parameters.Count];
            ParameterStates[0] = ParameterState.Consequential; // mu follows
            ParameterStates[1] = ParameterState.Consequential; // sigma follows from the others
            for (var i = 0; i < maxLag; ++i)
                ParameterStates[2 + i] = ParameterState.Locked; // locked at 0 by default
        }

        protected override void InitializeParameters()
        {
            LocalInitializeParameters();
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

            // Numerically stable approach: use innovations algorithm if possible
            double[] nus;
            double[] forecs;
            ComputeSpecialResiduals(startData, out nus, horizon, out forecs);

            //// now compute MSEs of 1...horizon step predictors
            //// compute psis in causal expansion,
            //// by phi(B) (1-B)^d psi(B) = theta(B) and match coefficients
            // MathNet.Numerics.LinearAlgebra.Vector psis = ComputePsiCoefficients(horizon);
            var psis = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(horizon + 1);

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

        public override MathNet.Numerics.LinearAlgebra.Vector<double> ComputeACF(int outToLag, bool normalize)
        {
            var acvf = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(outToLag + 1);

            acvf[0] = Sigma;
            for (var i = 0; i < outToLag; ++i)
                acvf[i + 1] = i < maxLag ? Parameters[2 + i] * acvf[0] : 0;

            if (normalize)
                acvf /= acvf[0];
            return acvf;
        }

        private double Autocovariance(int lag)
        {
            double tx;
            if (lag <= maxLag)
                if (lag != 0)
                    tx = Parameters[2 + lag - 1] * Sigma * Sigma;
                else
                    tx = Sigma * Sigma;
            else
                tx = 0;
            return tx;
        }

        /// <summary>
        ///  This function uses the Durbin-Levinson algorithm
        ///  to get one-step predictors and residuals.
        /// </summary>
        /// <param name="rs">double[] array of one-step predictive MSEs / sigma^2,
        //     based on sigma when this routine was called </param>
        /// <returns>double[] array of residuals = [ (x_i - \hat{x}_i)/\sqrt{rs_i} ]</returns>
        protected double[] ComputeSpecialResiduals(TimeSeries startData, out double[] rs, int forecastHorizon,
            out double[] forecasts)
        {
            // 1.  residuals are returned: retval[] =  [ (x_i - \hat{x}_i)/\sqrt{rs_{i-1}} ]
            // 2.  rs[...] are one-step predictive MSEs / sigma^2,
            //     based on sigma when this routine was called

            int i, n, nobs = startData.Count + forecastHorizon;
            var xhat = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(Math.Max(nobs + 1, 1));
            var localResiduals = new double[nobs];
            rs = new double[nobs + 1];

            var dl = new DurbinLevinsonPredictor(Mu, Autocovariance, nobs + 1);

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

        #region Other Mathematical Support Functions

        protected static Complex ComplexPower(Complex c, double pow)
        {
            var r = Math.Sqrt(Math.Pow(c.Magnitude, 2));
            var theta = c.Phase;
            var retval = new Complex(Math.Pow(r, pow), theta * pow);
            return retval;
        }

        #endregion

        #region IMLEEstimable Members

        public virtual MathNet.Numerics.LinearAlgebra.Vector<double> ParameterToCube(
            MathNet.Numerics.LinearAlgebra.Vector<double> param)
        {
            var cube = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(param.Count);

            cube[0] = Math.Exp(param[0] / muScale) / (1 + Math.Exp(param[0] * 1e-5)); // real to [0,1]
            cube[1] = param[1] / (1 + param[1]); // real+ to [0,1]

            for (var i = 0; i < maxLag; ++i)
                cube[i + 2] = param[i + 2] / 2 + 0.5; // [-1,1] to [0,1]

            return cube;
        }

        public virtual MathNet.Numerics.LinearAlgebra.Vector<double> CubeToParameter(
            MathNet.Numerics.LinearAlgebra.Vector<double> cube)
        {
            var param = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(2 + maxLag);
            param[0] = Math.Log(cube[0] / (1 - cube[0])) * muScale;
            param[1] = cube[1] / (1 - cube[1]);

            for (var i = 0; i < maxLag; ++i)
                param[2 + i] = cube[2 + i] * 2 - 1.0; // [0,1] to [-1,1]

            return param;
        }

        #endregion

        #region Real-Time Prediction Stuff

        [NonSerialized] private DurbinLevinsonPredictor realTimePredictor;

        public virtual void ResetRealTimePrediction()
        {
            realTimePredictor = new DurbinLevinsonPredictor(Mu, Autocovariance, 20000);
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
            throw new ApplicationException("Should not be using auxiliary info with an ACFModel");
        }

        // for registering one item at a time
        public virtual double Register(DateTime timeStamp, double value)
        {
            realTimePredictor.Register(value);
            return realTimePredictor.CurrentPredictor;
        }

        public virtual DistributionSummary GetCurrentPredictor(DateTime futureTime)
        {
            var ds = new DistributionSummary
                {Mean = realTimePredictor.CurrentPredictor, Variance = realTimePredictor.CurrentMSPE};
            return ds;
        }

        #endregion

        #region Auxiliary Functions

        public int NumAuxiliaryFunctions()
        {
            return 1;
        }

        public string AuxiliaryFunctionName(int index)
        {
            return "ARMA Approx.";
        }

        public string AuxiliaryFunctionHelp(int index)
        {
            return "Constructs ARMA model approximating the ACF model.";
        }

        public bool AuxiliaryFunction(int index, out object output)
        {
            var approx = new ARMAModel(0, maxLag); // we should be able to get close with an MA
            var hlds = new HaltonSequence(maxLag);

            var bestError = double.MaxValue;
            var bestMAPolynomial =
                MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(maxLag); //new Polynomial(maxLag);

            for (var i = 0; i < 200000; ++i)
            {
                var cube = hlds.GetNext(); // this is the MA part to try in the ARMA
                var curCube = approx.ParameterToCube(approx.Parameters); // el. 0=mu, el. 1=d, el. 2=sigma
                for (var j = 0; j < maxLag; ++j)
                    curCube[j + 3] = cube[j];
                approx.SetParameters(approx.CubeToParameter(curCube));

                // now compare autocorrelation function (don't care about mean or sigma)
                var acf = approx.ComputeACF(maxLag, true);
                double error = 0;
                for (var j = 0; j < maxLag; ++j)
                    error += Math.Abs(acf[j + 1] - Rho(j));
                if (error < bestError)
                {
                    bestError = error;
                    bestMAPolynomial = approx.GetMAPolynomial();
                }
            }

            approx.SetMAPolynomial(bestMAPolynomial);
            approx.Mu = Mu;
            output = approx;
            return true;
        }

        #endregion
    }
}
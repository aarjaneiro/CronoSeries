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
using CronoSeries.ABMath.Miscellaneous;
using CronoSeries.ABMath.ModelFramework.Data;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.ModelFramework.Models
{
    /// <summary>
    /// This is just an ARMA model with exogenous inputs.
    /// The model is
    ///    Phi(B) X_t = Theta(B) Z_t + Gamma U_{t-1}
    /// where Phi and Theta are standard autoregressive and moving average polynomials
    /// {U_t} is an exogenous time series.
    /// 
    /// Much of the analysis is the same, we just have extra coefficients for the exogenous inputs,
    /// and to compute likelihoods, we have to adjust the observations appropriately.
    /// 
    /// For now, we only allow a simple one-lag dependency on the exogenous series.
    /// Later I'll generalize this.
    /// </summary>
    [Serializable]
    public class ARMAXModel : ARMAModel
    {
        protected int numExogenous;
        public int NumExogenous
        { get { return numExogenous; } }

        [NonSerialized]
        protected TimeSeries[] exogenous;

        private int NumParameters()
        {
            return AROrder + MAOrder + 3 + numExogenous;
        }

        /// <summary>
        /// sets gamma for the model
        /// </summary>
        /// <param name="idx">index value, starting at 0</param>
        /// <param name="value">value to set</param>
        public void SetGamma(int idx, double value)
        {
            Parameters[NumParameters() - numExogenous + idx] = value;
        }

        /// <summary>
        /// returns the specified gamma value
        /// </summary>
        /// <param name="idx"></param>
        /// <returns></returns>
        public double GetGamma(int idx)
        {
            return Parameters[NumParameters() - numExogenous + idx];
        }

        public override Vector<double> Parameters
        {
            get // return ARMA param vector, with gamma coeff. appended to end
            {
                return base.Parameters; // see the set method: it ensures that this is the proper length
            }
            set
            {
                var v = Vector<double>.Build.Dense(NumParameters());
                for (int i = 0; i < value.Count ; ++i)
                    v[i] = value[i]; // copy what we can
                base.Parameters = v;
                for (int i = 0; i < numExogenous; ++i)
                    SetGamma(i, value[NumParameters() - numExogenous + i]);
            }
        }

        public override string GetParameterName(int index)
        {
            int sz = NumParameters();
            if (index >= sz - numExogenous)
                return string.Format("Gamma({0})", index - (sz - numExogenous) + 1);
            return base.GetParameterName(index);
        }

        /// <summary>
        /// basic constructor
        /// </summary>
        /// <param name="arOrder">autoregressive order</param>
        /// <param name="maOrder">moving average order</param>
        /// <param name="exOrder">number of exogenous inputs</param>
        public ARMAXModel(int arOrder, int maOrder, int exOrder) : 
            base(arOrder, maOrder)
        {
            numExogenous = exOrder;
            exogenous = new TimeSeries[numExogenous];
            LocalInitializeParameters();
        }

        /// <summary>
        /// constructor for heavy-tailed ARMAX model
        /// </summary>
        /// <param name="arOrder">autoregressive order</param>
        /// <param name="maOrder">moving average order</param>
        /// <param name="exOrder">number of exogenous inputs</param>
        /// <param name="tailDoF">degrees of freedom for t-distributed innovations</param>
        public ARMAXModel(int arOrder, int maOrder, int exOrder, int tailDoF) :
            base(arOrder, maOrder, tailDoF)
        {
            numExogenous = exOrder;
            exogenous = new TimeSeries[numExogenous];
            LocalInitializeParameters();
        }


        public override int NumInputs()
        {
            return 1+numExogenous;  // our second input is the time series of exogenous inputs
        }

        public override string GetInputName(int index)
        {
            if (index == 0)
                return base.GetInputName(0);
            if (index > numExogenous)
               throw new ArgumentException("Invalid index");
            return string.Format("Exogenous TS #{0}", index);
        }

        public override string Description
        {
            get
            {
                var sb = new StringBuilder(1024);
                sb.AppendFormat("ARMAX({0},{1},1) Model:{2}", AROrder, MAOrder, Environment.NewLine);
                sb.AppendLine();
                sb.AppendFormat("X(t)");
                for (int p = 0; p < AROrder; ++p)
                {
                    char sgn = ARCoeff(p) < 0 ? '+' : '-';
                    sb.AppendFormat(" {0}{1:0.000}X(t-{2})", sgn, Math.Abs(ARCoeff(p)), p + 1);
                }
                sb.AppendFormat(" = Z(t)");
                for (int q = 0; q < MAOrder; ++q)
                {
                    char sgn = MACoeff(q) >= 0 ? '+' : '-';
                    sb.AppendFormat(" {0}{1:0.000}Z(t-{2})", sgn, Math.Abs(MACoeff(q)), q + 1);
                }
                for (int i = 0; i < numExogenous; ++i)
                {
                    char sgn2 = GetGamma(i) > 0 ? '+' : '-';
                    sb.AppendFormat(" {0}{1:0.000}U_{2}(t-1)", sgn2, Math.Abs(GetGamma(i)), i+1);
                }
                sb.AppendLine();

                if (TailDegreesOfFreedom == 0)
                    sb.AppendFormat("Z(t) ~ N(0,{0:0.0000}^2) (iid){1}", Sigma, Environment.NewLine);
                else
                    sb.AppendFormat("Z(t) ~ {0:0.0000} x Students T({1} d.o.f.) (iid){2}", Sigma, TailDegreesOfFreedom, Environment.NewLine);
                sb.AppendFormat("Mean     = {0:0.0000}{1}", Mu, Environment.NewLine);
                sb.AppendFormat("FracDiff = {0:0.0000}", FracDiff);

                return sb.ToString();
            }
        }

        public override string GetShortDescription()
        {
            return string.Format("ARMAX ({0},{1})", AROrder, MAOrder);
        }

        protected new void LocalInitializeParameters()
        {
            Parameters = Vector<double>.Build.Dense(NumParameters()); // all coeffs are initially zero
            Mu = 0;
            Sigma = 1;
            FracDiff = 0;  // uses base ARMA properties to fill in appropriate components of vector

            ParameterStates = new ParameterState[Parameters.Count];
            ParameterStates[0] = ParameterState.Consequential; // mu follows
            ParameterStates[1] = ParameterState.Consequential; // sigma follows from the others
            ParameterStates[2] = ParameterState.Locked; // locked at 0 by default
            for (int i = 3; i < Parameters.Count; ++i)
                ParameterStates[i] = ParameterState.Free; // only AR and MA coefficients are free  
        }

        protected override void InitializeParameters()
        {
            LocalInitializeParameters();
        }

        private bool AllInputsValid()
        {
            var t1 = TheData as TimeSeries;
            if (t1 == null)
                return false;
            if (exogenous == null)
                throw new ApplicationException("Exogenous list is un-initialized.");
            bool sameCount = true;
            for (int i = 0; i < numExogenous; ++i)
            {
                if (exogenous[i] == null)
                    return false;
                sameCount &= exogenous[i].Count == t1.Count;
            }
            if (!sameCount)
                return false;
            return true;
        }

        public override bool SetInput(int socket, object item, StringBuilder failMsg)
        {
            if (socket > numExogenous)
                throw new ArgumentException("Invalid socket");
            if (exogenous == null)
                exogenous = new TimeSeries[numExogenous];

            bool retval = true;

            if (socket == 0)
               retval = base.SetInput(0, item, failMsg);
            if (socket >= 1)
               exogenous[socket-1] = item as TimeSeries;

            if (AllInputsValid())
                LogLikelihood(null, 0.0, true);

            return retval;
        }

        public TimeSeries GetExogenousTS(int idx)
        {
            return exogenous[idx];
        }

        /// <summary>
        /// performs adjustment to the original data based on exogenous time series,
        /// so that we can use the original likelihood function
        /// </summary>
        /// <returns></returns>
        protected TimeSeries ComputeAdjustedValues()
        {
            if (values == null)
                return null;
            if (exogenous == null)
                return values;
            for (int i=0 ; i<numExogenous ; ++i)
                if (exogenous[i] == null || exogenous[i].Count != values.Count)
                    return values;

            var adjustments = new TimeSeries();
            adjustments.Add(values.TimeStamp(0), 0, false);
            for (int t=1 ; t<values.Count ; ++t)
            {
                double tx = 0;
                for (int i=0 ; i<numExogenous ; ++i)
                    tx += exogenous[i][t - 1]*GetGamma(i);
                for (int i = 0; i < AROrder; ++i)
                    tx += t - i - 1 >= 0 ? ARCoeff(i)*adjustments[t - i - 1] : 0;
                adjustments.Add(values.TimeStamp(t), tx, false);
            }
            var adjusted = new TimeSeries();
            for (int t=0 ; t<values.Count ; ++t)
                adjusted.Add(adjustments.TimeStamp(t), values[t] - adjustments[t], false);
            return adjusted;
        }

        public override double LogLikelihood(Vector<double> parameter, double penaltyFactor, bool fillOutputs)
        {
            Vector<double> allLLs = null;
            Vector<double> pbak = Parameters; // save the current one

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

                var adjusted = ComputeAdjustedValues();

                double[] rs;
                double[] forecs;
                double[] resids = ComputeSpecialResiduals(adjusted, out rs, 1, out forecs);

                TimeSeries rts;
                TimeSeries unstdrts;

                if (fillOutputs)
                {
                    rts = new TimeSeries { Title = $"{values.Title}[ARMA Res]"};
                    unstdrts = new TimeSeries { Title = $"{values.Title}[ARMA Res]"};
                    oneStepPredictors = new TimeSeries { Title = $"{values.Title}[Predic]"};
                    oneStepPredStd = new TimeSeries { Title = $"{values.Title}[Pred. Stdev.]"};
                    oneStepPredictorsAtAvailability = new TimeSeries { Title = $"{values.Title}[Predic. AA]"};

                    for (int t = 0; t < values.Count; ++t)
                    {
                        rts.Add(values.TimeStamp(t), resids[t] / Sigma, false);
                        unstdrts.Add(values.TimeStamp(t), resids[t], false);
                        double stdev = Math.Sqrt(rs[t]);
                        double fx = values[t] - resids[t] * stdev;
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
                for (int t = 0; t < values.Count; ++t)
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
                Parameters = pbak;  // then restore original

            var llp = new LogLikelihoodPenalizer(allLLs);
            return llp.LogLikelihood - llp.Penalty * penaltyFactor;
        }

        protected override Vector<double> ComputeConsequentialParameters(Vector<double> parameter)
        {
            // fill in mean and sigma
            Vector<double> pbak = Parameters;
            Parameters = parameter;
            Vector<double> newParms;

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
                    var adjusted = ComputeAdjustedValues(); // account for exogenous inputs
                    res = ComputeSpecialResiduals(adjusted, out rs, 0, out forecs);

                    if (TailDegreesOfFreedom == 0) // i.e. if innovations are normal
                    {
                        // consequential sigma is just the standard deviation
                        double ss = 0;
                        for (int i = 0; i < res.Length; ++i)
                            ss += res[i] * res[i];
                        Sigma = Math.Sqrt(ss / res.Length);
                    }
                    else // for model with t-distribution innovations
                    {
                        var tdn = new StudentT(0, 1, TailDegreesOfFreedom);
                        var vres = Vector<double>.Build.Dense(res);
                        Sigma = tdn.MLEofSigma(vres);
                    }
                }

                newParms = Vector<double>.Build.DenseOfVector(Parameters);
            }
            else
            {
                // case 2: longitudinal data
                throw new ApplicationException("Longitudinal data not yet supported with ARMAX models");
            }
            Parameters = pbak;

            return newParms;
        }

        private double Logit(double realValue, double cubeMargin)
        {
            double retval = Math.Exp(realValue)/(1 + Math.Exp(realValue));
            retval = (retval - cubeMargin)/(1.0 - 2*cubeMargin);
            if (retval < 0)
                retval = 0;
            if (retval > 1)
                retval = 1;
            return retval;
        }

        private double InvLogit(double cubeValue, double cubeMargin)
        {
            double trimmed = cubeValue * (1.0-2*cubeMargin) + cubeMargin;  // keep it away from 0 and 1
            return Math.Log(trimmed/(1 - trimmed));
        }

        public override Vector<double> ParameterToCube(Vector<double> param)
        {
            int sz = NumParameters();
            var partCube = base.ParameterToCube(param);
            var fullCube = Vector<double>.Build.Dense(sz);
            for (int i = 0; i < partCube.Count; ++i)
                fullCube[i] = partCube[i];
            for (int i = 0; i < numExogenous; ++i )
                fullCube[sz - numExogenous + i] = Logit(param[sz - numExogenous + i], 0.00005);
            return fullCube;
        }

        public override Vector<double> CubeToParameter(Vector<double> cube)
        {
            int sz = NumParameters();
            var partial = base.CubeToParameter(cube);
            var full = Vector<double>.Build.Dense(sz);
            for (int i = 0; i < partial.Count; ++i)
                full[i] = partial[i];
            for (int i = 0; i < numExogenous; ++i )
                full[sz - numExogenous + i] = InvLogit(cube[sz - numExogenous + i], 0.00005);
            return full;
        }

        public override object SimulateData(object inputs, int randomSeed)
        {
            Console.WriteLine("ARMAX simulations use exogenous input = 0 for now.  To be fixed in the future ...");
            return base.SimulateData(inputs, randomSeed);
        }

        private int realTimeBufferSize = 100;
        private double[] realTimeAdjusted;
        private int realTimeT;
        private double realTimePredictor;
        private double realTimePredictiveVar;

        public override void ResetRealTimePrediction()
        {
            base.ResetRealTimePrediction();
            realTimeAdjusted = new double[realTimeBufferSize]; // these are the most recent values of adjusted
            realTimeT = 0;
        }

        public override double Register(DateTime timeStamp, double value)
        {
            throw new ApplicationException("ARMAX register requires auxiliary values.");
        }

        public override double Register(DateTime timeStamp, double value, double[] auxValues)
        {
            double oldTx = realTimeAdjusted[(realTimeT - 1 + realTimeBufferSize)%realTimeBufferSize];

            double tx = 0;
            for (int i=0 ; i<numExogenous ; ++i)
               tx += auxValues[i]*GetGamma(i);
            for (int i = 0; i < AROrder; ++i)
                tx += ARCoeff(i)*realTimeAdjusted[(realTimeT - i - 1 + realTimeBufferSize)%realTimeBufferSize];

            double armaPred = base.Register(timeStamp, value - oldTx);
            var ds = base.GetCurrentPredictor(timeStamp); 

            realTimeAdjusted[realTimeT] = tx;
            realTimeT = (realTimeT + 1)%realTimeBufferSize;

            realTimePredictor = armaPred + tx;
            realTimePredictiveVar = ds.Variance;
            return realTimePredictor;
        }

        public override DistributionSummary GetCurrentPredictor(DateTime futureTime)
        {
            var ds = new DistributionSummary {Mean = realTimePredictor, Variance = realTimePredictiveVar};
            // ds.FillGaussianQuantiles(0.01);
            return ds;
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket > numExogenous)
                throw new SocketException();
            return new List<Type> { typeof(TimeSeries) };
        }
    }
}

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
using System.Text;
using CronoSeries.TimeSeries.Data;
using CronoSeries.TimeSeries.Miscellaneous;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.TimeSeries.Models
{
    [Serializable]
    public abstract class Model : IConnectable
    {
        public enum ParameterState
        {
            Free,
            Locked,
            Consequential
        }

        private double currentPenalty;

        //[NonSerialized] protected object theData;
        [NonSerialized] public object theData;
        private IMLEEstimable thisAsMLEEstimable;
        private IMoMEstimable thisAsMoMEstimable;
        private string toolTipText;

        /// <summary>
        ///     Should return a description of the model, including current parameter values if desired.
        /// </summary>
        public abstract string Description { get; }

        public object Residuals { get; protected set; }

        // methods available for fitting model

        public object TheData
        {
            get => theData;
            set
            {
                if (CheckDataValidity(value, null))
                {
                    theData = value;
                    OnDataConnection();
                }
            }
        }

        public double GoodnessOfFit { get; protected set; }

        /// <summary>
        /// The parameters necessary for calculations pertaining to this model.
        /// </summary>
        public abstract Vector<double> Parameters { get; protected set; }
        public ParameterState[] ParameterStates { get; set; }

        public abstract int NumInputs();
        public abstract int NumOutputs();
        public abstract string GetInputName(int socket);
        public abstract string GetOutputName(int socket);

        public virtual List<Type> GetAllowedInputTypesFor(int socket)
        {
            return new List<Type>();
        }

        public virtual List<Type> GetOutputTypesFor(int socket)
        {
            return null;
        }

        public abstract bool InputIsFree(int socket);
        public abstract bool SetInput(int socket, object item, StringBuilder failMsg);
        public abstract object GetOutput(int socket);

        public string GetDescription()
        {
            return Description;
        }

        public virtual string GetShortDescription()
        {
            return Description;
        }

        //public Color GetBackgroundColor()
        //{
        //    return Color.LightBlue;
        //}

        //public Icon GetIcon()
        //{
        //    return null;
        //}

        public string ToolTipText
        {
            get => toolTipText;
            set => toolTipText = value;
        }

        public bool CanUseMLE()
        {
            return this as IMLEEstimable != null;
        }

        public bool CanUseMoM()
        {
            return this as IMoMEstimable != null;
        }

        public bool CanHandleNaNs()
        {
            return false;
        }

        /// <summary>
        ///     Returns the (possibly penalized) log-likelihood of the model with specified parameters and the
        ///     current object theData.  If parameter==null, it will use CURRENT parameters.
        ///     If fillOutputs is true, then the residuals and any other outputs will be filled in.
        /// </summary>
        /// <returns></returns>
        public abstract double LogLikelihood(Vector<double> parameter, double penaltyFactor, bool fillOutputs);

        // used to keep track of last evaluated log-likelihood or other G.o.F. measure

        /// <summary>
        ///     This function must fill in values of consequential parameters.
        ///     These parameters are determined by the current ParameterState[] settings in ParameterStates.
        ///     These can depend on the non-consequential parameters
        ///     and the data set.  The function should throw an exception if it is not possible.
        ///     The parameter vector with parameters filled in should be returned.
        /// </summary>
        protected abstract Vector<double> ComputeConsequentialParameters(Vector<double> parameter);

        /// <summary>
        ///     This function must simulate from the current model.
        /// </summary>
        /// <param name="dateTimes">
        ///     these may have different interpretations, but
        ///     for time series, is typically an IList of DateTime objects
        /// </param>
        /// <param name="randomSeed">random number seed</param>
        /// <returns></returns>
        public abstract Data.TimeSeries SimulateData(List<DateTime> dateTimes, int randomSeed);

        /// <summary>
        ///     This function generates forecasts (or fitted values) for the specified inputs,
        ///     based on the existing data object and current model parameters.
        ///     Results are returned in a form that depends on the model,
        ///     for example, as a TimeSeries of DistributionSummary objects
        /// </summary>
        /// <param name="otherData"></param>
        /// <param name="inputs">
        ///     may have different interpretations, for
        ///     time series, it is typically an IList of DateTime objects
        /// </param>
        /// <returns>a model-dependent object: for example, a time series of distribution summaries</returns>
        public abstract object BuildForecasts(object otherData, object inputs);

        /// <summary>
        ///     This function checks to see if the object can be cast into the appropriate form
        ///     for the model.
        /// </summary>
        /// <param name="data">data to be attached to the model</param>
        /// <param name="failMessage">if non-null, will be filled with descriptive error messages (if any)</param>
        /// <returns></returns>
        protected abstract bool CheckDataValidity(object data, StringBuilder failMessage);

        /// <summary>
        ///     This function is called immediately after a data object is connected to the model.
        ///     Any initial processing (e.g. determining dimension of parameter vector, etc.) should be
        ///     done here.
        /// </summary>
        protected abstract void OnDataConnection();

        /// <summary>
        ///     This function is called after OnDataConnection.  It can assume valid data is available,
        ///     and it must fill in valid default parameter values.
        ///     It typically also sets default parameter states, for purposes of estimation.
        /// </summary>
        protected abstract void InitializeParameters();


        /// This function samples from parameter space using a Halton sequence and picks 
        /// the model with best log-likelihood.
        /// Individual parameters are tagged as ParameterState.Locked, ParameterState.Free, or ParameterState.Consequential.
        /// Locked parameters are held at current values in optimization.
        /// Free parameters are optimized.
        /// Consequential parameters are computed as a function of other parameters and the data.
        /// <param name="numIterationsLDS">Number of parameters to consider per iteration</param>
        /// <param name="numIterationsOpt">Number of iterations corresponding to free parameters</param>
        /// <param name="consistencyPenalty">Penalty applied in LogLikelihood calculation</param>
        public virtual void FitByMLE(int numIterationsLDS, int numIterationsOpt,
            double consistencyPenalty,
            Optimizer.OptimizationCallback optCallback)
        {
            thisAsMLEEstimable = this as IMLEEstimable;
            if (thisAsMLEEstimable == null)
                throw new ApplicationException("MLE not supported for this model.");

            var optDimension = NumParametersOfType(ParameterState.Free);
            var numConsequential = NumParametersOfType(ParameterState.Consequential);
            var numIterations = numIterationsLDS + numIterationsOpt;

            var trialParameterList = new Vector<double>[numIterationsLDS];
            var trialCubeList = new Vector<double>[numIterationsLDS];

            var hsequence = new HaltonSequence(optDimension);

            if (optDimension == 0) // then all parameters are either locked or consequential
            {
                var tparms = Parameters;
                Parameters = ComputeConsequentialParameters(tparms);
            }
            else
            {
                thisAsMLEEstimable.CarryOutPreMLEComputations();

                for (var i = 0; i < numIterationsLDS; ++i)
                {
                    var smallCube = hsequence.GetNext();
                    var cube = CubeInsert(smallCube);
                    trialParameterList[i] = thisAsMLEEstimable.CubeToParameter(cube);
                    trialCubeList[i] = cube;
                }

                var logLikes = new double[numIterationsLDS];

                //const bool multiThreaded = false;
                //if (multiThreaded)
                //{
                //    Parallel.For(0, numIterations,
                //                 i =>
                //                 {
                //                     Vector tparms = trialParameterList[i];
                //                     if (numConsequential > 0)
                //                     {
                //                         tparms = ComputeConsequentialParameters(tparms);
                //                         lock (trialParameterList)
                //                             trialParameterList[i] = tparms;
                //                     }

                //                     double ll = LogLikelihood(tparms);
                //                     if (optCallback != null)
                //                         lock (logLikes)
                //                             optCallback(tparms, ll,
                //                                         (int)(i * 100 / numIterations), false);

                //                     lock (logLikes)
                //                         logLikes[i] = ll;
                //                 });
                //}

                for (var i = 0; i < numIterationsLDS; ++i)
                {
                    var tparms = trialParameterList[i];
                    if (numConsequential > 0)
                    {
                        tparms = ComputeConsequentialParameters(tparms);
                        trialParameterList[i] = tparms;
                    }

                    var ll = LogLikelihood(tparms, consistencyPenalty, false);
                    logLikes[i] = ll;

                    if (optCallback != null)
                        lock (logLikes)
                        {
                            optCallback(tparms, ll, i * 100 / numIterations, false);
                        }
                }

                // Step 1: Just take the best value.
                Array.Sort(logLikes, trialParameterList);
                Parameters = trialParameterList[numIterationsLDS - 1];

                // Step 2: Take some of the top values and use them to create a simplex, then optimize
                // further in natural parameter space with the Nelder Mead algorithm.
                // Here we optimize in cube space, reflecting the cube when necessary to make parameters valid.
                var simplex = new List<Vector<double>>();
                for (var i = 0; i <= optDimension; ++i)
                    simplex.Add(
                        FreeParameters(
                            thisAsMLEEstimable.ParameterToCube(trialParameterList[numIterationsLDS - 1 - i])));
                var nmOptimizer = new NelderMead {Callback = optCallback, StartIteration = numIterationsLDS};
                currentPenalty = consistencyPenalty;
                nmOptimizer.Minimize(NegativeLogLikelihood, simplex, numIterationsOpt);
                Parameters =
                    ComputeConsequentialParameters(
                        thisAsMLEEstimable.CubeToParameter(CubeFix(CubeInsert(nmOptimizer.ArgMin))));
            }

            LogLikelihood(null, 0.0, true);
        }

        /// <summary>
        ///     Fits model by method of moments.  This is the default method used of CanUseMLE is false.
        /// </summary>
        public virtual void FitByMoM()
        {
            thisAsMoMEstimable = this as IMoMEstimable;
            if (thisAsMoMEstimable == null)
                throw new NotImplementedException("No default implementation for Method-of-Moments model fitting.");

            thisAsMoMEstimable.FitByMethodOfMoments();
        }

        /// <summary>
        ///     This function makes sure that its vector argument really does contain
        ///     an element of the unit hypercube.  If not, it is mapped back to an element
        ///     on the hypercube (the mapping is continuous).
        /// </summary>
        /// <param name="cube"></param>
        /// <returns></returns>
        protected static Vector<double> CubeFix(Vector<double> cube)
        {
            var trialCube = Vector<double>.Build.DenseOfVector(cube);
            // fix cube if coordinates have strayed outside allowable values
            for (var i = 0; i < trialCube.Count; ++i)
                while (trialCube[i] < 0 || trialCube[i] > 1)
                {
                    if (trialCube[i] < 0.0)
                        trialCube[i] = -trialCube[i];
                    if (trialCube[i] > 1.0)
                        trialCube[i] = 2.0 - trialCube[i];
                }

            return trialCube;
        }

        /// <summary>
        ///     This function is a wrapper for another function, to be passed to a minimizer.
        /// </summary>
        /// <returns></returns>
        protected double NegativeLogLikelihood(Vector<double> partialCube)
        {
            var trialCube = CubeFix(CubeInsert(partialCube));
            return
                -LogLikelihood(ComputeConsequentialParameters(thisAsMLEEstimable.CubeToParameter(trialCube)),
                    currentPenalty, false);
        }

        public bool InputToOutputIsValid()
        {
            return true;
        }

        public abstract string GetParameterName(int index);
        public abstract string GetParameterDescription(int index);

        public bool SetParameters(Vector<double> v)
        {
            if (CheckParameterValidity(v))
            {
                Parameters = v;
                return true;
            }

            return false;
        }

        // Parameter states govern the way in which they are optimized.
        // Some parameters are free to be optimized, some depend on others (are "consequential"),
        // and some are "locked" at their current value.

        /// <summary>
        ///     Checks for validity of parameters.
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        protected abstract bool CheckParameterValidity(Vector<double> param);

        private int NumParametersOfType(ParameterState state)
        {
            var dimension = Parameters.Count;
            if (ParameterStates == null)
                switch (state)
                {
                    case ParameterState.Free:
                        return dimension;
                    case ParameterState.Locked:
                        return 0;
                    case ParameterState.Consequential:
                        return 0;
                }

            var n = 0;
            for (var i = 0; i < dimension; ++i)
                n += ParameterStates[i] == state ? 1 : 0;
            return n;
        }

        /// <summary>
        ///     copies the coordinates of the lower dimensional cube of free parameters
        ///     into the corresponding coordinates of the parameter cube
        /// </summary>
        /// <param name="lowDimCube"></param>
        /// <returns></returns>
        private Vector<double> CubeInsert(Vector<double> lowDimCube)
        {
            if (ParameterStates == null)
                return lowDimCube;

            var highDimCube = thisAsMLEEstimable.ParameterToCube(Parameters);

            var i1 = -1;
            for (var i0 = 0; i0 < lowDimCube.Count; ++i0)
            {
                ++i1;
                while (ParameterStates[i1] != ParameterState.Free)
                    ++i1;
                highDimCube[i1] = lowDimCube[i0];
            }

            return highDimCube;
        }

        /// <summary>
        ///     picks out the free parameters and bundles them into a (smaller) vector
        /// </summary>
        /// <param name="fullParameters"></param>
        /// <returns></returns>
        private Vector<double> FreeParameters(Vector<double> fullParameters)
        {
            if (ParameterStates == null)
                return fullParameters;
            var retval = Vector<double>.Build.Dense(NumParametersOfType(ParameterState.Free));
            for (int i = 0, j = 0; i < fullParameters.Count; ++i)
                if (ParameterStates[i] == ParameterState.Free)
                    retval[j++] = fullParameters[i];
            return retval;
        }
    }
}
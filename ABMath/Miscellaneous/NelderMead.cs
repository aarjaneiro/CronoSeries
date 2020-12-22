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
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.Miscellaneous
{
    public class NelderMead : Optimizer
    {
        private readonly double alpha = 1.0;
        private readonly double gamma = 2.0;
        private readonly double rho = 0.5;
        private readonly double sigma = 0.5;

        /// <summary>
        ///     minimizes function using the version of NM algorithm given on Wikipedia
        /// </summary>
        /// <param name="targetFunction"></param>
        /// <param name="initialValues"></param>
        public override void Minimize(TargetFunction targetFunction, List<Vector<double>> initialValues,
            int maxIterations)
        {
            var dimension = initialValues[0].Count;
            if (initialValues.Count != dimension + 1)
                throw new ArgumentException(
                    $"Initial value count must be {dimension + 1} so that a simplex can be defined.");

            var simplex = new Evaluation[dimension + 1];
            for (var i = 0; i <= dimension; ++i)
            {
                simplex[i].argument = initialValues[i];
                simplex[i].value = targetFunction(initialValues[i]);
                simplex[i].timeStamp = DateTime.Now;
            }

            Evaluations = new List<Evaluation>(maxIterations + 1);

            // now iterate
            for (var iter = 0; iter < maxIterations; ++iter)
            {
                Vector<double> reflecPoint;
                double reflectEval;

                // 1. sort by values
                Array.Sort(simplex);

                if (Evaluations.Count == 0 || simplex[0].timeStamp > Evaluations[Evaluations.Count - 1].timeStamp)
                    Evaluations.Add(simplex[0]);

                // 1.5.  notify callback if necessary
                if (Callback != null)
                {
                    var pc = 100 * (iter + StartIteration) / (maxIterations + StartIteration);
                    Callback(simplex[0].argument, simplex[0].value, pc, false);
                }

                // 2. compute center of mass of every point except the worst one
                var centerOfMass = Vector<double>.Build.Dense(dimension);
                for (var i = 0; i < dimension; ++i)
                    centerOfMass += simplex[i].argument;
                centerOfMass /= dimension;

                // 3. Get the reflected point, making sure that parameter is valid (i.e. evaluation is not NaN)
                var validAlpha = alpha;
                do
                {
                    reflecPoint = centerOfMass + validAlpha * (centerOfMass - simplex[dimension].argument);
                    reflectEval = targetFunction(reflecPoint);
                    if (double.IsNaN(reflectEval))
                        validAlpha *= 0.8;
                } while (double.IsNaN(reflectEval));

                if (reflectEval < simplex[dimension].value) // if it's better than the previous worst
                {
                    if (reflectEval > simplex[0].value)
                        // take the reflection by replacing the worst point
                    {
                        simplex[dimension] = new Evaluation(reflecPoint, reflectEval);
                    }
                    else
                    {
                        // try expanding
                        var expandPoint =
                            centerOfMass + gamma * (centerOfMass - simplex[dimension].argument);
                        var expandEval = targetFunction(expandPoint);

                        if (expandEval < reflectEval)
                            // take the expansion
                            simplex[dimension] = new Evaluation(expandPoint, expandEval);
                        else
                            simplex[dimension] = new Evaluation(reflecPoint, reflectEval);
                    }
                }
                else // we may contract
                {
                    var xc = simplex[dimension].argument +
                             rho * (centerOfMass - simplex[dimension].argument);
                    var xcEval = targetFunction(xc);
                    if (xcEval < simplex[dimension].value)
                        simplex[dimension] = new Evaluation(xc, xcEval);
                    else
                        for (var i = 1; i <= dimension; ++i)
                        {
                            simplex[i].argument = simplex[0].argument +
                                                  sigma * (simplex[i].argument - simplex[0].argument);
                            simplex[i].value = targetFunction(simplex[i].argument);
                        }
                }
            }

            Array.Sort(simplex);
            Minimum = simplex[0].value;
            ArgMin = simplex[0].argument;

            if (Callback != null)
                Callback(simplex[0].argument, simplex[0].value, 100, true);
        }
    }
}
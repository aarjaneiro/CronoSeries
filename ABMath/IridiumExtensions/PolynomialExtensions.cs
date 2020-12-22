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
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System.Numerics;

namespace ABMath.IridiumExtensions
{
    public static class PolynomialExtensions
    {
        private const double epsilon = 1e-8;
        //Vector stands for the coefficients of a polynomial

        public static List<Complex> Roots(this MathNet.Numerics.LinearAlgebra.Vector<double> p)
        {
            var allRoots = new List<Complex>();

            // build matrix whose char. polynomial is the target polynomial
            int sz = p.Count - 1;
            while (Math.Abs(p[sz])<epsilon && sz>0)
                --sz;
            if (sz > 0) // it has to be bigger than zero to have any roots at all
            {
                double highestCoeff = p[sz];
                var m = Matrix<double>.Build.Dense(sz, sz);
                for (int i = 0; i < sz - 1; ++i)
                    m[i, i + 1] = 1.0;
                for (int i = 0; i < sz; ++i)
                    m[sz - 1, i] = -p[i] / highestCoeff;

                Evd<double> ed = m.Evd();//new EigenvalueDecomposition(m);
                //ed.Solve(m);

                for (int i = 0; i < sz; ++i)
                {
                    var c = new Complex(ed.EigenValues[i].Real, ed.EigenValues[i].Imaginary);
                    allRoots.Add(c);
                }
            }

            return allRoots;
        }

        /// <summary>
        /// keeps only one of each pair of complex conjugates in the list by removing the other one
        /// </summary>
        /// <param name="roots"></param>
        public static void StripConjugates(List<Complex> roots)
        {
            for (int i=0 ; i<roots.Count ; ++i)
                if (Math.Abs(roots[i].Imaginary) > epsilon) // then we need to find the conjugate
                    for (int j=i+1 ; j<roots.Count ; ++j)
                        if ((Complex.Conjugate(roots[j]) - roots[i]).Magnitude < epsilon)
                        {
                            roots.RemoveAt(j);
                            j = roots.Count;
                        }
        }

        /// <summary>
        /// translates a cube into a polynomial with inverse-roots at least "barrier" inside the unit circle, and
        /// zero-order coefficient = 1.0
        /// </summary>
        /// <returns></returns>
        public static MathNet.Numerics.LinearAlgebra.Vector<double> MapFromCube(MathNet.Numerics.LinearAlgebra.Vector<double> originalCube, double barrier)
        {
            var invRoots = new List<Complex>();

            MathNet.Numerics.LinearAlgebra.Vector<double> cube = originalCube*2-1;
            
            for (int i=0 ; i<cube.Count ; )
            {
                if (i<cube.Count-1) // we can grab a pair
                {
                    double x1 = cube[i];
                    double y1 = cube[i + 1];
                    if (y1 > 0) // it's a conjugate pair
                    {
                        invRoots.Add(new Complex(x1, y1));
                        invRoots.Add(new Complex(x1, -y1));
                    }
                    else
                    {
                        invRoots.Add(new Complex(x1, 0));
                        invRoots.Add(new Complex(2*y1+1, 0));
                    }
                    i += 2;
                }
                else
                {
                    invRoots.Add(new Complex(cube[i], 0));
                    ++i;
                }
            }
            for (int i=0 ; i<invRoots.Count ; ++i)
            {
                // make sure norm is less than 1-epsilon, if not, invert
                double r = invRoots[i].Magnitude;
                double theta = invRoots[i].Phase;
                if (r > 1-barrier)
                {
                    r = 1.0/(r + 2*barrier);
                    invRoots[i] = new Complex(r, theta);
                }
            }

            // now rebuild polynomial from roots
            var retval = new Complex[invRoots.Count + 1];
            retval[0] = 1.0;
            for (int i=0 ; i<invRoots.Count ; ++i)
                for (int j = invRoots.Count ; j > 0; --j)
                    retval[j] -= invRoots[i]*retval[j - 1];

            var p = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(invRoots.Count + 1);//new Polynomial(invRoots.Count);
            for (int i = 0; i <= invRoots.Count; ++i)
                if (Math.Abs(retval[i].Imaginary) > epsilon)
                    p[i] = retval[i].Real;
                //throw new ApplicationException("Unmapped polynomial has complex coefficients.");
                else
                    p[i] = retval[i].Real;
            return p;
        }

        /// <summary>
        /// map a polynomial with roots at least "barrier" OUTSIDE the unit circle to a cube vector of dimension equal to the order of the polynomial,
        /// assuming the zero-order coefficient is equal to 1.0
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static MathNet.Numerics.LinearAlgebra.Vector<double> MapToCube(this MathNet.Numerics.LinearAlgebra.Vector<double> p, double barrier)
        {
            var allRoots = p.Roots();
            StripConjugates(allRoots);
            int order = p.Count - 1;
            var cube = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(order);
            var complexRoots = new List<Complex>();
            var realRoots = new List<double>();

            int j = 0;

            for (int i = 0; i < allRoots.Count; ++i)
                if (Math.Abs(allRoots[i].Imaginary) > epsilon)
                    complexRoots.Add(1.0/allRoots[i]);
                else
                    realRoots.Add(1.0/allRoots[i].Real);

            for (int i = 0 ; i < complexRoots.Count; ++i)
            {
                cube[j] = complexRoots[i].Real;
                cube[j + 1] = Math.Abs(complexRoots[i].Imaginary);
                j += 2;
            }

            for (int i = 0; i < realRoots.Count;  )
            {
                if (realRoots.Count - i > 1)
                {
                    cube[j] = realRoots[i];
                    cube[j + 1] = (realRoots[i + 1] - 1.0)/2.0;  // between -1 and 0
                    i += 2;
                    j += 2;
                }
                else
                {
                    cube[j] = realRoots[i];
                    ++i;
                    ++j;
                }
            }

            return (cube + 1.0)*0.5;
        }
    }
}
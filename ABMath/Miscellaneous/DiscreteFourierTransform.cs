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
using System.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.Miscellaneous
{
    public class DiscreteFourierTransform
    {
        /// <summary>
        ///     computes DFT of the time series data, if length is a power of 2, using FFT,
        ///     otherwise by successively dividing n by (2,3,5 or 7)
        /// </summary>
        /// <param name="data">vector of values to be DFT'd</param>
        /// <returns>2 column matrix: 1st col = real part, 2nd col = imag part</returns>
        /// <param name="offset">typically 0</param>
        /// <param name="skip">typically 1</param>
        public Matrix<double> DFT(Matrix<double> data, int offset, int skip)
        {
            var N = data.RowCount / skip;
            if (data.RowCount % skip != 0)
                throw new ApplicationException("Invalid skip value used in DFT.");
            var retval = Matrix<double>.Build.Dense(N, 2);

            var divisor = 1;
            if (N > 4)
            {
                if (N % 7 == 0)
                    divisor = 7;
                if (N % 5 == 0)
                    divisor = 5;
                if (N % 3 == 0)
                    divisor = 3;
                if (N % 2 == 0)
                    divisor = 2;
            }

            if (divisor > 1)
            {
                // splice together the pieces
                var pieces = new Matrix<double>[divisor];
                for (var i = 0; i < divisor; ++i)
                    pieces[i] = DFT(data, offset + skip * i, divisor * skip);

                var subsize = N / divisor;
                for (var j = 0; j < divisor; ++j)
                {
                    Complex factor = 1.0;
                    var tx = -2 * Math.PI / N * j;
                    var factorMultiplier = new Complex(Math.Cos(tx), Math.Sin(tx));
                    var piece = pieces[j];
                    for (var k = 0; k < N; ++k)
                    {
                        var adjustReal = piece[k % subsize, 0];
                        var adjustImag = piece[k % subsize, 1];
                        retval[k, 0] += adjustReal * factor.Real - adjustImag * factor.Imaginary;
                        retval[k, 1] += adjustImag * factor.Real + adjustReal * factor.Imaginary;
                        factor *= factorMultiplier;
                    }
                }

                return retval;
            }

            // simple DFT
            Complex expminusiwn = 1.0;
            var theta = 2 * Math.PI / N;
            var expminus2pionn = new Complex(Math.Cos(theta), -Math.Sin(theta));

            for (var n = 0; n < N; ++n)
            {
                var row = offset + skip * n;
                var xval = data.ColumnCount == 2
                    ? new Complex(data[row, 0], data[row, 1])
                    : data[row, 0];
                Complex multi = 1.0;
                for (var k = 0; k < N; ++k)
                {
                    retval[k, 0] += xval.Real * multi.Real - xval.Imaginary * multi.Imaginary;
                    retval[k, 1] += xval.Real * multi.Imaginary + xval.Imaginary * multi.Real;
                    multi *= expminusiwn;
                }

                expminusiwn *= expminus2pionn;
            }

            return retval;
        }

        /// <summary>
        ///     computes inverse DFT, also using fast factoring algorithm (=FFT if length is a power of 2)
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public Matrix<double> IDFT(Matrix<double> data)
        {
            var n = data.RowCount;
            var shuffled = Matrix<double>.Build.Dense(n, data.ColumnCount);
            for (var j = 0; j < data.ColumnCount; ++j)
                shuffled[0, j] = data[0, j] / n;
            for (var i = 1; i < n; ++i)
            for (var j = 0; j < data.ColumnCount; ++j)
                shuffled[i, j] = data[n - i, j] / n;
            return DFT(shuffled, 0, 1);
        }
    }
}
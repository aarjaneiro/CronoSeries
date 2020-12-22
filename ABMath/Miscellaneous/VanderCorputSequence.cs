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

using System.Data;

namespace CronoSeries.ABMath.Miscellaneous
{
    /// <summary>
    /// This class generates a one-dimensional low-discrepancy sequence.
    /// See discussion at http://en.wikipedia.org/wiki/Constructions_of_low-discrepancy_sequences
    /// </summary>
    public class VanderCorputSequence
    {
        private long _base;
        private int[] _representation;     // Here we have _currentn in base _base representation, backwards (0,base,base^2,...)
        private int _lastnonzerodigit;     // Here we have the index of the last non-zero digit in the representation (-1 to start)
        private const int _maxdigits = 200;
        private long _currentn;

        public VanderCorputSequence(long numbase)
        {
            _base = numbase;
            _representation = new int[_maxdigits];
            Reset();
        }

        private void Reset()
        {
            _currentn = 0;
            for (int i = 0; i < _maxdigits; ++i)
                _representation[i] = 0;
            _lastnonzerodigit = -1;
        }

        public double GetNext()
        {
            int i, curindex = 0;

            // First increment _currentn and its base _base representation

            ++_currentn;

            ++_representation[curindex];
            if (curindex > _lastnonzerodigit)
                _lastnonzerodigit = curindex;

            while (_representation[curindex]==_base)
            {
                _representation[curindex] = 0;
                ++curindex;
                if (curindex >= _maxdigits)
                    throw new DataException("Internal overflow - not enough digits - try changing _maxdigits.");

                ++_representation[curindex];
                if (curindex > _lastnonzerodigit)
                    _lastnonzerodigit = curindex;
            }

            // Then compute the van der Corput number

            double curpower = 1.0/_base, retval = 0.0;
            for (i = 0; i <= _lastnonzerodigit; ++i)
            {
                retval += _representation[i]*curpower;
                curpower /= _base;
            }

            return retval;
        }
    }
}
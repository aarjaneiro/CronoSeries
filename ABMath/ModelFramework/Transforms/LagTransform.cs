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

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    [Serializable]
    public class LagTransform : FilterTransform
    {
        public LagTransform()
        {
            arCoeffs = new[] {1.0};
            maCoeffs = new[] {0, 1.0};
        }

        public override string GetDescription()
        {
            return "One-Step Lag";
        }

        public override string GetShortDescription()
        {
            return "Lag";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}
    }
}
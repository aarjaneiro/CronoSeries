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
using System.Text;

namespace CronoSeries.ABMath.ModelFramework.Data
{
    /// <summary>
    /// Connectable units have inputs and outputs.
    /// When all inputs are assigned, outputs are automatically recomputed.
    /// They also have methods that control how they are displayed.
    /// This interface is required in order to display something in a DirectedGraph.
    /// </summary>
    public interface IConnectable
    {
        // inputs and outputs
        int NumInputs();
        int NumOutputs();
        string GetInputName(int socket);
        string GetOutputName(int socket);
        List<Type> GetAllowedInputTypesFor(int socket);
        List<Type> GetOutputTypesFor(int socket);
        bool InputIsFree(int socket);
        bool SetInput(int socket, object item, StringBuilder failMessage);   
        object GetOutput(int socket);

        // visual display properties
        string GetDescription();
        string GetShortDescription();
        //Color GetBackgroundColor();
        //Icon GetIcon();

        string ToolTipText { get; set; }
    }
}
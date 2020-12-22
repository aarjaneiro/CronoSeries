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
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using CronoSeries.ABMath.ModelFramework.Data;
using CronoSeries.ABMath.ModelFramework.Models;

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    /// <summary>
    ///     A TimeSeriesTransformation takes one or more univariate or multivariate inputs and
    ///     creates a single univariate or multivariate output.
    /// </summary>
    [Serializable]
    public abstract class TimeSeriesTransformation : IConnectable
    {
        [NonSerialized]
        protected string multivariateOutputPrefix; // if bundling, this name will be assigned to the multivariate output

        [NonSerialized] protected List<TimeSeries> outputs; // should be filled in by Recompute()

        [NonSerialized] protected Dictionary<int, object> socketedInputs;

        protected TimeSeriesTransformation()
        {
            socketedInputs = new Dictionary<int, object>(5);
        }

        protected virtual bool ShouldBundleOutputs => true;

        [Category("Result")]
        [Description("This is true if the transformation generates a valid output.")]
        public bool IsValid { get; protected set; }


        /// <summary>
        ///     returns input type for the specified socket, or
        ///     an overall input type if socket==-1
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        protected InputType GetInputType(int socket)
        {
            if (socket != -1)
            {
                var o = GetInput(socket);
                if (o == null)
                    return InputType.Invalid;
                if (o as TimeSeries != null)
                    return InputType.UnivariateTS;
                if (o as MVTimeSeries != null)
                    return InputType.MultivariateTS;
                if (o as Longitudinal != null)
                    return InputType.Longitudinal;
                return InputType.Invalid;
            }

            var retval = GetInputType(0);
            for (var s = 1; s < NumInputs(); ++s)
            {
                var t = GetInputType(s);
                if (t != retval)
                    retval = InputType.Mixed;
            }

            return retval;
        }

        protected void CheckInputsReady()
        {
            if (socketedInputs == null)
                socketedInputs = new Dictionary<int, object>(5);
        }

        public bool AllInputsValid()
        {
            var valid = true;
            if (NumInputs() == 0)
                return true;
            CheckInputsReady();
            for (var s = 0; s < NumInputs(); ++s)
                valid &= socketedInputs.ContainsKey(s);
            return valid;
        }

        protected object GetInputWithMVFlag(int socket, out bool isMV)
        {
            CheckInputsReady();
            isMV = false;
            if (!socketedInputs.ContainsKey(socket))
                return null;
            if (socketedInputs[socket] as MVTimeSeries != null)
                isMV = true;
            return socketedInputs[socket];
        }

        protected object GetInput(int socket)
        {
            CheckInputsReady();
            bool isMV;
            return GetInputWithMVFlag(socket, out isMV);
        }

        protected int GetTotalInputDimension()
        {
            var dimension = 0;
            for (var i = 0; i < NumInputs(); ++i)
                if (socketedInputs.ContainsKey(i))
                {
                    var ts = socketedInputs[i] as TimeSeries;
                    var mvts = socketedInputs[i] as MVTimeSeries;

                    if (ts != null)
                        ++dimension;
                    else if (mvts != null)
                        dimension += mvts.Dimension;
                }

            return dimension;
        }

        protected List<TimeSeries> GetInputBundle()
        {
            var bundle = new List<TimeSeries>();
            for (var i = 0; i < NumInputs(); ++i)
                if (socketedInputs.ContainsKey(i))
                {
                    var ts = socketedInputs[i] as TimeSeries;
                    var mvts = socketedInputs[i] as MVTimeSeries;

                    if (ts != null)
                    {
                        bundle.Add(ts);
                    }
                    else if (mvts != null)
                    {
                        var mList = mvts.ExtractList();
                        foreach (var sts in mList)
                            bundle.Add(sts);
                    }
                }

            return bundle;
        }

        protected string SimpleDescription(object o)
        {
            var sb = new StringBuilder(128);

            var ts = o as TimeSeries;
            var mvts = o as MVTimeSeries;
            var lts = o as Longitudinal;

            if (ts != null)
                sb.Append(ts.GetDescription());
            if (mvts != null)
                sb.AppendFormat("Multivariate, Length {0}", mvts.Count);
            if (lts != null)
                sb.AppendFormat("Longitudinal ({0}), MaxLength={1}", lts.Count, lts.MaxCount);

            return sb.ToString();
        }

        public abstract void Recompute();

        public virtual string GetLastComputationInfo()
        {
            var sb = new StringBuilder(1024);

            if (socketedInputs != null)
                for (var i = 0; i < NumInputs(); ++i)
                    if (socketedInputs.ContainsKey(i))
                        sb.AppendFormat("Input {0}: {1}{2}", i + 1, SimpleDescription(socketedInputs[i]),
                            Environment.NewLine);

            if (IsValid)
                for (var i = 0; i < NumOutputs(); ++i)
                    sb.AppendFormat("Output {0}: {1}{2}", i + 1, SimpleDescription(GetOutput(i)), Environment.NewLine);


            return sb.ToString();
        }

        #region Nested type: InputType

        protected enum InputType
        {
            Invalid,
            UnivariateTS,
            MultivariateTS,
            Longitudinal,
            Mixed
        }

        #endregion

        #region IConnectable Members

        public abstract int NumInputs();
        public abstract int NumOutputs();

        [Browsable(false)] public string ToolTipText { get; set; }

        public virtual List<Type> GetOutputTypesFor(int socket)
        {
            return null;
        }

        public bool InputIsFree(int socket)
        {
            CheckInputsReady();
            return !socketedInputs.ContainsKey(socket);
        }

        public virtual bool SetInput(int socket, object item, StringBuilder failMsg)
        {
            CheckInputsReady();
            if (socket >= NumInputs())
                throw new ArgumentException("Bad socket.");

            var ts = item as TimeSeries;
            var mts = item as MVTimeSeries;
            var lts = item as Longitudinal;
            var mi = item as Model;
            if (ts == null && mts == null && lts == null && mi == null)
                return
                    false; // failure when tsInput item is not of class TimeSeries or MVTimeSeries or Model of some kind

            socketedInputs[socket] = item;

            if (AllInputsValid())
                Recompute();
            return true;
        }

        public abstract string GetInputName(int socket);
        public abstract string GetOutputName(int socket);

        public virtual List<Type> GetAllowedInputTypesFor(int socket)
        {
            return new List<Type>();
        }

        //public virtual Color GetBackgroundColor()
        //{
        //    return Color.Honeydew;
        //}

        public abstract string GetDescription();

        public abstract string GetShortDescription();
        //public abstract Icon GetIcon();

        public virtual object GetOutput(int socket)
        {
            if (outputs == null)
                return null;
            if (outputs.Count == 0)
                return null;
            if (ShouldBundleOutputs && outputs.Count > 1) // then bundle them together
            {
                if (socket != 0)
                    throw new SocketException();
                if (outputs == null)
                    return null;
                if (outputs.Count == 1)
                    return outputs[0];
                var mvts = new MVTimeSeries(outputs, false);
                if (multivariateOutputPrefix != null)
                    mvts.Title = multivariateOutputPrefix;
                return mvts;
            }

            // otherwise just return them as they are
            if (outputs == null)
                return null;
            if (socket < NumOutputs())
                return outputs[socket];
            throw new SocketException();
        }

        #endregion
    }
}
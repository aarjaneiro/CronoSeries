using System;
using System.IO;
using System.Linq;
using System.Text;
using CronoSeries.ABMath.Forms.IridiumExtensions;
using CronoSeries.ABMath.ModelFramework.Models;
using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.Miscellaneous
{
    [Serializable]
    public class BStatistic
    {
        private int order;

        public BStatistic(int order)
        {
            this.order = order;
        }

        public Vector<double> pValues { get; protected set; }
        public int[] argMaxes { get; protected set; }

        public static Matrix<double> BuildAMatrix(int order)
        {
            var A = Matrix<double>.Build.Dense(order, order);
            for (var j = 0; j < order; ++j)
            for (var k = 0; k < order; ++k)
                A[j, k] = k >= j ? -1 : 1;
            return A;
        }

        private DnInfo InitializeTables()
        {
            DnInfo theTest;

            switch (order)
            {
                case 12:
                    theTest = new DnInfo {aMatrix = BuildAMatrix(12), order = 12, xmlTable = TablesResource.quants12};
                    break;
                case 24:
                    theTest = new DnInfo {aMatrix = BuildAMatrix(24), order = 24, xmlTable = TablesResource.quants24};
                    break;
                case 16:
                    theTest = new DnInfo {aMatrix = BuildAMatrix(16), order = 16, xmlTable = TablesResource.quants16};
                    break;
                default:
                    throw new ApplicationException("Unsupported order for Custom test.");
            }

            // load tables for each one
            var reader = new StringReader(theTest.xmlTable);
            theTest.dnApprox = new DistributionApproximation();
            theTest.dnApprox.LoadFromXmlArray(reader);
            reader.Close();

            return theTest;
        }

        /// <summary>
        ///     tests the specified TS for IIDness
        ///     H0: IIDness => sample autocorrelations are approx. iid normal with means 0, variances 1/n
        /// </summary>
        /// <param name="testSeries"></param>
        public void ComputeFrom(Vector<double> acf, int count)
        {
            var scale = Math.Sqrt(count);

            var pvals = Vector<double>.Build.Dense(1);
            argMaxes = new int[1];

            var test = InitializeTables();

            // first compute the statistic
            var order = test.order;
            var stacked = Vector<double>.Build.Dense(order);
            for (var i = 0; i < order; ++i)
                stacked[i] = acf[i + 1] * scale;
            var mstat = test.aMatrix.MultiplyBy(stacked);
            var minVal = mstat.Min();
            var maxVal = mstat.Max();
            var extreme = Math.Max(maxVal, -minVal);

            var argMax = -1;
            for (var i = 0; i < mstat.Count; ++i)
                if (extreme == Math.Abs(mstat[i]))
                    argMax = i;

            // then compute the p-value
            var pval1 = 1.0 - test.dnApprox.CDF.InterpolatedValue(extreme);

            test.pValue = pval1;
            test.argMax = argMax;

            pvals[0] = pval1;
            argMaxes[0] = argMax;

            pValues = pvals;
        }

        public override string ToString()
        {
            var sb = new StringBuilder(256);
            sb.AppendLine(
                string.Format("B({0:0}): p-value = {1:0.000}, ArgMax = {2:0}", order, pValues[0], argMaxes[0]));
            return sb.ToString();
        }

        private class DnInfo
        {
            public Matrix<double> aMatrix;
            public int argMax;
            public DistributionApproximation dnApprox;
            public int order;
            public double pValue;
            public string xmlTable;
        }
    }
}
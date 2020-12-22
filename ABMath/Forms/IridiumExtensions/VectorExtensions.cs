using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.Forms.IridiumExtensions
{
    public static class VectorExtensions
    {
        public static double Mean(this Vector<double> v)
        {
            double tx = 0;
            var numMissing = 0;
            for (var i = 0; i < v.Count; ++i)
                if (!double.IsNaN(v[i]))
                    tx += v[i];
                else
                    ++numMissing;
            var count = v.Count - numMissing;
            if (count > 0)
                return tx / count;
            return double.NaN;
        }

        public static double Variance(this Vector<double> v)
        {
            double tx = 0;
            var numMissing = 0;
            for (var i = 0; i < v.Count; ++i)
                if (!double.IsNaN(v[i]))
                    tx += v[i] * v[i];
                else
                    ++numMissing;
            var count = v.Count - numMissing;
            if (count == 0)
                return double.NaN;
            var ess = tx / count;
            var es = v.Mean();
            return ess - es * es;
        }

        public static double Kurtosis(this Vector<double> v)
        {
            double tx = 0;
            var numMissing = 0;
            var es = v.Mean();
            for (var i = 0; i < v.Count; ++i)
                if (!double.IsNaN(v[i]))
                {
                    var ty = v[i] - es;
                    tx += ty * ty * ty * ty;
                }
                else
                {
                    ++numMissing;
                }

            var count = v.Count - numMissing;
            if (count == 0)
                return double.NaN;
            var c4m = tx / count;
            var c2m = v.Variance();
            return c4m / (c2m * c2m);
        }

        public static double MaxDrawDown(this Vector<double> cumulative)
        {
            // now find draw-down in cumulative
            var max = double.MinValue;
            var maxDrawDown = double.MinValue;
            for (var t = 0; t < cumulative.Count; ++t)
            {
                if (cumulative[t] > max)
                    max = cumulative[t];
                var curDrawDown = max - cumulative[t];
                if (curDrawDown > maxDrawDown)
                    maxDrawDown = curDrawDown;
            }

            return maxDrawDown;
        }

        public static Vector<double> Integrate(this Vector<double> original)
        {
            var integrated = Vector<double>.Build.Dense(original.Count);
            if (integrated.Count == 0)
                return integrated;

            integrated[0] = original[0];
            for (var t = 1; t < original.Count; ++t)
                integrated[t] = integrated[t - 1] + original[t];
            return integrated;
        }
    }
}
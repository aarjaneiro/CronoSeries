using MathNet.Numerics.LinearAlgebra;

namespace CronoSeries.ABMath.Forms.IridiumExtensions
{
    public static class VectorExtensions
    {
        public static double Mean(this Vector<double> v)
        {
            double tx = 0;
            int numMissing = 0;
            for (int i = 0; i < v.Count; ++i)
                if (!double.IsNaN(v[i]))
                    tx += v[i];
                else
                    ++numMissing;
            int count = v.Count - numMissing;
            if (count > 0)
                return tx/count;
            return double.NaN;
        }

        public static double Variance(this Vector<double> v)
        {
            double tx = 0;
            int numMissing = 0;
            for (int i = 0; i < v.Count; ++i)
                if (!double.IsNaN(v[i]))
                    tx += v[i]*v[i];
                else
                    ++numMissing;
            int count = v.Count - numMissing;
            if (count == 0)
                return double.NaN;
            double ess = tx/count;
            double es = v.Mean();
            return (ess - es*es);
        }

        public static double Kurtosis(this Vector<double> v)
        {
            double tx = 0;
            int numMissing = 0;
            double es = v.Mean();
            for (int i = 0; i < v.Count; ++i)
                if (!double.IsNaN(v[i]))
                {
                    double ty = v[i] - es;
                    tx += ty*ty*ty*ty;
                }
                else
                    ++numMissing;
            int count = v.Count - numMissing;
            if (count == 0)
                return double.NaN;
            double c4m = tx / count;
            double c2m = v.Variance();
            return (c4m/(c2m*c2m));         
        }

        public static double MaxDrawDown(this Vector<double> cumulative)
        {
            // now find draw-down in cumulative
            double max = double.MinValue;
            double maxDrawDown = double.MinValue;
            for (int t = 0; t < cumulative.Count; ++t)
            {
                if (cumulative[t] > max)
                    max = cumulative[t];
                double curDrawDown = max - cumulative[t];
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
            for (int t = 1; t < original.Count; ++t)
                integrated[t] = integrated[t - 1] + original[t];
            return integrated;
        }
    }
}

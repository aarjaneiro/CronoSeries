using System;

namespace CronoSeries.TimeSeries.Models
{
    public interface IRealTimePredictable
    {
        void ResetRealTimePrediction();
        void Register(Data.TimeSeries series);
        double Register(DateTime timeStamp, double value);
        double Register(DateTime timeStamp, double value, double[] auxiliaryValues);
        DistributionSummary GetCurrentPredictor(DateTime futureTime);
    }
}
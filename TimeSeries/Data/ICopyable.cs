namespace CronoSeries.TimeSeries.Data
{
    public interface ICopyable
    {
        string CreateFullString(int detailLevel);
        void ParseFromFullString(string s);
    }
}
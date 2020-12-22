namespace CronoSeries.ABMath.Data
{
    public interface ICopyable
    {
        string CreateFullString(int detailLevel);
        void ParseFromFullString(string s);
    }
}
namespace CronoSeries.ABMath.ModelFramework.Data
{
    public interface ICopyable
    {
        string CreateFullString(int detailLevel);
        void ParseFromFullString(string s);
    }
}
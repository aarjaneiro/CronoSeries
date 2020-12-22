namespace CronoSeries.TimeSeries
{
    public interface IExtraFunctionality
    {
        int NumAuxiliaryFunctions();
        string AuxiliaryFunctionName(int index);
        string AuxiliaryFunctionHelp(int index);
    }
}
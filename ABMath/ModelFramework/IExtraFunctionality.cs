namespace CronoSeries.ABMath.ModelFramework
{
    public interface IExtraFunctionality
    {
        int NumAuxiliaryFunctions();
        string AuxiliaryFunctionName(int index);
        string AuxiliaryFunctionHelp(int index);
    }
}

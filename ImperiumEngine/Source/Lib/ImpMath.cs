namespace ImperiumEngine.Source.Lib;

public static class ImpMath
{
    public static bool Bool_Rand()
    {
        Random random = new Random();
        return random.Next(2) == 1;
    }
    public static double Dbl_RandRange(double min, double max)
    {
        Random random = new Random();
        return random.NextDouble() * (max - min) + min;
    }
}
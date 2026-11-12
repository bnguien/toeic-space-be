namespace ToeicSpace.BuildingBlocks.Pricing;

// Compiled into both Domain projects: one rounding rule, no cross-service dependency.
public static class VndDiscount
{
    public static decimal Calculate(decimal original, bool percentage, decimal value, decimal? maximum)
    {
        var discount = percentage ? decimal.Round(original * value / 100m, 0, MidpointRounding.AwayFromZero) : value;
        return Math.Min(original, Math.Min(discount, maximum ?? discount));
    }
}

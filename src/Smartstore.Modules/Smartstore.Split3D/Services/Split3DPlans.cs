namespace Smartstore.Split3D.Services;

/// <summary>
/// License plans. The names match the values stored by the original License Studio,
/// so imported and newly issued records stay consistent.
/// </summary>
public static class Split3DPlans
{
    public const string OneYear = "1 năm";
    public const string SixMonths = "6 tháng";
    public const string ThreeMonths = "3 tháng";
    public const string Lifetime = "Vĩnh viễn";
    public const string Custom = "Tùy chỉnh";

    public const int MaxDays = 36500;

    public static readonly string[] All = [OneYear, SixMonths, ThreeMonths, Lifetime, Custom];

    /// <summary>
    /// Gets the fixed number of days of a plan. <c>null</c> for <see cref="Lifetime"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="plan"/> is <see cref="Custom"/> or unknown.</exception>
    public static int? GetFixedDays(string plan) => plan switch
    {
        OneYear => 365,
        SixMonths => 180,
        ThreeMonths => 90,
        Lifetime => null,
        _ => throw new ArgumentException($"Plan '{plan}' has no fixed duration.", nameof(plan))
    };

    /// <summary>
    /// Derives the plan name from a validity period, like the original tool did for legacy rows.
    /// </summary>
    public static string FromDays(int? days) => days switch
    {
        null => Lifetime,
        365 => OneYear,
        180 => SixMonths,
        90 => ThreeMonths,
        _ => Custom
    };
}

namespace CreditWorks.Core.Models;

/// <summary>Icon files shipped with the SPA (frontend/creditworks-ui/public/icons).</summary>
public static class CategoryIcons
{
    /// <summary>Gets the list of all known category icons.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        "light.svg", "medium.svg", "heavy.svg", "car.svg", "truck.svg", "van.svg"
    };

    /// <summary>Determines whether the specified icon name is known.</summary>
    /// <param name="name">The icon name to check.</param>
    /// <returns><c>true</c> if the icon is known; otherwise, <c>false</c>.</returns>
    public static bool IsKnown(string? name) => name is not null && All.Contains(name);
}
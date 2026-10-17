using System.Text.Json;

namespace petergraves.ViewModels;

public sealed record CookiePolicy(DateOnly Updated, string Scope, CookiePolicyItem[] Items)
{
    public static CookiePolicy Current { get; } = Load();

    private static CookiePolicy Load()
    {
        using var stream = typeof(CookiePolicy).Assembly
            .GetManifestResourceStream("Data/cookie-policy.json")
            ?? throw new InvalidOperationException("The cookie policy inventory was not found.");
        return JsonSerializer.Deserialize<CookiePolicy>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The cookie policy inventory is empty.");
    }
}

public sealed record CookiePolicyItem(
    string Name, string Storage, string Category, string Provider,
    string Purpose, string Duration, string Source, int? MaxAgeDays);

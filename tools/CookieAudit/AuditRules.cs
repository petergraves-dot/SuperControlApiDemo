namespace CookieAudit;

public sealed record Inventory(string Scope, InventoryItem[] Items, string[]? ScanPaths = null);
public sealed record InventoryItem(string Name, string Storage, string Category, string Provider, int? MaxAgeDays);
public sealed record Observation(string Scenario, string Page, string Storage, string Name, string Domain,
    double? Expires, bool? Secure, bool? HttpOnly, string? SameSite);
public sealed record Finding(string Scenario, string Page, string Message);

public static class AuditRules
{
    public static InventoryItem? Match(Observation item, Inventory inventory) => inventory.Items.FirstOrDefault(rule =>
        rule.Storage == item.Storage &&
        (rule.Name.EndsWith('*') ? item.Name.StartsWith(rule.Name[..^1], StringComparison.Ordinal) : item.Name == rule.Name) &&
        item.Domain.TrimStart('.').Equals(rule.Provider, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<Finding> Check(Observation item, Inventory inventory, DateTimeOffset now)
    {
        var rule = Match(item, inventory);
        if (rule is null)
        {
            yield return new(item.Scenario, item.Page, $"Unreviewed {item.Storage}: {item.Name} on {item.Domain}.");
            yield break;
        }

        if (rule.Category == "Analytics" && item.Scenario != "accepted")
            yield return new(item.Scenario, item.Page, $"Analytics storage exists without consent: {item.Name}.");

        if (rule.MaxAgeDays is { } days && item.Expires is > 0 &&
            item.Expires > now.AddDays(days).ToUnixTimeSeconds() + 300)
            yield return new(item.Scenario, item.Page, $"{item.Name} exceeds its documented maximum lifetime of {days} days.");
    }

    public static bool IsAnalyticsRequest(Uri uri) =>
        uri.Host.Equals("googletagmanager.com", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.EndsWith(".googletagmanager.com", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.Equals("google-analytics.com", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.EndsWith(".google-analytics.com", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.Equals("analytics.google.com", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.EndsWith(".analytics.google.com", StringComparison.OrdinalIgnoreCase);
}

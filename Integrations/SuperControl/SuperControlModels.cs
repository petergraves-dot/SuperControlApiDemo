using System.Text.Json.Serialization;

namespace petergraves.Integrations.SuperControl;

public sealed class AccountsIndexResponse
{
    [JsonPropertyName("accounts")]
    public List<SuperControlAccount> Accounts { get; init; } = [];
}

public sealed class SuperControlAccount
{
    [JsonPropertyName("accountId")]
    public int AccountId { get; init; }

    [JsonPropertyName("companyName")]
    public string CompanyName { get; init; } = string.Empty;

    [JsonPropertyName("contentIndexUrl")]
    public string ContentIndexUrl { get; init; } = string.Empty;

    [JsonPropertyName("configurationIndexUrl")]
    public string ConfigurationIndexUrl { get; init; } = string.Empty;

    [JsonPropertyName("pricesIndexUrl")]
    public string PricesIndexUrl { get; init; } = string.Empty;

    [JsonPropertyName("availabilityIndexUrl")]
    public string AvailabilityIndexUrl { get; init; } = string.Empty;
}

public sealed class ContentIndexResponse
{
    [JsonPropertyName("accountId")]
    public int AccountId { get; init; }

    [JsonPropertyName("properties")]
    public List<SuperControlPropertyIndexEntry> Properties { get; init; } = [];
}

public sealed class ConfigurationIndexResponse
{
    [JsonPropertyName("accountId")]
    public int AccountId { get; init; }

    [JsonPropertyName("properties")]
    public List<PropertyConfigurationIndexEntry> Properties { get; init; } = [];
}

public sealed class PropertyConfigurationIndexEntry
{
    [JsonPropertyName("propertyId")]
    public int PropertyId { get; init; }

    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset LastUpdated { get; init; }

    [JsonPropertyName("configurationContentUrl")]
    public string ConfigurationContentUrl { get; init; } = string.Empty;
}

public sealed class PropertyConfigurationListingResponse
{
    [JsonPropertyName("propertyId")]
    public int PropertyId { get; init; }

    [JsonPropertyName("configuration")]
    public PropertyConfigurationData? Configuration { get; init; }
}

public sealed class PropertyConfigurationData
{
    [JsonPropertyName("acceptedPaymentForms")]
    public AcceptedPaymentForms? AcceptedPaymentForms { get; init; }

    [JsonPropertyName("cancellationPolicy")]
    public CancellationPolicyData? CancellationPolicy { get; init; }

    [JsonPropertyName("checkInTime")]
    public string? CheckInTime { get; init; }

    [JsonPropertyName("checkOutTime")]
    public string? CheckOutTime { get; init; }

    [JsonPropertyName("childrenAllowed")]
    public bool? ChildrenAllowed { get; init; }

    [JsonPropertyName("maximumOccupancy")]
    public MaximumOccupancyData? MaximumOccupancy { get; init; }

    [JsonPropertyName("merchantName")]
    public string? MerchantName { get; init; }

    [JsonPropertyName("petsAllowed")]
    public bool? PetsAllowed { get; init; }

    [JsonPropertyName("smokingAllowed")]
    public bool? SmokingAllowed { get; init; }

    [JsonPropertyName("allowBookings")]
    public bool? AllowBookings { get; init; }

    [JsonPropertyName("allowEnquiries")]
    public bool? AllowEnquiries { get; init; }
}

public sealed class AcceptedPaymentForms
{
    [JsonPropertyName("cardPaymentForms")]
    public List<CardPaymentForm> CardPaymentForms { get; init; } = [];
}

public sealed class CardPaymentForm
{
    [JsonPropertyName("cardCode")]
    public string? CardCode { get; init; }

    [JsonPropertyName("cardType")]
    public string? CardType { get; init; }
}

public sealed class CancellationPolicyData
{
    [JsonPropertyName("policy")]
    public string? Policy { get; init; }
}

public sealed class MaximumOccupancyData
{
    [JsonPropertyName("adults")]
    public int? Adults { get; init; }

    [JsonPropertyName("guests")]
    public int? Guests { get; init; }

    [JsonPropertyName("children")]
    public int? Children { get; init; }
}

public sealed class SuperControlPropertyIndexEntry
{
    [JsonPropertyName("propertyId")]
    public int PropertyId { get; init; }

    [JsonPropertyName("active")]
    public bool Active { get; init; }

    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset LastUpdated { get; init; }

    [JsonPropertyName("listingUrl")]
    public string ListingUrl { get; init; } = string.Empty;
}

public sealed class PropertyListingResponse
{
    [JsonPropertyName("propertyId")]
    public int PropertyId { get; init; }

    [JsonPropertyName("active")]
    public bool? Active { get; init; }

    [JsonPropertyName("adContent")]
    public PropertyAdContent? AdContent { get; init; }

    [JsonPropertyName("location")]
    public PropertyLocation? Location { get; init; }

    [JsonPropertyName("images")]
    public List<PropertyImage> Images { get; init; } = [];

    [JsonPropertyName("property")]
    public PropertyDetails? Property { get; init; }
}

public sealed class PropertyAdContent
{
    [JsonPropertyName("propertyName")]
    public string? PropertyName { get; init; }

    [JsonPropertyName("subcaption")]
    public string? Subcaption { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("accommodationsSummary")]
    public string? AccommodationsSummary { get; init; }

    [JsonPropertyName("location")]
    public string? LocationText { get; init; }
}

public sealed class PropertyLocation
{
    [JsonPropertyName("address")]
    public PropertyAddress? Address { get; init; }
}

public sealed class PropertyAddress
{
    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }
}

public sealed class PropertyImage
{
    [JsonPropertyName("photoId")]
    public string? PhotoId { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

public sealed class PropertyDetails
{
    [JsonPropertyName("propertyType")]
    public string? PropertyType { get; init; }

    [JsonPropertyName("amenities")]
    public List<string> Amenities { get; init; } = [];
}

public sealed class AvailabilityIndexResponse
{
    [JsonPropertyName("accountId")]
    public int AccountId { get; init; }

    [JsonPropertyName("properties")]
    public List<AvailabilityIndexEntry> Properties { get; init; } = [];
}

public sealed class AvailabilityIndexEntry
{
    [JsonPropertyName("propertyId")]
    public int PropertyId { get; init; }

    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset LastUpdated { get; init; }

    [JsonPropertyName("listingUrl")]
    public string ListingUrl { get; init; } = string.Empty;
}

public sealed class PricesIndexResponse
{
    [JsonPropertyName("accountId")]
    public int AccountId { get; init; }

    [JsonPropertyName("properties")]
    public List<PricesIndexEntry> Properties { get; init; } = [];
}

public sealed class PricesIndexEntry
{
    [JsonPropertyName("propertyId")]
    public int PropertyId { get; init; }

    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset LastUpdated { get; init; }

    [JsonPropertyName("listingUrl")]
    public string ListingUrl { get; init; } = string.Empty;
}

public sealed class PricesListingResponse
{
    [JsonPropertyName("propertyId")]
    public int PropertyId { get; init; }

    [JsonPropertyName("prices")]
    public PricesData? Prices { get; init; }
}

public sealed class PricesData
{
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("priceLos")]
    public List<string> PriceLos { get; init; } = [];
}

public sealed class AvailabilityListingResponse
{
    [JsonPropertyName("propertyId")]
    public int PropertyId { get; init; }

    [JsonPropertyName("propertyAvailability")]
    public PropertyAvailabilityData? PropertyAvailability { get; init; }
}

public sealed class PropertyAvailabilityData
{
    [JsonPropertyName("startDate")]
    public DateTimeOffset StartDate { get; init; }

    [JsonPropertyName("endDate")]
    public DateTimeOffset EndDate { get; init; }

    [JsonPropertyName("availability")]
    public string? Availability { get; init; }

    [JsonPropertyName("minStay")]
    public string? MinStay { get; init; }
}

using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using petergraves.Integrations.SuperControl;
using petergraves.ViewModels.SuperControlDataExportDemo;

namespace petergraves.Features.SuperControlDataExportDemo;

public sealed class DataExportDemoViewModelFactory : IDataExportDemoViewModelFactory
{
    private static readonly HashSet<string> PiiXmlElementNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "FirstName",
        "LastName",
        "Address1",
        "Address2",
        "Town",
        "Postcode",
        "County",
        "TelMain",
        "TelAlt",
        "TelMobile",
        "Email",
        "GuestId",
        "ClientRef",
        "address",
        "town",
        "postcode"
    };

    private readonly ISuperControlClient _client;
    private readonly SuperControlOptions _options;

    public DataExportDemoViewModelFactory(
        ISuperControlClient client,
        IOptions<SuperControlOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<DataExportBookingsResponseViewModel> BuildBookingsAsync(
        DataExportBookingsRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new DataExportBookingsResponseViewModel
            {
                Request = request,
                Error = "SuperControl__ApiKey is not configured."
            };
        }

        var queryString = BuildBookingsQueryString(request);
        if (queryString is null)
        {
            return new DataExportBookingsResponseViewModel { Request = request };
        }

        var apiResponse = await _client.GetDataExportBookingsAsync(queryString, cancellationToken);
        var body = apiResponse.Body.Trim();
        var anonymizedBody = AnonymizeXmlPii(body);

        if (IsApiErrorResponse(body))
        {
            return new DataExportBookingsResponseViewModel
            {
                Request = request,
                Loaded = true,
                Error = ExtractStringMessage(body),
                RawXml = anonymizedBody
            };
        }

        try
        {
            var parsed = DeserializeXml<DataExportBookingsResponse>(body);
            return new DataExportBookingsResponseViewModel
            {
                Request = request,
                Loaded = true,
                CurrentPage = parsed.CurrentPage,
                TotalPages = parsed.TotalPages,
                Bookings = parsed.Bookings.Select(MapBooking).ToList(),
                RawXml = FormatXml(anonymizedBody)
            };
        }
        catch (Exception ex)
        {
            return new DataExportBookingsResponseViewModel
            {
                Request = request,
                Loaded = true,
                Error = $"Failed to parse API response: {ex.Message}",
                RawXml = anonymizedBody
            };
        }
    }

    public async Task<DataExportPropertiesResponseViewModel> BuildPropertiesAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new DataExportPropertiesResponseViewModel
            {
                Error = "SuperControl__ApiKey is not configured."
            };
        }

        var apiResponse = await _client.GetDataExportPropertiesAsync(cancellationToken);
        var body = apiResponse.Body.Trim();
        var anonymizedBody = AnonymizeXmlPii(body);

        if (IsApiErrorResponse(body))
        {
            return new DataExportPropertiesResponseViewModel
            {
                Loaded = true,
                Error = ExtractStringMessage(body),
                RawXml = anonymizedBody
            };
        }

        try
        {
            var parsed = DeserializeXml<DataExportPropertiesResponse>(body);
            return new DataExportPropertiesResponseViewModel
            {
                Loaded = true,
                Properties = parsed.Properties.Select(p => new DataExportPropertyViewModel
                {
                    SupercontrolId = p.SupercontrolId,
                    PropertyName = p.PropertyName,
                    Arrive = p.Arrive,
                    Depart = p.Depart,
                    Address = AnonymizeFreeText(p.Address),
                    Town = AnonymizeFreeText(p.Town),
                    Postcode = AnonymizePostcode(p.Postcode),
                    Country = p.Country,
                    Longitude = p.Longitude,
                    Latitude = p.Latitude
                }).ToList(),
                RawXml = FormatXml(anonymizedBody)
            };
        }
        catch (Exception ex)
        {
            return new DataExportPropertiesResponseViewModel
            {
                Loaded = true,
                Error = $"Failed to parse API response: {ex.Message}",
                RawXml = anonymizedBody
            };
        }
    }

    private static string? BuildBookingsQueryString(DataExportBookingsRequestViewModel request)
    {
        var parts = new List<string>();

        switch (request.SearchMode)
        {
            case "single":
                if (request.BookingId.HasValue)
                    parts.Add($"BookingId={request.BookingId}");
                else if (request.OwnerBookingId.HasValue)
                    parts.Add($"OwnerBookingId={request.OwnerBookingId}");
                else if (!string.IsNullOrWhiteSpace(request.OwnerRef))
                    parts.Add($"OwnerRef={Uri.EscapeDataString(request.OwnerRef.Trim())}");
                else
                    return null;
                break;

            case "dateRange":
                if (!request.StartDate.HasValue || !request.EndDate.HasValue)
                    return null;
                parts.Add($"StartDate={request.StartDate:yyyy-MM-dd}");
                parts.Add($"EndDate={request.EndDate:yyyy-MM-dd}");
                break;

            case "lastUpdate":
                if (!request.LastUpdate.HasValue)
                    return null;
                parts.Add($"LastUpdate={request.LastUpdate:yyyy-MM-dd}");
                break;

            default:
                return null;
        }

        if (!string.IsNullOrWhiteSpace(request.BookingStatus))
            parts.Add($"BookingStatus={request.BookingStatus}");

        var limit = Math.Clamp(request.Limit, 1, 1000);
        parts.Add($"Limit={limit}");

        if (request.Page > 1)
            parts.Add($"Page={request.Page}");

        return string.Join("&", parts);
    }

    private static DataExportBookingViewModel MapBooking(DataExportBooking b)
    {
        var guestName = b.Guest is null
            ? string.Empty
            : AnonymizePersonName(b.Guest.FirstName, b.Guest.LastName);

        return new DataExportBookingViewModel
        {
            BookingId = b.BookingId,
            BookingDate = b.BookingDate,
            Status = b.Status,
            Type = b.Type,
            Currency = b.Currency,
            ClientRef = AnonymizeIdentifier(b.ClientRef),
            GuestName = guestName,
            GuestEmail = AnonymizeEmail(b.Guest?.Email),
            GuestCountry = b.Guest?.Country ?? string.Empty,
            Properties = b.Properties.Select(p => new DataExportBookingPropertyViewModel
            {
                PropertyId = p.PropertyId,
                Start = p.Start,
                End = p.End,
                Status = p.Status,
                Adults = p.Adults,
                Children = p.Childrens,
                Infants = p.Infants,
                Total = p.Total
            }).ToList()
        };
    }

    private static bool IsApiErrorResponse(string body) =>
        body.StartsWith("<string>", StringComparison.OrdinalIgnoreCase);

    private static string ExtractStringMessage(string body)
    {
        var start = body.IndexOf('>') + 1;
        var end = body.LastIndexOf('<');
        return start > 0 && end > start ? body[start..end] : body;
    }

    private static T DeserializeXml<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(xml);
        return (T)serializer.Deserialize(reader)!;
    }

    private static string AnonymizeXmlPii(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return xml;
        }

        try
        {
            var document = XDocument.Parse(xml);
            foreach (var element in document
                         .Descendants()
                         .Where(element => PiiXmlElementNames.Contains(element.Name.LocalName)))
            {
                var name = element.Name.LocalName;
                element.Value = AnonymizeXmlValue(name, element.Value);
            }

            return document.ToString(SaveOptions.None);
        }
        catch
        {
            var anonymized = Regex.Replace(
                xml,
                @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
                match => AnonymizeEmail(match.Value),
                RegexOptions.IgnoreCase);

            anonymized = RedactXmlTagValueWithRegex(anonymized, "FirstName", AnonymizeFreeText);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "LastName", AnonymizeFreeText);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "Address1", AnonymizeFreeText);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "Address2", AnonymizeFreeText);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "Town", AnonymizeFreeText);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "Postcode", AnonymizePostcode);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "County", AnonymizeFreeText);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "TelMain", AnonymizePhone);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "TelAlt", AnonymizePhone);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "TelMobile", AnonymizePhone);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "GuestId", AnonymizeIdentifier);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "ClientRef", AnonymizeIdentifier);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "address", AnonymizeFreeText);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "town", AnonymizeFreeText);
            anonymized = RedactXmlTagValueWithRegex(anonymized, "postcode", AnonymizePostcode);

            return anonymized;
        }
    }

    private static string RedactXmlTagValueWithRegex(string xml, string tagName, Func<string, string> redactor)
    {
        var pattern = $"(<{tagName}\\b[^>]*>)([\\s\\S]*?)(</{tagName}>)";
        return Regex.Replace(
            xml,
            pattern,
            match => $"{match.Groups[1].Value}{redactor(match.Groups[2].Value)}{match.Groups[3].Value}",
            RegexOptions.IgnoreCase);
    }

    private static string AnonymizeXmlValue(string elementName, string value)
    {
        return elementName.ToLowerInvariant() switch
        {
            "email" => AnonymizeEmail(value),
            "firstname" => AnonymizeFreeText(value),
            "lastname" => AnonymizeFreeText(value),
            "address1" => AnonymizeFreeText(value),
            "address2" => AnonymizeFreeText(value),
            "town" => AnonymizeFreeText(value),
            "postcode" => AnonymizePostcode(value),
            "county" => AnonymizeFreeText(value),
            "telmain" => AnonymizePhone(value),
            "telalt" => AnonymizePhone(value),
            "telmobile" => AnonymizePhone(value),
            "guestid" => AnonymizeIdentifier(value),
            "clientref" => AnonymizeIdentifier(value),
            "address" => AnonymizeFreeText(value),
            _ => AnonymizeFreeText(value)
        };
    }

    private static string AnonymizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@');
        if (at <= 0 || at == trimmed.Length - 1)
        {
            return "***";
        }

        var local = trimmed[..at];
        var domain = trimmed[(at + 1)..];

        var maskedLocal = local.Length <= 1 ? "*" : $"{local[0]}***";
        var lastDot = domain.LastIndexOf('.');

        if (lastDot > 0 && lastDot < domain.Length - 1)
        {
            var domainName = domain[..lastDot];
            var tld = domain[lastDot..];
            var maskedDomainName = domainName.Length <= 1 ? "*" : $"{domainName[0]}***";
            return $"{maskedLocal}@{maskedDomainName}{tld}";
        }

        var maskedDomain = domain.Length <= 1 ? "*" : $"{domain[0]}***";
        return $"{maskedLocal}@{maskedDomain}";
    }

    private static string AnonymizePersonName(string? firstName, string? lastName)
    {
        var maskedFirst = AnonymizeFreeText(firstName);
        var maskedLast = AnonymizeFreeText(lastName);
        return $"{maskedFirst} {maskedLast}".Trim();
    }

    private static string AnonymizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 2)
        {
            return "**";
        }

        return $"{trimmed[0]}***{trimmed[^1]}";
    }

    private static string AnonymizePostcode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var compact = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (compact.Length <= 3)
        {
            return "***";
        }

        return $"{compact[..2]}***{compact[^1]}";
    }

    private static string AnonymizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var suffix = trimmed.Length >= 2 ? trimmed[^2..] : "**";
        return $"***{suffix}";
    }

    private static string AnonymizeFreeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 1 ? "*" : $"{trimmed[0]}***";
    }

    private static string FormatXml(string xml)
    {
        try
        {
            return XDocument.Parse(xml).ToString(SaveOptions.None);
        }
        catch
        {
            return xml;
        }
    }
}

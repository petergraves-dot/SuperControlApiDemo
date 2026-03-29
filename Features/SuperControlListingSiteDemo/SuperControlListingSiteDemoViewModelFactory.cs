using Microsoft.Extensions.Options;
using petergraves.Integrations.SuperControl;
using petergraves.ViewModels.SuperControlListingSiteDemo;

namespace petergraves.Features.SuperControlListingSiteDemo;

public sealed class SuperControlListingSiteDemoViewModelFactory : ISuperControlListingSiteDemoViewModelFactory
{
    private readonly ISuperControlListingSiteService _listingSiteService;
    private readonly SuperControlOptions _options;

    public SuperControlListingSiteDemoViewModelFactory(
        ISuperControlListingSiteService listingSiteService,
        IOptions<SuperControlOptions> options)
    {
        _listingSiteService = listingSiteService;
        _options = options.Value;
    }

    public async Task<SuperControlListingSiteDemoResponseViewModel> BuildAsync(
        SuperControlListingSiteDemoRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = request with { Guests = Math.Clamp(request.Guests, 1, 30) };

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new SuperControlListingSiteDemoResponseViewModel
            {
                Request = normalizedRequest,
                Error = "SuperControl__ApiKey is not configured."
            };
        }

        if (_options.AccountId is not int accountId)
        {
            return new SuperControlListingSiteDemoResponseViewModel
            {
                Request = normalizedRequest,
                Error = "SuperControl__AccountId is not configured."
            };
        }

        var snapshot = await _listingSiteService.BuildSnapshotAsync(
            accountId,
            normalizedRequest.Where,
            normalizedRequest.Guests,
            normalizedRequest.CheckIn,
            normalizedRequest.CheckOut,
            cancellationToken);

        var error = snapshot.Errors.Count > 0
            ? string.Join(" ", snapshot.Errors)
            : null;

        return new SuperControlListingSiteDemoResponseViewModel
        {
            Request = normalizedRequest,
            Loaded = true,
            AccountId = accountId,
            Error = error,
            Stats = new SuperControlListingSiteDemoStatsViewModel
            {
                TotalActiveProperties = snapshot.TotalActiveProperties,
                ReturnedProperties = snapshot.ReturnedProperties,
                CacheHits = snapshot.CacheHits,
                CacheMisses = snapshot.CacheMisses,
                StaleFallbackHits = snapshot.StaleFallbackHits
            },
            Properties = snapshot.Properties.Select(property => new SuperControlListingSiteDemoPropertyViewModel
            {
                PropertyId = property.PropertyId,
                Name = property.Name,
                SubCaption = property.SubCaption,
                DescriptionHtml = property.DescriptionHtml,
                Location = property.Location,
                HeroImageUrl = property.HeroImageUrl,
                PropertyType = property.PropertyType,
                Amenities = property.Amenities,
                FromPrice = property.FromPrice,
                SelectedStayPrice = property.SelectedStayPrice,
                SelectedStayNights = property.SelectedStayNights,
                Currency = property.Currency,
                IsAvailableForSelectedDates = property.IsAvailableForSelectedDates,
                NextKnownAvailableDateUtc = property.NextKnownAvailableDateUtc,
                LastUpdatedUtc = property.LastUpdatedUtc
            }).ToList()
        };
    }
}

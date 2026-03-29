# SuperControl Listing Site Demo Tutorial

This guide explains how the listing site demo in this project is built, and how to reproduce the same pattern in your own ASP.NET Core app.

## What you are building

Two routes power the experience:

- `/supercontrol-listing-site-demo` (search + listing cards)
- `/supercontrol-listing-site-demo/property/{propertyId}` (property detail)

The runtime flow is:

1. Pull account indexes from SuperControl.
2. Resolve each property's `listingUrl`.
3. Hydrate cards and detail views with listing + prices + availability (+ configuration on the detail page).
4. Cache payloads using index `lastUpdated` for deterministic invalidation.

## Prerequisites

- .NET 10 SDK
- A SuperControl API token
- A SuperControl account ID
- (Optional) SuperControl calendar embed key for the detail page widget

## Environment configuration

Configure settings in `appsettings.json`:

```json
"SuperControl": {
  "ApiKey": "your-supercontrol-sc-token",
  "AccountId": 22263,
  "BaseUrl": "https://api.supercontrol.co.uk/v3/",
  "CalendarKey": "your-supercontrol-calendar-key",
  "DefaultPropertyId": 671777
}
```

Environment variables can still override these values in production (for example `SuperControl__ApiKey`).

For the listing and property routes, both `ApiKey` and `AccountId` must be configured. `CalendarKey` is optional and only required to render the embedded calendar widget.

## Service wiring (DI)

`Program.cs` registers:

- `AddOptions<SuperControlOptions>().Bind(...)`
- `AddMemoryCache()`
- `AddSingleton<ISuperControlResponseCache, SuperControlResponseCache>()`
- `AddHttpClient<ISuperControlClient, SuperControlClient>(...)`
- `AddScoped<ISuperControlListingSiteService, SuperControlListingSiteService>()`

`SuperControlClient` adds the `SC-TOKEN` auth header to each request.

## Endpoints used

The listing page calls these indexes in parallel:

1. `GET /properties/contentindex/{accountId}`
2. `GET /properties/pricesindex/{accountId}`
3. `GET /properties/availabilityindex/{accountId}`

Then, for each property, it follows index URLs:

- Listing payload: `listingUrl`
- Prices payload: `listingUrl` from prices index
- Availability payload: `listingUrl` from availability index

The detail page also calls:

- `GET /properties/configurationindex/{accountId}`
- Configuration payload: `configurationContentUrl`

## Core implementation files

- `Integrations/SuperControl/SuperControlOptions.cs`
- `Integrations/SuperControl/SuperControlClient.cs`
- `Integrations/SuperControl/SuperControlModels.cs`
- `Integrations/SuperControl/SuperControlListingSiteService.cs`
- `Controllers/SuperControlListingSiteDemoController.cs`
- `Features/SuperControlListingSiteDemo/SuperControlListingSiteDemoViewModelFactory.cs`
- `Views/SuperControlListingSiteDemo/Index.cshtml`
- `Controllers/SuperControlPropertyController.cs`
- `Features/SuperControlProperty/SuperControlPropertyViewModelFactory.cs`
- `Views/SuperControlProperty/Index.cshtml`

## Listing page behavior

`SuperControlListingSiteDemoController` binds search params from query string into `SuperControlListingSiteDemoRequestViewModel`:

- `where`
- `checkIn`
- `checkOut`
- `guests`

`SuperControlListingSiteDemoViewModelFactory.BuildAsync` validates API config and clamps guests to `1..30`, then calls:

```csharp
_listingSiteService.BuildSnapshotAsync(
  accountId,
  normalizedRequest.Where,
  normalizedRequest.Guests,
  normalizedRequest.CheckIn,
  normalizedRequest.CheckOut,
  cancellationToken)
```

`BuildSnapshotAsync` in `SuperControlListingSiteService`:

1. Loads 3 indexes in parallel.
2. Filters to active properties.
3. Loads listing payload for candidates (delta cache aware).
4. Applies search text matching (name/subcaption/description/location).
5. Enriches with prices and availability.
6. Computes:
   - from price (minimum LOS value)
   - selected-stay price for chosen dates
   - availability flag for selected dates
   - next known available date from the availability string.
7. Sorts and returns a `SuperControlListingSiteSnapshot`.

## Property detail page behavior

`SuperControlPropertyController` receives `{propertyId:int}` from route values and query params via `SuperControlPropertyRequestViewModel`, then delegates to `SuperControlPropertyViewModelFactory`, which calls:

```csharp
_listingSiteService.GetPropertyDetailAsync(accountId, PropertyId, Guests, CheckIn, CheckOut, cancellationToken)
```

Detail hydration includes:

- listing content (hero/gallery, description, amenities)
- prices (currency, from price, sample rates)
- availability (selected range status + next available date)
- configuration (rules, occupancy, payment forms, cancellation policy)

The detail view also renders the SuperControl calendar widget using `SuperControl__CalendarKey`.

## Delta cache strategy

`GetDeltaCachedPropertyPayloadAsync<T>` uses a two-layer cache:

- in-memory (`IMemoryCache`) for fast repeat reads within the current app instance
- disk (`.supercontrol-cache/{payloadType}`) for durable local fallback between requests/restarts

For each payload type (`listing`, `prices`, `availability`, `propertyconfiguration`):

1. Build cache key from payload type + property id + index `lastUpdated` + payload URL.
2. Check memory cache first; if found, return hit immediately.
3. Read disk `*.meta.json`; if `meta.lastUpdatedUtc` matches current index `lastUpdated`, return disk JSON and hydrate memory cache (hit).
4. Otherwise fetch remote payload, update disk JSON + metadata, and hydrate memory cache (miss).
5. If fetch fails but cached disk body exists, return stale cached payload and seed short memory cache (stale fallback hit).

Metadata fields:

- `url`
- `lastUpdatedUtc`
- `cachedAtUtc`

This keeps payload refresh logic tied to SuperControl index deltas instead of fixed TTLs.

Important: memory hits are per app instance. In multi-instance production, memory cache is not shared unless you add a distributed cache layer.

## Run and verify

```bash
dotnet restore supercontrol-listing-site-demo-public.sln
dotnet run --project supercontrol-listing-site-demo.csproj
```

Open:

- `https://localhost:{port}/supercontrol-listing-site-demo`
- `https://localhost:{port}/supercontrol-listing-site-demo/property/{propertyId}`

Verification checklist:

1. Search with and without `where` to confirm filtering.
2. Provide valid `checkIn`/`checkOut` to validate availability and selected-stay price.
3. Refresh twice and confirm cache hit counters increase.
4. Temporarily break token/network and confirm stale fallback behavior when cache exists.

## Production hardening

1. Move `.supercontrol-cache` to shared durable storage (db/blob/redis-backed strategy).
2. Add background refresh jobs based on SuperControl refresh guidance (indexes more frequent than static content).
3. Persist normalized listing/price/availability data in your own read model for frontend queries.
4. Add rate limiting/retry/circuit-breaker policies to HTTP client calls.
5. Rotate tokens and avoid exposing `SC-TOKEN` outside server-side calls.
6. Prefer environment variables or secret manager storage for production secrets.

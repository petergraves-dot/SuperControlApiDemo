# SuperControl API Demo (ASP.NET Core)

This project includes a working demo page at `/supercontrol-demo` that connects to SuperControl OpenIntegration.

## What the demo does

1. Calls `GET /properties/index`.
2. Calls `GET /properties/contentindex/{accountId}` using `SuperControl__AccountId` when set, otherwise the first account returned by `properties/index`.
3. Sends your API key via `SC-TOKEN` header.
4. Renders HTTP status and formatted JSON response in the browser.
5. Uses local response cache (`.supercontrol-cache/responses/supercontrol-demo`) with short TTL to speed repeated runs.

## Configure (`appsettings.json`)

Set the keys in `appsettings.json`:

```json
"SuperControl": {
  "ApiKey": "replace-with-live-supercontrol-token",
  "AccountId": 22263,
  "BaseUrl": "https://api.supercontrol.co.uk/v3/",
  "CalendarKey": "replace-with-live-calendar-key",
  "DefaultPropertyId": 671777
}
```

Required for the API demo page:

- `ApiKey`

Recommended:

- `AccountId` (if omitted, the demo uses the first account from `properties/index`)

Used by listing/property pages and widgets:

- `CalendarKey` (optional)
- `DefaultPropertyId` (optional fallback id for the calendar widget)

Run locally:

```bash
dotnet restore supercontrol-listing-site-demo-public.sln
dotnet run --project supercontrol-listing-site-demo.csproj
```

Open: `https://localhost:{port}/supercontrol-demo` (use the HTTPS URL shown in terminal).

Environment variables can still override appsettings (for example `SuperControl__ApiKey`).

## SuperControl reference notes

- Base API: `https://api.supercontrol.co.uk/v3/`
- Auth header: `SC-TOKEN: <token>`
- Recommended refresh cadence:
  - Accounts index: every 12 hours
  - Content/configuration indexes: every 6 hours
  - Prices/availability indexes: every 30 minutes

## Cache refresh demo UI

- Cadence values:
  - `accounts` -> refreshes `properties/index` with 12 hour TTL
  - `content-config` -> refreshes `contentindex` + `configurationindex` with 6 hour TTL
  - `prices-availability` -> refreshes `pricesindex` + `availabilityindex` with 30 minute TTL
  - `all` -> runs all cadence groups in one pass
- The response view shows a JSON summary with request counts, successes/failures, cache hits/misses, stale fallback count, and per-index breakdown.

The `/supercontrol-demo` page includes a cache cadence selector and `Refresh Cache Cadence` button. The action is protected by anti-forgery validation. The standalone demo intentionally does not expose a public scheduler endpoint; production applications should implement scheduled refreshes behind their own authentication and job infrastructure.

## Verification checklist

1. Open `/supercontrol-demo` and confirm `API key detected`.
2. Select `Run Demo Calls` and confirm `Accounts Index` and `Content Index` return `HTTP 200`.
3. Select `Refresh Cache Cadence` twice and confirm cache-hit counters increase on the second run.

## Security notes

- Do not commit live tokens.
- If a token has been shared in chat or committed anywhere, rotate it in SuperControl and update your environment.
- Best practice for production is server-level environment variables or a secret manager.

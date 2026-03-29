# SuperControlApiDemo

Standalone ASP.NET Core project focused on SuperControl integration demo files.

## Included routes

- `/supercontrol-demo` - API diagnostics and payload inspector
- `/internal/supercontrol/cache-refresh?cadence=accounts|content-config|prices-availability|all` - cache pre-warm endpoint for scheduled jobs
- `/supercontrol-listing-site-demo` - listing site style search/cards demo
- `/supercontrol-listing-site-demo/property/{propertyId}` - property detail page
- `/supercontrol-listing-site-tutorial` - implementation tutorial page

The `/supercontrol-demo` page includes a cache cadence selector and `Refresh Cache Cadence` action to run and inspect cache refresh runs directly in the UI.

## Documentation

- API diagnostics guide: `SuperControl-Demo.md`
- Listing implementation guide: `SuperControl-Listing-Site-Tutorial.md`

## Setup

1. Configure `SuperControl` settings in `appsettings.json` (or via environment variables for production).
2. Ensure `ApiKey` is set; set `AccountId` as well for listing/property routes.
3. Run:

```bash
dotnet restore
dotnet run
```

## Example SuperControl settings

```json
"SuperControl": {
	"ApiKey": "your-supercontrol-sc-token",
	"AccountId": 22263,
	"BaseUrl": "https://api.supercontrol.co.uk/v3/",
	"CalendarKey": "your-supercontrol-calendar-key",
	"DefaultPropertyId": 671777
}
```

Environment variable overrides are still supported, for example:

```bash
SuperControl__ApiKey=your-supercontrol-sc-token
```

## Notes

- `.supercontrol-cache/` is local runtime cache and is intentionally git-ignored.
- This repo is extracted from a larger site and excludes unrelated pages/assets.
- Best practice for production is server-level environment variables or a secret manager.

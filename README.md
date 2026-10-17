# SuperControlApiDemo

Standalone ASP.NET Core project focused on SuperControl integration demo files.

## Included routes

- `/supercontrol-demo` - API diagnostics and payload inspector
- `/supercontrol-listing-site-demo` - listing site style search/cards demo
- `/supercontrol-property-explorer` - synchronized list-and-map property search demo
- `/supercontrol-property-explorer-tutorial` - map property explorer implementation tutorial
- `/supercontrol-listing-site-demo/property/{propertyId}` - property detail page
- `/supercontrol-listing-site-tutorial` - implementation tutorial page
- `/supercontrol-data-export` - Data Export bookings XML demo
- `/supercontrol-data-export/properties` - Data Export properties XML demo
- `/supercontrol-data-export-tutorial` - Data Export implementation tutorial page

The `/supercontrol-demo` page includes a cache cadence selector and `Refresh Cache Cadence` action to run and inspect cache refresh runs directly in the UI.

## Documentation

- API diagnostics guide: `SuperControl-Demo.md`
- Listing implementation guide: `SuperControl-Listing-Site-Tutorial.md`
- Data Export implementation guide: `Views/SuperControlDataExportTutorial/Index.cshtml`

## Setup

1. Configure `SuperControl` settings in a git-ignored `appsettings.Local.json` (or via environment variables for production).
2. Ensure `ApiKey` is set; set `AccountId` as well for listing/property routes.
3. Run:

```bash
dotnet restore supercontrol-listing-site-demo-public.sln
dotnet run --project supercontrol-listing-site-demo.csproj
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

`appsettings.Local.json` is optional, overrides the checked-in settings, is excluded from publishing, and is intentionally git-ignored. Environment variables and command-line settings take precedence over it.

## Testing

Run the full test suite with:

```bash
dotnet test supercontrol-listing-site-demo-public.sln
```

The property explorer browser tests use Playwright with deterministic property data, so they do not call the live SuperControl API. Before the first browser-test run on a machine, build the UI test project and install Chromium:

```bash
dotnet build tests/supercontrol-listing-site-demo.UiTests/supercontrol-listing-site-demo.UiTests.csproj
pwsh tests/supercontrol-listing-site-demo.UiTests/bin/Debug/net10.0/playwright.ps1 install chromium
```

The UI tests cover synchronized map/list results, search within map bounds, marker clustering and selection, location suggestions, stable filter URLs, reset, and mobile map/list switching. OpenStreetMap tile requests are replaced with a local transparent image during tests.

## Notes

- `.supercontrol-cache/` is local runtime cache and is intentionally git-ignored.
- Authenticated API requests are restricted to the HTTPS origin configured by `SuperControl:BaseUrl`; redirects are not followed automatically.
- This repo is extracted from a larger site and excludes unrelated pages/assets.
- Best practice for production is server-level environment variables or a secret manager.

## Deployment

Run `./deploy.sh` to test, publish, and upload the application over FTPS to
`/demo.petergraves.co.uk/public_html`.

The script loads credentials from the first available source: `DEPLOY_ENV_FILE`,
`.env.deploy`, `.env`, or the existing `../Us2U.org.uk/.env`. Local `.env*`
files and `appsettings.Local.json` are excluded from Git; `.env.example`
contains only safe placeholders. The script also refuses to use a tracked
environment or deployment-settings file.


## Cookies and analytics

The Silktide consent banner and Google Analytics run outside Development.
`dotnet watch` uses the Development launch profile and does not load them.
The `/cookies` page reads the reviewed inventory in `Data/cookie-policy.json`.
See [.docs/COOKIE-AUDIT.md](.docs/COOKIE-AUDIT.md) for the production Playwright
scan, its limitations and the review workflow.

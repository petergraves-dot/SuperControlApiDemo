# Cookie policy and production audits

`Data/cookie-policy.json` is the reviewed storage inventory. The `/cookies`
Razor view reads it through `CookiePolicy.Current`. The inventory includes
cookies and local storage. Change its `updated` date when the published policy
changes. Scan dates belong in scan reports, not the policy's update date.

Descriptions are based on the site's code and supplier documentation. The
policy uses the topics in the supplied Us2U/Termly policy as a reference, with
new wording for this site. It does not inherit Us2U's cookies, contact details,
advertising statements or Flash guidance.

## Install and run

Run these commands from the repository root with .NET 10 installed:

```bash
rtk dotnet build tools/CookieAudit/CookieAudit.csproj
rtk dotnet run --no-build --project tools/CookieAudit -- --install
rtk dotnet run --no-build --project tools/CookieAudit
```

The default target is `https://demo.petergraves.co.uk`. The scanner uses fresh,
isolated Chromium contexts without any saved user browser data. It visits
same-origin sitemap pages and the reviewed scanPaths in Data/cookie-policy.json.
It does not submit forms or open other origins as top-level pages.
The default scan blocks requests to Google Analytics and Google Tag Manager.
This checks attempted tracking without sending test visits to Google, but
cannot verify real analytics cookies. The report records that limitation and
exits with findings. For a complete scan, use `--allow-analytics` only when
authorised to send the scan browser's visit metadata to Google:

```bash
rtk dotnet run --no-build --project tools/CookieAudit -- --allow-analytics
```

These test visits can appear in Google Analytics.

The scanner checks four scenarios: no choice, rejection, acceptance and
withdrawal after acceptance. It revisits pages after a choice, so it also
checks that choices survive navigation. The observations contain
cookie names, domains, expiry and flags, plus local/session storage keys in
loaded frames. Values and full network request URLs are excluded.

The JSON report is written to `.cookie-audit/latest.json`, which is ignored by
Git. Override it with `--output path/to/report.json`. Exit codes:

- `0`: the covered checks passed.
- `1`: findings or incomplete consent scenarios; review the report.
- `2`: the scanner could not start or retrieve the sitemap.

Unknown storage, analytics storage outside the accepted scenario, analytics
requests without consent, and cookies that exceed their documented maximum
lifetime are findings. An absent banner or failed page makes coverage
incomplete. Documented items not observed are listed separately; absence in
one scan does not prove that the site never sets an item.

Use `--settle-ms 2000` to change the observation window in milliseconds; the
default is two seconds per page. Shorter windows are useful for local checks
but can miss delayed storage or tracking.

The production scan will report missing consent controls until the consent
banner has been deployed. Re-run after deployment to verify the public site.

## Review changes

Run the scan after changes to scripts, embeds or consent handling, and as a
scheduled CI job if ongoing checks are wanted. The scanner is a standalone
tool, not a request-time dependency. No recurring job is installed by this
change.

For each unreviewed item, identify the script or server response that sets it.
Confirm its purpose and expiry against supplier documentation. Add a reviewed
description to `Data/cookie-policy.json` only if that storage is intended. Fix
unexpected tracking in the site's code. Rebuild to regenerate the sitemap and
publish the policy through the normal deployment workflow.

A scan never rewrites policy text or removes an inventory entry automatically.
It cannot infer purpose from a cookie name. It observes a two-second window on
each page in one browser and location. It does not cover every interaction,
browser, region, authenticated flow, IndexedDB or Cache Storage. This is an
inventory and consent regression check, not a legal assessment.

## Sources

- [Google Analytics cookie names, purposes and expiry](https://support.google.com/analytics/answer/11397207)
- [Silktide consent storage](https://silktide.com/consent-manager/docs/)
- [Playwright .NET browser contexts](https://playwright.dev/dotnet/docs/api/class-browsercontext)
- [ICO guidance on cookies and similar technologies](https://ico.org.uk/for-organisations/direct-marketing-and-privacy-and-electronic-communications/guide-to-pecr/cookies-and-similar-technologies/)

## Demo differences

Analytics uses measurement ID `G-2B9VN4NJYY`, the same property as the main site.
The `demo` cookie prefix and host-only cookie domain keep demo analytics
cookies separate from the main site's cookies. Withdrawing consent only
deletes `demo_ga` cookies. Consent choices use origin-local storage.

The inventory includes ASP.NET Core antiforgery cookies. These are session
cookies needed by demo forms and state-changing requests. Query strings and
fragments are removed from page and referrer addresses in the analytics config.

The sitemap scan does not submit demo searches, make bookings or inspect every
property detail page. SuperControl embeds and other conditional third-party
content need separate coverage before making claims about all embedded storage.

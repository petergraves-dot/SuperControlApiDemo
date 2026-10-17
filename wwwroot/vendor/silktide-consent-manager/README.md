# Silktide Consent Manager

Version 2.0, vendored from https://github.com/silktide/consent-manager at commit
e01dee803352a565d9f5eefddf427ffa46579a2a. The JavaScript and CSS are unmodified.
The MIT licence is included in LICENSE.

Site configuration lives in wwwroot/js/consent.js. Google Analytics loads only
after analytics consent. Revoking consent disables analytics, deletes its
cookies and reloads the page through Silktide's script revocation handling.

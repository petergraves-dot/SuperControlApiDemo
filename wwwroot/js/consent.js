(() => {
    'use strict';

    const measurementId = 'G-2B9VN4NJYY';
    let analyticsStarted = false;

    window.dataLayer = window.dataLayer || [];
    window.gtag = function () { window.dataLayer.push(arguments); };
    window['ga-disable-' + measurementId] = true;
    window.gtag('consent', 'default', {
        analytics_storage: 'denied',
        ad_storage: 'denied',
        ad_user_data: 'denied',
        ad_personalization: 'denied'
    });

    function sanitisedReferrer() {
        try {
            const referrer = new URL(document.referrer);
            return referrer.origin + referrer.pathname;
        } catch {
            return '';
        }
    }

    function clearAnalyticsCookies() {
        window['ga-disable-' + measurementId] = true;

        // Only remove this demo's cookies; the main site has separate consent.
        const domains = ['', window.location.hostname, '.' + window.location.hostname];

        document.cookie.split(';').forEach(cookie => {
            const name = cookie.split('=')[0].trim();
            if (name !== 'demo_ga' && !name.startsWith('demo_ga_')) return;
            domains.forEach(domain => {
                document.cookie = name + '=; Max-Age=0; path=/;'
                    + (domain ? ' domain=' + domain + ';' : '');
            });
        });
    }

    window.silktideConsentManager.init({
        consentTypes: [
            {
                id: 'essential',
                label: 'Essential storage',
                description: '<p>This site stores your cookie preferences in your browser so it can remember your choice. This does not track your visits.</p>',
                required: true
            },
            {
                id: 'analytics',
                label: 'Analytics cookies',
                description: '<p>Google Analytics uses demo_ga cookies to measure visits and how people use this site. These cookies can last up to two years. Google receives information about your visit. Analytics is optional and stays off until you accept.</p>',
                defaultValue: false,
                gtag: 'analytics_storage',
                scripts: [{
                    url: 'https://www.googletagmanager.com/gtag/js?id=' + measurementId,
                    load: 'async'
                }],
                onAccept: function () {
                    window['ga-disable-' + measurementId] = false;
                    // Restore consent before config on returning visits too.
                    window.gtag('consent', 'update', { analytics_storage: 'granted' });
                    if (analyticsStarted) return;
                    analyticsStarted = true;
                    window.gtag('js', new Date());
                    window.gtag('config', measurementId, {
                        cookie_prefix: 'demo',
                        cookie_domain: 'none',
                        page_location: window.location.origin + window.location.pathname,
                        page_referrer: sanitisedReferrer(),
                        allow_google_signals: false,
                        allow_ad_personalization_signals: false
                    });
                },
                onReject: clearAnalyticsCookies
            }
        ],
        prompt: { position: 'bottomRight' },
        icon: { position: 'bottomLeft' },
        backdrop: { show: true },
        text: {
            prompt: {
                description: '<p>This site uses optional Google Analytics cookies to measure visits. You can accept or reject analytics. Your choice is stored in your browser, and you can change it using the cookie preferences button. <a href="/cookies">Read the cookie policy</a>.</p>',
                acceptAllButtonText: 'Accept analytics',
                acceptAllButtonAccessibleLabel: 'Accept analytics cookies',
                rejectNonEssentialButtonText: 'Reject analytics',
                rejectNonEssentialButtonAccessibleLabel: 'Reject analytics cookies',
                preferencesButtonText: 'Cookie preferences',
                preferencesButtonAccessibleLabel: 'Manage cookie preferences'
            },
            preferences: {
                title: 'Cookie preferences',
                description: '<p>Choose whether to allow Google Analytics. You can change your choice at any time. Rejecting analytics removes its cookies and stops measurement on this browser.</p><p><a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer">Google privacy policy</a></p>',
                saveButtonText: 'Save preferences',
                saveButtonAccessibleLabel: 'Save cookie preferences'
            }
        }
    });

    // Clear this demo's analytics cookies when there is no saved acceptance.
    if (window.silktideConsentManager.getInstance().getConsentChoice('analytics') !== true) {
        clearAnalyticsCookies();
    }

    document.querySelectorAll('[data-cookie-preferences]').forEach(button => {
        button.addEventListener('click', () => {
            window.silktideConsentManager.getInstance().toggleModal(true);
        });
    });
})();

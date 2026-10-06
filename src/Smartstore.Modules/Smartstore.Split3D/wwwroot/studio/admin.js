/*
 * TT Minimal admin: count badges on the top menu ("3 new orders" on Orders, key orders waiting on License keys, …).
 * Polls the badge endpoint (data-badges-url on this script tag); an item with work lights up, and flashes when the
 * number grows while the page is open.
 */
(function () {
    'use strict';

    var script = document.currentScript;
    var url = script && script.getAttribute('data-badges-url');
    if (!url || !window.fetch) return;

    var INTERVAL = 60 * 1000;
    var last = {};
    var baseTitle = document.title;

    function apply(badges) {
        var total = 0;

        Object.keys(badges).forEach(function (id) {
            var count = badges[id] | 0;
            var item = document.querySelector('#navbar li[data-id="nav-' + id + '"]');
            if (!item) return;

            var link = item.querySelector(':scope > .nav-link');
            var badge = link && link.querySelector('.tt-nav-badge');
            total += count;

            if (!link) return;
            if (count > 0) {
                if (!badge) {
                    badge = document.createElement('span');
                    badge.className = 'tt-nav-badge';
                    link.appendChild(badge);
                }
                badge.textContent = count > 99 ? '99+' : String(count);
                link.setAttribute('title', count + ' mục đang chờ xử lý');
                item.classList.add('tt-nav-alert');

                if (last[id] !== undefined && count > last[id]) {
                    item.classList.remove('tt-nav-new');
                    void item.offsetWidth; // restart the animation
                    item.classList.add('tt-nav-new');
                }
            }
            else {
                if (badge) badge.remove();
                link.removeAttribute('title');
                item.classList.remove('tt-nav-alert', 'tt-nav-new');
            }

            last[id] = count;
        });

        document.title = total > 0 ? '(' + total + ') ' + baseTitle : baseTitle;
    }

    function load() {
        fetch(url, { credentials: 'same-origin', cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) { if (data) apply(data); })
            .catch(function () { /* offline or logged out: keep the last state */ });
    }

    function start() {
        load();
        setInterval(function () {
            if (!document.hidden) load();
        }, INTERVAL);
        document.addEventListener('visibilitychange', function () {
            if (!document.hidden) load();
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();
})();

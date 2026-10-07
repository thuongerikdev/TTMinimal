/*
 * TT Minimal admin: highlights the top menu entry of the current page. The core admin menu renders no current
 * state, so the link is matched here: exact path first, then the same controller (e.g. /admin/order/edit/5 keeps
 * "Orders" lit), preferring list/index links and top-level links.
 */
(function () {
    'use strict';

    function parse(href) {
        try {
            var a = new URL(href, location.href);
            if (a.origin !== location.origin) return null;
            var path = a.pathname.toLowerCase().replace(/\/+$/, '');
            var parts = path.split('/').filter(Boolean);
            var admin = parts.indexOf('admin');
            if (admin < 0) return null;
            return { path: path, controller: parts[admin + 1] || 'home', action: parts[admin + 2] || 'index' };
        }
        catch (e) {
            return null;
        }
    }

    function mark() {
        var current = parse(location.href);
        var navbar = document.getElementById('navbar-menu');
        if (!current || !navbar) return;

        var best = null, bestScore = 0;
        navbar.querySelectorAll('a.nav-link[href], a.dropdown-item[href]').forEach(function (link) {
            var target = parse(link.getAttribute('href'));
            if (!target) return;

            var score = 0;
            if (target.path === current.path) score = 100;
            else if (target.controller === current.controller) score = /^(list|index)$/.test(target.action) ? 50 : 40;
            if (!score) return;
            if (link.classList.contains('nav-link')) score += 5;

            if (score > bestScore) {
                best = link;
                bestScore = score;
            }
        });

        if (!best) return;

        best.classList.add('active');
        var top = best.closest('#navbar-menu .navbar-nav > .nav-item');
        if (top) top.classList.add('active');
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', mark);
    else mark();
})();

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

/*
 * Quote requests: the contact buttons (call, Zalo, SMS, Messenger, email) open the channel and write the attempt
 * into the contact log of the request in the same click, so the studio never has to log anything by hand.
 */
(function () {
    'use strict';

    var root = document.querySelector('[data-tt-contact]');
    if (!root || !window.fetch) return;

    var url = root.getAttribute('data-tt-contact');
    var id = root.getAttribute('data-tt-id');
    var messageBox = document.querySelector('[data-tt-message]');

    function token() {
        var meta = document.querySelector('meta[name="__rvt"]');
        if (meta) return meta.getAttribute('content');
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function log(channelId) {
        var body = new FormData();
        body.append('Id', id);
        body.append('ChannelId', channelId);
        body.append('Message', messageBox ? messageBox.value : '');
        body.append('__RequestVerificationToken', token());

        return fetch(url, {
            method: 'POST',
            body: body,
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
    }

    root.addEventListener('click', function (e) {
        var link = e.target.closest('[data-tt-channel]');
        if (!link) return;

        // The link opens the channel on its own (tel:, sms:, zalo.me, …); logging runs alongside it.
        log(link.getAttribute('data-tt-channel')).then(function (r) {
            if (!r || !r.ok) return;
            var badge = document.querySelector('[data-tt-contact-count]');
            if (badge) badge.textContent = (parseInt(badge.textContent, 10) || 0) + 1;
        }).catch(function () { /* logging must never block the call */ });
    });

    var copy = document.querySelector('[data-tt-copy]');
    if (copy && messageBox) {
        copy.addEventListener('click', function () {
            messageBox.select();
            try { document.execCommand('copy'); } catch (e) { }
            var label = copy.querySelector('span');
            if (!label) return;
            var text = label.textContent;
            label.textContent = 'Đã copy';
            setTimeout(function () { label.textContent = text; }, 1500);
        });
    }
})();

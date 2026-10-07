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

    // Pages that stand in for a core list: /admin/studioproducts lights the "Products" link (/admin/product/list).
    var aliases = { studioproducts: 'product' };

    function mark() {
        var current = parse(location.href);
        if (current && aliases[current.controller]) current.controller = aliases[current.controller];
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

/*
 * Products overview (StudioProducts/Index): search without diacritics ("chau" finds "Chậu"), status filters,
 * collapsible groups (remembered per browser), the group navigation and the "Đang bán / Đang ẩn" switch.
 */
(function () {
    'use strict';

    var root = document.querySelector('[data-tt-products]');
    if (!root) return;

    var search = root.querySelector('[data-ttp-search]');
    var empty = root.querySelector('[data-ttp-empty]');
    var toast = root.querySelector('[data-ttp-toast]');
    var groups = Array.prototype.slice.call(root.querySelectorAll('[data-ttp-group]'));
    var filter = 'all';
    var STORE_KEY = 'ttp-collapsed';
    var toastTimer;

    function simplify(value) {
        return (value || '').toLowerCase().replace(/đ/g, 'd').normalize('NFD').replace(/[̀-ͯ]/g, '').trim();
    }

    function readCollapsed() {
        try { return JSON.parse(localStorage.getItem(STORE_KEY) || 'null'); } catch (e) { return null; }
    }

    function writeCollapsed() {
        var state = {};
        groups.forEach(function (g) { state[g.getAttribute('data-ttp-group')] = g.classList.contains('collapsed'); });
        try { localStorage.setItem(STORE_KEY, JSON.stringify(state)); } catch (e) { }
    }

    function setCollapsed(group, collapsed) {
        group.classList.toggle('collapsed', collapsed);
        var toggle = group.querySelector('[data-ttp-toggle]');
        if (toggle) toggle.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
    }

    function matchesFilter(row) {
        var state = row.getAttribute('data-state');
        if (filter === 'published') return state === 'published';
        if (filter === 'hidden') return state === 'hidden';
        if (filter === 'attention') return row.getAttribute('data-attention') === '1';
        return true;
    }

    function apply() {
        var terms = simplify(search ? search.value : '').split(/\s+/).filter(Boolean);
        var narrowing = terms.length > 0 || filter !== 'all';
        var total = 0;

        groups.forEach(function (group) {
            var count = 0;
            group.querySelectorAll('[data-ttp-row]').forEach(function (row) {
                var text = row.getAttribute('data-search') || '';
                var visible = matchesFilter(row) && terms.every(function (t) { return text.indexOf(t) >= 0; });
                row.hidden = !visible;
                if (visible) count++;
            });

            total += count;
            group.hidden = count === 0;
            group.classList.toggle('is-searching', narrowing);
            if (narrowing && count > 0) group.classList.remove('collapsed');
            else if (!narrowing) restore(group);

            var badge = group.querySelector('[data-ttp-group-count]');
            if (badge) badge.textContent = count;

            var nav = root.querySelector('[data-ttp-nav="' + group.getAttribute('data-ttp-group') + '"]');
            if (nav) {
                nav.classList.toggle('is-empty', count === 0);
                var navCount = nav.querySelector('[data-ttp-nav-count]');
                if (navCount) navCount.textContent = count;
            }
        });

        if (empty) empty.hidden = total > 0;
    }

    var initial = readCollapsed();
    function restore(group) {
        var key = group.getAttribute('data-ttp-group');
        if (initial && key in initial) setCollapsed(group, initial[key]);
        else setCollapsed(group, group.hasAttribute('data-default-collapsed'));
    }

    groups.forEach(function (group) {
        if (group.classList.contains('collapsed')) group.setAttribute('data-default-collapsed', '');
        restore(group);

        var toggle = group.querySelector('[data-ttp-toggle]');
        if (toggle) {
            toggle.addEventListener('click', function () {
                setCollapsed(group, !group.classList.contains('collapsed'));
                if (!group.classList.contains('is-searching')) {
                    writeCollapsed();
                    initial = readCollapsed();
                }
            });
        }
    });

    if (search) {
        search.addEventListener('input', apply);
        search.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') { search.value = ''; apply(); }
        });
        document.addEventListener('keydown', function (e) {
            var tag = (e.target && e.target.tagName) || '';
            if (e.key === '/' && !/^(INPUT|TEXTAREA|SELECT)$/.test(tag) && !e.target.isContentEditable) {
                e.preventDefault();
                search.focus();
            }
        });
    }

    root.querySelectorAll('[data-ttp-filter]').forEach(function (button) {
        button.addEventListener('click', function () {
            filter = button.getAttribute('data-ttp-filter');
            root.querySelectorAll('[data-ttp-filter]').forEach(function (b) { b.classList.toggle('active', b === button); });
            apply();
        });
    });

    var reset = root.querySelector('[data-ttp-reset]');
    if (reset) {
        reset.addEventListener('click', function () {
            if (search) search.value = '';
            var all = root.querySelector('[data-ttp-filter="all"]');
            if (all) all.click();
            else apply();
        });
    }

    // Group navigation: open the group when jumping to it, highlight the group in view.
    root.querySelectorAll('[data-ttp-nav]').forEach(function (link) {
        link.addEventListener('click', function (e) {
            var group = root.querySelector('[data-ttp-group="' + link.getAttribute('data-ttp-nav') + '"]');
            if (!group || group.hidden) return;
            e.preventDefault();
            setCollapsed(group, false);
            group.scrollIntoView({ behavior: 'smooth', block: 'start' });
        });
    });

    if ('IntersectionObserver' in window) {
        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                var key = entry.target.getAttribute('data-ttp-group');
                root.querySelectorAll('[data-ttp-nav]').forEach(function (l) {
                    l.classList.toggle('current', l.getAttribute('data-ttp-nav') === key);
                });
            });
        }, { rootMargin: '-10% 0px -70% 0px' });
        groups.forEach(function (g) { observer.observe(g); });
    }

    function showToast(text, error) {
        if (!toast) return;
        toast.textContent = text;
        toast.classList.toggle('error', !!error);
        toast.classList.add('show');
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { toast.classList.remove('show'); }, 2200);
    }

    function token() {
        var meta = document.querySelector('meta[name="__rvt"]');
        if (meta) return meta.getAttribute('content');
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function bumpCount(name, delta) {
        var el = root.querySelector('[data-ttp-count="' + name + '"]');
        if (el) el.textContent = Math.max(0, (parseInt(el.textContent, 10) || 0) + delta);
    }

    // Publish switch: saves at once, reverts on error.
    var url = root.getAttribute('data-toggle-url');
    root.addEventListener('change', function (e) {
        var input = e.target.closest('[data-ttp-publish]');
        if (!input || !url) return;

        var published = input.checked;
        var label = input.closest('.ttp-switch');
        var text = label && label.querySelector('.ttp-switch-text');
        var row = input.closest('[data-ttp-row]');
        var name = row ? (row.querySelector('.ttp-name') || {}).textContent : '';

        var body = new FormData();
        body.append('id', input.getAttribute('data-ttp-publish'));
        body.append('published', published ? 'true' : 'false');
        body.append('__RequestVerificationToken', token());

        if (label) label.classList.add('busy');

        fetch(url, { method: 'POST', body: body, credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
            .then(function () {
                if (text) text.textContent = text.getAttribute(published ? 'data-on' : 'data-off');
                if (row) {
                    row.setAttribute('data-state', published ? 'published' : 'hidden');
                    row.classList.toggle('is-hidden', !published);
                }
                bumpCount('published', published ? 1 : -1);
                bumpCount('hidden', published ? -1 : 1);
                showToast(published ? 'Đã mở bán: ' + name : 'Đã ẩn khỏi web: ' + name);
            })
            .catch(function () {
                input.checked = !published;
                showToast('Không lưu được, thử lại sau.', true);
            })
            .then(function () {
                if (label) label.classList.remove('busy');
            });
    });

    apply();
})();

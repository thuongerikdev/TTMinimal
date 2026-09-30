/* TT Minimal "Workshop" storefront behaviour. No dependencies. Loaded on every storefront page. */
(function () {
    'use strict';

    var doc = document.documentElement;
    doc.classList.add('tt-js');

    var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var canHover = window.matchMedia && window.matchMedia('(hover: hover) and (pointer: fine)').matches;
    var money = new Intl.NumberFormat('vi-VN');

    function vnd(value) { return money.format(Math.round(value)) + 'đ'; }
    function weight(g) { return g >= 1000 ? money.format(Math.round(g / 10) / 100) + ' kg' : money.format(Math.round(g)) + ' g'; }
    function each(sel, fn, root) { Array.prototype.forEach.call((root || document).querySelectorAll(sel), fn); }
    function onReady(fn) { if (document.readyState !== 'loading') fn(); else document.addEventListener('DOMContentLoaded', fn); }
    function clamp(v, a, b) { return Math.min(b, Math.max(a, v)); }

    // ---------- Header: scrolled state, hide on scroll down, progress bar ----------

    function initScrollChrome() {
        var header = document.getElementById('header');
        var bar = document.querySelector('.tt-progress');
        var lastY = window.scrollY, ticking = false;

        function update() {
            ticking = false;
            var y = window.scrollY;
            if (header) {
                header.classList.toggle('is-scrolled', y > 20);
                var searchOpen = header.querySelector('.tt-search.is-open');
                if (!searchOpen && y > 420 && y > lastY + 4) header.classList.add('is-hidden');
                else if (y < lastY - 4 || y <= 420) header.classList.remove('is-hidden');
            }
            if (bar) {
                var max = document.documentElement.scrollHeight - window.innerHeight;
                bar.style.transform = 'scaleX(' + (max > 0 ? y / max : 0) + ')';
            }
            lastY = y;
        }

        window.addEventListener('scroll', function () {
            if (!ticking) { ticking = true; requestAnimationFrame(update); }
        }, { passive: true });
        update();
    }

    function initSearch() {
        each('.tt-search', function (box) {
            var btn = box.querySelector('.tt-search-toggle');
            var input = box.querySelector('input[type=search]');
            if (!btn) return;

            function set(open) {
                box.classList.toggle('is-open', open);
                btn.setAttribute('aria-expanded', open ? 'true' : 'false');
                if (open && input) setTimeout(function () { input.focus(); }, 60);
            }

            btn.addEventListener('click', function (e) { e.stopPropagation(); set(!box.classList.contains('is-open')); });
            document.addEventListener('click', function (e) { if (!box.contains(e.target)) set(false); });
            document.addEventListener('keydown', function (e) { if (e.key === 'Escape') set(false); });
        });
    }

    // ---------- Reveal & split text ----------

    function splitWords(el) {
        if (el.dataset.split) return;
        el.dataset.split = '1';
        var i = 0;
        (function walk(node) {
            Array.prototype.slice.call(node.childNodes).forEach(function (child) {
                if (child.nodeType === 3) {
                    var frag = document.createDocumentFragment();
                    child.textContent.split(/(\s+)/).forEach(function (part) {
                        if (!part) return;
                        if (/^\s+$/.test(part)) { frag.appendChild(document.createTextNode(part)); return; }
                        var w = document.createElement('span'); w.className = 'w';
                        var s = document.createElement('span'); s.textContent = part; s.style.setProperty('--i', i++);
                        w.appendChild(s); frag.appendChild(w);
                    });
                    node.replaceChild(frag, child);
                } else if (child.nodeType === 1 && child.tagName !== 'BR' && !child.classList.contains('tt-rotator')) {
                    walk(child);
                } else if (child.nodeType === 1 && child.tagName !== 'BR') {
                    var wrap = document.createElement('span'); wrap.className = 'w';
                    var inner = document.createElement('span'); inner.style.setProperty('--i', i++);
                    node.replaceChild(wrap, child); inner.appendChild(child); wrap.appendChild(inner);
                }
            });
        })(el);
    }

    function initReveal() {
        each('.tt-split', splitWords);
        var items = document.querySelectorAll('[data-tt-reveal], .tt-split');
        if (!('IntersectionObserver' in window) || reduceMotion) {
            Array.prototype.forEach.call(items, function (el) { el.classList.add('is-in'); });
            return;
        }
        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) { entry.target.classList.add('is-in'); io.unobserve(entry.target); }
            });
        }, { rootMargin: '0px 0px -8% 0px', threshold: 0.1 });
        Array.prototype.forEach.call(items, function (el) { io.observe(el); });
    }

    // ---------- Cards: spotlight, tilt ----------

    function initPointerFx() {
        if (!canHover || reduceMotion) return;

        each('.tt-card', function (card) {
            card.addEventListener('pointermove', function (e) {
                var r = card.getBoundingClientRect();
                card.style.setProperty('--mx', (e.clientX - r.left) + 'px');
                card.style.setProperty('--my', (e.clientY - r.top) + 'px');
            });
        });

        each('[data-tt-tilt]', function (card) {
            var max = parseFloat(card.getAttribute('data-tt-tilt')) || 6;
            card.addEventListener('pointermove', function (e) {
                var r = card.getBoundingClientRect();
                var px = (e.clientX - r.left) / r.width - 0.5, py = (e.clientY - r.top) / r.height - 0.5;
                card.style.transform = 'perspective(1000px) rotateX(' + (-py * max) + 'deg) rotateY(' + (px * max) + 'deg)';
            });
            card.addEventListener('pointerleave', function () { card.style.transform = ''; });
        });
    }

    // ---------- 3D illustration card ----------

    function init3D() {
        each('[data-tt-3d]', function (card) {
            var stage = card.parentElement;
            var hovering = false, start = performance.now();

            function set(rx, ry, mx, my) {
                card.style.setProperty('--rx', rx.toFixed(2) + 'deg');
                card.style.setProperty('--ry', ry.toFixed(2) + 'deg');
                card.style.setProperty('--mx', mx + '%');
                card.style.setProperty('--my', my + '%');
            }

            if (reduceMotion) return;

            if (canHover) {
                stage.addEventListener('pointermove', function (e) {
                    var r = stage.getBoundingClientRect();
                    var px = (e.clientX - r.left) / r.width - 0.5, py = (e.clientY - r.top) / r.height - 0.5;
                    hovering = true;
                    card.classList.add('is-live');
                    card.classList.remove('is-idle');
                    set(-py * 18, px * 22, Math.round((px + 0.5) * 100), Math.round((py + 0.5) * 100));
                });
                stage.addEventListener('pointerleave', function () {
                    hovering = false;
                    card.classList.remove('is-live');
                    start = performance.now();
                });
            }

            // Gentle idle sway so the depth is visible without a mouse (phones, tablets).
            (function idle(now) {
                requestAnimationFrame(idle);
                if (hovering) return;
                var t = (now - start) / 1000;
                set(Math.sin(t * 0.9) * 5, Math.sin(t * 0.6) * 9, 50 + Math.sin(t * 0.6) * 25, 40);
            })(start);
        });
    }

    // ---------- Word rotator ----------

    function initRotator() {
        each('.tt-rotator', function (box) {
            var words = Array.prototype.slice.call(box.children);
            if (!words.length) return;
            var i = 0;
            words[0].classList.add('is-on');
            if (reduceMotion || words.length < 2) return;
            setInterval(function () {
                var cur = words[i];
                cur.classList.remove('is-on'); cur.classList.add('is-off');
                setTimeout(function () { cur.classList.remove('is-off'); }, 900);
                i = (i + 1) % words.length;
                words[i].classList.add('is-on');
            }, 2600);
        });
    }

    // ---------- Marquee speeds up with scroll velocity ----------

    function initMarquee() {
        var rows = document.querySelectorAll('.tt-marquee-row');
        if (!rows.length || reduceMotion || !Element.prototype.getAnimations) return;
        var lastY = window.scrollY, speed = 1;
        (function loop() {
            var y = window.scrollY, v = Math.abs(y - lastY);
            lastY = y;
            speed += ((1 + Math.min(v / 12, 5)) - speed) * 0.08;
            Array.prototype.forEach.call(rows, function (row) {
                row.getAnimations().forEach(function (a) { a.playbackRate = speed; });
            });
            requestAnimationFrame(loop);
        })();
    }

    // ---------- Hero: model printed layer by layer ----------

    var shapes = [
        { name: 'VASE_01.STL', fn: function (t, a) { var r = 0.5 + 0.2 * Math.sin(t * Math.PI * 1.5 + 0.6) - 0.08 * t; return r * (1 + 0.08 * Math.cos(6 * a + t * 7)); } },
        { name: 'POT_LOBED.STL', fn: function (t, a) { var r = Math.sin(Math.PI * (0.1 + 0.8 * t)) * 0.72 + 0.12; return r * (1 + 0.06 * Math.cos(5 * a)); } },
        { name: 'TOWER_HEX.3MF', fn: function (t, a) {
            var sides = 8, seg = Math.PI * 2 / sides;
            var poly = Math.cos(seg / 2) / Math.cos(((a % seg) + seg) % seg - seg / 2);
            var r = t < 0.82 ? 0.42 + 0.14 * Math.pow(1 - t, 2) : 0.56 - (t > 0.9 && Math.cos(a * 4) > 0.2 ? 0.1 : 0);
            return r * poly;
        } },
        { name: 'TWIST_12.OBJ', fn: function (t, a) { var r = 0.34 + 0.16 * Math.cos(t * Math.PI * 2); return r * (1 + 0.16 * Math.cos(3 * (a + t * 2.4))); } }
    ];

    function initHero() {
        var canvas = document.getElementById('tt-hero-canvas');
        if (!canvas || !canvas.getContext) return;

        var ctx = canvas.getContext('2d');
        var hudLayer = document.querySelector('[data-hud-layer]');
        var hudFile = document.querySelector('[data-hud-file]');
        var hudPct = document.querySelector('[data-hud-pct]');
        var hudBar = document.querySelector('[data-hud-bar]');
        var layers = 46, points = 96;
        var width = 0, height = 0;
        var shapeIndex = 0, cycleStart = performance.now();
        var printTime = 6000, holdTime = 2000, fadeTime = 900;
        var rotation = 0, tiltX = 0.4, targetTilt = 0.4, mouseX = 0;
        var visible = true;

        function resize() {
            var rect = canvas.getBoundingClientRect();
            var dpr = Math.min(window.devicePixelRatio || 1, 2);
            width = rect.width; height = rect.height;
            canvas.width = Math.round(width * dpr); canvas.height = Math.round(height * dpr);
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        }

        function project(x, y, z, rot) {
            var c = Math.cos(rot), s = Math.sin(rot);
            var rx = x * c - z * s, rz = x * s + z * c;
            var ct = Math.cos(tiltX), st = Math.sin(tiltX);
            var ry = y * ct - rz * st, rz2 = y * st + rz * ct;
            var scale = Math.min(width, height) * 1.25 / (3.4 - rz2);
            return { x: width / 2 + rx * scale, y: height * 0.46 - ry * scale, z: rz2 };
        }

        function plate(rot) {
            var size = 1.05, n = 10, y = -0.98;
            ctx.lineWidth = 1;
            for (var i = 0; i <= n; i++) {
                var v = -size + (2 * size * i) / n;
                var a1 = project(v, y, -size, rot), a2 = project(v, y, size, rot);
                var b1 = project(-size, y, v, rot), b2 = project(size, y, v, rot);
                var edge = i === 0 || i === n;
                ctx.strokeStyle = 'rgba(255,255,255,' + (edge ? 0.6 : 0.16) + ')';
                ctx.beginPath(); ctx.moveTo(a1.x, a1.y); ctx.lineTo(a2.x, a2.y); ctx.moveTo(b1.x, b1.y); ctx.lineTo(b2.x, b2.y); ctx.stroke();
            }
        }

        function ring(fn, t, rot) {
            var y = -0.95 + t * 1.8, list = [];
            for (var j = 0; j <= points; j++) {
                var a = (j / points) * Math.PI * 2, r = fn(t, a);
                list.push(project(Math.cos(a) * r, y, Math.sin(a) * r, rot));
            }
            return list;
        }

        function render(now) {
            requestAnimationFrame(render);
            if (!visible || !width) return;

            var elapsed = now - cycleStart, total = printTime + holdTime + fadeTime;
            if (elapsed > total) { shapeIndex = (shapeIndex + 1) % shapes.length; cycleStart = now; elapsed = 0; }

            var progress = reduceMotion ? 1 : Math.min(elapsed / printTime, 1);
            var fade = clamp(elapsed > printTime + holdTime ? 1 - (elapsed - printTime - holdTime) / fadeTime : 1, 0, 1);

            if (!reduceMotion) rotation += 0.0042;
            tiltX += (targetTilt - tiltX) * 0.05;
            var rot = rotation + mouseX * 0.6;

            ctx.clearRect(0, 0, width, height);
            plate(rot);

            var fn = shapes[shapeIndex].fn;
            var shown = progress * layers, done = Math.floor(shown), tip = null;

            for (var i = 0; i <= Math.min(done, layers - 1); i++) {
                var t = i / (layers - 1), pts = ring(fn, t, rot);
                var isTop = i === done && progress < 1;
                var count = Math.max(2, Math.round(points * (isTop ? shown - done : 1)));

                for (var pass = 0; pass < 2; pass++) {
                    ctx.beginPath();
                    var drawing = false;
                    for (var j = 0; j < count; j++) {
                        var p = pts[j], q = pts[j + 1];
                        var front = (p.z + q.z) / 2 > -0.05;
                        if ((pass === 1) === front) {
                            if (!drawing) { ctx.moveTo(p.x, p.y); drawing = true; }
                            ctx.lineTo(q.x, q.y);
                        } else { drawing = false; }
                    }
                    ctx.strokeStyle = isTop ? 'rgba(255,216,77,' + fade + ')' : 'rgba(255,255,255,' + ((pass ? 0.9 : 0.22) * fade) + ')';
                    ctx.lineWidth = isTop ? 2.2 : (pass ? 1.2 : 0.9);
                    ctx.stroke();
                }
                if (isTop) tip = pts[count - 1];
            }

            if (done > 1) {
                ctx.lineWidth = 0.7;
                for (var k = 0; k < points; k += 16) {
                    ctx.beginPath();
                    for (var m = 0; m <= Math.min(done, layers - 1); m++) {
                        var tt = m / (layers - 1), a = (k / points) * Math.PI * 2, r = fn(tt, a);
                        var pp = project(Math.cos(a) * r, -0.95 + tt * 1.8, Math.sin(a) * r, rot);
                        if (m === 0) ctx.moveTo(pp.x, pp.y); else ctx.lineTo(pp.x, pp.y);
                    }
                    ctx.strokeStyle = 'rgba(255,255,255,' + (0.28 * fade) + ')';
                    ctx.stroke();
                }
            }

            if (tip && fade > 0) {
                var g = ctx.createRadialGradient(tip.x, tip.y, 0, tip.x, tip.y, 30);
                g.addColorStop(0, 'rgba(255,216,77,1)'); g.addColorStop(0.3, 'rgba(255,216,77,.45)'); g.addColorStop(1, 'rgba(255,216,77,0)');
                ctx.fillStyle = g; ctx.beginPath(); ctx.arc(tip.x, tip.y, 22, 0, Math.PI * 2); ctx.fill();
                ctx.strokeStyle = 'rgba(255,255,255,.7)'; ctx.lineWidth = 1;
                ctx.beginPath(); ctx.moveTo(tip.x, tip.y - 8); ctx.lineTo(tip.x, tip.y - 64); ctx.stroke();
                ctx.fillStyle = '#ffffff'; ctx.fillRect(tip.x - 10, tip.y - 76, 20, 12);
            }

            var layer = Math.min(done + 1, layers), pct = Math.round(progress * 100);
            if (hudLayer) hudLayer.textContent = (layer < 10 ? '0' : '') + layer + ' / ' + layers;
            if (hudFile) hudFile.textContent = shapes[shapeIndex].name;
            if (hudPct) hudPct.textContent = pct + '%';
            if (hudBar) hudBar.style.setProperty('--w', pct + '%');
        }

        resize();
        window.addEventListener('resize', resize);
        if (!reduceMotion) {
            window.addEventListener('pointermove', function (e) {
                mouseX = e.clientX / window.innerWidth - 0.5;
                targetTilt = 0.4 + (e.clientY / window.innerHeight - 0.5) * 0.25;
            }, { passive: true });
        }
        if ('IntersectionObserver' in window) {
            new IntersectionObserver(function (entries) { visible = entries[0].isIntersecting; }).observe(canvas);
        }
        document.addEventListener('visibilitychange', function () { visible = !document.hidden; });
        requestAnimationFrame(render);
    }

    // ---------- Pinned horizontal process ----------

    function initProcess() {
        var section = document.querySelector('.tt-process');
        if (!section) return;
        var track = section.querySelector('.tt-process-track');
        var bar = section.querySelector('.tt-process-bar i');
        var desktop = window.matchMedia('(min-width: 992px)');
        var distance = 0;

        function update() {
            if (!distance) return;
            var r = section.getBoundingClientRect();
            var p = clamp(-r.top / (section.offsetHeight - window.innerHeight), 0, 1);
            track.style.transform = 'translate3d(' + (-p * distance) + 'px,0,0)';
            if (bar) bar.style.width = (p * 100) + '%';
        }

        function layout() {
            if (!desktop.matches || reduceMotion) { section.style.height = ''; track.style.transform = ''; distance = 0; return; }
            distance = Math.max(0, track.scrollWidth - window.innerWidth);
            section.style.height = (window.innerHeight + distance) + 'px';
            update();
        }

        window.addEventListener('scroll', function () { requestAnimationFrame(update); }, { passive: true });
        window.addEventListener('resize', layout);
        window.addEventListener('load', layout);
        layout();
    }

    // ---------- Price calculators ----------

    function tierFor(tech, grams) {
        var idx = 0;
        tech.tiers.forEach(function (t, i) { if (grams >= t.min) idx = i; });
        return idx;
    }

    function countTo(el, value) {
        if (!el) return;
        var from = parseFloat(el.dataset.v || '0');
        el.dataset.v = value;
        if (reduceMotion || from === value) { el.textContent = value > 0 ? vnd(value) : '—'; return; }
        var start = performance.now(), dur = 500;
        (function step(now) {
            var k = Math.min((now - start) / dur, 1), e = 1 - Math.pow(1 - k, 3);
            var v = from + (value - from) * e;
            el.textContent = value > 0 || k < 1 ? vnd(v) : '—';
            if (k < 1) requestAnimationFrame(step);
        })(start);
    }

    function sliderToGrams(v) { return Math.round(5 * Math.pow(1000, v / 100)); }
    function gramsToSlider(g) { return clamp(Math.log(Math.max(g, 5) / 5) / Math.log(1000) * 100, 0, 100); }
    function paintRange(range) { range.style.setProperty('--p', range.value + '%'); }
    function readInt(input, def) { var n = parseInt(String(input.value).replace(/\D/g, ''), 10); return isNaN(n) ? def : n; }

    // Model weighing. The reader (studio-mesh.js) is only downloaded when a file is dropped.
    var FDM_FILLS = [[10, '10%'], [15, '15%'], [20, '20% (chuẩn)'], [30, '30%'], [50, '50%'], [100, '100% (đặc)']];
    var RESIN_FILLS = [[100, 'Đặc'], [0, 'Rỗng, vỏ 2 mm']];
    var FDM_WALL_MM = 1.2, RESIN_WALL_MM = 2;
    var MESH_EXT = /\.(stl|obj|3mf)$/i;

    function loadMesh(src) {
        if (window.TTMesh) return Promise.resolve(window.TTMesh);
        if (!loadMesh.p) {
            loadMesh.p = new Promise(function (resolve, reject) {
                var s = document.createElement('script');
                s.src = src; s.async = true;
                s.onload = function () { if (window.TTMesh) resolve(window.TTMesh); else reject(new Error('Không tải được bộ đọc file.')); };
                s.onerror = function () { loadMesh.p = null; reject(new Error('Không tải được bộ đọc file, hãy thử lại.')); };
                document.head.appendChild(s);
            });
        }
        return loadMesh.p;
    }

    // Slicer-like estimate: walls (surface × wall thickness) are solid, the inside is filled by the infill ratio.
    function estimate(mesh, scale, tech, mat, fill) {
        var s3 = scale[0] * scale[1] * scale[2];
        var vol = mesh.volume * s3, area = mesh.area * Math.pow(s3, 2 / 3);
        var shell = Math.min(vol, area * (tech.resin ? RESIN_WALL_MM : FDM_WALL_MM));
        var solid = tech.resin ? (fill >= 100 ? vol : shell) : shell + (vol - shell) * fill / 100;
        return { volume: vol, grams: solid * mat.density / 1000 };
    }

    function flash(el) {
        if (!el) return;
        el.classList.remove('is-flash');
        void el.offsetWidth; // restart the animation
        el.classList.add('is-flash');
        setTimeout(function () { el.classList.remove('is-flash'); }, 1600);
    }

    // ---------- "Drop a 3D file" entry points: hero drop zone, floating button, page-wide drop ----------

    function initDropEntry() {
        var calc = document.querySelector('[data-tt-config]');
        var fab = document.querySelector('[data-tt-dropfab]');
        var canWeigh = !!(calc && calc.ttAnalyze);

        function weigh(file) {
            if (!file || !canWeigh) return;
            calc.ttFocus();
            calc.ttAnalyze(file);
        }

        each('[data-tt-herodrop]', function (zone) {
            var input = zone.querySelector('input[type=file]');
            if (!canWeigh) {
                // No calculator on this page: the zone becomes a link to it.
                input.remove();
                zone.addEventListener('click', function () { if (fab) location.href = fab.href; });
                return;
            }
            ['dragenter', 'dragover'].forEach(function (t) { zone.addEventListener(t, function () { zone.classList.add('is-over'); }); });
            ['dragleave', 'drop'].forEach(function (t) { zone.addEventListener(t, function () { zone.classList.remove('is-over'); }); });
            input.addEventListener('change', function () { weigh(input.files && input.files[0]); input.value = ''; });
        });

        if (fab) {
            if (canWeigh) {
                fab.addEventListener('click', function (e) { e.preventDefault(); calc.ttFocus(); });
            }
            // Hidden while another drop zone is on screen; slides in shortly after the page opens.
            var visible = new Set();
            function toggleFab() { fab.classList.toggle('is-shown', visible.size === 0); }
            if ('IntersectionObserver' in window) {
                var io = new IntersectionObserver(function (entries) {
                    entries.forEach(function (en) { if (en.isIntersecting) visible.add(en.target); else visible.delete(en.target); });
                    toggleFab();
                }, { threshold: 0.25 });
                each('[data-tt-herodrop], .tt-meter-dropwrap', function (el) { io.observe(el); });
            }
            setTimeout(toggleFab, 900);
        }

        if (!canWeigh) return;

        // Flash the calculator's drop zone the first time it scrolls into view.
        var wrap = calc.querySelector('.tt-meter-dropwrap');
        if (wrap && 'IntersectionObserver' in window) {
            var seen = new IntersectionObserver(function (entries) {
                if (entries[0].isIntersecting) { flash(wrap); seen.disconnect(); }
            }, { threshold: 0.6 });
            seen.observe(wrap);
        }

        // Page-wide drop: dragging a file anywhere over the page shows a full-screen target.
        var overlay = document.createElement('div'), depth = 0;
        overlay.className = 'tt-dropover';
        overlay.innerHTML = '<div><span class="tt-dropover-icon"></span><b>Thả file vào đây</b><small>Tự đo kích thước, xem 3D và tính giá ngay</small></div>';
        overlay.querySelector('.tt-dropover-icon').innerHTML = (calc.querySelector('.tt-meter-icon') || {}).innerHTML || '';
        document.body.appendChild(overlay);

        function hasFiles(e) { return e.dataTransfer && Array.prototype.indexOf.call(e.dataTransfer.types || [], 'Files') >= 0; }
        window.addEventListener('dragenter', function (e) { if (!hasFiles(e)) return; depth++; overlay.classList.add('is-on'); });
        window.addEventListener('dragleave', function (e) { if (!hasFiles(e)) return; depth = Math.max(0, depth - 1); if (!depth) overlay.classList.remove('is-on'); });
        window.addEventListener('dragover', function (e) { if (hasFiles(e)) e.preventDefault(); });
        window.addEventListener('drop', function (e) {
            if (!hasFiles(e)) return;
            depth = 0; overlay.classList.remove('is-on');
            // The overlay ignores the pointer: drops on a real file field (quote form, drop zones) keep their normal behaviour.
            if (e.target.closest && e.target.closest('.tt-dropzone, [data-tt-drop], [data-tt-herodrop]')) return;
            e.preventDefault();
            weigh(e.dataTransfer.files && e.dataTransfer.files[0]);
        });
    }

    function fmtNum(v, digits) { return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: digits }).format(v); }
    function readFloat(input) { var n = parseFloat(String(input.value).replace(',', '.')); return isNaN(n) ? 0 : n; }

    function initConfigurator() {
        each('[data-tt-config]', function (root) {
            var techs;
            try { techs = JSON.parse(root.getAttribute('data-tt-config')); } catch (e) { return; }
            techs = (techs || []).filter(function (t) { return t.mats && t.mats.length; });
            if (!techs.length) return;

            var cards = root.querySelectorAll('.tt-tech[data-tech]');
            var chart = root.querySelector('[data-tt-chart]');
            var labels = root.querySelector('[data-tt-labels]');
            var nameEl = root.querySelector('[data-tt-techname]');
            var grams = root.querySelector('[data-tt-grams]');
            var qty = root.querySelector('[data-tt-qty]');
            var range = root.querySelector('[data-tt-range]');
            var total = root.querySelector('[data-tt-total]');
            var unit = root.querySelector('[data-tt-unit]');
            var cta = root.querySelector('[data-tt-cta]');
            var autoTag = root.querySelector('[data-tt-auto]');
            var active = 0, activeMat = 0;

            // Weighing state
            var meter = root.querySelector('[data-tt-meter]');
            var fileInput = root.querySelector('[data-tt-file]');
            var drop = root.querySelector('[data-tt-drop]');
            var body = meter.querySelector('[data-tt-meter-body]');
            var errorEl = root.querySelector('[data-tt-meter-error]');
            var dims = meter.querySelectorAll('[data-axis]');
            var lock = meter.querySelector('[data-tt-lock]');
            var unitSel = meter.querySelector('[data-tt-unitsel]');
            var fillSel = meter.querySelector('[data-tt-fill]');
            var stats = meter.querySelector('[data-tt-stats]');
            var viewer = root.querySelector('[data-tt-viewer]');
            var view = null, mesh = null, file = null, auto = false, measurement = '';
            var scale = [1, 1, 1], fills = { fdm: 20, resin: 100 };

            function tech() { return techs[active]; }
            function mat() { return tech().mats[activeMat] || tech().mats[0]; }
            function kind() { return tech().resin ? 'resin' : 'fdm'; }
            // Size in mm of the model as stored in the file (before the customer's scale).
            function baseSize(a) { return mesh.size[a] * (parseFloat(unitSel.value) || 1); }

            function drawChart() {
                var tiers = mat().tiers, max = Math.max.apply(null, tiers.map(function (t) { return t.price; }));
                chart.innerHTML = ''; labels.innerHTML = '';
                tiers.forEach(function (t) {
                    var col = document.createElement('div'); col.className = 'tt-bar';
                    col.innerHTML = '<b>' + money.format(t.price) + 'đ</b><i style="--h:0%"></i>';
                    chart.appendChild(col);
                    var lab = document.createElement('span'); lab.textContent = t.label; labels.appendChild(lab);
                    requestAnimationFrame(function () { requestAnimationFrame(function () { col.querySelector('i').style.setProperty('--h', Math.max(12, t.price / max * 100) + '%'); }); });
                });
                if (nameEl) nameEl.textContent = tech().name + ' · ' + mat().name;
            }

            function update() {
                var g = readInt(grams, 0), q = Math.max(readInt(qty, 1), 1), sum = g * q;
                var tiers = mat().tiers, idx = tierFor({ tiers: tiers }, sum), tier = tiers[idx];
                each('.tt-bar', function (b, i) { b.classList.toggle('is-active', i === idx); }, chart);
                countTo(total, sum * tier.price);
                if (unit) unit.textContent = money.format(tier.price) + 'đ/g · ' + tier.label + ' · tổng ' + weight(sum);
                if (cta) {
                    cta.href = cta.getAttribute('data-base') + '?tech=' + encodeURIComponent(tech().name) + '&mat=' + encodeURIComponent(mat().name)
                        + (g > 0 ? '&g=' + g : '') + '&q=' + q + '#tt-quote';
                }
            }

            function select(ti, mi) {
                var kindBefore = kind();
                active = ti; activeMat = mi;
                Array.prototype.forEach.call(cards, function (card, i) {
                    card.classList.toggle('is-active', i === ti);
                    each('.tt-tech-mat', function (b) {
                        var on = i === ti && parseInt(b.getAttribute('data-mat'), 10) === mi;
                        b.classList.toggle('is-active', on); b.setAttribute('aria-pressed', on ? 'true' : 'false');
                    }, card);
                });
                if (mesh && kindBefore !== kind()) { fillOptions(); paintView(); }
                drawChart();
                if (mesh && auto) weigh(); else update();
            }

            Array.prototype.forEach.call(cards, function (card) {
                var ti = parseInt(card.getAttribute('data-tech'), 10) || 0;
                each('button[data-mat]', function (btn) {
                    btn.addEventListener('click', function () {
                        var mi = btn.classList.contains('tt-tech-head') && ti === active ? activeMat : parseInt(btn.getAttribute('data-mat'), 10) || 0;
                        select(ti, mi);
                    });
                }, card);
            });

            [grams, qty].forEach(function (input) {
                input.addEventListener('input', function (e) {
                    input.value = String(input.value).replace(/\D/g, '').slice(0, 6);
                    if (input === grams) {
                        range.value = gramsToSlider(readInt(grams, 0)); paintRange(range);
                        if (e.isTrusted) setAuto(false);
                    }
                    update();
                });
            });

            each('[data-step]', function (btn) {
                btn.addEventListener('click', function () {
                    var input = root.querySelector(btn.getAttribute('data-target'));
                    var step = parseInt(btn.getAttribute('data-step'), 10);
                    var cur = readInt(input, 0);
                    if (input === grams) { step *= cur >= 1000 ? 100 : cur >= 100 ? 10 : 1; setAuto(false); }
                    input.value = Math.max(1, cur + step);
                    input.dispatchEvent(new Event('input'));
                });
            }, root);

            range.addEventListener('input', function () { grams.value = sliderToGrams(parseFloat(range.value)); paintRange(range); setAuto(false); update(); });

            // ----- weighing -----

            function setAuto(on) { auto = on; if (autoTag) autoTag.hidden = !on; }

            function showError(msg) { errorEl.textContent = msg || ''; errorEl.hidden = !msg; }

            function fillOptions() {
                var list = tech().resin ? RESIN_FILLS : FDM_FILLS, cur = fills[kind()];
                fillSel.innerHTML = list.map(function (o) { return '<option value="' + o[0] + '"' + (o[0] === cur ? ' selected' : '') + '>' + o[1] + '</option>'; }).join('');
                meter.querySelector('[data-tt-filllabel]').textContent = tech().resin ? 'Kiểu in' : 'Độ đặc (infill)';
            }

            // Preview in the card color of the technology: mint for FDM, lilac for resin.
            function paintView() {
                if (!view) return;
                view.color = tech().resin ? [201, 166, 247] : [128, 229, 203];
                view.request();
            }

            function writeDims(except) {
                Array.prototype.forEach.call(dims, function (input, a) {
                    if (a !== except) input.value = fmtNum(baseSize(a) * scale[a], 1);
                });
            }

            function weigh() {
                var s = [0, 1, 2].map(function (a) { return scale[a] * (parseFloat(unitSel.value) || 1); });
                var r = estimate(mesh, s, tech(), mat(), fills[kind()]);
                var g = Math.max(1, Math.round(r.grams));
                var size = [0, 1, 2].map(function (a) { return fmtNum(baseSize(a) * scale[a], 1); }).join(' × ') + ' mm';
                var pct = Math.round(scale[0] * 100), uniform = Math.abs(scale[0] - scale[1]) < 1e-6 && Math.abs(scale[1] - scale[2]) < 1e-6;
                var fillText = fillSel.options[fillSel.selectedIndex] ? fillSel.options[fillSel.selectedIndex].text : '';

                var chips = [
                    '<span>Thể tích ' + fmtNum(r.volume / 1000, 1) + ' cm³</span>',
                    '<span>' + (uniform ? 'Tỉ lệ ' + pct + '%' : 'Tỉ lệ tự do') + '</span>',
                    '<span class="is-key">≈ ' + fmtNum(g, 0) + ' g ' + mat().name + '</span>'
                ];
                var maxDim = Math.max.apply(null, [0, 1, 2].map(function (a) { return baseSize(a) * scale[a]; }));
                if (maxDim < 3) chips.push('<span class="is-warn">Mô hình rất nhỏ — file có thể dùng đơn vị cm/m, hãy chọn lại đơn vị.</span>');
                if (maxDim > 1000) chips.push('<span class="is-warn">Mô hình lớn hơn 1 m — cần cắt ghép, studio sẽ tư vấn.</span>');
                stats.innerHTML = chips.join('');
                viewer.querySelector('[data-tt-vsize]').textContent = size;

                measurement = size + ' · ' + fmtNum(r.volume / 1000, 1) + ' cm³ · ' + tech().name + ' ' + mat().name + ' ' + fillText
                    + ' ≈ ' + fmtNum(g, 0) + ' g/cái' + (file ? ' (' + file.name + ')' : '');

                grams.value = g;
                range.value = gramsToSlider(g); paintRange(range);
                setAuto(true);
                update();
            }

            function analyze(f) {
                if (!f) return Promise.resolve(false);
                if (!MESH_EXT.test(f.name)) {
                    showError('Tự cân hỗ trợ STL, OBJ, 3MF. File này vẫn gửi báo giá được bình thường.');
                    return Promise.resolve(false);
                }
                showError('');
                drop.classList.add('is-busy');
                drop.querySelector('b').textContent = 'Đang cân ' + f.name + '…';
                return loadMesh(root.getAttribute('data-tt-mesh-src')).then(function (M) {
                    return M.read(f).then(function (m) {
                        mesh = m; file = f; scale = [1, 1, 1];
                        unitSel.value = '1';
                        body.hidden = false;
                        meter.querySelector('[data-tt-fname]').textContent = f.name;
                        fillOptions(); writeDims(-1);
                        // Unhide first: the canvas needs its layout size before the first draw.
                        viewer.hidden = false;
                        viewer.querySelector('[data-tt-vname]').textContent = f.name;
                        if (!view) view = new M.View(viewer.querySelector('[data-tt-view]'));
                        view.set(m);
                        paintView();
                        weigh();
                        return true;
                    });
                }).catch(function (err) {
                    showError(err && err.message ? err.message : 'Không đọc được file này.');
                    return false;
                }).then(function (ok) {
                    drop.classList.remove('is-busy');
                    drop.querySelector('b').textContent = mesh ? 'Thả file khác để cân lại' : 'Thả file 3D vào đây để tính giá';
                    drop.classList.toggle('is-loaded', !!mesh);
                    drop.parentNode.classList.toggle('is-loaded', !!mesh);
                    return ok;
                });
            }

            // Entry points elsewhere on the page (hero drop zone, floating button, page-wide drop) use these.
            root.ttAnalyze = analyze;
            root.ttFocus = function () {
                root.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });
                flash(drop.parentNode);
            };

            fileInput.addEventListener('change', function () { analyze(fileInput.files && fileInput.files[0]); });
            ['dragenter', 'dragover'].forEach(function (t) { drop.addEventListener(t, function () { drop.classList.add('is-over'); }); });
            ['dragleave', 'drop'].forEach(function (t) { drop.addEventListener(t, function () { drop.classList.remove('is-over'); }); });
            meter.querySelector('[data-tt-change]').addEventListener('click', function () { fileInput.click(); });
            viewer.querySelector('[data-tt-vreset]').addEventListener('click', function () { if (view) view.reset(); });

            Array.prototype.forEach.call(dims, function (input, a) {
                input.addEventListener('input', function () {
                    input.value = String(input.value).replace(/[^\d.,]/g, '').slice(0, 8);
                    var v = readFloat(input), base = baseSize(a);
                    if (!mesh || v <= 0 || base <= 0) return;
                    if (lock.checked) {
                        var k = v / base;
                        scale = [k, k, k];
                        writeDims(a);
                    } else {
                        scale[a] = v / base;
                    }
                    weigh();
                });
                input.addEventListener('blur', function () { if (mesh) writeDims(-1); });
            });

            lock.addEventListener('change', function () {
                if (lock.checked && mesh) { scale = [scale[0], scale[0], scale[0]]; writeDims(-1); weigh(); }
            });
            unitSel.addEventListener('change', function () { if (mesh) { scale = [1, 1, 1]; writeDims(-1); weigh(); } });
            fillSel.addEventListener('change', function () { fills[kind()] = parseInt(fillSel.value, 10); if (mesh) weigh(); });

            // ----- hand over to the quote form on the same page -----

            function fillQuote(form, withFile) {
                var t = tech(), m = mat();
                each('input[name="Form.Technology"]', function (r) { r.checked = r.value.toLowerCase() === t.name.toLowerCase(); }, form);
                var matInput = form.querySelector('[name="Form.Material"]');
                if (matInput && (!matInput.value || matInput.getAttribute('data-auto') === matInput.value)) {
                    matInput.value = m.name; matInput.setAttribute('data-auto', m.name);
                }
                var q = form.querySelector('[name="Form.Quantity"]'); if (q) q.value = Math.max(readInt(qty, 1), 1);
                var g = form.querySelector('[name="Form.EstimatedGrams"]'); if (g && readInt(grams, 0) > 0) g.value = readInt(grams, 0);
                var hidden = form.querySelector('[name="Form.Measurement"]'); if (hidden) hidden.value = mesh ? measurement : '';

                var input = form.querySelector('input[type=file][name=modelFile]');
                if (withFile && file && input && (!input.files || input.files[0] !== file)) {
                    try {
                        var dt = new DataTransfer(); dt.items.add(file);
                        input.files = dt.files;
                        input.dispatchEvent(new Event('change'));
                    } catch (e) { /* old browsers: the customer attaches the file again */ }
                }
            }

            var quoteForm = document.querySelector('form[data-tt-quote]');
            if (cta && quoteForm) {
                cta.addEventListener('click', function (e) {
                    e.preventDefault();
                    fillQuote(quoteForm, true);
                    var target = document.getElementById('tt-quote') || quoteForm;
                    target.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });
                });

                // A model picked in the quote form is weighed with the technology chosen there.
                var formFile = quoteForm.querySelector('input[type=file][name=modelFile]');
                if (formFile) {
                    formFile.addEventListener('change', function () {
                        var f = formFile.files && formFile.files[0];
                        if (!f || f === file || !MESH_EXT.test(f.name)) return;
                        var checked = quoteForm.querySelector('input[name="Form.Technology"]:checked');
                        techs.forEach(function (t, i) { if (checked && t.name.toLowerCase() === checked.value.toLowerCase() && i !== active) select(i, 0); });
                        analyze(f).then(function (ok) { if (ok) fillQuote(quoteForm, false); });
                    });
                }
            }

            // Preselect technology/material from the URL (?tech=Resin&mat=Standard).
            var params = new URLSearchParams(location.search), pt = (params.get('tech') || '').toLowerCase(), pm = (params.get('mat') || '').toLowerCase();
            techs.forEach(function (t, i) {
                if (t.name.toLowerCase() !== pt) return;
                var mi = 0;
                t.mats.forEach(function (m, j) { if (m.name.toLowerCase() === pm) mi = j; });
                active = i; activeMat = mi;
            });
            if (active || activeMat) select(active, activeMat);

            range.value = gramsToSlider(readInt(grams, 0)); paintRange(range);
            drawChart(); update();
        });

        each('[data-tt-mini]', function (root) {
            var techs;
            try { techs = JSON.parse(root.getAttribute('data-tt-mini')); } catch (e) { return; }
            if (!techs || !techs.length) return;
            var chips = root.querySelectorAll('[data-tech]');
            var range = root.querySelector('[data-tt-range]');
            var total = root.querySelector('[data-tt-total]');
            var unit = root.querySelector('[data-tt-unit]');
            var active = 0;

            function update() {
                var g = sliderToGrams(parseFloat(range.value)), tech = techs[active], tier = tech.tiers[tierFor(tech, g)];
                paintRange(range);
                countTo(total, g * tier.price);
                unit.textContent = weight(g) + ' · ' + money.format(tier.price) + 'đ/g';
            }

            Array.prototype.forEach.call(chips, function (chip) {
                chip.addEventListener('click', function (e) {
                    e.preventDefault(); e.stopPropagation();
                    active = parseInt(chip.getAttribute('data-tech'), 10) || 0;
                    Array.prototype.forEach.call(chips, function (c) { c.classList.toggle('is-active', c === chip); });
                    update();
                });
            });
            range.addEventListener('input', update);
            update();
        });
    }

    // ---------- Terminal typing (addon bento card) ----------

    function initTerminal() {
        each('[data-tt-term]', function (el) {
            var lines;
            try { lines = JSON.parse(el.getAttribute('data-tt-term')); } catch (e) { return; }
            var fmt = function (s) { return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/\[ok\]/g, '<span class="ok">✔</span>'); };
            if (reduceMotion) { el.innerHTML = lines.map(fmt).join('\n'); return; }
            var started = false;
            function run() {
                if (started) return; started = true;
                var li = 0, ci = 0, out = '';
                (function type() {
                    if (li >= lines.length) {
                        el.innerHTML = out + '<span class="caret"></span>';
                        setTimeout(function () { out = ''; li = 0; ci = 0; type(); }, 3500);
                        return;
                    }
                    var line = lines[li];
                    if (ci <= line.length) {
                        el.innerHTML = out + fmt(line.slice(0, ci)) + '<span class="caret"></span>';
                        ci++; setTimeout(type, 22);
                    } else {
                        out += fmt(line) + '\n'; li++; ci = 0; setTimeout(type, 260);
                    }
                })();
            }
            if ('IntersectionObserver' in window) {
                var io = new IntersectionObserver(function (en) { if (en[0].isIntersecting) { run(); io.disconnect(); } });
                io.observe(el);
            } else { run(); }
        });
    }

    // ---------- FAQ smooth accordion ----------

    function initFaq() {
        each('.tt-faq details', function (d) {
            var summary = d.querySelector('summary'), body = d.querySelector('.tt-faq-a');
            if (!summary || !body || reduceMotion) return;
            summary.addEventListener('click', function (e) {
                e.preventDefault();
                if (d.open) {
                    body.style.height = body.scrollHeight + 'px';
                    requestAnimationFrame(function () { requestAnimationFrame(function () { body.style.height = '0px'; }); });
                    setTimeout(function () { d.open = false; body.style.height = ''; }, 500);
                } else {
                    d.open = true;
                    var h = body.scrollHeight; body.style.height = '0px';
                    requestAnimationFrame(function () { requestAnimationFrame(function () { body.style.height = h + 'px'; }); });
                    setTimeout(function () { body.style.height = ''; }, 520);
                }
            });
        });
    }

    // ---------- Shop filter ----------

    function initFilter() {
        each('[data-tt-filter]', function (bar) {
            var grid = document.querySelector(bar.getAttribute('data-tt-filter'));
            if (!grid) return;
            var chips = bar.querySelectorAll('[data-cat]');
            Array.prototype.forEach.call(chips, function (chip) {
                chip.addEventListener('click', function () {
                    var cat = chip.getAttribute('data-cat');
                    Array.prototype.forEach.call(chips, function (c) { c.classList.toggle('is-active', c === chip); });
                    each('[data-cat]', function (card) {
                        card.classList.toggle('is-hidden', cat !== 'all' && card.getAttribute('data-cat') !== cat);
                    }, grid);
                });
            });
        });
    }

    // ---------- Quote form ----------

    function initQuote() {
        each('.tt-dropzone', function (zone) {
            var input = zone.querySelector('input[type=file]'), nameEl = zone.querySelector('.tt-file-name');
            if (!input) return;
            ['dragenter', 'dragover'].forEach(function (t) { zone.addEventListener(t, function () { zone.classList.add('is-over'); }); });
            ['dragleave', 'drop'].forEach(function (t) { zone.addEventListener(t, function () { zone.classList.remove('is-over'); }); });
            input.addEventListener('change', function () {
                var f = input.files && input.files[0];
                if (!nameEl) return;
                if (!f) { nameEl.textContent = ''; return; }
                var max = parseInt(zone.getAttribute('data-max-mb'), 10) || 100, mb = f.size / 1048576;
                nameEl.textContent = f.name + ' · ' + (mb < 1 ? Math.max(1, Math.round(mb * 1024)) + ' KB' : mb.toFixed(1) + ' MB');
                nameEl.style.color = mb > max ? '#ff6b85' : '';
                if (mb > max) nameEl.textContent += ' — vượt quá ' + max + ' MB, hãy gửi link tải';
            });
        });

        each('input[data-numeric]', function (input) {
            input.addEventListener('input', function () { input.value = String(input.value).replace(/\D/g, '').slice(0, 7); });
        });

        each('form[data-tt-quote]', function (form) {
            form.addEventListener('submit', function () {
                var btn = form.querySelector('button[type=submit]');
                if (btn && (!window.jQuery || !jQuery(form).valid || jQuery(form).valid())) {
                    btn.disabled = true;
                    var s = btn.querySelector('span'); if (s) s.textContent = 'Đang gửi…';
                }
            });
        });
    }

    onReady(function () {
        initScrollChrome();
        initSearch();
        initReveal();
        initPointerFx();
        initRotator();
        init3D();
        initMarquee();
        initHero();
        initProcess();
        initConfigurator();
        initDropEntry();
        initTerminal();
        initFaq();
        initFilter();
        initQuote();
    });
})();

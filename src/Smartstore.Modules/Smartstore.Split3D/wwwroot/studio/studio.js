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
    var RESIN_FILLS = [[0, 'Rỗng, vỏ 2 mm'], [100, 'Đặc']];
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
    // tech.factor calibrates the result against real weights (Admin → Studio settings).
    function estimate(mesh, scale, tech, mat, fill) {
        var s3 = scale[0] * scale[1] * scale[2];
        var vol = mesh.volume * s3, area = mesh.area * Math.pow(s3, 2 / 3);
        var shell = Math.min(vol, area * (tech.resin ? RESIN_WALL_MM : FDM_WALL_MM));
        var solid = tech.resin ? (fill >= 100 ? vol : shell) : shell + (vol - shell) * fill / 100;
        return { volume: vol, grams: solid * mat.density / 1000 * (tech.factor || 1) };
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

        function weigh(files) {
            if (!files || !files.length || !canWeigh) return;
            calc.ttFocus();
            calc.ttAnalyze(files);
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
            input.addEventListener('change', function () { weigh(Array.prototype.slice.call(input.files || [])); input.value = ''; });
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
            weigh(e.dataTransfer.files);
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

            // Weighing state. Every dropped model is an entry of `models`; dims, viewer and the grams / quantity
            // fields edit the active one (`cur`). The total weight of all models picks the price tier.
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
            var list = meter.querySelector('[data-tt-models]');
            var listRows = meter.querySelector('[data-tt-mlist]');
            var listSum = meter.querySelector('[data-tt-msum]');
            var listBody = meter.querySelector('[data-tt-mbody]');
            var listToggle = meter.querySelector('[data-tt-mtoggle]');
            var listAll = meter.querySelector('[data-tt-mall]');
            var listTotal = meter.querySelector('[data-tt-mtotal]');
            var tierNow = null;
            var viewer = root.querySelector('[data-tt-viewer]');
            var view = null, models = [], cur = null, auto = false;
            var fills = { fdm: 20, resin: 0 };

            function tech() { return techs[active]; }
            function mat() { return tech().mats[activeMat] || tech().mats[0]; }
            function kind() { return tech().resin ? 'resin' : 'fdm'; }
            // Size in mm of a model as stored in the file (before the customer's scale).
            function baseSize(m, a) { return m.mesh.size[a] * m.unit; }
            function sizeOf(m, a) { return baseSize(m, a) * m.scale[a]; }

            function autoWeight(m) {
                var r = estimate(m.mesh, [0, 1, 2].map(function (a) { return m.scale[a] * m.unit; }), tech(), mat(), fills[kind()]);
                return { volume: r.volume, grams: Math.max(1, Math.round(r.grams)) };
            }
            function gramsOf(m) { return m.manual != null ? m.manual : autoWeight(m).grams; }

            // Models with the tick removed stay in the list but are left out of the total.
            function included() { return models.filter(function (m) { return m.on; }); }

            function totals() {
                if (!models.length) {
                    var q = Math.max(readInt(qty, 1), 1);
                    return { grams: readInt(grams, 0) * q, pieces: q };
                }
                return included().reduce(function (t, m) { t.grams += gramsOf(m) * m.qty; t.pieces += m.qty; return t; }, { grams: 0, pieces: 0 });
            }

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
                // The fields belong to the active model.
                if (cur) {
                    if (!auto) cur.manual = Math.max(readInt(grams, 0), 0);
                    cur.qty = Math.max(readInt(qty, 1), 1);
                }
                var t = totals(), sum = t.grams;
                var tiers = mat().tiers, idx = tierFor({ tiers: tiers }, sum), tier = tiers[idx];
                tierNow = tier;
                each('.tt-bar', function (b, i) { b.classList.toggle('is-active', i === idx); }, chart);
                countTo(total, sum * tier.price);
                if (unit) {
                    unit.textContent = models.length && !t.pieces
                        ? 'Chưa tích file nào để tính'
                        : money.format(tier.price) + 'đ/g · ' + tier.label + ' · tổng ' + weight(sum)
                            + (models.length > 1 ? ' · ' + included().length + '/' + models.length + ' mô hình, ' + t.pieces + ' cái' : '');
                }
                orderState();
                if (cta) {
                    var g = t.pieces ? Math.round(sum / t.pieces) : 0;
                    cta.href = cta.getAttribute('data-base') + '?tech=' + encodeURIComponent(tech().name) + '&mat=' + encodeURIComponent(mat().name)
                        + (g > 0 ? '&g=' + g : '') + '&q=' + t.pieces + '#tt-quote';
                }
                renderList();
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
                if (cur && kindBefore !== kind()) { fillOptions(); paintView(); }
                drawChart();
                if (cur) refresh(); else update();
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
                    var val = readInt(input, 0);
                    if (input === grams) { step *= val >= 1000 ? 100 : val >= 100 ? 10 : 1; setAuto(false); }
                    input.value = Math.max(1, val + step);
                    input.dispatchEvent(new Event('input'));
                });
            }, root);

            range.addEventListener('input', function () { grams.value = sliderToGrams(parseFloat(range.value)); paintRange(range); setAuto(false); update(); });

            // ----- weighing -----

            function setAuto(on) { auto = on; if (autoTag) autoTag.hidden = !on; }

            function showError(msg) { errorEl.textContent = msg || ''; errorEl.hidden = !msg; }

            function fillOptions() {
                var opts = tech().resin ? RESIN_FILLS : FDM_FILLS, val = fills[kind()];
                fillSel.innerHTML = opts.map(function (o) { return '<option value="' + o[0] + '"' + (o[0] === val ? ' selected' : '') + '>' + o[1] + '</option>'; }).join('');
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
                    if (a !== except) input.value = fmtNum(sizeOf(cur, a), 1);
                });
            }

            function sizeText(m) { return [0, 1, 2].map(function (a) { return fmtNum(sizeOf(m, a), 1); }).join(' × ') + ' mm'; }

            // One line per model for the quote form ("Đo từ file").
            function describe(m) {
                var r = autoWeight(m), fillText = fillSel.options[fillSel.selectedIndex] ? fillSel.options[fillSel.selectedIndex].text : '';
                return m.file.name + ': ' + sizeText(m) + ' · ' + fmtNum(r.volume / 1000, 1) + ' cm³ · ' + tech().name + ' ' + mat().name + ' ' + fillText
                    + ' ≈ ' + fmtNum(gramsOf(m), 0) + ' g/cái × ' + m.qty;
            }

            function esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;'); }

            function priceOf(m) { return Math.round(gramsOf(m) * m.qty * (tierNow ? tierNow.price : 0)); }

            function renderList() {
                if (!list) return;
                list.hidden = !models.length;
                if (!models.length) return;
                var t = totals(), on = included();
                listSum.textContent = on.length + '/' + models.length + ' file · ' + t.pieces + ' cái · ' + weight(t.grams)
                    + ' · ' + money.format(Math.round(t.grams * (tierNow ? tierNow.price : 0))) + 'đ';
                listAll.checked = on.length === models.length;
                listAll.indeterminate = on.length > 0 && on.length < models.length;

                // Typing in a quantity box re-renders the rows; keep the caret there.
                var focused = document.activeElement && listRows.contains(document.activeElement) ? document.activeElement.getAttribute('data-mqty') : null;
                listRows.innerHTML = models.map(function (m, i) {
                    return '<li class="' + (m === cur ? 'is-active' : '') + (m.on ? '' : ' is-off') + '">'
                        + '<label class="tt-model-check" title="' + (m.on ? 'Bỏ tích để không tính file này' : 'Tích để tính file này') + '">'
                        + '<input type="checkbox" data-on="' + i + '"' + (m.on ? ' checked' : '') + ' aria-label="Tính ' + esc(m.file.name) + '" /><span aria-hidden="true"></span></label>'
                        + '<button type="button" class="tt-model-pick" data-pick="' + i + '" aria-pressed="' + (m === cur) + '"><b>' + esc(m.file.name) + '</b>'
                        + '<small>' + fmtNum(gramsOf(m), 0) + ' g/cái</small></button>'
                        + '<span class="tt-model-qty"><button type="button" data-mstep="-1" data-i="' + i + '" aria-label="Giảm">−</button>'
                        + '<input type="text" inputmode="numeric" maxlength="4" value="' + m.qty + '" data-mqty="' + i + '" aria-label="Số lượng ' + esc(m.file.name) + '" />'
                        + '<button type="button" data-mstep="1" data-i="' + i + '" aria-label="Tăng">+</button></span>'
                        + '<span class="tt-model-price">' + (m.on ? money.format(priceOf(m)) + 'đ' : 'không tính') + '</span>'
                        + '<button type="button" class="tt-model-del" data-del="' + i + '" aria-label="Bỏ ' + esc(m.file.name) + '">×</button></li>';
                }).join('');
                if (focused != null) {
                    var inp = listRows.querySelector('[data-mqty="' + focused + '"]');
                    if (inp) { inp.focus(); inp.setSelectionRange(inp.value.length, inp.value.length); }
                }
                renderTotal(t, on);
            }

            // "Tổng" tab: the ticked models with their amounts, then weight, price tier and total.
            function renderTotal(t, on) {
                var price = tierNow ? tierNow.price : 0;
                listTotal.innerHTML = '<table class="tt-models-sumtable"><thead><tr><th>File</th><th>SL</th><th>Khối lượng</th><th>Thành tiền</th></tr></thead><tbody>'
                    + (on.length ? on.map(function (m) {
                        return '<tr><td>' + esc(m.file.name) + '</td><td>' + m.qty + '</td><td>' + weight(gramsOf(m) * m.qty) + '</td><td>' + money.format(priceOf(m)) + 'đ</td></tr>';
                    }).join('') : '<tr><td colspan="4">Chưa tích file nào.</td></tr>')
                    + '</tbody><tfoot><tr><td>Tổng ' + on.length + '/' + models.length + ' file</td><td>' + t.pieces + '</td><td>' + weight(t.grams) + '</td>'
                    + '<td>' + money.format(Math.round(t.grams * price)) + 'đ</td></tr></tfoot></table>'
                    + (tierNow ? '<p>Bậc giá <b>' + esc(tierNow.label) + '</b> · ' + money.format(price) + 'đ/g · ' + esc(tech().name + ' ' + mat().name)
                        + ' — tổng khối lượng các file được tích quyết định bậc giá chung.</p>' : '');
            }

            function showTab(name) {
                each('[data-tt-mtab]', function (b) { b.setAttribute('aria-selected', b.getAttribute('data-tt-mtab') === name ? 'true' : 'false'); }, list);
                listRows.hidden = name !== 'list';
                listTotal.hidden = name !== 'sum';
            }

            // Redraws the info of the active model; the grams field follows the estimate unless typed in by hand.
            function refresh() {
                var r = autoWeight(cur), g = gramsOf(cur);
                var pct = Math.round(cur.scale[0] * 100), uniform = Math.abs(cur.scale[0] - cur.scale[1]) < 1e-6 && Math.abs(cur.scale[1] - cur.scale[2]) < 1e-6;
                var chips = [
                    '<span>Thể tích ' + fmtNum(r.volume / 1000, 1) + ' cm³</span>',
                    '<span>' + (uniform ? 'Tỉ lệ ' + pct + '%' : 'Tỉ lệ tự do') + '</span>',
                    '<span class="is-key">≈ ' + fmtNum(r.grams, 0) + ' g ' + mat().name + '</span>'
                ];
                var maxDim = Math.max.apply(null, [0, 1, 2].map(function (a) { return sizeOf(cur, a); }));
                if (maxDim < 3) chips.push('<span class="is-warn">Mô hình rất nhỏ — file có thể dùng đơn vị cm/m, hãy chọn lại đơn vị.</span>');
                if (maxDim > 1000) chips.push('<span class="is-warn">Mô hình lớn hơn 1 m — cần cắt ghép, studio sẽ tư vấn.</span>');
                stats.innerHTML = chips.join('');
                viewer.querySelector('[data-tt-vsize]').textContent = sizeText(cur);

                if (auto) grams.value = g;
                range.value = gramsToSlider(readInt(grams, 0)); paintRange(range);
                update();
            }

            // Size / unit / infill changed: the active model is weighed again.
            function weigh() { cur.manual = null; setAuto(true); refresh(); }

            function activate(m) {
                cur = m;
                body.hidden = false;
                meter.querySelector('[data-tt-fname]').textContent = m.file.name;
                unitSel.value = String(m.unit);
                fillOptions(); writeDims(-1);
                // Unhide first: the canvas needs its layout size before the first draw.
                viewer.hidden = false;
                viewer.querySelector('[data-tt-vname]').textContent = m.file.name;
                if (!view) view = new window.TTMesh.View(viewer.querySelector('[data-tt-view]'));
                view.set(m.mesh);
                paintView();
                qty.value = m.qty;
                setAuto(m.manual == null);
                if (!auto) grams.value = m.manual;
                refresh();
            }

            function dropState() {
                drop.classList.remove('is-busy');
                drop.querySelector('b').textContent = models.length ? 'Thả thêm file để tính tổng' : 'Thả file 3D vào đây để tính giá';
                drop.classList.toggle('is-loaded', !!models.length);
                drop.parentNode.classList.toggle('is-loaded', !!models.length);
            }

            function removeModel(i) {
                var gone = models.splice(i, 1)[0];
                if (gone !== cur) { update(); return; }
                if (models.length) { activate(models[Math.min(i, models.length - 1)]); }
                else {
                    cur = null;
                    body.hidden = true; viewer.hidden = true;
                    if (view) view.stopSpin();
                    stats.innerHTML = '';
                    setAuto(false);
                    update();
                }
                dropState();
            }

            function sameFile(a, b) { return a.name === b.name && a.size === b.size && a.lastModified === b.lastModified; }

            // Reads the dropped files one after another and adds them to the list; the last one read becomes active.
            function analyze(files) {
                files = Array.prototype.slice.call(files || []).filter(Boolean);
                if (!files.length) return Promise.resolve(false);
                var good = files.filter(function (f) { return MESH_EXT.test(f.name); });
                var errors = files.filter(function (f) { return !MESH_EXT.test(f.name); }).map(function (f) {
                    return f.name + ': tự cân hỗ trợ STL, OBJ, 3MF. File này vẫn gửi báo giá được bình thường.';
                });
                showError(errors.join(' '));
                if (!good.length) return Promise.resolve(false);

                drop.classList.add('is-busy');
                return loadMesh(root.getAttribute('data-tt-mesh-src')).then(function (M) {
                    return good.reduce(function (p, f, i) {
                        return p.then(function (last) {
                            var known = models.filter(function (m) { return sameFile(m.file, f); })[0];
                            if (known) return known;
                            drop.querySelector('b').textContent = 'Đang cân ' + f.name + (good.length > 1 ? ' (' + (i + 1) + '/' + good.length + ')' : '') + '…';
                            return M.read(f).then(function (mesh) {
                                // The first model keeps the quantity already typed in.
                                var m = { file: f, mesh: mesh, on: true, scale: [1, 1, 1], unit: 1, manual: null, qty: models.length ? 1 : Math.max(readInt(qty, 1), 1) };
                                models.push(m);
                                return m;
                            }, function (err) {
                                errors.push(f.name + ': ' + (err && err.message ? err.message : 'không đọc được file này.'));
                                return last;
                            });
                        });
                    }, Promise.resolve(null));
                }).then(function (last) {
                    showError(errors.join(' '));
                    if (last) activate(last);
                    return !!last;
                }, function (err) {
                    showError(err && err.message ? err.message : 'Không đọc được file này.');
                    return false;
                }).then(function (ok) {
                    dropState();
                    return ok;
                });
            }

            // Entry points elsewhere on the page (hero drop zone, floating button, page-wide drop) use these.
            root.ttAnalyze = analyze;
            root.ttFocus = function () {
                root.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });
                flash(drop.parentNode);
            };

            fileInput.addEventListener('change', function () { analyze(fileInput.files).then(function () { fileInput.value = ''; }); });
            ['dragenter', 'dragover'].forEach(function (t) { drop.addEventListener(t, function () { drop.classList.add('is-over'); }); });
            ['dragleave', 'drop'].forEach(function (t) { drop.addEventListener(t, function () { drop.classList.remove('is-over'); }); });
            meter.querySelector('[data-tt-change]').addEventListener('click', function () { fileInput.click(); });
            // Quantity of one model: the fields above follow when it is the active one.
            function setQty(m, value) {
                m.qty = Math.max(1, Math.min(9999, value || 1));
                if (m === cur) qty.value = m.qty;
                update();
            }
            listRows.addEventListener('click', function (e) {
                var del = e.target.closest('[data-del]'), pick = e.target.closest('[data-pick]'), step = e.target.closest('[data-mstep]');
                if (del) removeModel(parseInt(del.getAttribute('data-del'), 10));
                else if (step) { var sm = models[parseInt(step.getAttribute('data-i'), 10)]; if (sm) setQty(sm, sm.qty + parseInt(step.getAttribute('data-mstep'), 10)); }
                else if (pick) { var m = models[parseInt(pick.getAttribute('data-pick'), 10)]; if (m && m !== cur) activate(m); }
            });
            listRows.addEventListener('change', function (e) {
                if (!e.target.matches('[data-on]')) return;
                var m = models[parseInt(e.target.getAttribute('data-on'), 10)];
                if (m) { m.on = e.target.checked; update(); }
            });
            listRows.addEventListener('input', function (e) {
                if (!e.target.matches('[data-mqty]')) return;
                e.target.value = e.target.value.replace(/\D/g, '');
                var m = models[parseInt(e.target.getAttribute('data-mqty'), 10)];
                if (m && e.target.value) setQty(m, parseInt(e.target.value, 10));
            });
            listRows.addEventListener('focusout', function (e) {
                if (e.target.matches('[data-mqty]') && !e.target.value) update();
            });
            listAll.addEventListener('change', function () {
                models.forEach(function (m) { m.on = listAll.checked; });
                update();
            });
            each('[data-tt-mtab]', function (b) { b.addEventListener('click', function () { showTab(b.getAttribute('data-tt-mtab')); }); }, list);
            listToggle.addEventListener('click', function () {
                var open = listToggle.getAttribute('aria-expanded') !== 'true';
                listToggle.setAttribute('aria-expanded', open ? 'true' : 'false');
                listBody.hidden = !open;
                list.classList.toggle('is-collapsed', !open);
            });

            // Viewer: reset, and a full-screen mode to look at details (Esc or the button closes it).
            var maxBtn = viewer.querySelector('[data-tt-vmax]');
            function maximize(on) {
                viewer.classList.toggle('is-max', on);
                document.documentElement.classList.toggle('tt-noscroll', on);
                if (maxBtn) { maxBtn.setAttribute('aria-pressed', on ? 'true' : 'false'); maxBtn.querySelector('span').textContent = on ? 'Thu nhỏ' : 'Phóng to'; }
                if (view) view.request();
            }
            if (maxBtn) maxBtn.addEventListener('click', function () { maximize(!viewer.classList.contains('is-max')); });
            document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && viewer.classList.contains('is-max')) maximize(false); });
            viewer.querySelector('[data-tt-vreset]').addEventListener('click', function () { if (view) view.reset(); });

            Array.prototype.forEach.call(dims, function (input, a) {
                input.addEventListener('input', function () {
                    input.value = String(input.value).replace(/[^\d.,]/g, '').slice(0, 8);
                    if (!cur) return;
                    var v = readFloat(input), base = baseSize(cur, a);
                    if (v <= 0 || base <= 0) return;
                    if (lock.checked) {
                        var k = v / base;
                        cur.scale = [k, k, k];
                        writeDims(a);
                    } else {
                        cur.scale[a] = v / base;
                    }
                    weigh();
                });
                input.addEventListener('blur', function () { if (cur) writeDims(-1); });
            });

            lock.addEventListener('change', function () {
                if (lock.checked && cur) { cur.scale = [cur.scale[0], cur.scale[0], cur.scale[0]]; writeDims(-1); weigh(); }
            });
            unitSel.addEventListener('change', function () { if (cur) { cur.unit = parseFloat(unitSel.value) || 1; cur.scale = [1, 1, 1]; writeDims(-1); weigh(); } });
            fillSel.addEventListener('change', function () { fills[kind()] = parseInt(fillSel.value, 10); if (cur) weigh(); });

            // ----- hand over to the quote form on the same page -----

            function fillQuote(form, withFiles) {
                var t = tech(), m = mat(), sum = totals();
                each('input[name="Form.Technology"]', function (r) { r.checked = r.value.toLowerCase() === t.name.toLowerCase(); }, form);
                var matInput = form.querySelector('[name="Form.Material"]');
                if (matInput && (!matInput.value || matInput.getAttribute('data-auto') === matInput.value)) {
                    matInput.value = m.name; matInput.setAttribute('data-auto', m.name);
                }
                // Several models: total pieces, and the average weight so that grams × quantity is the total.
                var q = form.querySelector('[name="Form.Quantity"]'); if (q) q.value = sum.pieces;
                var g = form.querySelector('[name="Form.EstimatedGrams"]'); if (g && sum.grams > 0) g.value = Math.round(sum.grams / sum.pieces);
                var on = included();
                var hidden = form.querySelector('[name="Form.Measurement"]'); if (hidden) hidden.value = on.map(describe).join('\n');

                var input = form.querySelector('input[type=file][name=modelFile]');
                if (!withFiles || !on.length || !input) return;
                var have = Array.prototype.slice.call(input.files || []);
                var add = on.filter(function (x) { return !have.some(function (f) { return sameFile(f, x.file); }); });
                if (!add.length) return;
                try {
                    var dt = new DataTransfer();
                    (input.multiple ? have : []).forEach(function (f) { dt.items.add(f); });
                    (input.multiple ? add : add.slice(-1)).forEach(function (x) { dt.items.add(x.file); });
                    input.files = dt.files;
                    input.dispatchEvent(new Event('change'));
                } catch (e) { /* old browsers: the customer attaches the files again */ }
            }

            var quoteForm = document.querySelector('form[data-tt-quote]');
            if (cta && quoteForm) {
                cta.addEventListener('click', function (e) {
                    e.preventDefault();
                    fillQuote(quoteForm, true);
                    var target = document.getElementById('tt-quote') || quoteForm;
                    target.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });
                });

                // Models picked in the quote form are weighed with the technology chosen there.
                var formFile = quoteForm.querySelector('input[type=file][name=modelFile]');
                if (formFile) {
                    formFile.addEventListener('change', function () {
                        var fresh = Array.prototype.filter.call(formFile.files || [], function (f) {
                            return MESH_EXT.test(f.name) && !models.some(function (m) { return sameFile(m.file, f); });
                        });
                        if (!fresh.length) return;
                        var checked = quoteForm.querySelector('input[name="Form.Technology"]:checked');
                        techs.forEach(function (t, i) { if (checked && t.name.toLowerCase() === checked.value.toLowerCase() && i !== active) select(i, 0); });
                        analyze(fresh).then(function (ok) { if (ok) fillQuote(quoteForm, false); });
                    });
                }
            }

            // ----- order the weighed models and pay the deposit -----

            var orderBtn = root.querySelector('[data-tt-order]');
            var orderUrl = root.getAttribute('data-tt-order-url');
            var orderLabel = root.querySelector('[data-tt-order-label]');
            var orderError = root.querySelector('[data-tt-order-error]');
            var orderPost = root.querySelector('[data-tt-orderpost]');
            var deposit = Math.min(100, Math.max(1, parseInt(root.getAttribute('data-tt-deposit'), 10) || 100));
            var ordering = false;

            function orderError_(msg) {
                if (!orderError) return;
                orderError.textContent = msg || '';
                orderError.hidden = !msg;
            }

            // Only models the customer ticked and could be weighed can be ordered.
            function orderable() {
                return included().filter(function (m) { return m.file && gramsOf(m) > 0; });
            }

            function orderState() {
                if (!orderBtn || ordering) return;
                var list = orderable(), t = totals();
                var ready = list.length > 0 && t.grams > 0;
                orderBtn.disabled = !ready;
                if (!orderLabel) return;
                orderLabel.textContent = ready
                    ? 'Đặt in · trả trước ' + money.format(Math.round(t.grams * (tierNow ? tierNow.price : 0) * deposit / 100)) + 'đ'
                    : 'Thả file 3D để đặt in';
            }

            function fillText() {
                return fillSel.options[fillSel.selectedIndex] ? fillSel.options[fillSel.selectedIndex].text : '';
            }

            function orderPayload(list) {
                return list.map(function (m) {
                    var r = autoWeight(m);
                    return {
                        name: m.file.name,
                        grams: gramsOf(m),
                        quantity: m.qty,
                        size: sizeText(m),
                        volume: Math.round(r.volume / 1000 * 10) / 10,
                        fill: fillText(),
                        manual: m.manual != null
                    };
                });
            }

            if (orderBtn && orderUrl) {
                orderBtn.addEventListener('click', function () {
                    if (ordering) return;
                    var list = orderable();
                    if (!list.length) return;

                    var data = new FormData();
                    data.append('models', JSON.stringify(orderPayload(list)));
                    data.append('technology', tech().name);
                    data.append('material', mat().name);
                    data.append('fill', (tech().resin ? 'Kiểu in: ' : 'Độ đặc: ') + fillText());
                    list.forEach(function (m) { data.append('modelFile', m.file, m.file.name); });

                    var token = orderPost ? orderPost.querySelector('input[name="__RequestVerificationToken"]') : null;
                    if (token) data.append('__RequestVerificationToken', token.value);

                    ordering = true;
                    orderError_('');
                    orderBtn.disabled = true;
                    orderBtn.classList.add('is-busy');
                    if (orderLabel) orderLabel.textContent = 'Đang gửi file…';

                    var xhr = new XMLHttpRequest();
                    xhr.open('POST', orderUrl, true);
                    xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
                    if (xhr.upload && orderLabel) {
                        xhr.upload.addEventListener('progress', function (e) {
                            if (!e.lengthComputable) return;
                            orderLabel.textContent = 'Đang gửi file ' + Math.round(e.loaded / e.total * 100) + '%';
                        });
                    }
                    xhr.addEventListener('load', function () {
                        var res = null;
                        try { res = JSON.parse(xhr.responseText); } catch (e) { }
                        if (xhr.status === 200 && res && res.ok && res.url) {
                            // Leave the page; no need to restore the button.
                            location.href = res.url;
                            return;
                        }
                        ordering = false;
                        orderBtn.classList.remove('is-busy');
                        orderError_((res && res.error) || 'Không gửi được đơn in. Hãy thử lại hoặc gửi yêu cầu báo giá.');
                        orderState();
                    });
                    xhr.addEventListener('error', function () {
                        ordering = false;
                        orderBtn.classList.remove('is-busy');
                        orderError_('Mất kết nối khi gửi file. Hãy thử lại.');
                        orderState();
                    });
                    xhr.send(data);
                });
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
                    var n = 0;
                    each('[data-cat]', function (card) {
                        var hide = cat !== 'all' && card.getAttribute('data-cat') !== cat;
                        card.classList.toggle('is-hidden', hide);
                        card.classList.remove('is-pop');
                        if (!hide) {
                            void card.offsetWidth;
                            card.style.setProperty('--pd', (Math.min(n++, 7) * .05) + 's');
                            card.classList.add('is-pop');
                        }
                    }, grid);

                    // "Show all of <category>" link below the grid follows the selected category.
                    var more = grid.parentNode.querySelector('[data-tt-filter-more]');
                    if (more) {
                        var url = chip.getAttribute('data-url');
                        more.hidden = !url;
                        if (url) {
                            more.href = url;
                            more.querySelector('span').textContent = chip.getAttribute('data-name') || '';
                        }
                    }
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

            // Picked files are listed under the drop zone. Picking again adds to the list instead of replacing it,
            // × removes one, and a 3D file (STL/OBJ/3MF) opens in the preview below the list.
            var meshSrc = zone.getAttribute('data-mesh-src');
            var files = [], meshes = [], shown = null, view = null;
            var box = document.createElement('div');
            box.className = 'tt-models tt-qfiles';
            box.hidden = true;
            box.innerHTML = '<div class="tt-models-head"><b></b><button type="button">+ Thêm file</button></div><ol class="tt-models-list"></ol>';
            zone.parentNode.insertBefore(box, zone.nextSibling);
            var sumEl = box.querySelector('b'), rowsEl = box.querySelector('ol'), viewer = null;

            function escHtml(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;'); }
            function fmtSize(bytes) { var mb = bytes / 1048576; return mb < 1 ? Math.max(1, Math.round(mb * 1024)) + ' KB' : mb.toFixed(1) + ' MB'; }
            function sameFile(a, b) { return a.name === b.name && a.size === b.size && a.lastModified === b.lastModified; }
            function canView(f) { return !!(meshSrc && MESH_EXT.test(f.name)); }
            function isShown(f) { return !!shown && sameFile(f, shown); }
            function cachedMesh(f) { var hit = meshes.filter(function (x) { return sameFile(x.file, f); })[0]; return hit && hit.mesh; }

            function setFiles(list) {
                try {
                    var dt = new DataTransfer();
                    list.forEach(function (f) { dt.items.add(f); });
                    input.files = dt.files;
                } catch (e) { /* old browsers: the input keeps its own selection */ }
                files = Array.prototype.slice.call(input.files || []);
            }

            function render() {
                box.hidden = !files.length;
                var bytes = files.reduce(function (s, f) { return s + f.size; }, 0);
                var max = parseInt(zone.getAttribute('data-max-mb'), 10) || 100, over = bytes / 1048576 > max;
                sumEl.textContent = files.length + ' file · ' + fmtSize(bytes);
                if (nameEl) {
                    nameEl.textContent = !files.length ? '' : over ? 'Tổng ' + fmtSize(bytes) + ' — vượt quá ' + max + ' MB, hãy gửi link tải' : '';
                    nameEl.style.color = over ? '#ff6b85' : '';
                }
                rowsEl.innerHTML = files.map(function (f, i) {
                    var on = isShown(f), view3d = canView(f);
                    return '<li class="' + (on ? 'is-active' : '') + '">'
                        + '<button type="button" class="tt-model-pick" data-pick="' + i + '"' + (view3d ? ' aria-pressed="' + on + '"' : ' disabled') + '>'
                        + '<b>' + escHtml(f.name) + '</b><small>' + fmtSize(f.size) + (view3d ? ' · ' + (on ? 'Đang xem' : 'Xem 3D') : '') + '</small></button>'
                        + '<button type="button" class="tt-model-del" data-del="' + i + '" aria-label="Bỏ ' + escHtml(f.name) + '">×</button></li>';
                }).join('');
            }

            function buildViewer() {
                viewer = document.createElement('div');
                viewer.className = 'tt-viewer tt-qfiles-viewer';
                viewer.innerHTML = '<div class="tt-viewer-head"><span class="tt-viewer-tag">Xem trước 3D</span><b></b>'
                    + '<button type="button" class="tt-viewer-max" aria-pressed="false"><span>Phóng to</span></button></div>'
                    + '<div class="tt-viewer-stage"><canvas aria-label="Mô hình 3D, kéo để xoay, chuột phải hoặc Shift kéo để di chuyển, cuộn để phóng to"></canvas><span class="tt-viewer-size"></span></div>'
                    + '<div class="tt-viewer-foot"><span>Kéo để xoay · chuột phải / Shift + kéo để di chuyển · cuộn để phóng to</span>'
                    + '<button type="button" class="tt-textlink" data-reset>Góc nhìn ban đầu</button><button type="button" class="tt-textlink" data-hide>Đóng</button></div>';
                box.parentNode.insertBefore(viewer, box.nextSibling);
                var maxBtn = viewer.querySelector('.tt-viewer-max');
                function maximize(on) {
                    viewer.classList.toggle('is-max', on);
                    document.documentElement.classList.toggle('tt-noscroll', on);
                    maxBtn.setAttribute('aria-pressed', on ? 'true' : 'false');
                    maxBtn.querySelector('span').textContent = on ? 'Thu nhỏ' : 'Phóng to';
                    if (view) view.request();
                }
                viewer.maximize = maximize;
                maxBtn.addEventListener('click', function () { maximize(!viewer.classList.contains('is-max')); });
                document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && viewer.classList.contains('is-max')) maximize(false); });
                viewer.querySelector('[data-reset]').addEventListener('click', function () { if (view) view.reset(); });
                viewer.querySelector('[data-hide]').addEventListener('click', hide);
            }

            function hide() {
                shown = null;
                if (viewer) { viewer.maximize(false); viewer.hidden = true; }
                if (view) view.stopSpin();
                render();
            }

            function show(f) {
                shown = f;
                render();
                if (!viewer) buildViewer();
                // Unhide first: the canvas needs its layout size before the first draw.
                viewer.hidden = false;
                viewer.querySelector('.tt-viewer-head b').textContent = f.name;
                var sizeEl = viewer.querySelector('.tt-viewer-size');
                sizeEl.textContent = 'Đang đọc…';
                var cached = cachedMesh(f);
                loadMesh(meshSrc).then(function (M) {
                    return (cached ? Promise.resolve(cached) : M.read(f)).then(function (mesh) {
                        if (!cached) meshes.push({ file: f, mesh: mesh });
                        if (!isShown(f)) return;
                        if (!view) view = new M.View(viewer.querySelector('canvas'));
                        var tech = document.querySelector('input[name="Form.Technology"]:checked');
                        view.color = tech && /resin/i.test(tech.value) ? [201, 166, 247] : [128, 229, 203];
                        view.set(mesh);
                        sizeEl.textContent = mesh.size.map(function (v) { return fmtNum(v, 1); }).join(' × ') + ' mm';
                    });
                }).catch(function (err) {
                    if (isShown(f)) sizeEl.textContent = (err && err.message) || 'Không đọc được file này.';
                });
            }

            input.addEventListener('change', function () {
                var picked = Array.prototype.slice.call(input.files || []);
                var add = picked.filter(function (f) { return !files.some(function (k) { return sameFile(k, f); }); });
                setFiles(files.concat(add));
                if (shown && !files.some(isShown)) hide(); else render();
            });

            box.querySelector('.tt-models-head button').addEventListener('click', function () { input.click(); });
            rowsEl.addEventListener('click', function (e) {
                var del = e.target.closest('[data-del]'), pick = e.target.closest('[data-pick]');
                if (del) {
                    var gone = files[parseInt(del.getAttribute('data-del'), 10)];
                    meshes = meshes.filter(function (x) { return !sameFile(x.file, gone); });
                    setFiles(files.filter(function (f) { return f !== gone; }));
                    if (isShown(gone)) hide(); else render();
                }
                else if (pick) {
                    var f = files[parseInt(pick.getAttribute('data-pick'), 10)];
                    if (f && isShown(f)) hide(); else if (f && canView(f)) show(f);
                }
            });
        });

        // Tools page: the device chips filter the packages; the first visible package gets selected.
        // The picture follows the selected package.
        each('[data-tt-tool]', function (tool) {
            var img = tool.querySelector('[data-tt-tool-img]');
            tool.addEventListener('change', function (e) {
                var src = e.target.getAttribute && e.target.getAttribute('data-img');
                if (img && src) img.src = src;
            });
            var chips = tool.querySelectorAll('.tt-tool-devices [data-devices]');
            Array.prototype.forEach.call(chips, function (chip) {
                chip.addEventListener('click', function () {
                    var d = chip.getAttribute('data-devices'), first = null;
                    Array.prototype.forEach.call(chips, function (c) { c.classList.toggle('is-active', c === chip); });
                    each('.tt-package', function (p) {
                        var show = p.getAttribute('data-devices') === d;
                        p.hidden = !show;
                        if (show && !first) first = p;
                    }, tool);
                    var checked = tool.querySelector('.tt-package:not([hidden]) input:checked');
                    if (!checked && first) {
                        var radio = first.querySelector('input');
                        radio.checked = true;
                        radio.dispatchEvent(new Event('change', { bubbles: true }));
                    }
                });
            });
        });

        // Design page: a category card picks the category in the form below instead of reloading the page.
        each('[data-tt-design-cat]', function (card) {
            card.addEventListener('click', function (e) {
                var radio = document.querySelector('input[name="Form.Category"][value="' + card.getAttribute('data-tt-design-cat') + '"]');
                if (!radio) return;
                e.preventDefault();
                radio.checked = true;
                var target = document.getElementById('tt-design-form');
                if (target) target.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });
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

    // ---------- Personalized products: order a whole list at once (one cart line per row) ----------

    // Every row has its own text, colors (the swatch attributes of the product), quantity and note. "Thêm vào giỏ"
    // posts one add-to-cart request per row with the product form, so each piece keeps its own colors and price;
    // the other attributes (size, profile…) are taken from the product page.
    function initTextList() {
        var cfgEl = document.querySelector('script[data-tt-textlist]'), cfg;
        if (!cfgEl) return;
        try { cfg = JSON.parse(cfgEl.textContent); } catch (e) { return; }
        var field = document.getElementById(cfg.control);
        if (!field) return;
        var form = field.closest('form');
        var addBtn = document.querySelector('.btn-add-to-cart[data-href]');
        if (!form || !addBtn) return;
        var qtyInput = form.querySelector('input[name$="EnteredQuantity"]');
        var MAX_ROWS = 300;
        var EXCEL_TOOLS = '<span class="tt-tl-excel"><button type="button" class="tt-textlink" data-tl-template>Tải file mẫu Excel</button>'
            + '<button type="button" class="tt-textlink" data-tl-import>Import Excel</button></span>';
        var LIST_ICON = '<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M9 6h11M9 12h11M9 18h11"/><circle cx="4.5" cy="6" r="1"/><circle cx="4.5" cy="12" r="1"/><circle cx="4.5" cy="18" r="1"/></svg>';

        function escAttr(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;'); }
        // "Xanh Dương " -> "xanh duong": headers and color names are matched loosely.
        function norm(s) { return String(s || '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/\s+/g, ' ').trim(); }

        // Color columns: the swatch attributes of the product ("Màu nền, Trắng" is the aria-label of an option).
        var attrs = [];
        each('input.swatch-input[type=radio]', function (input) {
            var label = input.getAttribute('aria-label') || '', cut = label.indexOf(', ');
            if (cut < 0) return;
            var a = attrs.filter(function (x) { return x.name === input.name; })[0];
            if (!a) attrs.push(a = { name: input.name, title: label.slice(0, cut), options: [] });
            a.options.push({ value: input.value, text: label.slice(cut + 2) });
        }, form);
        function current(a) { var c = form.querySelector('input[name="' + a.name + '"]:checked'); return c ? c.value : ''; }
        function optionFor(a, text) { var n = norm(text); return a.options.filter(function (o) { return norm(o.text) === n; })[0]; }

        var tools = document.createElement('div');
        tools.className = 'tt-textlist-tools';
        tools.innerHTML = '<button type="button" class="tt-textlist-btn" data-tl-open>' + LIST_ICON + '<span>Đặt nhiều ' + escAttr(cfg.item) + ' một lượt</span></button>'
            + EXCEL_TOOLS;
        field.parentNode.insertBefore(tools, field.nextSibling);

        var dlg = null, tbody, totalEl, msgEl, sendBtn, lastFocus, busy = false;

        function rowHtml(r) {
            var html = '<tr><td class="tt-tl-no"></td>'
                + '<td><input type="text" class="tt-input" maxlength="80" value="' + escAttr(r.text) + '" aria-label="Nội dung" data-tl-text /></td>';
            attrs.forEach(function (a, i) {
                var val = r.colors[i] == null ? current(a) : r.colors[i];
                html += '<td><select class="tt-input noskin' + (val ? '' : ' is-invalid') + '" aria-label="' + escAttr(a.title) + '" data-tl-attr="' + i + '">'
                    + (val ? '' : '<option value="">— chọn —</option>')
                    + a.options.map(function (o) { return '<option value="' + escAttr(o.value) + '"' + (o.value === val ? ' selected' : '') + '>' + escAttr(o.text) + '</option>'; }).join('')
                    + '</select></td>';
            });
            return html
                + '<td><input type="text" class="tt-input" inputmode="numeric" maxlength="4" value="' + r.qty + '" aria-label="Số lượng" data-tl-qty /></td>'
                + '<td><input type="text" class="tt-input" maxlength="120" value="' + escAttr(r.note) + '" aria-label="Ghi chú" placeholder="font, kiểu chữ…" data-tl-note /></td>'
                + '<td><button type="button" class="tt-tl-del" aria-label="Xoá dòng">×</button></td></tr>';
        }

        function readRow(tr) {
            return {
                tr: tr,
                text: tr.querySelector('[data-tl-text]').value.trim(),
                colors: Array.prototype.map.call(tr.querySelectorAll('[data-tl-attr]'), function (s) { return s.value; }),
                qty: Math.max(1, parseInt(tr.querySelector('[data-tl-qty]').value, 10) || 1),
                note: tr.querySelector('[data-tl-note]').value.trim()
            };
        }
        function collect() { return Array.prototype.map.call(tbody.rows, readRow).filter(function (r) { return r.text; }); }
        function pieces(rows) { return rows.reduce(function (s, r) { return s + r.qty; }, 0); }

        function renumber() {
            var rows = collect();
            each('tr', function (tr, i) { tr.querySelector('.tt-tl-no').textContent = i + 1; }, tbody);
            totalEl.textContent = rows.length + ' dòng · ' + pieces(rows) + ' ' + cfg.item;
            sendBtn.querySelector('span').textContent = rows.length ? 'Thêm ' + pieces(rows) + ' ' + cfg.item + ' vào giỏ' : 'Thêm vào giỏ';
        }

        // Appends rows (or inserts them after a row); returns the first new row.
        function addRows(rows, after) {
            var count = tbody.rows.length;
            rows = rows.slice(0, Math.max(0, MAX_ROWS - count));
            if (!rows.length) return null;
            var html = rows.map(rowHtml).join('');
            if (after) after.insertAdjacentHTML('afterend', html); else tbody.insertAdjacentHTML('beforeend', html);
            renumber();
            return after ? after.nextElementSibling : tbody.rows[count];
        }

        function blank() { return { text: '', colors: [], qty: 1, note: '' }; }

        // Pasted cells from Excel / Google Sheets. With a header row the columns are matched by name
        // (Tên, Màu chữ, Màu nền, SL, font…); without one: first text column = text, color names fill
        // the color columns in order, a small number is the quantity, anything else goes to the note.
        // STT and prices are skipped. Colors the product does not offer stay "— chọn —" and are noted.
        function parsePaste(text) {
            return parseRows(text.replace(/\r/g, '').split('\n').map(function (l) { return l.split('\t'); }));
        }

        function parseRows(lines) {
            lines = lines.map(function (cells) { return cells.map(function (c) { return c == null ? '' : String(c); }); })
                .filter(function (cells) { return cells.some(function (c) { return c.trim(); }); });
            if (!lines.length) return [];
            // The header is the first of the top lines with a text column next to a color or quantity column;
            // title lines above it ("Thông tin tag tên") are dropped.
            var TEXT_HEAD = /^(ten|noi dung|chu|text|name)/, map = null;
            function isAttrHead(h) { return attrs.some(function (a) { return norm(a.title) === h; }); }
            for (var li = 0; li < Math.min(3, lines.length) && !map; li++) {
                var head = lines[li].map(norm);
                if (!head.some(function (h) { return TEXT_HEAD.test(h); }) || !head.some(function (h) { return isAttrHead(h) || /^(sl|so luong|qty|mau)/.test(h); })) continue;
                map = head.map(function (h) {
                    if (/^(stt|#|gia|thanh tien|don gia)/.test(h)) return { skip: true };
                    if (/^(sl|so luong|qty)/.test(h)) return { qty: true };
                    var ai = -1;
                    attrs.forEach(function (a, i) { if (ai < 0 && norm(a.title) === h) ai = i; });
                    if (ai >= 0) return { attr: ai };
                    if (TEXT_HEAD.test(h)) return { text: true };
                    return { note: true };
                });
                lines = lines.slice(li + 1);
            }
            return lines.map(function (cells) {
                var r = blank(), notes = [], nextAttr = 0;
                r.colors = attrs.map(function () { return null; });
                cells.forEach(function (raw, ci) {
                    var c = raw.trim(), m = map && map[ci];
                    if (!c || (m && m.skip)) return;
                    if (m) {
                        if (m.text) r.text = r.text ? r.text + ' ' + c : c;
                        else if (m.qty) r.qty = Math.max(1, parseInt(c, 10) || 1);
                        else if (m.attr != null) {
                            var o = optionFor(attrs[m.attr], c);
                            r.colors[m.attr] = o ? o.value : '';
                            if (!o) notes.push(attrs[m.attr].title + ': ' + c);
                        }
                        else if (notes.indexOf(c) < 0) notes.push(c);
                        return;
                    }
                    if (/^\d+([.,]\d+)?$/.test(c)) {
                        if (ci > 0 && r.text && +c.replace(',', '.') < 1000 && r.qty === 1) r.qty = Math.max(1, parseInt(c, 10));
                        return;  // STT, price
                    }
                    if (!r.text) { r.text = c; return; }
                    for (var i = nextAttr; i < attrs.length; i++) {
                        var opt = optionFor(attrs[i], c);
                        if (opt) { r.colors[i] = opt.value; nextAttr = i + 1; return; }
                    }
                    notes.push(c);
                });
                r.note = notes.join(', ');
                return r;
            }).filter(function (r) { return r.text; });
        }

        function build() {
            dlg = document.createElement('div');
            dlg.className = 'tt-dialog';
            dlg.hidden = true;
            dlg.innerHTML = '<div class="tt-dialog-box tt-dialog-box--wide" role="dialog" aria-modal="true" aria-labelledby="tt-tl-title">'
                + '<div class="tt-dialog-head"><h3 id="tt-tl-title">' + escAttr(cfg.title) + '</h3><button type="button" class="tt-dialog-x" data-tl-close aria-label="Đóng">×</button></div>'
                + '<p class="tt-dialog-hint">Mỗi dòng là một ' + escAttr(cfg.item) + ' với màu riêng; các lựa chọn khác (kích thước…) lấy theo trang sản phẩm. '
                + '<b>Bấm Import Excel</b> (nên dùng file mẫu có sẵn danh sách màu) hoặc copy cả bảng trong Excel kèm dòng tiêu đề rồi dán vào ô Nội dung — cột font / ghi chú được giữ lại.</p>'
                + '<div class="tt-dialog-body"><table class="tt-tl-table"><thead><tr><th>#</th><th>Nội dung</th>'
                + attrs.map(function (a) { return '<th class="tt-tl-color">' + escAttr(a.title) + '</th>'; }).join('')
                + '<th class="tt-tl-qty">SL</th><th>Ghi chú</th><th></th></tr></thead><tbody></tbody></table></div>'
                + '<div class="tt-tl-actions"><button type="button" class="tt-textlink tt-tl-add" data-tl-add>+ Thêm dòng</button>' + EXCEL_TOOLS + '</div>'
                + '<p class="tt-tl-msg" data-tl-msg hidden></p>'
                + '<div class="tt-dialog-foot"><b data-tl-total></b><button type="button" class="tt-button" data-tl-close>Huỷ</button>'
                + '<button type="button" class="tt-button tt-button--dark" data-tl-send><span>Thêm vào giỏ</span></button></div></div>';
            document.body.appendChild(dlg);
            tbody = dlg.querySelector('tbody');
            totalEl = dlg.querySelector('[data-tl-total]');
            msgEl = dlg.querySelector('[data-tl-msg]');
            sendBtn = dlg.querySelector('[data-tl-send]');

            dlg.addEventListener('click', function (e) {
                if (busy) return;
                var tr;
                if (e.target === dlg || e.target.closest('[data-tl-close]')) close();
                else if (e.target.closest('[data-tl-add]')) { tr = addRows([blank()]); if (tr) tr.querySelector('input').focus(); }
                else if (e.target.closest('.tt-tl-del')) {
                    e.target.closest('tr').remove();
                    if (!tbody.rows.length) addRows([blank()]);
                    renumber();
                }
                else if (e.target.closest('[data-tl-send]')) send();
                else excelClick(e);
            });
            dlg.addEventListener('input', function (e) {
                if (e.target.matches('[data-tl-qty]')) e.target.value = e.target.value.replace(/\D/g, '');
                renumber();
            });
            dlg.addEventListener('change', function (e) {
                if (!e.target.matches('[data-tl-attr]')) return;
                e.target.classList.toggle('is-invalid', !e.target.value);
                // A color picked for an unavailable one replaces the "Màu nền: Xanh dương" note.
                var note = e.target.closest('tr').querySelector('[data-tl-note]'), title = attrs[+e.target.getAttribute('data-tl-attr')].title;
                if (e.target.value) note.value = note.value.split(', ').filter(function (p) { return p.indexOf(title + ': ') !== 0; }).join(', ');
            });
            dlg.addEventListener('keydown', function (e) {
                if (e.key === 'Escape' && !busy) { e.preventDefault(); close(); return; }
                // Enter jumps to the next row (a new one at the end).
                if (e.key === 'Enter' && e.target.matches('input')) {
                    e.preventDefault();
                    var tr = e.target.closest('tr'), next = tr.nextElementSibling || addRows([blank()]);
                    if (next) next.querySelector('[data-tl-text]').focus();
                }
            });
            dlg.addEventListener('paste', function (e) {
                if (!e.target.matches('[data-tl-text]')) return;
                var text = (e.clipboardData || window.clipboardData).getData('text') || '';
                if (!/[\r\n\t]/.test(text.trim())) return;
                e.preventDefault();
                var rows = parsePaste(text);
                if (!rows.length) return;
                var tr = e.target.closest('tr');
                // Pasting into an empty row replaces it, otherwise the rows go below.
                var empty = !readRow(tr).text;
                var first = addRows(rows, tr);
                if (empty) tr.remove();
                renumber();
                if (first) first.querySelector('[data-tl-text]').focus();
            });
        }

        function showMsg(text, ok) {
            msgEl.textContent = text || '';
            msgEl.hidden = !text;
            msgEl.classList.toggle('is-ok', !!ok);
        }

        function open() {
            if (!dlg) build();
            lastFocus = document.activeElement;
            showMsg('');
            if (!tbody.rows.length) {
                // A text typed on the page starts the list; otherwise five empty rows.
                var start = field.value.trim() ? [{ text: field.value.trim(), colors: [], qty: Math.max(1, parseInt(qtyInput && qtyInput.value, 10) || 1), note: '' }] : [];
                addRows(start.concat(start.length ? [blank()] : [blank(), blank(), blank(), blank(), blank()]));
            }
            dlg.hidden = false;
            document.documentElement.classList.add('tt-noscroll');
            var empty = Array.prototype.filter.call(tbody.querySelectorAll('[data-tl-text]'), function (i) { return !i.value; })[0];
            (empty || tbody.querySelector('[data-tl-text]')).focus();
        }

        function close() {
            dlg.hidden = true;
            document.documentElement.classList.remove('tt-noscroll');
            if (lastFocus && lastFocus.focus) lastFocus.focus();
        }

        function setBusy(on) {
            busy = on;
            dlg.classList.toggle('is-busy', on);
            Array.prototype.forEach.call(dlg.querySelectorAll('button, input, select'), function (el) { el.disabled = on; });
        }

        // One add-to-cart request per row, one after another. Rows added are removed from the table,
        // so after an error a second click only sends the rest.
        function send() {
            var rows = collect();
            if (!rows.length) { showMsg('Nhập ít nhất một dòng.'); return; }
            var missing = rows.filter(function (r) { return r.colors.some(function (c) { return !c; }); });
            if (missing.length) {
                showMsg('Chọn màu cho ' + missing.length + ' dòng đánh dấu đỏ (màu không có trong danh sách đã được ghi vào cột Ghi chú).');
                var bad = missing[0].tr.querySelector('.is-invalid'); if (bad) bad.focus();
                return;
            }
            var href = addBtn.getAttribute('data-href'), done = 0, total = rows.length;
            setBusy(true);
            showMsg('Đang thêm 0/' + total + '…', true);

            rows.reduce(function (p, r) {
                return p.then(function () {
                    var data = new FormData(form);
                    data.set(field.name, r.note ? r.text + ' — ' + r.note : r.text);
                    attrs.forEach(function (a, i) { data.set(a.name, r.colors[i]); });
                    if (qtyInput) data.set(qtyInput.name, r.qty);
                    return fetch(href, {
                        method: 'POST',
                        credentials: 'same-origin',
                        headers: { 'X-Requested-With': 'XMLHttpRequest' },
                        body: new URLSearchParams(data)
                    }).then(function (res) {
                        if (!res.ok) throw new Error('Lỗi máy chủ (' + res.status + ').');
                        return res.json();
                    }).then(function (json) {
                        if (json && json.success === false) {
                            var m = json.message;
                            throw new Error('Dòng "' + r.text + '": ' + (Array.isArray(m) ? m.join(' ') : m || 'không thêm được.'));
                        }
                        r.tr.remove();
                        done++;
                        showMsg('Đang thêm ' + done + '/' + total + '…', true);
                    });
                });
            }, Promise.resolve()).then(function () {
                showMsg('Đã thêm ' + total + ' dòng vào giỏ hàng, đang mở giỏ…', true);
                location.href = cfg.cartUrl || '/cart';
            }, function (err) {
                setBusy(false);
                if (!tbody.rows.length) addRows([blank()]);
                renumber();
                showMsg((done ? 'Đã thêm ' + done + ' dòng. ' : '') + ((err && err.message) || 'Không thêm được, hãy thử lại.'));
            });
        }

        // ----- Excel: template download and import (studio-xlsx.js is loaded on first use) -----

        function loadXlsx() {
            if (window.TTXlsx) return Promise.resolve(window.TTXlsx);
            if (!loadXlsx.p) {
                loadXlsx.p = new Promise(function (resolve, reject) {
                    var el = document.createElement('script');
                    var own = document.querySelector('script[src*="/studio/studio.js"]');
                    el.src = cfg.xlsxSrc || (own ? own.src.replace('/studio.js', '/studio-xlsx.js') : '');
                    el.async = true;
                    el.onload = function () { if (window.TTXlsx) resolve(window.TTXlsx); else reject(new Error('Không tải được bộ đọc Excel.')); };
                    el.onerror = function () { loadXlsx.p = null; reject(new Error('Không tải được bộ đọc Excel, hãy thử lại.')); };
                    document.head.appendChild(el);
                });
            }
            return loadXlsx.p;
        }

        // Header row with dropdowns of the product's colors; the same headers are recognized on import.
        function downloadTemplate() {
            loadXlsx().then(function (X) {
                var head = ['STT', 'Tên'].concat(attrs.map(function (a) { return a.title; }), ['SL', 'Ghi chú (font, kiểu chữ…)']);
                var lists = {};
                attrs.forEach(function (a, i) { lists[i + 2] = a.options.map(function (o) { return o.text; }); });
                var blob = X.write({ sheet: cfg.item, rows: [head], widths: [6, 28].concat(attrs.map(function () { return 14; }), [6, 30]), lists: lists, listRows: MAX_ROWS + 1 });
                var a = document.createElement('a');
                a.href = URL.createObjectURL(blob);
                a.download = 'mau-' + norm(cfg.item).replace(/[^a-z0-9]+/g, '-') + '.xlsx';
                document.body.appendChild(a);
                a.click();
                setTimeout(function () { URL.revokeObjectURL(a.href); a.remove(); }, 1000);
            }, function (err) { alert(err.message); });
        }

        // CSV / TSV text into rows of cells (quoted fields supported).
        function parseCsv(text) {
            var first = text.split(/\r?\n/)[0] || '', sep = first.indexOf('\t') >= 0 ? '\t' : first.split(';').length > first.split(',').length ? ';' : ',';
            var rows = [], row = [], cell = '', q = false;
            for (var i = 0; i < text.length; i++) {
                var ch = text[i];
                if (q) {
                    if (ch === '"' && text[i + 1] === '"') { cell += '"'; i++; }
                    else if (ch === '"') q = false;
                    else cell += ch;
                }
                else if (ch === '"') q = true;
                else if (ch === sep) { row.push(cell); cell = ''; }
                else if (ch === '\n' || ch === '\r') {
                    if (ch === '\r' && text[i + 1] === '\n') i++;
                    row.push(cell); rows.push(row); row = []; cell = '';
                }
                else cell += ch;
            }
            row.push(cell); rows.push(row);
            return rows;
        }

        var importInput = document.createElement('input');
        importInput.type = 'file';
        importInput.accept = '.xlsx,.csv,.tsv,.txt';
        importInput.hidden = true;
        document.body.appendChild(importInput);
        importInput.addEventListener('change', function () {
            var file = importInput.files && importInput.files[0];
            importInput.value = '';
            if (!file) return;
            var read = /\.xlsx$/i.test(file.name)
                ? loadXlsx().then(function (X) { return X.read(file); })
                : file.text().then(parseCsv);
            read.then(function (lines) {
                var rows = parseRows(lines);
                if (!dlg || dlg.hidden) open();
                if (!rows.length) { showMsg('Không thấy dòng nào trong file ' + file.name + ' — cần cột Tên (dùng file mẫu cho chắc).'); return; }
                // Empty rows are replaced by the imported ones.
                Array.prototype.slice.call(tbody.rows).forEach(function (tr) { if (!readRow(tr).text) tr.remove(); });
                addRows(rows);
                var bad = rows.filter(function (r) { return r.colors.some(function (c) { return c === ''; }); }).length;
                showMsg('Đã nhập ' + rows.length + ' dòng từ ' + file.name + (bad ? ' — ' + bad + ' dòng có màu shop chưa có (tô đỏ), hãy chọn lại.' : '.'), !bad);
            }).catch(function (err) {
                if (!dlg || dlg.hidden) open();
                showMsg('Không đọc được ' + file.name + ': ' + ((err && err.message) || 'file lỗi.'));
            });
        });

        function excelClick(e) {
            if (e.target.closest('[data-tl-template]')) downloadTemplate();
            else if (e.target.closest('[data-tl-import]')) importInput.click();
        }

        tools.querySelector('[data-tl-open]').addEventListener('click', open);
        tools.addEventListener('click', excelClick);
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
        initTextList();
    });
})();

/* TT Minimal studio storefront: scroll reveal, tilt cards, hero print animation, price estimate, file drop. No dependencies. */
(function () {
    'use strict';

    var doc = document.documentElement;
    doc.classList.add('tt-js');

    var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var money = new Intl.NumberFormat('vi-VN');

    function formatVnd(value) {
        return money.format(Math.round(value)) + 'đ';
    }

    function formatWeight(grams) {
        return grams >= 1000 ? money.format(Math.round(grams / 10) / 100) + ' kg' : money.format(Math.round(grams)) + ' g';
    }

    function onReady(fn) {
        if (document.readyState !== 'loading') fn();
        else document.addEventListener('DOMContentLoaded', fn);
    }

    // ---------- Reveal on scroll ----------

    function initReveal() {
        var items = document.querySelectorAll('[data-tt-reveal], .tt-steps');
        if (!('IntersectionObserver' in window) || reduceMotion) {
            items.forEach(function (el) { el.classList.add('is-in'); });
            return;
        }

        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('is-in');
                    io.unobserve(entry.target);
                }
            });
        }, { rootMargin: '0px 0px -8% 0px', threshold: 0.12 });

        items.forEach(function (el) { io.observe(el); });
    }

    // ---------- Tilt + spotlight cards ----------

    function initTilt() {
        if (reduceMotion || !window.matchMedia('(hover: hover)').matches) return;

        document.querySelectorAll('[data-tt-tilt]').forEach(function (card) {
            var max = parseFloat(card.getAttribute('data-tt-tilt')) || 8;
            var frame = 0;

            card.addEventListener('pointermove', function (e) {
                var rect = card.getBoundingClientRect();
                var x = (e.clientX - rect.left) / rect.width;
                var y = (e.clientY - rect.top) / rect.height;
                cancelAnimationFrame(frame);
                frame = requestAnimationFrame(function () {
                    card.style.setProperty('--mx', (x * 100) + '%');
                    card.style.setProperty('--my', (y * 100) + '%');
                    card.style.transform = 'rotateX(' + ((0.5 - y) * max) + 'deg) rotateY(' + ((x - 0.5) * max) + 'deg) translateZ(0)';
                });
            });

            card.addEventListener('pointerleave', function () {
                cancelAnimationFrame(frame);
                card.style.transform = '';
            });
        });
    }

    // ---------- Hero: a model printed layer by layer ----------

    var shapes = [
        // Twisted vase
        function (t, a) {
            var r = 0.5 + 0.2 * Math.sin(t * Math.PI * 1.5 + 0.6) - 0.08 * t;
            return r * (1 + 0.08 * Math.cos(6 * a + t * 7));
        },
        // Round pot with lobes
        function (t, a) {
            var r = Math.sin(Math.PI * (0.1 + 0.8 * t)) * 0.72 + 0.12;
            return r * (1 + 0.06 * Math.cos(5 * a));
        },
        // Faceted tower
        function (t, a) {
            var sides = 8;
            var seg = Math.PI * 2 / sides;
            var poly = Math.cos(seg / 2) / Math.cos(((a % seg) + seg) % seg - seg / 2);
            var r = t < 0.82 ? 0.42 + 0.14 * Math.pow(1 - t, 2) : 0.56 - (t > 0.9 && Math.cos(a * 4) > 0.2 ? 0.1 : 0);
            return r * poly;
        }
    ];

    function initHero() {
        var canvas = document.getElementById('tt-hero-canvas');
        if (!canvas || !canvas.getContext) return;

        var ctx = canvas.getContext('2d');
        var layers = 44;
        var points = 90;
        var width = 0, height = 0, dpr = 1;
        var shapeIndex = 0;
        var cycleStart = performance.now();
        var printTime = 5600, holdTime = 2200, fadeTime = 900;
        var rotation = 0, targetTiltX = 0.38, tiltX = 0.38, mouseX = 0;
        var running = true, visible = true;

        function resize() {
            var rect = canvas.getBoundingClientRect();
            dpr = Math.min(window.devicePixelRatio || 1, 2);
            width = rect.width;
            height = rect.height;
            canvas.width = Math.round(width * dpr);
            canvas.height = Math.round(height * dpr);
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        }

        function project(x, y, z) {
            // Rotate around Y, then tilt around X, then perspective.
            var cos = Math.cos(rotation), sin = Math.sin(rotation);
            var rx = x * cos - z * sin;
            var rz = x * sin + z * cos;
            var ct = Math.cos(tiltX), st = Math.sin(tiltX);
            var ry = y * ct - rz * st;
            var rz2 = y * st + rz * ct;
            var dist = 3.4;
            var scale = Math.min(width, height) * 1.2 / (dist - rz2);
            return { x: width / 2 + rx * scale, y: height * 0.42 - ry * scale, z: rz2 };
        }

        function drawPlate(alpha) {
            var size = 1.05, lines = 8, y = -0.98;
            ctx.lineWidth = 1;
            for (var i = 0; i <= lines; i++) {
                var v = -size + (2 * size * i) / lines;
                var a1 = project(v, y, -size), a2 = project(v, y, size);
                var b1 = project(-size, y, v), b2 = project(size, y, v);
                var edge = i === 0 || i === lines;
                ctx.strokeStyle = 'rgba(96,165,250,' + (alpha * (edge ? 0.55 : 0.18)) + ')';
                ctx.beginPath();
                ctx.moveTo(a1.x, a1.y); ctx.lineTo(a2.x, a2.y);
                ctx.moveTo(b1.x, b1.y); ctx.lineTo(b2.x, b2.y);
                ctx.stroke();
            }
        }

        function layerPoints(shape, t) {
            var y = -0.95 + t * 1.8;
            var list = [];
            for (var j = 0; j <= points; j++) {
                var a = (j / points) * Math.PI * 2;
                var r = shape(t, a);
                list.push(project(Math.cos(a) * r, y, Math.sin(a) * r));
            }
            return list;
        }

        function render(now) {
            if (!running) return;
            requestAnimationFrame(render);
            if (!visible) return;

            var elapsed = now - cycleStart;
            var total = printTime + holdTime + fadeTime;
            if (elapsed > total) {
                shapeIndex = (shapeIndex + 1) % shapes.length;
                cycleStart = now;
                elapsed = 0;
            }

            var progress = reduceMotion ? 1 : Math.min(elapsed / printTime, 1);
            var fade = elapsed > printTime + holdTime ? 1 - (elapsed - printTime - holdTime) / fadeTime : 1;
            fade = Math.max(0, Math.min(1, fade));

            rotation += reduceMotion ? 0 : 0.0045;
            tiltX += (targetTiltX - tiltX) * 0.05;
            var baseRotation = rotation;
            rotation = baseRotation + mouseX * 0.5;

            ctx.clearRect(0, 0, width, height);
            drawPlate(1);

            var shape = shapes[shapeIndex];
            var shown = progress * layers;
            var done = Math.floor(shown);
            var current = null;

            for (var i = 0; i <= Math.min(done, layers - 1); i++) {
                var t = i / (layers - 1);
                var pts = layerPoints(shape, t);
                var isTop = i === done && progress < 1;
                var hue = 196 + t * 34;
                var partial = isTop ? shown - done : 1;
                var count = Math.max(2, Math.round(points * partial));

                // Back half dim, front half bright: gives depth without sorting.
                for (var pass = 0; pass < 2; pass++) {
                    ctx.beginPath();
                    var drawing = false;
                    for (var j = 0; j < count; j++) {
                        var p = pts[j], q = pts[j + 1];
                        var front = (p.z + q.z) / 2 > -0.05;
                        if ((pass === 1) === front) {
                            if (!drawing) { ctx.moveTo(p.x, p.y); drawing = true; }
                            ctx.lineTo(q.x, q.y);
                        } else {
                            drawing = false;
                        }
                    }
                    var alpha = (pass === 1 ? 0.9 : 0.28) * fade;
                    ctx.strokeStyle = isTop ? 'rgba(224,242,254,' + fade + ')' : 'hsla(' + hue + ',95%,' + (pass === 1 ? 66 : 55) + '%,' + alpha + ')';
                    ctx.lineWidth = isTop ? 2.2 : (pass === 1 ? 1.35 : 1);
                    ctx.stroke();
                }

                if (isTop) current = pts[count - 1];
            }

            // Vertical ribs for a wireframe feel.
            if (done > 1) {
                ctx.lineWidth = 0.7;
                for (var k = 0; k < points; k += 15) {
                    ctx.beginPath();
                    for (var m = 0; m <= Math.min(done, layers - 1); m++) {
                        var tt = m / (layers - 1);
                        var a = (k / points) * Math.PI * 2;
                        var r = shape(tt, a);
                        var pp = project(Math.cos(a) * r, -0.95 + tt * 1.8, Math.sin(a) * r);
                        if (m === 0) ctx.moveTo(pp.x, pp.y); else ctx.lineTo(pp.x, pp.y);
                    }
                    ctx.strokeStyle = 'rgba(147,197,253,' + (0.22 * fade) + ')';
                    ctx.stroke();
                }
            }

            // Nozzle: glowing tip following the current layer.
            if (current && fade > 0) {
                var glow = ctx.createRadialGradient(current.x, current.y, 0, current.x, current.y, 26);
                glow.addColorStop(0, 'rgba(255,255,255,0.95)');
                glow.addColorStop(0.25, 'rgba(34,211,238,0.6)');
                glow.addColorStop(1, 'rgba(34,211,238,0)');
                ctx.fillStyle = glow;
                ctx.beginPath();
                ctx.arc(current.x, current.y, 26, 0, Math.PI * 2);
                ctx.fill();

                ctx.strokeStyle = 'rgba(191,219,254,0.55)';
                ctx.lineWidth = 1;
                ctx.beginPath();
                ctx.moveTo(current.x, current.y - 8);
                ctx.lineTo(current.x, current.y - 60);
                ctx.stroke();
                ctx.fillStyle = 'rgba(191,219,254,0.85)';
                ctx.fillRect(current.x - 9, current.y - 72, 18, 12);
            }

            rotation = baseRotation;
        }

        resize();
        window.addEventListener('resize', resize);

        if (!reduceMotion) {
            window.addEventListener('pointermove', function (e) {
                mouseX = (e.clientX / window.innerWidth - 0.5);
                targetTiltX = 0.38 + (e.clientY / window.innerHeight - 0.5) * 0.25;
            }, { passive: true });
        }

        if ('IntersectionObserver' in window) {
            new IntersectionObserver(function (entries) {
                visible = entries[0].isIntersecting;
            }).observe(canvas);
        }

        document.addEventListener('visibilitychange', function () {
            visible = !document.hidden;
        });

        requestAnimationFrame(render);
    }

    // ---------- Price estimate ----------

    function initCalc() {
        var root = document.querySelector('[data-tt-calc]');
        if (!root) return;

        var techs;
        try { techs = JSON.parse(root.getAttribute('data-tt-calc')); } catch (e) { return; }
        if (!techs || !techs.length) return;

        var tabs = root.querySelectorAll('.tt-calc-tab');
        var panels = root.querySelectorAll('[data-tt-panel]');
        var indicator = root.querySelector('.tt-calc-indicator');
        var grams = root.querySelector('[data-tt-grams]');
        var qty = root.querySelector('[data-tt-qty]');
        var range = root.querySelector('[data-tt-range]');
        var totalEl = root.querySelector('[data-tt-total]');
        var unitEl = root.querySelector('[data-tt-unit]');
        var cta = root.querySelector('[data-tt-cta]');
        var active = 0;

        function moveIndicator() {
            var tab = tabs[active];
            if (!tab || !indicator) return;
            indicator.style.width = tab.offsetWidth + 'px';
            indicator.style.transform = 'translateX(' + (tab.offsetLeft - tabs[0].offsetLeft) + 'px)';
        }

        function sliderToGrams(v) { return Math.round(5 * Math.pow(1000, v / 100)); }
        function gramsToSlider(g) { return Math.max(0, Math.min(100, Math.log(Math.max(g, 5) / 5) / Math.log(1000) * 100)); }

        function update() {
            var tech = techs[active];
            var g = Math.max(parseInt(grams.value, 10) || 0, 0);
            var q = Math.max(parseInt(qty.value, 10) || 1, 1);
            var totalGrams = g * q;
            var tierIndex = 0;
            tech.tiers.forEach(function (tier, i) { if (totalGrams >= tier.min) tierIndex = i; });
            var tier = tech.tiers[tierIndex];

            panels.forEach(function (panel, i) {
                panel.hidden = i !== active;
                panel.querySelectorAll('.tt-tier').forEach(function (row, j) {
                    row.classList.toggle('is-active', i === active && j === tierIndex);
                });
            });

            totalEl.textContent = totalGrams > 0 ? formatVnd(totalGrams * tier.price) : '—';
            unitEl.textContent = formatVnd(tier.price) + '/g · ' + tier.label + ' · tổng ' + formatWeight(totalGrams);
            if (cta) cta.href = cta.getAttribute('data-base') + '?tech=' + encodeURIComponent(tech.name);
        }

        tabs.forEach(function (tab, i) {
            tab.addEventListener('click', function () {
                active = i;
                tabs.forEach(function (t, j) {
                    t.classList.toggle('is-active', j === i);
                    t.setAttribute('aria-selected', j === i ? 'true' : 'false');
                });
                moveIndicator();
                update();
            });
        });

        grams.addEventListener('input', function () {
            range.value = gramsToSlider(parseInt(grams.value, 10) || 0);
            update();
        });
        qty.addEventListener('input', update);
        range.addEventListener('input', function () {
            grams.value = sliderToGrams(parseFloat(range.value));
            update();
        });

        window.addEventListener('resize', moveIndicator);
        range.value = gramsToSlider(parseInt(grams.value, 10) || 0);
        moveIndicator();
        update();

        // Animate the tier bars once visible.
        var bars = root.querySelectorAll('.tt-tier-bar i');
        var show = function () { bars.forEach(function (bar) { bar.style.width = bar.getAttribute('data-w') + '%'; }); };
        if ('IntersectionObserver' in window) {
            var io = new IntersectionObserver(function (entries) {
                if (entries[0].isIntersecting) { show(); io.disconnect(); }
            });
            io.observe(root);
        } else {
            show();
        }
    }

    // ---------- File drop zone ----------

    function initDrop() {
        document.querySelectorAll('.tt-drop').forEach(function (zone) {
            var input = zone.querySelector('input[type=file]');
            var nameEl = zone.querySelector('.tt-file-name');
            if (!input) return;

            ['dragenter', 'dragover'].forEach(function (type) {
                zone.addEventListener(type, function () { zone.classList.add('is-over'); });
            });
            ['dragleave', 'drop'].forEach(function (type) {
                zone.addEventListener(type, function () { zone.classList.remove('is-over'); });
            });

            input.addEventListener('change', function () {
                var file = input.files && input.files[0];
                if (!nameEl) return;
                if (!file) { nameEl.textContent = ''; return; }

                var max = parseInt(zone.getAttribute('data-max-mb'), 10) || 100;
                var size = file.size / 1024 / 1024;
                nameEl.textContent = file.name + ' · ' + (size < 1 ? Math.max(1, Math.round(size * 1024)) + ' KB' : size.toFixed(1) + ' MB');
                nameEl.style.color = size > max ? '#dc2626' : '';
                if (size > max) {
                    nameEl.textContent += ' — vượt quá ' + max + ' MB, hãy gửi link tải';
                }
            });
        });

        // Prevent double submits of the quote form while the file uploads.
        document.querySelectorAll('form[data-tt-quote]').forEach(function (form) {
            form.addEventListener('submit', function () {
                var btn = form.querySelector('button[type=submit]');
                if (btn && (!window.jQuery || !jQuery(form).valid || jQuery(form).valid())) {
                    btn.disabled = true;
                    btn.querySelector('span').textContent = 'Đang gửi…';
                }
            });
        });
    }

    onReady(function () {
        initReveal();
        initTilt();
        initHero();
        initCalc();
        initDrop();
    });
})();

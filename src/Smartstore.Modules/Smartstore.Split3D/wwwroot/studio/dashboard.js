/*
 * TT Minimal admin start page (Views/StudioDashboard/Index.cshtml): tear-off calendar with the lunar date,
 * count-up numbers, sparklines, the revenue chart, the store rhythm heatmap, busy hours and the Ctrl+K palette.
 * No libraries: plain SVG and DOM.
 */
(function () {
    'use strict';

    var root = document.querySelector('[data-ttd]');
    if (!root) return;

    var script = document.currentScript;
    var findUrl = script && script.getAttribute('data-find-url');
    var data = {};
    try { data = JSON.parse(root.querySelector('[data-ttd-data]').textContent); } catch (e) { }

    var SVG = 'http://www.w3.org/2000/svg';
    var reduceMotion = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
    var WEEKDAYS = ['Chủ nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];

    // ---------- Formatting ----------

    function money(v) {
        return Math.round(v).toLocaleString('vi-VN') + 'đ';
    }

    function short(v) {
        var a = Math.abs(v);
        var f = function (x, unit) { return (Math.round(x * 10) / 10).toString().replace('.', ',') + unit; };
        if (a >= 1e9) return f(v / 1e9, ' tỷ');
        if (a >= 1e6) return f(v / 1e6, 'tr');
        if (a >= 1e3) return Math.round(v / 1e3) + 'k';
        return Math.round(v).toString();
    }

    function parseDate(s) {
        var p = s.split('-');
        return new Date(+p[0], +p[1] - 1, +p[2]);
    }

    function dm(d) {
        return ('0' + d.getDate()).slice(-2) + '/' + ('0' + (d.getMonth() + 1)).slice(-2);
    }

    function el(name, attrs, parent) {
        var node = document.createElementNS(SVG, name);
        for (var k in attrs) node.setAttribute(k, attrs[k]);
        if (parent) parent.appendChild(node);
        return node;
    }

    // ---------- Greeting ----------

    (function () {
        var greet = root.querySelector('[data-ttd-greet]');
        if (!greet) return;
        var h = typeof data.hour === 'number' ? data.hour : new Date().getHours();
        greet.textContent =
            h < 5 ? '🌙 Khuya rồi đó' :
            h < 11 ? '☀️ Chào buổi sáng' :
            h < 14 ? '🍜 Chào buổi trưa' :
            h < 18 ? '🌤️ Chào buổi chiều' :
            h < 22 ? '🌆 Chào buổi tối' : '🌙 Khuya rồi đó';
    })();

    // ---------- Lunar calendar (Hồ Ngọc Đức's algorithm, UTC+7) ----------

    var Lunar = (function () {
        var PI = Math.PI, TZ = 7, floor = Math.floor;

        function jdFromDate(dd, mm, yy) {
            var a = floor((14 - mm) / 12), y = yy + 4800 - a, m = mm + 12 * a - 3;
            return dd + floor((153 * m + 2) / 5) + 365 * y + floor(y / 4) - floor(y / 100) + floor(y / 400) - 32045;
        }

        function newMoon(k) {
            var T = k / 1236.85, T2 = T * T, T3 = T2 * T, dr = PI / 180;
            var jd1 = 2415020.75933 + 29.53058868 * k + 0.0001178 * T2 - 0.000000155 * T3;
            jd1 += 0.00033 * Math.sin((166.56 + 132.87 * T - 0.009173 * T2) * dr);
            var M = 359.2242 + 29.10535608 * k - 0.0000333 * T2 - 0.00000347 * T3;
            var Mpr = 306.0253 + 385.81691806 * k + 0.0107306 * T2 + 0.00001236 * T3;
            var F = 21.2964 + 390.67050646 * k - 0.0016528 * T2 - 0.00000239 * T3;
            var C1 = (0.1734 - 0.000393 * T) * Math.sin(M * dr) + 0.0021 * Math.sin(2 * dr * M);
            C1 = C1 - 0.4068 * Math.sin(Mpr * dr) + 0.0161 * Math.sin(dr * 2 * Mpr);
            C1 = C1 - 0.0004 * Math.sin(dr * 3 * Mpr);
            C1 = C1 + 0.0104 * Math.sin(dr * 2 * F) - 0.0051 * Math.sin(dr * (M + Mpr));
            C1 = C1 - 0.0074 * Math.sin(dr * (M - Mpr)) + 0.0004 * Math.sin(dr * (2 * F + M));
            C1 = C1 - 0.0004 * Math.sin(dr * (2 * F - M)) - 0.0006 * Math.sin(dr * (2 * F + Mpr));
            C1 = C1 + 0.0010 * Math.sin(dr * (2 * F - Mpr)) + 0.0005 * Math.sin(dr * (2 * Mpr + M));
            var deltat = T < -11
                ? 0.001 + 0.000839 * T + 0.0002261 * T2 - 0.00000845 * T3 - 0.000000081 * T * T3
                : -0.000278 + 0.000265 * T + 0.000262 * T2;
            return floor(jd1 + C1 - deltat + 0.5 + TZ / 24);
        }

        function sunLongitude(jdn) {
            var T = (jdn - 2451545.5 - TZ / 24) / 36525, T2 = T * T, dr = PI / 180;
            var M = 357.52910 + 35999.05030 * T - 0.0001559 * T2 - 0.00000048 * T * T2;
            var L0 = 280.46645 + 36000.76983 * T + 0.0003032 * T2;
            var DL = (1.914600 - 0.004817 * T - 0.000014 * T2) * Math.sin(dr * M);
            DL = DL + (0.019993 - 0.000101 * T) * Math.sin(dr * 2 * M) + 0.000290 * Math.sin(dr * 3 * M);
            var L = (L0 + DL) * dr;
            L = L - PI * 2 * floor(L / (PI * 2));
            return floor(L / PI * 6);
        }

        function month11(yy) {
            var off = jdFromDate(31, 12, yy) - 2415021;
            var k = floor(off / 29.530588853);
            var nm = newMoon(k);
            return sunLongitude(nm) >= 9 ? newMoon(k - 1) : nm;
        }

        function leapOffset(a11) {
            var k = floor((a11 - 2415021.076998695) / 29.530588853 + 0.5), last, i = 1;
            var arc = sunLongitude(newMoon(k + i));
            do {
                last = arc;
                i++;
                arc = sunLongitude(newMoon(k + i));
            } while (arc !== last && i < 14);
            return i - 1;
        }

        function fromSolar(dd, mm, yy) {
            var day = jdFromDate(dd, mm, yy);
            var k = floor((day - 2415021.076998695) / 29.530588853);
            var start = newMoon(k + 1);
            if (start > day) start = newMoon(k);
            var a11 = month11(yy), b11 = a11, year;
            if (a11 >= start) {
                year = yy;
                a11 = month11(yy - 1);
            }
            else {
                year = yy + 1;
                b11 = month11(yy + 1);
            }
            var lday = day - start + 1;
            var diff = floor((start - a11) / 29);
            var leap = false, month = diff + 11;
            if (b11 - a11 > 365) {
                var ld = leapOffset(a11);
                if (diff >= ld) {
                    month = diff + 10;
                    if (diff === ld) leap = true;
                }
            }
            if (month > 12) month -= 12;
            if (month >= 11 && diff < 4) year -= 1;
            return { day: lday, month: month, year: year, leap: leap };
        }

        var CAN = ['Canh', 'Tân', 'Nhâm', 'Quý', 'Giáp', 'Ất', 'Bính', 'Đinh', 'Mậu', 'Kỷ'];
        var CHI = ['Thân', 'Dậu', 'Tuất', 'Hợi', 'Tý', 'Sửu', 'Dần', 'Mão', 'Thìn', 'Tỵ', 'Ngọ', 'Mùi'];

        return {
            fromSolar: fromSolar,
            yearName: function (y) { return CAN[y % 10] + ' ' + CHI[y % 12]; }
        };
    })();

    (function () {
        var cal = root.querySelector('[data-ttd-cal]');
        var slot = root.querySelector('[data-ttd-lunar]');
        if (!cal || !slot) return;

        var today = data.today ? parseDate(data.today) : new Date();
        var l;
        try { l = Lunar.fromSolar(today.getDate(), today.getMonth() + 1, today.getFullYear()); } catch (e) { return; }

        var dayText = l.day === 1 ? 'Mùng 1' : l.day === 15 ? 'Rằm' : (l.day <= 10 ? 'Mùng ' : '') + l.day;
        var lunarHtml = 'Âm lịch <b>' + dayText + '/' + l.month + (l.leap ? ' nhuận' : '') + '</b><br>năm ' + Lunar.yearName(l.year);
        slot.innerHTML = lunarHtml;

        // Countdown to Tết (next lunar 1/1).
        var tet = root.querySelector('[data-ttd-tet]');
        if (tet) {
            for (var i = 1; i <= 400; i++) {
                var d = new Date(today.getFullYear(), today.getMonth(), today.getDate() + i);
                var x = Lunar.fromSolar(d.getDate(), d.getMonth() + 1, d.getFullYear());
                if (x.day === 1 && x.month === 1 && !x.leap) {
                    tet.textContent = '🧧 Còn ' + i + ' ngày đến Tết';
                    tet.hidden = false;
                    break;
                }
            }
        }

        // Tear a page off: the calendar shows a studio note for a moment.
        var tips = [
            'Gọi lại khách báo giá trong 24h, tỉ lệ chốt cao gấp đôi.',
            'Ảnh sản phẩm thật luôn bán tốt hơn ảnh render.',
            'Lau bàn in bằng cồn trước mỗi mẻ PLA.',
            'Key sắp hết hạn = cơ hội gia hạn. Nhắn Zalo ngay!',
            'Đơn in gấp? Báo trước ngày giao để khách yên tâm.',
            'Sấy nhựa PETG 4 tiếng ở 65°C nếu thấy sợi tơ.',
            'Một câu cảm ơn sau khi giao hàng = một khách quay lại.'
        ];
        var tip = 0, timer;
        cal.addEventListener('click', function () {
            if (reduceMotion) return;
            cal.classList.remove('is-tearing');
            void cal.offsetWidth;
            cal.classList.add('is-tearing');
            slot.innerHTML = '💡 ' + tips[tip++ % tips.length];
            clearTimeout(timer);
            timer = setTimeout(function () {
                cal.classList.remove('is-tearing');
                slot.innerHTML = lunarHtml;
            }, 5000);
        });
    })();

    // ---------- Count-up numbers ----------

    (function () {
        if (reduceMotion || !('IntersectionObserver' in window)) return;
        var nodes = root.querySelectorAll('[data-ttd-count]');
        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                io.unobserve(entry.target);
                var node = entry.target;
                var target = parseFloat(node.getAttribute('data-ttd-count')) || 0;
                var isMoney = node.getAttribute('data-money') === '1';
                var finalText = node.textContent;
                if (target <= 0) return;
                var start = performance.now(), dur = 900;
                (function step(t) {
                    var p = Math.min(1, (t - start) / dur);
                    var e = 1 - Math.pow(1 - p, 3);
                    var v = target * e;
                    node.textContent = p < 1 ? (isMoney ? money(v) : Math.round(v).toLocaleString('vi-VN')) : finalText;
                    if (p < 1) requestAnimationFrame(step);
                })(start);
            });
        });
        nodes.forEach(function (n) { io.observe(n); });
    })();

    // ---------- Sparklines ----------

    root.querySelectorAll('[data-ttd-spark]').forEach(function (svg) {
        var values = (svg.getAttribute('data-ttd-spark') || '').split(',').map(Number).filter(function (x) { return !isNaN(x); });
        if (values.length < 2) return;
        var max = Math.max.apply(null, values) || 1;
        var step = 100 / (values.length - 1);
        var pts = values.map(function (v, i) { return [i * step, 26 - (v / max) * 22]; });
        var line = pts.map(function (p, i) { return (i ? 'L' : 'M') + p[0].toFixed(2) + ' ' + p[1].toFixed(2); }).join(' ');
        el('path', { d: line + ' L100 28 L0 28 Z', class: 'area' }, svg);
        el('path', { d: line, class: 'line' }, svg);
    });

    // ---------- Relative times and avatar colors ----------

    (function () {
        var now = new Date();
        root.querySelectorAll('[data-ttd-ago]').forEach(function (node) {
            var t = new Date(node.getAttribute('data-ttd-ago'));
            if (isNaN(t)) return;
            var mins = Math.round((now - t) / 60000);
            if (mins < 0 || mins > 60 * 24 * 6) return;
            node.title = node.textContent;
            node.textContent = mins < 1 ? 'vừa xong' : mins < 60 ? mins + ' phút trước' : mins < 1440 ? Math.round(mins / 60) + ' giờ trước' : Math.round(mins / 1440) + ' ngày trước';
        });

        var tones = ['#ffe348', '#80e5cb', '#c9a6f7', '#ff9fd3', '#9fd8ff', '#ffc38a'];
        root.querySelectorAll('[data-ttd-avatar]').forEach(function (node) {
            var s = node.getAttribute('data-ttd-avatar'), h = 0;
            for (var i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) | 0;
            node.style.setProperty('--av', tones[Math.abs(h) % tones.length]);
        });
    })();

    // ---------- Revenue chart ----------

    var days = (data.days || []).map(function (d) { return { date: parseDate(d[0]), revenue: +d[1], orders: +d[2] }; });

    (function () {
        var box = root.querySelector('[data-ttd-chart]');
        if (!box || !days.length) return;
        var svg = box.querySelector('[data-ttd-chart-svg]');
        var tip = box.querySelector('[data-ttd-tip]');
        var sum = box.querySelector('[data-ttd-chart-sum]');
        var ghostLegend = box.querySelector('[data-ttd-ghost-legend]');
        var range = '30';

        function series() {
            if (range !== '12m') {
                var n = +range;
                return {
                    items: days.slice(-n).map(function (d) {
                        return { label: dm(d.date), title: WEEKDAYS[d.date.getDay()] + ', ' + dm(d.date), revenue: d.revenue, orders: d.orders, date: d.date };
                    }),
                    previous: days.slice(-2 * n, -n),
                    unit: 'ngày'
                };
            }

            var last = days[days.length - 1].date, items = [];
            for (var i = 11; i >= 0; i--) {
                var m = new Date(last.getFullYear(), last.getMonth() - i, 1);
                var bucket = days.filter(function (d) { return d.date.getFullYear() === m.getFullYear() && d.date.getMonth() === m.getMonth(); });
                items.push({
                    label: 'T' + (m.getMonth() + 1),
                    title: 'Tháng ' + (m.getMonth() + 1) + '/' + m.getFullYear(),
                    revenue: bucket.reduce(function (a, d) { return a + d.revenue; }, 0),
                    orders: bucket.reduce(function (a, d) { return a + d.orders; }, 0),
                    month: true
                });
            }
            var current = items[items.length - 1];
            current.ghost = Math.max(0, (data.forecast || 0) - current.revenue);
            return { items: items, previous: null, unit: 'tháng' };
        }

        function niceMax(v) {
            if (v <= 0) return 1;
            var p = Math.pow(10, Math.floor(Math.log10(v))), f = v / p;
            return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10) * p;
        }

        function render() {
            var s = series(), items = s.items;
            var W = svg.clientWidth || 600, H = Math.max(260, Math.min(520, svg.clientHeight || 300)), L = 46, R = 8, T = 22, B = 26;
            svg.setAttribute('viewBox', '0 0 ' + W + ' ' + H);
            while (svg.firstChild) svg.removeChild(svg.firstChild);

            var defs = el('defs', {}, svg);
            var pat = el('pattern', { id: 'ttd-hatch', width: 6, height: 6, patternUnits: 'userSpaceOnUse', patternTransform: 'rotate(45)' }, defs);
            el('rect', { width: 6, height: 6, fill: '#fff' }, pat);
            el('rect', { width: 2.5, height: 6, fill: '#ff67bc' }, pat);

            var max = niceMax(Math.max.apply(null, items.map(function (x) { return x.revenue + (x.ghost || 0); }).concat([1])));
            var ih = H - T - B, iw = W - L - R;
            var y = function (v) { return T + ih - (v / max) * ih; };

            for (var g = 0; g <= 4; g++) {
                var gv = max * g / 4;
                el('line', { x1: L, x2: W - R, y1: y(gv), y2: y(gv), class: 'grid' }, svg);
                el('text', { x: L - 8, y: y(gv) + 4, 'text-anchor': 'end', class: 'axis' }, svg).textContent = short(gv);
            }

            var n = items.length, slot = iw / n, bw = Math.max(3, Math.min(34, slot * (n > 60 ? .7 : .62)));
            var labelEvery = Math.ceil(n / Math.max(4, Math.floor(iw / 58)));
            var best = -1, bestV = 0;
            items.forEach(function (it, i) { if (it.revenue > bestV) { bestV = it.revenue; best = i; } });

            var bars = [];
            items.forEach(function (it, i) {
                var cx = L + slot * i + slot / 2, x = cx - bw / 2;
                if (it.ghost > 0) {
                    el('rect', { x: x, y: y(it.revenue + it.ghost), width: bw, height: Math.max(0, y(it.revenue) - y(it.revenue + it.ghost)), rx: Math.min(6, bw / 3), class: 'bar ghost' }, svg);
                }
                var h = Math.max(it.revenue > 0 ? 2 : 3, T + ih - y(it.revenue));
                var cls = 'bar' + (it.revenue <= 0 ? ' zero' : '') + (i === n - 1 && !it.month ? ' today' : '');
                var bar = el('rect', { x: x, y: T + ih - h, width: bw, height: h, rx: Math.min(6, bw / 3), class: cls }, svg);
                if (!reduceMotion) bar.style.animationDelay = (i * Math.min(25, 600 / n)) + 'ms';
                bars.push(bar);

                if (i % labelEvery === (n - 1) % labelEvery) {
                    el('text', { x: cx, y: H - 6, 'text-anchor': 'middle', class: 'axis' }, svg).textContent = it.label;
                }
            });

            // Record bar: a small crown.
            if (best >= 0 && bestV > 0) {
                var bx = L + slot * best + slot / 2, by = y(bestV) - 6;
                el('path', {
                    d: 'M' + (bx - 8) + ' ' + by + ' l2 -10 l4 5 l2 -7 l2 7 l4 -5 l2 10 z',
                    fill: '#ffe348', stroke: '#20201f', 'stroke-width': 1.5, 'stroke-linejoin': 'round'
                }, svg);
            }

            // Average line.
            var total = items.reduce(function (a, x) { return a + x.revenue; }, 0);
            var orders = items.reduce(function (a, x) { return a + x.orders; }, 0);
            var avg = total / n;
            if (avg > 0) {
                el('line', { x1: L, x2: W - R, y1: y(avg), y2: y(avg), class: 'avg' }, svg);
                var lbl = el('text', { x: W - R, y: y(avg) - 5, 'text-anchor': 'end', class: 'avg-label' }, svg);
                lbl.textContent = 'TB ' + short(avg) + '/' + s.unit;
            }

            // Hover targets.
            items.forEach(function (it, i) {
                var hit = el('rect', { x: L + slot * i, y: T, width: slot, height: ih, class: 'hit' }, svg);
                hit.addEventListener('mouseenter', function () {
                    bars[i].classList.add('hover');
                    tip.innerHTML = '<div>' + it.title + '</div><b>' + money(it.revenue) + '</b><div>' + it.orders + ' đơn' +
                        (it.ghost > 0 ? ' · dự báo ' + short(it.revenue + it.ghost) : '') + (i === best && bestV > 0 ? ' · 👑 cao nhất' : '') + '</div>';
                    tip.hidden = false;
                    var px = (L + slot * i + slot / 2) / W * svg.clientWidth;
                    tip.style.left = Math.min(Math.max(px, 70), svg.clientWidth - 70) + 'px';
                    tip.style.top = (y(it.revenue + (it.ghost || 0)) / H * svg.clientHeight) + 'px';
                });
                hit.addEventListener('mouseleave', function () {
                    bars[i].classList.remove('hover');
                    tip.hidden = true;
                });
            });

            // Summary with the previous period of the same length.
            var html = 'Tổng <b>' + money(total) + '</b> · ' + orders + ' đơn';
            if (s.previous && s.previous.length) {
                var prev = s.previous.reduce(function (a, d) { return a + d.revenue; }, 0);
                if (prev > 0) {
                    var pct = Math.round((total - prev) / prev * 100);
                    html += ' · <span class="ttd-delta ' + (pct >= 0 ? 'up' : 'down') + '">' + (pct >= 0 ? '↗ ' : '↘ ') + Math.abs(pct) + '%</span> so với ' + s.items.length + ' ngày trước đó';
                }
            }
            sum.innerHTML = html;
            if (ghostLegend) ghostLegend.hidden = range !== '12m';
        }

        box.querySelectorAll('[data-range]').forEach(function (b) {
            b.addEventListener('click', function () {
                range = b.getAttribute('data-range');
                box.querySelectorAll('[data-range]').forEach(function (x) { x.classList.toggle('active', x === b); });
                tip.hidden = true;
                render();
            });
        });

        var rt;
        window.addEventListener('resize', function () { clearTimeout(rt); rt = setTimeout(render, 150); });
        render();
    })();

    // ---------- Store rhythm heatmap (as many weeks as fit, 13–52) ----------

    (function () {
        var host = root.querySelector('[data-ttd-heat]');
        if (!host || !days.length) return;

        var today = days[days.length - 1].date;
        var weeks = Math.max(13, Math.min(52, Math.floor(((host.clientWidth || 600) - 36) / 18)));
        // Monday of the first week.
        var offset = (today.getDay() + 6) % 7;
        var start = new Date(today.getFullYear(), today.getMonth(), today.getDate() - offset - (weeks - 1) * 7);
        var byKey = {};
        days.forEach(function (d) { byKey[d.date.toDateString()] = d; });

        var shown = days.filter(function (d) { return d.date >= start; });
        var max = Math.max.apply(null, shown.map(function (d) { return d.revenue; }).concat([0]));

        var wrap = document.createElement('div');
        wrap.className = 'ttd-heat-wrap';
        var labels = document.createElement('div');
        labels.className = 'ttd-heat-days';
        ['', 'T2', '', 'T4', '', 'T6', '', 'CN'].forEach(function (t) { var s = document.createElement('span'); s.textContent = t; labels.appendChild(s); });
        wrap.appendChild(labels);

        var grid = document.createElement('div');
        grid.className = 'ttd-heat-grid';
        var lastMonth = -1;
        var weekday = [0, 0, 0, 0, 0, 0, 0];

        for (var w = 0; w < weeks; w++) {
            var first = new Date(start.getFullYear(), start.getMonth(), start.getDate() + w * 7);
            var lbl = document.createElement('span');
            lbl.className = 'lbl';
            if (first.getMonth() !== lastMonth) {
                lbl.textContent = 'Th' + (first.getMonth() + 1);
                lastMonth = first.getMonth();
            }
            grid.appendChild(lbl);

            for (var i = 0; i < 7; i++) {
                var d = new Date(first.getFullYear(), first.getMonth(), first.getDate() + i);
                var cell = document.createElement('span');
                if (d > today) {
                    cell.style.visibility = 'hidden';
                    cell.className = 'ttd-heat-cell';
                    grid.appendChild(cell);
                    continue;
                }
                var rec = byKey[d.toDateString()] || { revenue: 0, orders: 0 };
                var level = rec.revenue <= 0 || max <= 0 ? 0 : Math.max(1, Math.ceil(rec.revenue / max * 4));
                cell.className = 'ttd-heat-cell l' + level + (d.getTime() === today.getTime() ? ' is-today' : '');
                cell.title = WEEKDAYS[d.getDay()] + ' ' + dm(d) + ': ' + rec.orders + ' đơn · ' + money(rec.revenue);
                weekday[(d.getDay() + 6) % 7] += rec.orders;
                grid.appendChild(cell);
            }
        }
        wrap.appendChild(grid);
        host.appendChild(wrap);
        host.scrollLeft = host.scrollWidth;

        var sumNode = root.querySelector('[data-ttd-heat-sum]');
        if (sumNode) {
            var totalOrders = shown.reduce(function (a, d) { return a + d.orders; }, 0);
            var activeDays = shown.filter(function (d) { return d.orders > 0; }).length;
            var bestDay = weekday.indexOf(Math.max.apply(null, weekday));
            sumNode.textContent = totalOrders + ' đơn trong ' + weeks + ' tuần · ' + activeDays + ' ngày có đơn' +
                (totalOrders > 0 ? ' · bận nhất vào ' + WEEKDAYS[(bestDay + 1) % 7] : '');
        }
    })();

    // ---------- Busy hours ----------

    (function () {
        var host = root.querySelector('[data-ttd-hours]');
        var best = root.querySelector('[data-ttd-hours-best]');
        var hours = data.hours || [];
        if (!host || hours.length !== 24) return;

        var max = Math.max.apply(null, hours);
        var ranked = hours.map(function (v, h) { return { v: v, h: h }; }).sort(function (a, b) { return b.v - a.v; });
        var top = ranked.slice(0, 3).filter(function (x) { return x.v > 0; }).map(function (x) { return x.h; });
        var avg = hours.reduce(function (a, v) { return a + v; }, 0) / 24;

        hours.forEach(function (v, h) {
            var bar = document.createElement('i');
            bar.style.height = (max > 0 ? Math.max(3, v / max * 100) : 3) + '%';
            if (!reduceMotion) bar.style.animationDelay = (h * 20) + 'ms';
            bar.className = top.indexOf(h) >= 0 ? 'top' : v > avg ? 'hot' : '';
            bar.title = h + 'h–' + (h + 1) + 'h: ' + v + ' đơn';
            host.appendChild(bar);
        });

        if (best) {
            best.innerHTML = max > 0
                ? 'Khách đặt nhiều nhất lúc <b>' + ranked[0].h + 'h–' + (ranked[0].h + 1) + 'h</b>. Trực tin nhắn khung giờ này nhé!'
                : '<span class="ttd-muted">Chưa đủ đơn để biết giờ vàng.</span>';
        }
    })();

    // ---------- Confetti when there is nothing left to do (once a day) ----------

    (function () {
        var notes = root.querySelector('[data-ttd-notes]');
        if (!notes || notes.getAttribute('data-clear') !== '1' || reduceMotion) return;
        var key = 'ttd-clear-' + (data.today || '');
        try {
            if (localStorage.getItem(key)) return;
            localStorage.setItem(key, '1');
        }
        catch (e) { }

        var layer = document.createElement('div');
        layer.className = 'ttd-confetti';
        var colors = ['#ff67bc', '#80e5cb', '#ffe348', '#c9a6f7', '#9fd8ff'];
        for (var i = 0; i < 70; i++) {
            var c = document.createElement('i');
            c.style.left = (Math.random() * 100) + 'vw';
            c.style.background = colors[i % colors.length];
            c.style.setProperty('--x', (Math.random() * 200 - 100) + 'px');
            c.style.setProperty('--rot', (Math.random() * 900 - 450) + 'deg');
            c.style.setProperty('--d', (1.8 + Math.random() * 1.6) + 's');
            c.style.setProperty('--delay', (Math.random() * .6) + 's');
            layer.appendChild(c);
        }
        document.body.appendChild(layer);
        setTimeout(function () { layer.remove(); }, 4500);
    })();

    // ---------- Quick jump palette (Ctrl+K) ----------

    (function () {
        var pal = root.querySelector('[data-ttd-palette]');
        if (!pal) return;
        var input = pal.querySelector('[data-ttd-palette-input]');
        var list = pal.querySelector('[data-ttd-palette-list]');
        var items = null, shown = [], active = 0;

        function norm(s) {
            return (s || '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').trim();
        }

        function collect() {
            var seen = {}, result = [];
            function add(text, href, group, icon) {
                text = (text || '').replace(/\s+/g, ' ').trim();
                if (!text || !href || href === '#' || href.indexOf('javascript:') === 0 || seen[href]) return;
                seen[href] = true;
                result.push({ text: text, href: href, group: group || '', icon: icon || 'bi-arrow-return-right', key: norm(text + ' ' + (group || '')) });
            }

            // Work waiting first (the sticky notes), then every admin menu entry.
            root.querySelectorAll('.ttd-note').forEach(function (n) {
                add(n.querySelector('.ttd-note-title').textContent, n.getAttribute('href'), 'Việc cần làm', 'bi-pin-angle');
            });
            document.querySelectorAll('#navbar-menu .navbar-nav > .nav-item').forEach(function (top) {
                var topLink = top.querySelector(':scope > .nav-link');
                var group = topLink ? topLink.textContent : '';
                if (topLink) add(group, topLink.getAttribute('href'), '', 'bi-signpost');
                top.querySelectorAll('.dropdown-item[href]').forEach(function (a) {
                    add(a.textContent, a.getAttribute('href'), group, 'bi-arrow-return-right');
                });
            });
            return result;
        }

        function render() {
            var q = norm(input.value);
            var words = q.split(/\s+/).filter(Boolean);
            shown = items.filter(function (it) { return words.every(function (w) { return it.key.indexOf(w) >= 0; }); }).slice(0, 40);
            active = 0;
            list.innerHTML = '';

            if (q && findUrl && /^#?[a-z]{0,3}\d+$/i.test(q)) {
                shown.unshift({ text: 'Mở đơn "' + input.value.trim() + '"', href: findUrl + '?q=' + encodeURIComponent(input.value.trim()), group: 'Tìm theo mã đơn / mã in', icon: 'bi-search' });
            }

            if (!shown.length) {
                var none = document.createElement('li');
                none.className = 'none';
                none.textContent = 'Không thấy trang nào khớp "' + input.value + '"';
                list.appendChild(none);
                return;
            }

            shown.forEach(function (it, i) {
                var li = document.createElement('li');
                var a = document.createElement('a');
                a.href = it.href;
                a.innerHTML = '<i class="bi ' + it.icon + '"></i><span></span><small></small>';
                a.querySelector('span').textContent = it.text;
                a.querySelector('small').textContent = it.group;
                li.appendChild(a);
                li.addEventListener('mousemove', function () { select(i); });
                list.appendChild(li);
            });
            select(0);
        }

        function select(i) {
            var lis = list.querySelectorAll('li');
            if (!lis.length) return;
            active = (i + lis.length) % lis.length;
            lis.forEach(function (li, j) { li.classList.toggle('active', j === active); });
            lis[active].scrollIntoView({ block: 'nearest' });
        }

        function open() {
            if (!items) items = collect();
            pal.hidden = false;
            input.value = '';
            render();
            setTimeout(function () { input.focus(); }, 10);
        }

        function close() { pal.hidden = true; }

        root.querySelectorAll('[data-ttd-jump-open]').forEach(function (b) { b.addEventListener('click', open); });
        pal.addEventListener('mousedown', function (e) { if (e.target === pal) close(); });
        input.addEventListener('input', render);
        input.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); select(active + 1); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); select(active - 1); }
            else if (e.key === 'Escape') { close(); }
            else if (e.key === 'Enter') {
                e.preventDefault();
                var target = shown[active];
                if (target) location.href = target.href;
                else if (findUrl && input.value.trim()) location.href = findUrl + '?q=' + encodeURIComponent(input.value.trim());
            }
        });
        document.addEventListener('keydown', function (e) {
            if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
                e.preventDefault();
                if (pal.hidden) open(); else close();
            }
            else if (e.key === '/' && pal.hidden && !/^(input|textarea|select)$/i.test(document.activeElement.tagName) && !document.activeElement.isContentEditable) {
                e.preventDefault();
                open();
            }
        });
    })();
})();

/* TT Minimal name plate preview: a plate with raised / engraved / flush text, drawn with WebGL.
   Everything is 2D masks first: the plate shape (with holes) and the relief (text lines, icons, border) are
   painted on canvases with the chosen font; marching squares on each mask give the side walls, the masks
   themselves (alpha test) are the top and bottom faces. So any font and shape works, without triangulation.
   Vietnamese marks missing from a font are painted next to its letters. No dependencies; loaded by studio.js on
   the name plate page. */
(function () {
    'use strict';

    // The studio's fonts. Yellowtail, Pacifico and Titan One ship as files in studio/fonts; the Google Fonts entries
    // below are only the fallback when those files are missing. Every other font comes as a file from studio/fonts
    // (see addFonts); a file named like a font here replaces it. Vietnamese marks a font lacks are drawn by
    // vietPlan / markOps, so every font takes Vietnamese names.
    var FONTS = [
        { key: 'yellowtail', name: 'Yellowtail', family: 'Yellowtail', weight: 400, google: true },
        { key: 'pacifico', name: 'Pacifico', family: 'Pacifico', weight: 400, google: true },
        { key: 'titanone', name: 'Titan One', family: 'Titan One', weight: 400, google: true }
    ];

    // Order of the studio's font list; fonts not named here follow alphabetically.
    var FONT_ORDER = ['yellowtail', 'patricktonight', 'birthdayparty', 'bollifia', 'mjmilestonescript', 'pacifico', 'mobsters', 'peanutbutter', 'titanone', 'baguetscript'];

    function fontKey(name) {
        return String(name).toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[^a-z0-9]/g, '').replace(/regular$/, '');
    }

    /**
     * Adds font files of the shop ([{ name, url }]) to the list, replacing a font of the same name.
     * The files load only when a font is shown (font chips) or used.
     */
    function addFonts(list) {
        var css = '';
        (list || []).forEach(function (f) {
            if (!f || !f.name || !f.url) return;
            var key = fontKey(f.name), family = 'TT ' + f.name.replace(/["\\]/g, '');
            var entry = { key: key, name: f.name, family: family, weight: 400 };
            var i = FONTS.map(function (x) { return x.key; }).indexOf(key);
            if (i >= 0) FONTS[i] = entry; else FONTS.push(entry);
            css += '@font-face{font-family:"' + family + '";src:url("' + encodeURI(f.url).replace(/"/g, '%22') + '");font-display:swap}';
        });
        FONTS.sort(function (a, b) {
            var ia = FONT_ORDER.indexOf(a.key), ib = FONT_ORDER.indexOf(b.key);
            if (ia < 0) ia = 999;
            if (ib < 0) ib = 999;
            return ia - ib || a.name.localeCompare(b.name);
        });
        if (css) {
            var style = document.createElement('style');
            style.textContent = css;
            document.head.appendChild(style);
        }
    }

    var SHAPES = [
        { key: 'rounded', name: 'Bo góc' },
        { key: 'rect', name: 'Vuông góc' },
        { key: 'pill', name: 'Viên thuốc' },
        { key: 'oval', name: 'Oval' },
        { key: 'tag', name: 'Thẻ treo' },
        { key: 'outline', name: 'Ôm theo chữ' }
    ];

    // Icons as filled paths in a unit box (y down). Composite cut-outs are drawn on their own canvas.
    function star(c, n, ro, ri) {
        for (var i = 0; i < n * 2; i++) {
            var a = -Math.PI / 2 + i * Math.PI / n, r = i % 2 ? ri : ro;
            c[i ? 'lineTo' : 'moveTo'](0.5 + Math.cos(a) * r, 0.54 + Math.sin(a) * r);
        }
        c.closePath();
    }
    function circle(c, x, y, r) { c.moveTo(x + r, y); c.arc(x, y, r, 0, Math.PI * 2); }
    function poly(c, pts) { pts.forEach(function (p, i) { c[i ? 'lineTo' : 'moveTo'](p[0], p[1]); }); c.closePath(); }

    var ICONS = [
        { key: 'heart', name: 'Tim', draw: function (c) {
            c.moveTo(0.5, 0.92);
            c.bezierCurveTo(0.12, 0.66, 0, 0.42, 0.06, 0.26);
            c.bezierCurveTo(0.14, 0.06, 0.42, 0.04, 0.5, 0.26);
            c.bezierCurveTo(0.58, 0.04, 0.86, 0.06, 0.94, 0.26);
            c.bezierCurveTo(1, 0.42, 0.88, 0.66, 0.5, 0.92);
        } },
        { key: 'star', name: 'Sao', draw: function (c) { star(c, 5, 0.5, 0.21); } },
        { key: 'flower', name: 'Hoa', draw: function (c) {
            for (var i = 0; i < 5; i++) { var a = -Math.PI / 2 + i * Math.PI * 2 / 5; circle(c, 0.5 + Math.cos(a) * 0.26, 0.52 + Math.sin(a) * 0.26, 0.21); }
            circle(c, 0.5, 0.52, 0.2);
        } },
        { key: 'paw', name: 'Chân mèo', draw: function (c) {
            c.ellipse(0.5, 0.68, 0.24, 0.2, 0, 0, Math.PI * 2);
            [[0.2, 0.42, 0.1], [0.38, 0.22, 0.11], [0.62, 0.22, 0.11], [0.8, 0.42, 0.1]].forEach(function (t) { circle(c, t[0], t[1], t[2]); });
        } },
        { key: 'cat', name: 'Mèo', draw: function (c) {
            poly(c, [[0.12, 0.08], [0.42, 0.34], [0.58, 0.34], [0.88, 0.08], [0.9, 0.55], [0.5, 0.6], [0.1, 0.55]]);
            c.ellipse(0.5, 0.6, 0.4, 0.32, 0, 0, Math.PI * 2);
        } },
        { key: 'crown', name: 'Vương miện', draw: function (c) {
            poly(c, [[0.06, 0.82], [0.06, 0.28], [0.3, 0.52], [0.5, 0.14], [0.7, 0.52], [0.94, 0.28], [0.94, 0.82]]);
        } },
        { key: 'moon', name: 'Trăng', cut: function (c) { c.beginPath(); circle(c, 0.66, 0.38, 0.36); c.fill(); }, draw: function (c) { circle(c, 0.48, 0.52, 0.44); } },
        { key: 'sun', name: 'Mặt trời', draw: function (c) {
            circle(c, 0.5, 0.5, 0.22);
            for (var i = 0; i < 8; i++) {
                var a = i * Math.PI / 4, ca = Math.cos(a), sa = Math.sin(a), px = -sa * 0.06, py = ca * 0.06;
                poly(c, [[0.5 + ca * 0.3 + px, 0.5 + sa * 0.3 + py], [0.5 + ca * 0.49, 0.5 + sa * 0.49], [0.5 + ca * 0.3 - px, 0.5 + sa * 0.3 - py]]);
            }
        } },
        { key: 'note', name: 'Nốt nhạc', draw: function (c) {
            c.ellipse(0.32, 0.8, 0.18, 0.13, -0.4, 0, Math.PI * 2);
            poly(c, [[0.42, 0.78], [0.42, 0.06], [0.5, 0.06], [0.5, 0.78]]);
            poly(c, [[0.5, 0.06], [0.84, 0.24], [0.84, 0.42], [0.5, 0.26]]);
        } },
        { key: 'bolt', name: 'Tia sét', draw: function (c) {
            poly(c, [[0.6, 0.02], [0.16, 0.58], [0.46, 0.58], [0.36, 0.98], [0.84, 0.38], [0.54, 0.38], [0.68, 0.02]]);
        } },
        { key: 'leaf', name: 'Lá', cut: function (c) { c.lineWidth = 0.05; c.beginPath(); c.moveTo(0.16, 0.84); c.lineTo(0.7, 0.3); c.stroke(); }, draw: function (c) {
            c.moveTo(0.1, 0.9); c.quadraticCurveTo(0.02, 0.12, 0.92, 0.08); c.quadraticCurveTo(0.96, 0.92, 0.1, 0.9);
        } },
        { key: 'ball', name: 'Bóng', cut: function (c) { c.lineWidth = 0.05; c.beginPath(); circle(c, 0.5, 0.5, 0.3); c.moveTo(0.06, 0.5); c.lineTo(0.94, 0.5); c.moveTo(0.5, 0.06); c.lineTo(0.5, 0.94); c.stroke(); }, draw: function (c) { circle(c, 0.5, 0.5, 0.46); } }
    ];

    var DEFAULTS = {
        text: '', line2: '', font: 'pacifico', upper: false, spacing: 0, textScale: 1,
        base: '#ffffff', color: '#20201f',
        length: 100, heightPct: 30, thickness: 3, shape: 'rounded', radius: 5, margin: 4,
        style: 'raised', relief: 1.6, border: false, hole: 'none', icon: '', iconSide: 'left', stand: false
    };

    var MAX_PX = 1300;     // raster width of the masks (contour detail)
    var HOLE_R = 2;        // hole radius, mm
    var FOV = 30 * Math.PI / 180;
    var TILT = 75 * Math.PI / 180;  // standing plate leans back 15°

    // Resolves once the Google Fonts stylesheet is in (or failed): before that a font load finds no face to load.
    var fontsLinked = null;
    function ensureFonts() {
        if (fontsLinked) return fontsLinked;
        var google = FONTS.filter(function (f) { return f.google; });
        if (!google.length) return (fontsLinked = Promise.resolve());
        var link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = 'https://fonts.googleapis.com/css2?' + google.map(function (f) { return 'family=' + f.family.replace(/ /g, '+') + ':wght@' + f.weight; }).join('&') + '&display=swap';
        fontsLinked = new Promise(function (resolve) { link.onload = link.onerror = function () { resolve(); }; setTimeout(resolve, 8000); });
        document.head.appendChild(link);
        return fontsLinked;
    }
    function fontOf(key) { return FONTS.filter(function (f) { return f.key === key; })[0] || FONTS[0]; }
    function iconOf(key) { return ICONS.filter(function (i) { return i.key === key; })[0] || null; }

    // Any CSS color to [r, g, b] in 0..1.
    function rgb(color) {
        var ctx = rgb.ctx || (rgb.ctx = document.createElement('canvas').getContext('2d'));
        ctx.fillStyle = '#000';
        ctx.fillStyle = color || '#000';
        var c = ctx.fillStyle;
        if (c.charAt(0) === '#') return [1, 3, 5].map(function (i) { return parseInt(c.substr(i, 2), 16) / 255; });
        var m = c.match(/[\d.]+/g) || [0, 0, 0];
        return [m[0] / 255, m[1] / 255, m[2] / 255];
    }

    // ---------- Masks ----------

    // Plate outline (centered, y down, mm) as a path on ctx.
    function shapePath(c, shape, L, H, r, inset) {
        var x = -L / 2 + inset, y = -H / 2 + inset, w = L - 2 * inset, h = H - 2 * inset;
        c.beginPath();
        if (shape === 'oval') c.ellipse(0, 0, w / 2, h / 2, 0, 0, Math.PI * 2);
        else if (shape === 'pill') roundRect(c, x, y, w, h, h / 2);
        else if (shape === 'rect') c.rect(x, y, w, h);
        else if (shape === 'tag') {
            var cut = Math.min(h * 0.38, w * 0.2), rr = Math.max(0.5, Math.min(r, h / 3) - inset * 0.3);
            c.moveTo(x + cut, y); c.lineTo(x + w - rr, y); c.arcTo(x + w, y, x + w, y + rr, rr); c.lineTo(x + w, y + h - rr);
            c.arcTo(x + w, y + h, x + w - rr, y + h, rr); c.lineTo(x + cut, y + h); c.lineTo(x, y + h - cut); c.lineTo(x, y + cut); c.closePath();
        }
        else roundRect(c, x, y, w, h, Math.max(0, Math.min(r - inset, w / 2, h / 2)));
    }
    function roundRect(c, x, y, w, h, r) {
        r = Math.max(0, Math.min(r, w / 2, h / 2));
        c.moveTo(x + r, y); c.arcTo(x + w, y, x + w, y + h, r); c.arcTo(x + w, y + h, x, y + h, r);
        c.arcTo(x, y + h, x, y, r); c.arcTo(x, y, x + w, y, r); c.closePath();
    }

    function canvas(w, h) { var c = document.createElement('canvas'); c.width = w; c.height = h; return c; }

    function fontCss(font, px) { return font.weight + ' ' + px + 'px "' + font.family + '"'; }

    // ---------- Vietnamese in any font ----------
    // Decorative fonts mostly have the plain letters and a few accented ones (á, â, ă, đ…) but not the Vietnamese
    // stacks (ấ, ợ, ử, ỵ…), and the browser would take those letters from another font. Instead, such a letter is
    // drawn as the closest letter the font has (â for ấ, o for ở) and the missing marks are painted on it, sized,
    // placed and slanted after the letter's own ink (top, stroke width, right edge, slant).

    var VI_MARKS = { 0x300: 'grave', 0x301: 'acute', 0x303: 'tilde', 0x309: 'hook', 0x323: 'dot', 0x302: 'circ', 0x306: 'breve', 0x31b: 'horn' };

    // Whether the font itself has a glyph for ch: with a missing glyph the width follows the fallback font.
    var glyphCache = {};
    function hasGlyph(font, ch) {
        var key = font.family + '\n' + ch;
        if (key in glyphCache) return glyphCache[key];
        var c = hasGlyph.ctx || (hasGlyph.ctx = canvas(4, 4).getContext('2d'));
        if ('letterSpacing' in c) c.letterSpacing = '0px';
        c.font = fontCss(font, 100) + ', monospace';
        var a = c.measureText(ch).width;
        c.font = fontCss(font, 100) + ', serif';
        var ok = Math.abs(a - c.measureText(ch).width) < 0.01;
        // Before the font file is in, everything is a fallback: do not remember that.
        if (ok || !document.fonts || document.fonts.check(fontCss(font, 100), ch)) glyphCache[key] = ok;
        return ok;
    }

    // How to draw a letter the font lacks: { base, marks } or null (the font has it, or it is no Vietnamese letter).
    function vietPlan(font, ch) {
        if (ch.charCodeAt(0) < 0xc0 || hasGlyph(font, ch)) return null;
        var base, marks = [];
        if (ch === 'đ' || ch === 'Đ') { base = ch === 'đ' ? 'd' : 'D'; marks = ['bar']; }
        else {
            var d = ch.normalize('NFD');
            base = d.charAt(0);
            for (var i = 1; i < d.length; i++) {
                if (!VI_MARKS[d.charCodeAt(i)]) return null;
                marks.push(d.charAt(i));
            }
            if (!marks.length) return null;
            // Keep the marks the font has in a letter of its own: â of ấ, ơ of ở, ă of ặ. Not ò of ờ: the horn
            // needs the bare letter to find its corner.
            var horn = marks.indexOf('̛') >= 0;
            for (i = 0; i < marks.length && marks.length > 1; i++) {
                if (horn && marks[i] !== '̛') continue;
                var part = (base + marks[i]).normalize('NFC');
                if (part.length === 1 && hasGlyph(font, part)) { base = part; marks.splice(i, 1); break; }
            }
            marks = marks.map(function (m) { return VI_MARKS[m.charCodeAt(0)]; });
            // No mark on top of the dot of i.
            if (base === 'i' && marks.some(function (m) { return m !== 'dot' && m !== 'horn'; }) && hasGlyph(font, 'ı')) base = 'ı';
        }
        return hasGlyph(font, base) ? { base: base, marks: marks } : null;
    }

    // Ink of one glyph in em units, relative to its origin on the baseline (y down): box, stroke width, slant,
    // center of its top and bottom, the top right corner (horn) and the stem a bar crosses (đ, Đ).
    var inkCache = {};
    function glyphInk(font, ch) {
        var key = font.family + '\n' + ch;
        if (inkCache[key]) return inkCache[key];
        var S = 160, W = S * 3, H = S * 2, ox = S, oy = Math.round(S * 1.4);
        var c = canvas(W, H).getContext('2d', { willReadFrequently: true });
        c.font = fontCss(font, S) + ', sans-serif';
        c.textBaseline = 'alphabetic';
        c.fillStyle = '#000';
        c.fillText(ch, ox, oy);
        var data = c.getImageData(0, 0, W, H).data, rows = [], top = -1, bottom = -1;
        for (var y = 0; y < H; y++) {
            var runs = [], start = -1;
            for (var x = 0; x <= W; x++) {
                var on = x < W && data[(y * W + x) * 4 + 3] > 127;
                if (on && start < 0) start = x;
                else if (!on && start >= 0) { runs.push([start, x]); start = -1; }
            }
            rows.push(runs);
            if (runs.length) { if (top < 0) top = y; bottom = y; }
        }
        var ink;
        if (top < 0) ink = { top: -0.5, bottom: 0, left: 0, right: 0.4, stroke: 0.08, slant: 0, topX: 0.2, bottomX: 0.2, hornX: 0.4, hornY: -0.4, barX: 0.2, barY: -0.4 };
        else {
            var h = bottom - top + 1;
            var span = function (y0, y1, pick) {
                var lo = W, hi = -1;
                for (var yy = Math.max(top, Math.round(y0)); yy <= Math.min(bottom, Math.round(y1)); yy++) {
                    rows[yy].forEach(function (r) { lo = Math.min(lo, r[0]); hi = Math.max(hi, r[1]); });
                }
                return hi < 0 ? null : pick === 'l' ? lo : pick === 'r' ? hi : (lo + hi) / 2;
            };
            var left = W, right = 0, widths = [];
            rows.forEach(function (runs, yy) {
                runs.forEach(function (r) {
                    left = Math.min(left, r[0]); right = Math.max(right, r[1]);
                    if (yy > top + h * 0.25 && yy < bottom - h * 0.25) widths.push(r[1] - r[0]);
                });
            });
            widths.sort(function (a, b) { return a - b; });
            var stroke = widths.length ? widths[Math.floor(widths.length * 0.35)] : S * 0.08;
            // Slant from the top against the middle of the body; the bottom center follows it (tails of script
            // letters would pull a measured one aside).
            var midY = top + h * 0.5, topX = span(top, top + h * 0.15), midX = span(top + h * 0.3, bottom - h * 0.3);
            if (midX == null) midX = topX;
            var slant = Math.max(0, Math.min(0.45, (topX - midX) / Math.max(1, midY - top - h * 0.075)));
            var bottomX = midX - (bottom - midY) * slant;
            // Horn: the rightmost ink in the upper part. Bar: the ascender of d, or the stem of D halfway up.
            var hornY = top + h * 0.12, hornX = span(top, top + h * 0.3, 'r');
            var lower = ch === 'd', barY = lower ? top + h * 0.2 : top + h * 0.5;
            var barX = lower ? span(top, top + h * 0.12) : (rows[Math.round(barY)][0] ? (rows[Math.round(barY)][0][0] + rows[Math.round(barY)][0][1]) / 2 : left + stroke / 2);
            var em = function (v, o) { return (v - o) / S; };
            ink = {
                top: em(top, oy), bottom: em(bottom + 1, oy), left: em(left, ox), right: em(right, ox),
                stroke: stroke / S, slant: slant, topX: em(topX, ox), bottomX: em(bottomX, ox),
                hornX: em(hornX, ox), hornY: em(hornY, oy), barX: em(barX, ox), barY: em(barY, oy), upperBar: !lower
            };
        }
        // Remember only once the font is in (see hasGlyph).
        if (!document.fonts || document.fonts.check(fontCss(font, S), ch)) inkCache[key] = ink;
        return ink;
    }

    // The marks of a plan as paths in pixels, relative to the glyph origin; box = [x0, y0, x1, y1] of the ink.
    function markOps(g, marks, px) {
        var t = Math.max(0.05, Math.min(0.13, g.stroke * 0.8)) * px, gap = 0.05 * px + t * 0.4;
        var ops = [], cur = g.top * px, slant = g.slant, shift = marks.indexOf('horn') >= 0 ? -0.04 * px : 0;   // tone marks clear the horn
        var at = function (y) { return g.topX * px + (g.top * px - y) * slant; };   // x of the slanted top axis
        marks.forEach(function (m) {
            var op;
            if (m === 'dot') {
                var r = Math.max(t * 0.62, 0.035 * px), dy = g.bottom * px + gap + r, dx = g.bottomX * px - (dy - g.bottom * px) * slant;
                op = { fill: true, box: [dx - r, dy - r, dx + r, dy + r], path: function (c) { c.moveTo(dx + r, dy); c.arc(dx, dy, r, 0, Math.PI * 2); } };
            } else if (m === 'horn') {
                // Out to the right of the letter's top right corner, then up.
                var hx = g.hornX * px - t * 0.5, hy = g.hornY * px + 0.04 * px, ex = g.hornX * px + 0.1 * px, ey = hy - 0.13 * px;
                op = { box: [hx - t, ey - t, ex + 0.03 * px + t, hy + t], path: function (c) { c.moveTo(hx, hy); c.quadraticCurveTo(ex + 0.03 * px, hy, ex, ey); } };
            } else if (m === 'bar') {
                var by = g.barY * px, bx0 = g.barX * px - (g.upperBar ? 0.11 : 0.1) * px, bx1 = g.barX * px + (g.upperBar ? 0.13 : 0.1) * px, bt = t * 0.85;
                op = { width: bt, box: [bx0 - bt, by - bt, bx1 + bt, by + bt], path: function (c) { c.moveTo(bx0, by); c.lineTo(bx1, by); } };
            } else {
                // Marks above the letter, stacked upwards.
                var mh = { acute: 0.17, grave: 0.17, hook: 0.2, tilde: 0.09, circ: 0.12, breve: 0.1 }[m] * px;
                var y0 = cur - gap - t / 2, y1 = y0 - mh, cx = at((y0 + y1) / 2) + shift, k = slant * mh;
                var draw = {
                    acute: function (c) { c.moveTo(cx - 0.045 * px - k / 2, y0); c.lineTo(cx + 0.055 * px + k / 2, y1); },
                    grave: function (c) { c.moveTo(cx + 0.045 * px - k / 2, y0); c.lineTo(cx - 0.055 * px + k / 2, y1); },
                    circ: function (c) { c.moveTo(cx - 0.11 * px - k / 2, y0); c.lineTo(cx + k / 2, y1); c.lineTo(cx + 0.11 * px - k / 2, y0); },
                    breve: function (c) { c.moveTo(cx - 0.1 * px + k / 2, y1); c.quadraticCurveTo(cx - k / 2, y0 + mh * 0.7, cx + 0.1 * px + k / 2, y1); },
                    tilde: function (c) { c.moveTo(cx - 0.11 * px - k / 2, y0); c.bezierCurveTo(cx - 0.06 * px, y1 - mh * 0.6, cx + 0.03 * px, y0 + mh * 0.6, cx + 0.11 * px + k / 2, y1); },
                    hook: function (c) {
                        c.moveTo(cx - 0.065 * px + k * 0.75, y1 + mh * 0.3);
                        c.bezierCurveTo(cx - 0.05 * px + k, y1 - mh * 0.12, cx + 0.085 * px + k, y1 - mh * 0.08, cx + 0.07 * px + k * 0.6, y1 + mh * 0.38);
                        c.quadraticCurveTo(cx + 0.055 * px + k * 0.4, y1 + mh * 0.58, cx + k * 0.15, y1 + mh * 0.62);
                        c.lineTo(cx - k / 2, y0);
                    }
                }[m];
                var wx = 0.13 * px + k;
                op = { box: [cx - wx - t, y1 - t, cx + wx + t, y0 + t], path: draw };
                cur = y1 - t / 2;
            }
            ops.push(op);
        });
        ops.forEach(function (op) { op.width = op.width || t; });
        return ops;
    }

    // Lays out one line of text: the letters to draw with the font and the marks to paint over them.
    function measure(ctx, text, font, px, spacing) {
        var parts = [], drawn = '';
        Array.from(String(text).normalize('NFC')).forEach(function (ch) {
            var p = vietPlan(font, ch);
            if (p) parts.push({ at: drawn, base: p.base, marks: p.marks });
            drawn += p ? p.base : ch;
        });
        ctx.font = fontCss(font, px) + ', Arial, sans-serif';
        if ('letterSpacing' in ctx) ctx.letterSpacing = (spacing * px) + 'px';
        var m = ctx.measureText(drawn);
        var x0 = -(m.actualBoundingBoxLeft || 0), x1 = m.actualBoundingBoxRight || m.width;
        var y0 = -(m.actualBoundingBoxAscent || px * 0.75), y1 = m.actualBoundingBoxDescent || px * 0.25;
        var marks = [];
        parts.forEach(function (p) {
            var dx = p.at ? ctx.measureText(p.at).width : 0;
            markOps(glyphInk(font, p.base), p.marks, px).forEach(function (op) {
                op.dx = dx;
                x0 = Math.min(x0, dx + op.box[0]); x1 = Math.max(x1, dx + op.box[2]);
                y0 = Math.min(y0, op.box[1]); y1 = Math.max(y1, op.box[3]);
                marks.push(op);
            });
        });
        return {
            l: -x0, w: Math.max(0, x1 - x0), a: -y0, h: Math.max(0, y1 - y0),
            // Draws the line with its origin (left end of the baseline) at x, y; uses the font set by measure.
            draw: function (c, x, y) {
                c.fillText(drawn, x, y);
                if (!marks.length) return;
                c.save();
                c.strokeStyle = c.fillStyle;
                c.lineCap = c.lineJoin = 'round';
                marks.forEach(function (op) {
                    c.beginPath();
                    c.save();
                    c.translate(x + op.dx, y);
                    op.path(c);
                    c.restore();
                    if (op.fill) c.fill();
                    else { c.lineWidth = op.width; c.stroke(); }
                });
                c.restore();
            }
        };
    }

    // Joins every separate piece of the plate (hole bosses, letters or icons far apart) to the largest piece with a
    // bar along the shortest way, so the print always comes out in one piece. Works on a 0.5 mm grid.
    function connect(bc, w, h, R, FW, FH, barW) {
        var g = Math.max(1, Math.round(R * 0.5)), gw = Math.ceil(w / g), gh = Math.ceil(h / g), n = gw * gh;
        var data = bc.getImageData(0, 0, w, h).data, full = new Uint8Array(n), label = new Int32Array(n).fill(-1);
        for (var y = 0; y < gh; y++) {
            for (var x = 0; x < gw; x++) {
                var px = Math.min(w - 1, x * g + (g >> 1)), py = Math.min(h - 1, y * g + (g >> 1));
                full[y * gw + x] = data[(py * w + px) * 4 + 3] > 127 ? 1 : 0;
            }
        }
        var queue = new Int32Array(n), sizes = [];
        function neighbours(i, fn) {
            var x = i % gw, y = (i - x) / gw;
            for (var dy = -1; dy <= 1; dy++) {
                for (var dx = -1; dx <= 1; dx++) {
                    var nx = x + dx, ny = y + dy;
                    if ((dx || dy) && nx >= 0 && ny >= 0 && nx < gw && ny < gh) fn(ny * gw + nx);
                }
            }
        }
        for (var i = 0; i < n; i++) {
            if (!full[i] || label[i] >= 0) continue;
            var id = sizes.length, head = 0, tail = 0;
            label[i] = id; queue[tail++] = i;
            while (head < tail) neighbours(queue[head++], function (j) { if (full[j] && label[j] < 0) { label[j] = id; queue[tail++] = j; } });
            sizes.push(tail);
        }
        if (sizes.length < 2) return;

        var main = sizes.indexOf(Math.max.apply(null, sizes)), joined = new Uint8Array(sizes.length), from = new Int32Array(n);
        joined[main] = 1;
        function mm(i) { var x = i % gw, y = (i - x) / gw; return [(x * g + g / 2) / R - FW / 2, (y * g + g / 2) / R - FH / 2]; }
        bc.save();
        bc.strokeStyle = '#fff';
        bc.lineWidth = barW;
        bc.lineCap = 'round';
        for (var pass = 1; pass < sizes.length; pass++) {
            // Breadth-first search from all pieces not joined yet until a joined one is reached.
            from.fill(-1);
            var head2 = 0, tail2 = 0, hit = -1;
            for (i = 0; i < n; i++) if (full[i] && !joined[label[i]]) { from[i] = i; queue[tail2++] = i; }
            while (head2 < tail2 && hit < 0) {
                var cur = queue[head2++];
                neighbours(cur, function (j) {
                    if (hit >= 0 || from[j] >= 0) return;
                    from[j] = from[cur];
                    if (full[j] && joined[label[j]]) hit = j; else queue[tail2++] = j;
                });
            }
            if (hit < 0) break;
            var a = mm(from[hit]), b = mm(hit);
            bc.beginPath(); bc.moveTo(a[0], a[1]); bc.lineTo(b[0], b[1]); bc.stroke();
            joined[label[from[hit]]] = 1;
        }
        bc.restore();
    }

    // Builds the plate and relief masks for a spec. R = mask pixels per mm; frame = mask size in mm (centered).
    function masks(s) {
        var font = fontOf(s.font), L = s.length, H = Math.max(8, L * s.heightPct / 100), m = s.margin;
        var R = Math.min(9, MAX_PX / (L + 4)), w = Math.ceil((L + 4) * R), h = Math.ceil((H + 4) * R);
        var FW = w / R, FH = h / R;
        var relief = canvas(w, h), rc = relief.getContext('2d');
        var base = canvas(w, h), bc = base.getContext('2d');
        // mm, y down, origin at the plate center.
        rc.setTransform(R, 0, 0, R, FW / 2 * R, FH / 2 * R);
        bc.setTransform(R, 0, 0, R, FW / 2 * R, FH / 2 * R);
        rc.fillStyle = bc.fillStyle = rc.strokeStyle = '#fff';

        var vi = 'vi';
        var line1 = s.upper ? s.text.toLocaleUpperCase(vi) : s.text, line2 = s.upper ? s.line2.toLocaleUpperCase(vi) : s.line2;
        var icon = iconOf(s.icon), nIcons = icon ? (s.iconSide === 'both' ? 2 : 1) : 0;

        // Content in reference pixels: text block (two centered lines) with icons beside it.
        var ref = 100, mc = canvas(4, 4).getContext('2d');
        var t1 = line1 ? measure(mc, line1, font, ref, s.spacing) : null;
        var t2 = line2 ? measure(mc, line2, font, ref * 0.42, s.spacing) : null;
        var gap = t1 && t2 ? ref * 0.14 : 0;
        var tw = Math.max(t1 ? t1.w : 0, t2 ? t2.w : 0), th = (t1 ? t1.h : 0) + gap + (t2 ? t2.h : 0);
        var iconSize = icon ? (th ? th * (t2 ? 0.8 : 0.95) : ref) : 0, iconGap = icon && tw ? iconSize * 0.2 : 0;
        var cw = tw + nIcons * (iconSize + iconGap), ch = Math.max(th, iconSize);

        // Room for the content: margins, and the holes beside or above it.
        var holeSide = 2 * HOLE_R + 2.5, aw = L - 2 * m, ah = H - 2 * m, cx = 0, cy = 0;
        if (s.hole === 'left') { aw -= holeSide; cx = holeSide / 2; }
        else if (s.hole === 'top1' || (s.hole === 'top2' && s.shape === 'outline')) { ah -= holeSide; cy = holeSide / 2; }
        else if (s.hole === 'top2') aw -= 2 * holeSide;
        if (s.border && s.shape !== 'outline') { aw -= 3; ah -= 3; }

        var k = cw > 0 && ch > 0 && aw > 0 && ah > 0 ? Math.min(aw / cw, ah / ch) * s.textScale : 0;  // mm per reference px
        var left = cx - cw * k / 2;
        if (k > 0) {
            var tl = left + (icon && s.iconSide !== 'right' ? (iconSize + iconGap) * k : 0), tt = cy - th * k / 2;
            // Text is drawn in pixels (canvas fonts do not scale well below 1px).
            rc.save();
            rc.setTransform(1, 0, 0, 1, 0, 0);
            var X = function (x) { return (x + FW / 2) * R; }, Y = function (y) { return (y + FH / 2) * R; };
            rc.textBaseline = 'alphabetic';
            rc.textAlign = 'left';
            if (t1) {
                measure(rc, line1, font, ref * k * R, s.spacing).draw(rc, X(tl + ((tw - t1.w) / 2 + t1.l) * k), Y(tt + t1.a * k));
            }
            if (t2) {
                measure(rc, line2, font, ref * 0.42 * k * R, s.spacing).draw(rc, X(tl + ((tw - t2.w) / 2 + t2.l) * k), Y(tt + ((t1 ? t1.h : 0) + gap + t2.a) * k));
            }
            rc.restore();

            if (icon) {
                var size = iconSize * k, ic = canvas(Math.max(2, Math.ceil(size * R)), Math.max(2, Math.ceil(size * R))), icc = ic.getContext('2d');
                icc.setTransform(ic.width, 0, 0, ic.height, 0, 0);
                icc.fillStyle = icc.strokeStyle = '#fff';
                icc.beginPath(); icon.draw(icc); icc.fill();
                if (icon.cut) { icc.globalCompositeOperation = 'destination-out'; icon.cut(icc); }
                var put = function (x) { rc.drawImage(ic, x, cy - size / 2, size, size); };
                if (s.iconSide !== 'right') put(left);
                if (s.iconSide !== 'left') put(left + cw * k - size);
            }
        }

        // Plate: the shape, or the content grown by the margin ("outline").
        if (s.shape === 'outline') {
            bc.save();
            bc.setTransform(1, 0, 0, 1, 0, 0);
            var dr = m * R;
            [[1, 30], [0.66, 20], [0.33, 10]].forEach(function (ring) {
                for (var i = 0; i < ring[1]; i++) {
                    var a = i * Math.PI * 2 / ring[1];
                    bc.drawImage(relief, Math.cos(a) * dr * ring[0], Math.sin(a) * dr * ring[0]);
                }
            });
            bc.drawImage(relief, 0, 0);
            bc.restore();
            if (k <= 0) { shapePath(bc, 'pill', Math.min(L, H * 2), H, H / 2, 0); bc.fill(); }
        } else {
            shapePath(bc, s.shape, L, H, s.radius, 0);
            bc.fill();
            if (s.border) {
                var bi = Math.min(m * 0.4, 2) + 0.6;
                rc.lineWidth = 1.2;
                shapePath(rc, s.shape, L, H, s.radius, bi);
                rc.stroke();
            }
        }

        // Holes: a boss so the hole always has material around it, then the cut through plate and relief.
        // On an "outline" plate the holes sit right next to the content.
        var holes = [], hug = s.shape === 'outline' && k > 0, top = cy - ch * k / 2 - m - HOLE_R * 0.3;
        if (s.hole === 'left') holes.push(hug ? [left - m - HOLE_R * 0.4, cy] : [-L / 2 + 2.5 + HOLE_R, 0]);
        else if (hug && s.hole === 'top1') holes.push([cx, top]);
        else if (hug && s.hole === 'top2') holes.push([left + HOLE_R + 1.5, top], [left + cw * k - HOLE_R - 1.5, top]);
        else if (s.hole === 'top1') holes.push([0, -H / 2 + 2.5 + HOLE_R]);
        else if (s.hole === 'top2') holes.push([-L / 2 + 2.5 + HOLE_R + (s.shape === 'oval' || s.shape === 'pill' ? H * 0.18 : 0), -H / 2 + 2.5 + HOLE_R], [L / 2 - 2.5 - HOLE_R - (s.shape === 'oval' || s.shape === 'pill' ? H * 0.18 : 0), -H / 2 + 2.5 + HOLE_R]);
        holes.forEach(function (p) { bc.beginPath(); circle(bc, p[0], p[1], HOLE_R + 2.5); bc.fill(); });
        connect(bc, w, h, R, FW, FH, Math.max(3, Math.min(5, m + 1)));
        bc.globalCompositeOperation = rc.globalCompositeOperation = 'destination-out';
        holes.forEach(function (p) {
            bc.beginPath(); circle(bc, p[0], p[1], HOLE_R); bc.fill();
            rc.beginPath(); circle(rc, p[0], p[1], HOLE_R + 1); rc.fill();
        });
        // The relief never sticks out of the plate.
        rc.globalCompositeOperation = 'destination-in';
        rc.setTransform(1, 0, 0, 1, 0, 0);
        rc.drawImage(base, 0, 0);

        var fb = bc.getImageData(0, 0, w, h).data, fr = rc.getImageData(0, 0, w, h).data;
        var n = w * h, B = new Float32Array(n), Rf = new Float32Array(n), tex = new Uint8Array(n * 4), any = false;
        var minX = w, maxX = -1, minY = h, maxY = -1;
        for (var i = 0, x = 0, y = 0; i < n; i++) {
            var b = fb[i * 4 + 3], r = fr[i * 4 + 3];
            B[i] = b / 255; Rf[i] = r / 255;
            tex[i * 4] = b; tex[i * 4 + 1] = r; tex[i * 4 + 3] = 255;
            if (r > 127) any = true;
            if (b > 127) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            if (++x === w) { x = 0; y++; }
        }
        return {
            w: w, h: h, R: R, FW: FW, FH: FH, base: B, relief: Rf, tex: tex, hasRelief: any, baseCanvas: base, reliefCanvas: relief,
            size: maxX < 0 ? [L, H] : [(maxX - minX + 1) / R, (maxY - minY + 1) / R]
        };
    }

    // ---------- Geometry: interleaved position (3) + normal (3) ----------

    // Marching squares (iso 0.5) over a mask: every contour segment becomes a wall quad from z0 to z1. Cell edges
    // are shared by neighbouring cells, so normals summed per edge give smooth shading along the curves.
    // Pairs of cell edges (0 top, 1 right, 2 bottom, 3 left) per corner case; 5 and 10 are decided by the center.
    var CASES = [null, [3, 0], [0, 1], [3, 1], [1, 2], null, [0, 2], [3, 2], [2, 3], [0, 2], null, [1, 2], [1, 3], [0, 1], [3, 0], null];

    function walls(M, f, z0, z1, flip) {
        var w = M.w, h = M.h, t = 0.5, segs = [], acc = new Map();
        var px = new Float64Array(4), py = new Float64Array(4), id = new Float64Array(4);
        function add(key, nx, ny) {
            var v = acc.get(key);
            if (v) { v[0] += nx; v[1] += ny; } else acc.set(key, [nx, ny]);
        }
        for (var j = 0; j < h - 1; j++) {
            for (var i = 0; i < w - 1; i++) {
                var o = j * w + i, a = f[o], b = f[o + 1], c = f[o + w + 1], d = f[o + w];
                var idx = (a > t ? 1 : 0) | (b > t ? 2 : 0) | (c > t ? 4 : 0) | (d > t ? 8 : 0);
                if (idx === 0 || idx === 15) continue;
                var pairs = CASES[idx];
                if (!pairs) {
                    var inside = (a + b + c + d) / 4 > t;
                    pairs = (idx === 5) === inside ? [0, 1, 2, 3] : [3, 0, 1, 2];
                }
                px[0] = i + (t - a) / (b - a); py[0] = j; id[0] = o * 2;
                px[1] = i + 1; py[1] = j + (t - b) / (c - b); id[1] = (o + 1) * 2 + 1;
                px[2] = i + (t - d) / (c - d); py[2] = j + 1; id[2] = (o + w) * 2;
                px[3] = i; py[3] = j + (t - a) / (d - a); id[3] = o * 2 + 1;
                // Outward = down the coverage gradient.
                var gx = (b + c - a - d) / 2, gy = (d + c - a - b) / 2;
                for (var p = 0; p < pairs.length; p += 2) {
                    var e1 = pairs[p], e2 = pairs[p + 1];
                    var nx = py[e2] - py[e1], ny = px[e1] - px[e2];
                    if (nx * gx + ny * gy > 0) { nx = -nx; ny = -ny; }
                    add(id[e1], nx, ny); add(id[e2], nx, ny);
                    segs.push(px[e1], py[e1], id[e1], px[e2], py[e2], id[e2]);
                }
            }
        }
        // Mask pixel (x + 0.5, y + 0.5) is the sample; y goes up in mm.
        var out = new Float32Array(segs.length / 6 * 36), q = 0, sg = flip ? -1 : 1;
        function vert(x, y, key, z) {
            var nn = acc.get(key), l = (Math.hypot(nn[0], nn[1]) || 1) * sg;
            out[q++] = (x + 0.5) / M.R - M.FW / 2; out[q++] = M.FH / 2 - (y + 0.5) / M.R; out[q++] = z;
            out[q++] = nn[0] / l; out[q++] = -nn[1] / l; out[q++] = 0;
        }
        for (var s = 0; s < segs.length; s += 6) {
            vert(segs[s], segs[s + 1], segs[s + 2], z0); vert(segs[s + 3], segs[s + 4], segs[s + 5], z0); vert(segs[s + 3], segs[s + 4], segs[s + 5], z1);
            vert(segs[s], segs[s + 1], segs[s + 2], z0); vert(segs[s + 3], segs[s + 4], segs[s + 5], z1); vert(segs[s], segs[s + 1], segs[s + 2], z1);
        }
        return out;
    }

    function frameQuad(M, z, nz) {
        var x = M.FW / 2, y = M.FH / 2;
        return new Float32Array([-x, -y, z, 0, 0, nz, x, -y, z, 0, 0, nz, x, y, z, 0, 0, nz, -x, -y, z, 0, 0, nz, x, y, z, 0, 0, nz, -x, y, z, 0, 0, nz]);
    }

    function box(x0, x1, y0, y1, z0, z1) {
        var a = [];
        function q(p1, p2, p3, p4, n) { [p1, p2, p3, p1, p3, p4].forEach(function (p) { a.push(p[0], p[1], p[2], n[0], n[1], n[2]); }); }
        q([x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1], [0, 0, 1]);
        q([x0, y0, z0], [x1, y0, z0], [x1, y1, z0], [x0, y1, z0], [0, 0, -1]);
        q([x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1], [0, -1, 0]);
        q([x0, y1, z0], [x1, y1, z0], [x1, y1, z1], [x0, y1, z1], [0, 1, 0]);
        q([x0, y0, z0], [x0, y1, z0], [x0, y1, z1], [x0, y0, z1], [-1, 0, 0]);
        q([x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1], [1, 0, 0]);
        return new Float32Array(a);
    }

    // ---------- WebGL ----------

    var VS = 'attribute vec3 aPos; attribute vec3 aNor; uniform mat4 uMVP; uniform mat3 uRot; uniform vec3 uTrans; uniform vec4 uTexRect;' +
        'varying vec3 vN; varying vec3 vP; varying vec2 vUV;' +
        'void main() { vec3 p = uRot * aPos + uTrans; vN = uRot * aNor; vP = p; vUV = (aPos.xy - uTexRect.xy) / uTexRect.zw; gl_Position = uMVP * vec4(p, 1.0); }';

    // uMode: 0 solid, 1 inside the plate mask, 2 inside the relief mask, 3 plate without relief (engraved / flush).
    var FS = 'precision mediump float; uniform vec3 uColor; uniform vec3 uLight; uniform vec3 uEye; uniform sampler2D uTex; uniform float uMode;' +
        'varying vec3 vN; varying vec3 vP; varying vec2 vUV;' +
        'void main() { vec4 m = texture2D(uTex, vec2(vUV.x, 1.0 - vUV.y));' +
        ' if (uMode > 0.5 && uMode < 1.5 && m.r < 0.5) discard;' +
        ' if (uMode > 1.5 && uMode < 2.5 && m.g < 0.5) discard;' +
        ' if (uMode > 2.5 && (m.r < 0.5 || m.g >= 0.5)) discard;' +
        ' vec3 n = normalize(vN); vec3 v = normalize(uEye - vP); float diff = max(dot(n, uLight), 0.0);' +
        ' float spec = pow(max(dot(n, normalize(uLight + v)), 0.0), 36.0); float sky = 0.5 + 0.5 * n.z;' +
        ' gl_FragColor = vec4(uColor * (0.34 + 0.52 * diff + 0.18 * sky) + vec3(0.09 * spec), 1.0); }';

    var PARTS = ['baseWalls', 'baseTop', 'baseBottom', 'reliefWalls', 'reliefTop', 'foot'];

    function createGL(cv) {
        var gl = null;
        try { gl = cv.getContext('webgl', { antialias: true, alpha: true, premultipliedAlpha: true }) || cv.getContext('experimental-webgl'); } catch (e) { gl = null; }
        if (!gl) return null;
        function shader(type, src) {
            var s = gl.createShader(type);
            gl.shaderSource(s, src); gl.compileShader(s);
            return gl.getShaderParameter(s, gl.COMPILE_STATUS) ? s : null;
        }
        var vs = shader(gl.VERTEX_SHADER, VS), fs = shader(gl.FRAGMENT_SHADER, FS);
        if (!vs || !fs) return null;
        var prog = gl.createProgram();
        gl.attachShader(prog, vs); gl.attachShader(prog, fs); gl.linkProgram(prog);
        if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) return null;
        gl.useProgram(prog);

        var g = { gl: gl, tex: gl.createTexture(), counts: {} };
        PARTS.forEach(function (p) { g[p] = gl.createBuffer(); g.counts[p] = 0; });
        ['aPos', 'aNor'].forEach(function (n) { g[n] = gl.getAttribLocation(prog, n); });
        ['uMVP', 'uRot', 'uTrans', 'uTexRect', 'uColor', 'uLight', 'uEye', 'uTex', 'uMode'].forEach(function (n) { g[n] = gl.getUniformLocation(prog, n); });
        gl.bindTexture(gl.TEXTURE_2D, g.tex);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.uniform1i(g.uTex, 0);
        return g;
    }

    function upload(g, name, data) {
        var gl = g.gl;
        gl.bindBuffer(gl.ARRAY_BUFFER, g[name]);
        gl.bufferData(gl.ARRAY_BUFFER, data || new Float32Array(0), gl.STATIC_DRAW);
        g.counts[name] = data ? data.length / 6 : 0;
    }

    function sub(a, b) { return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]; }
    function dot(a, b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
    function cross(a, b) { return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]; }
    function unit(a) { var l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; }

    // Fields that change the mesh; colors only repaint.
    var GEOMETRY = ['text', 'line2', 'font', 'upper', 'spacing', 'textScale', 'length', 'heightPct', 'thickness', 'shape', 'radius', 'margin', 'style', 'relief', 'border', 'hole', 'icon', 'iconSide', 'stand'];

    // ---------- View: drag to rotate, Ctrl + wheel (or any wheel while opts.wheel() is true) to zoom ----------

    function View(cv, opts) {
        this.canvas = cv;
        this.opts = opts || {};
        this.gl = createGL(cv);
        this.spec = Object.assign({}, DEFAULTS);
        this.key = null;
        this.dims = null;
        this.seq = 0;
        this.reset(true);

        var self = this, drag = null;
        cv.addEventListener('pointerdown', function (e) {
            if (e.button !== 0) return;
            self.stopSway();
            drag = { x: e.clientX, y: e.clientY, yaw: self.yaw, pitch: self.pitch };
            cv.setPointerCapture(e.pointerId);
        });
        cv.addEventListener('pointermove', function (e) {
            if (!drag) return;
            self.yaw = drag.yaw - (e.clientX - drag.x) * 0.01;
            self.pitch = Math.max(-0.4, Math.min(1.52, drag.pitch + (e.clientY - drag.y) * 0.01));
            self.request();
        });
        ['pointerup', 'pointercancel'].forEach(function (t) { cv.addEventListener(t, function () { drag = null; }); });
        cv.addEventListener('dblclick', function () { self.reset(); });
        cv.addEventListener('wheel', function (e) {
            if (!(e.ctrlKey || e.metaKey || (self.opts.wheel && self.opts.wheel()))) return;
            e.preventDefault();
            self.stopSway();
            self.zoom = Math.max(0.6, Math.min(6, self.zoom * Math.exp(-e.deltaY * 0.0015)));
            self.request();
        }, { passive: false });
        if ('ResizeObserver' in window) new ResizeObserver(function () { self.request(); }).observe(cv);
    }

    View.prototype.home = function () { return this.spec.stand ? { yaw: -0.4, pitch: 0.28 } : { yaw: -0.32, pitch: 0.95 }; };

    View.prototype.reset = function (quiet) {
        var h = this.home();
        this.yaw = h.yaw; this.pitch = h.pitch; this.zoom = 1;
        if (!quiet) this.request();
        this.startSway();
    };

    // Gentle back-and-forth turn until the visitor grabs the plate.
    View.prototype.startSway = function () {
        var self = this, t0 = 0;
        if (this.swaying || !this.gl || (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches)) return;
        this.swaying = true;
        (function tick(now) {
            if (!self.swaying) return;
            if (!t0) t0 = now;
            var r = self.canvas.getBoundingClientRect();
            if (r.bottom > 0 && r.top < window.innerHeight && r.width) {
                self.yaw = self.home().yaw + 0.38 * Math.sin((now - t0) * 0.0007);
                self.draw();
            }
            requestAnimationFrame(tick);
        })(0);
    };

    View.prototype.stopSway = function () { this.swaying = false; };

    /**
     * Shows a plate; spec fields as in TTNameplate.DEFAULTS (colors are CSS colors, sizes in mm, heightPct in % of
     * the length). Missing fields keep their last value. Resolves with the outer size { length, height, depth }.
     */
    View.prototype.set = function (spec) {
        var self = this, next = Object.assign({}, this.spec);
        Object.keys(spec || {}).forEach(function (k) { if (spec[k] != null && k in DEFAULTS) next[k] = spec[k]; });
        next.text = String(next.text).replace(/\s+/g, ' ').trim();
        next.line2 = String(next.line2).replace(/\s+/g, ' ').trim();
        var standChanged = next.stand !== this.spec.stand;
        this.spec = next;
        if (standChanged) this.reset(true);
        var key = JSON.stringify(GEOMETRY.map(function (k) { return next[k]; }));
        if (key === this.key && this.dims) { this.request(); return Promise.resolve(this.dims); }

        var seq = ++this.seq, font = fontOf(next.font), sample = (next.text + next.line2) || 'A';
        var ready = (font.google ? ensureFonts() : Promise.resolve()).then(function () {
            return document.fonts && document.fonts.load ? document.fonts.load(font.weight + ' 40px "' + font.family + '"', sample).catch(function () { }) : null;
        });
        return ready.then(function () {
            if (seq !== self.seq) return self.dims;
            self.key = key;
            self.build();
            self.request();
            return self.dims;
        });
    };

    View.prototype.build = function () {
        var s = this.spec, M = masks(s), T = s.thickness, rel = Math.min(s.relief, s.style === 'engraved' ? T - 0.6 : 5);
        this.M = M;
        this.dims = { length: Math.round(M.size[0] * 10) / 10, height: Math.round(M.size[1] * 10) / 10, depth: Math.round((T + (s.style === 'raised' && M.hasRelief ? rel : 0)) * 10) / 10 };
        this.radius = Math.hypot(M.FW / 2, M.FH / 2);
        var g = this.gl;
        if (!g) return;
        var gl = g.gl;
        upload(g, 'baseWalls', walls(M, M.base, 0, T, false));
        upload(g, 'baseTop', frameQuad(M, T, 1));
        upload(g, 'baseBottom', frameQuad(M, 0, -1));
        if (!M.hasRelief) { upload(g, 'reliefWalls', null); upload(g, 'reliefTop', null); }
        else if (s.style === 'raised') { upload(g, 'reliefWalls', walls(M, M.relief, T - 0.05, T + rel, false)); upload(g, 'reliefTop', frameQuad(M, T + rel, 1)); }
        else if (s.style === 'engraved') { upload(g, 'reliefWalls', walls(M, M.relief, T - rel, T, true)); upload(g, 'reliefTop', frameQuad(M, T - rel, 1)); }
        else { upload(g, 'reliefWalls', null); upload(g, 'reliefTop', frameQuad(M, T, 1)); }
        // Standing: a slotted foot behind the plate's lower edge.
        var H = M.size[1], L = M.size[0];
        upload(g, 'foot', s.stand ? box(-L * 0.36, L * 0.36, -T * 1.6, H * 0.42, 0, Math.max(2.4, T * 0.9)) : null);
        this.texRect = [-M.FW / 2, -M.FH / 2, M.FW, M.FH];
        gl.bindTexture(gl.TEXTURE_2D, g.tex);
        gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, M.w, M.h, 0, gl.RGBA, gl.UNSIGNED_BYTE, M.tex);
    };

    View.prototype.request = function () {
        var self = this;
        if (this.pending || this.swaying) return;
        this.pending = true;
        requestAnimationFrame(function () { self.pending = false; self.draw(); });
    };

    View.prototype.draw = function () {
        var cv = this.canvas, dpr = Math.min(window.devicePixelRatio || 1, 2), w = cv.clientWidth, h = cv.clientHeight;
        if (!w || !h || !this.dims) return;
        if (cv.width !== Math.round(w * dpr) || cv.height !== Math.round(h * dpr)) { cv.width = Math.round(w * dpr); cv.height = Math.round(h * dpr); }
        if (this.gl) this.drawGL(w / h); else this.draw2D(w, h, dpr);
    };

    View.prototype.drawGL = function (aspect) {
        var g = this.gl, gl = g.gl, s = this.spec, M = this.M;
        // Standing: tilt around X so the face looks at the viewer (-Y), lower edge on the ground.
        var plateRot = [1, 0, 0, 0, 1, 0, 0, 0, 1], plateTrans = [0, 0, 0], target = [0, 0, s.thickness / 2];
        if (s.stand) {
            var c = Math.cos(TILT), sn = Math.sin(TILT), half = M.size[1] / 2;
            plateRot = [1, 0, 0, 0, c, sn, 0, -sn, c];   // column-major
            plateTrans = [0, 0, half * sn];
            target = [0, 0, half * sn];
        }

        // Distance that fits the plate across, and its tilted, swaying outline from top to bottom.
        var tanH = Math.tan(FOV / 2), d = this.radius * Math.max(0.9 / aspect, 0.85) / tanH / this.zoom;
        var cp = Math.cos(this.pitch), sp = Math.sin(this.pitch), cy = Math.cos(this.yaw), sy = Math.sin(this.yaw);
        var eye = [target[0] + d * cp * sy, target[1] - d * cp * cy, target[2] + d * sp];
        var f = unit(sub(target, eye)), r = unit(cross(f, [0, 0, 1])), u = cross(r, f);
        var near = Math.max(1, d - this.radius * 1.6), far = d + this.radius * 1.6, ff = 1 / tanH;
        var A = (far + near) / (near - far), B = 2 * far * near / (near - far);
        var re = -dot(r, eye), ue = -dot(u, eye), fe = dot(f, eye);
        var mvp = [
            ff / aspect * r[0], ff * u[0], -A * f[0], f[0],
            ff / aspect * r[1], ff * u[1], -A * f[1], f[1],
            ff / aspect * r[2], ff * u[2], -A * f[2], f[2],
            ff / aspect * re, ff * ue, A * fe + B, -fe
        ];
        // Key light from the upper left of the viewer, mostly frontal so a standing plate is lit as well as a lying one.
        var light = unit([-0.3 * r[0] + 0.5 * u[0] - 0.8 * f[0], -0.3 * r[1] + 0.5 * u[1] - 0.8 * f[1], -0.3 * r[2] + 0.5 * u[2] - 0.8 * f[2]]);

        gl.viewport(0, 0, this.canvas.width, this.canvas.height);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
        gl.enable(gl.DEPTH_TEST);
        gl.uniformMatrix4fv(g.uMVP, false, mvp);
        gl.uniform3fv(g.uLight, light);
        gl.uniform3fv(g.uEye, eye);
        gl.uniform4fv(g.uTexRect, this.texRect);

        function part(name, color, mode, rot, trans) {
            if (!g.counts[name]) return;
            var c = rgb(color);
            gl.uniform3f(g.uColor, c[0], c[1], c[2]);
            gl.uniform1f(g.uMode, mode);
            gl.uniformMatrix3fv(g.uRot, false, rot);
            gl.uniform3fv(g.uTrans, trans);
            gl.bindBuffer(gl.ARRAY_BUFFER, g[name]);
            gl.enableVertexAttribArray(g.aPos);
            gl.vertexAttribPointer(g.aPos, 3, gl.FLOAT, false, 24, 0);
            gl.enableVertexAttribArray(g.aNor);
            gl.vertexAttribPointer(g.aNor, 3, gl.FLOAT, false, 24, 12);
            gl.drawArrays(gl.TRIANGLES, 0, g.counts[name]);
        }
        var cut = s.style !== 'raised' && M.hasRelief;
        part('baseWalls', s.base, 0, plateRot, plateTrans);
        part('baseTop', s.base, cut ? 3 : 1, plateRot, plateTrans);
        part('baseBottom', s.base, 1, plateRot, plateTrans);
        part('reliefWalls', s.style === 'raised' ? s.color : s.base, 0, plateRot, plateTrans);
        part('reliefTop', s.color, 2, plateRot, plateTrans);
        part('foot', s.base, 0, [1, 0, 0, 0, 1, 0, 0, 0, 1], [0, 0, 0]);
    };

    // Fallback without WebGL: the plate seen from the front.
    View.prototype.draw2D = function (w, h, dpr) {
        var ctx = this.canvas.getContext('2d'), M = this.M, k = Math.min(w * 0.9 / M.FW, h * 0.8 / M.FH);
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, w, h);
        var dw = M.FW * k, dh = M.FH * k, x = (w - dw) / 2, y = (h - dh) / 2, spec = this.spec;
        [[M.baseCanvas, spec.base], [M.reliefCanvas, spec.color]].forEach(function (p) {
            var t = canvas(M.w, M.h), tc = t.getContext('2d');
            tc.drawImage(p[0], 0, 0);
            tc.globalCompositeOperation = 'source-in';
            tc.fillStyle = p[1]; tc.fillRect(0, 0, M.w, M.h);
            ctx.drawImage(t, x, y, dw, dh);
        });
    };

    window.TTNameplate = { View: View, FONTS: FONTS, addFonts: addFonts, SHAPES: SHAPES, ICONS: ICONS, DEFAULTS: DEFAULTS, ensureFonts: ensureFonts, fontOf: fontOf, iconOf: iconOf };
})();

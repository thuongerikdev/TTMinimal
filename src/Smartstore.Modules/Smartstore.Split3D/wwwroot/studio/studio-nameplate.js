/* TT Minimal 3D previews, drawn with WebGL: name plates, class boards (timetable / seating chart) and keycaps.
   Name plate: a plate with raised / engraved / flush text.
   Everything is 2D masks first: the plate shape (with holes) and the relief (text lines, icons, border) are
   painted on canvases with the chosen font; marching squares on each mask give the side walls, the masks
   themselves (alpha test) are the top and bottom faces. So any font and shape works, without triangulation.
   Vietnamese marks missing from a font are painted next to its letters. A class board is the same plate with a
   table (timetable) or desks with names (seating chart) as relief. A keycap is a lofted cap with a dished top whose
   legend is a mask too (colored on the top, raised or engraved as a height field). No dependencies; loaded by
   studio.js on the pages of these products. */
(function () {
    'use strict';

    // The studio's fonts. Yellowtail, Pacifico and Titan One ship as files in studio/fonts; the Google Fonts entries
    // below are only the fallback when those files are missing. Every other font comes as a file from studio/fonts
    // (see addFonts); a file named like a font here replaces it. Vietnamese marks a font lacks are drawn by
    // vietPlan / markOps, so every font takes Vietnamese names.
    var FONTS = [
        { key: 'yellowtail', name: 'Yellowtail', family: 'Yellowtail', weight: 400, google: true },
        { key: 'pacifico', name: 'Pacifico', family: 'Pacifico', weight: 400, google: true },
        { key: 'titanone', name: 'Titan One', family: 'Titan One', weight: 400, google: true },
        // Plain, readable letters for timetables, seating charts and keycap legends only (kinds).
        { key: 'be', name: 'Be Vietnam Pro', family: 'Be Vietnam Pro', weight: 800, google: true, kinds: ['classboard', 'keycap', 'qr'] }
    ];

    // Order of the studio's font list; fonts not named here follow alphabetically. Each commercial font is followed
    // by the free look-alike shipped in studio/fonts until the licensed file is added (Ms Madi for Patrick Tonight…).
    var FONT_ORDER = ['be', 'yellowtail', 'patricktonight', 'msmadi', 'birthdayparty', 'sriracha', 'bollifia', 'dancingscript',
        'mjmilestonescript', 'greatvibes', 'pacifico', 'mobsters', 'anton', 'peanutbutter', 'bangers', 'titanone', 'baguetscript', 'pattaya'];

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
            // The URL comes escaped by the server ("Titan%20One.woff2"); encoding it again would break the file name.
            css += '@font-face{font-family:"' + family + '";src:url("' + String(f.url).replace(/["\\]/g, encodeURIComponent) + '");font-display:swap}';
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
        { key: 'up', name: 'Mũi tên', draw: function (c) { poly(c, [[0.5, 0.06], [0.92, 0.5], [0.64, 0.5], [0.64, 0.94], [0.36, 0.94], [0.36, 0.5], [0.08, 0.5]]); } },
        { key: 'enter', name: 'Enter', draw: function (c) {
            poly(c, [[0.86, 0.1], [0.86, 0.62], [0.36, 0.62], [0.36, 0.82], [0.06, 0.52], [0.36, 0.22], [0.36, 0.42], [0.66, 0.42], [0.66, 0.1]]);
        } },
        { key: 'power', name: 'Nguồn', cut: function (c) { c.beginPath(); circle(c, 0.5, 0.56, 0.27); c.fill(); c.fillRect(0.38, 0.02, 0.24, 0.42); }, draw: function (c) {
            circle(c, 0.5, 0.56, 0.4); c.rect(0.44, 0.04, 0.12, 0.46);
        } },
        { key: 'ball', name: 'Bóng', cut: function (c) { c.lineWidth = 0.05; c.beginPath(); circle(c, 0.5, 0.5, 0.3); c.moveTo(0.06, 0.5); c.lineTo(0.94, 0.5); c.moveTo(0.5, 0.06); c.lineTo(0.5, 0.94); c.stroke(); }, draw: function (c) { circle(c, 0.5, 0.5, 0.46); } }
    ];

    var DEFAULTS = {
        text: '', line2: '', font: 'pacifico', upper: false, spacing: 0, textScale: 1,
        base: '#ffffff', color: '#20201f',
        length: 100, heightPct: 30, thickness: 3, shape: 'rounded', radius: 5, margin: 4,
        style: 'raised', relief: 1.6, border: false, hole: 'none', icon: '', iconSide: 'left', stand: false,
        // Product kind: 'nameplate', 'classboard' (board = table or desks, see drawBoard) or 'keycap'.
        kind: 'nameplate', board: null,
        // QR plate: { rows: ['0101…'] matrix from the server, quiet (modules), style (square, round, dots), scale,
        // caption ('bottom', 'top', 'none') }. text / line2 = caption lines.
        qr: null,
        // Class board with removable tiles (board.tiles): tile color and the tiles shown lifted out of their pockets.
        tileColor: '#ffffff', explode: false,
        // Keycap: profile key of PROFILES, width in units (1u = 19.05 mm), row R1..R4, legend position
        // (center, tl, tc, bl), homing bump, stem (mx, choc, alps). text = main legend, line2 = shift legend.
        profile: 'oem', units: 1, row: 3, legendPos: 'center', homing: false, stem: 'mx'
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

    // ---------- Class board: timetable or seating chart ----------

    // Text fitted into a box (mm, y down) centered at cx, cy: one line, or two when wrap and one line would shrink a
    // lot. Returns the size used (mm per reference px). k: draw at this size (cells of a table share one size);
    // dry: only measure. rc has the mm transform; text is drawn in pixels.
    function fitText(rc, text, font, cx, cy, maxW, maxH, R, FW, FH, spacing, wrap, k, dry) {
        text = String(text || '').trim();
        if (!text || maxW <= 0 || maxH <= 0) return Infinity;
        var ref = 100, mc = fitText.ctx || (fitText.ctx = canvas(4, 4).getContext('2d'));
        var one = measure(mc, text, font, ref, spacing), k1 = Math.min(maxW / (one.w || 1), maxH / (one.h || 1));
        var best = null;
        if (wrap && text.indexOf(' ') > 0) {
            var words = text.split(' ');
            for (var i = 1; i < words.length; i++) {
                var a = words.slice(0, i).join(' '), b = words.slice(i).join(' ');
                var ta = measure(mc, a, font, ref, spacing), tb = measure(mc, b, font, ref, spacing);
                var k2 = Math.min(maxW / Math.max(ta.w, tb.w, 1), maxH * 0.47 / Math.max(ta.h, tb.h, 1));
                if (!best || k2 > best.k) best = { k: k2, lines: [a, b], t: [ta, tb] };
            }
        }
        var fit = best && best.k > k1 * 1.15 ? best.k : k1;
        if (dry) return fit;
        k = Math.min(k || fit, fit * 1.0001);
        // Two lines only when one line does not fit at this size.
        var two = best && one.w * k > maxW, lines = two ? best.lines : [text], t = two ? best.t : [one];
        rc.save();
        rc.setTransform(1, 0, 0, 1, 0, 0);
        rc.textBaseline = 'alphabetic';
        rc.textAlign = 'left';
        lines.forEach(function (line, i) {
            var tm = measure(rc, line, font, ref * k * R, spacing), lt = t[i];
            var y = two ? cy + (i ? 1 : -1) * Math.max(lt.h * k * 0.55, maxH * 0.24) : cy;
            tm.draw(rc, (cx - lt.w * k / 2 + lt.l * k + FW / 2) * R, (y - lt.h * k / 2 + lt.a * k + FH / 2) * R);
        });
        rc.restore();
        return k;
    }

    // Draws a set of texts at one shared size: the largest size every one of them fits at.
    function fitAll(rc, items, font, R, FW, FH, spacing) {
        var k = Infinity;
        items.forEach(function (it) { k = Math.min(k, fitText(rc, it.text, font, it.x, it.y, it.w, it.h, R, FW, FH, spacing, true, 0, true)); });
        if (!isFinite(k)) return;
        items.forEach(function (it) { fitText(rc, it.text, font, it.x, it.y, it.w, it.h, R, FW, FH, spacing, true, k); });
    }

    var DAY_NAMES = ['Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7', 'CN'];

    function strokeRound(rc, x, y, w, h, r) { rc.beginPath(); roundRect(rc, x, y, w, h, r); rc.stroke(); }

    // The board content inside box (mm, y down): title, sub line, then the timetable grid or the desks.
    function drawBoard(rc, s, font, box, R, FW, FH) {
        var b = s.board, vi = 'vi', lw = Math.max(0.3, +b.line || 0.8);
        var up = function (t) { return s.upper ? String(t || '').toLocaleUpperCase(vi) : t; };
        var titleH = s.text ? box.h * 0.12 : 0, subH = s.line2 ? box.h * 0.055 : 0, y = box.y, sc = s.textScale || 1;
        if (s.text) fitText(rc, up(s.text), font, box.x + box.w / 2, y + titleH / 2, box.w * 0.92, titleH * 0.82, R, FW, FH, s.spacing);
        y += titleH;
        if (s.line2) fitText(rc, up(s.line2), font, box.x + box.w / 2, y + subH / 2, box.w * 0.85, subH * 0.8, R, FW, FH, s.spacing);
        y += subH + (titleH || subH ? box.h * 0.025 : 0);
        var h = box.y + box.h - y, x = box.x, w = box.w;
        if (h <= 2) return;
        rc.lineWidth = lw;
        rc.lineJoin = 'round';

        if (b.mode === 'seating') {
            // Front: the board in the middle, the teacher's desk to one side; then rows of desks, front row first.
            var bandH = h * 0.11, bw = w * 0.42, bh = bandH * 0.78;
            strokeRound(rc, x + (w - bw) / 2, y, bw, bh, 1.2);
            fitText(rc, up('Bảng'), font, x + w / 2, y + bh / 2, bw * 0.6, bh * 0.6, R, FW, FH, s.spacing);
            if (b.teacher === 'left' || b.teacher === 'right') {
                var tw = w * 0.2, tx = b.teacher === 'left' ? x : x + w - tw;
                strokeRound(rc, tx, y, tw, bh, 1.2);
                fitText(rc, up('Bàn giáo viên'), font, tx + tw / 2, y + bh / 2, tw * 0.85, bh * 0.5, R, FW, FH, s.spacing, true);
            }
            var top = y + bandH + h * 0.04, area = y + h - top;
            var rows = Math.max(1, b.rows | 0), groups = Math.max(1, b.groups | 0), seats = Math.max(1, b.seats | 0);
            var deskH = area / (rows + (rows - 1) * 0.3), aisle = groups > 1 ? w * 0.06 : 0, deskW = (w - (groups - 1) * aisle) / groups;
            var names = String(b.names || '').split(/\r?\n/), n = 0, items = [];
            for (var r = 0; r < rows; r++) {
                var dy = top + r * deskH * 1.3;
                for (var g = 0; g < groups; g++) {
                    var dx = x + g * (deskW + aisle), seatW = deskW / seats;
                    strokeRound(rc, dx, dy, deskW, deskH, Math.min(1.5, deskH * 0.15));
                    for (var k = 0; k < seats; k++) {
                        if (k) { rc.beginPath(); rc.moveTo(dx + k * seatW, dy + deskH * 0.18); rc.lineTo(dx + k * seatW, dy + deskH * 0.82); rc.stroke(); }
                        items.push({ text: up(names[n++] || ''), x: dx + (k + 0.5) * seatW, y: dy + deskH / 2, w: seatW - lw * 2 - 1, h: deskH * 0.62 * sc, cell: [dx + k * seatW, dy, seatW, deskH] });
                    }
                }
            }
            // Removable tiles: the caller cuts a pocket per seat and makes the name tiles.
            if (b.tiles) return { items: items, line: lw };
            fitAll(rc, items, font, R, FW, FH, s.spacing);
            return null;
        }

        // Timetable: header row with the days, a label column, one row per period; "Sáng" / "Chiều" rows between
        // the sessions when both are used.
        var days = Math.max(1, Math.min(7, b.days | 0 || 6)), am = Math.max(0, b.am | 0), pm = Math.max(0, b.pm | 0);
        var cells = b.cells || {}, list = [{ kind: 'head', u: 0.9 }];
        if (am && pm) list.push({ kind: 'sep', u: 0.7, label: 'Sáng' });
        for (var i = 0; i < am; i++) list.push({ kind: 'p', u: 1, s: 'am', i: i });
        if (am && pm) list.push({ kind: 'sep', u: 0.7, label: 'Chiều' });
        for (i = 0; i < pm; i++) list.push({ kind: 'p', u: 1, s: 'pm', i: i });
        var units = list.reduce(function (t, row) { return t + row.u; }, 0), unitH = h / units;
        var labelW = w * 0.12, colW = (w - labelW) / days, yy = y, heads = [], labels = [], subjects = [], seps = [];
        rc.strokeRect(x, y, w, h);
        list.forEach(function (row, ri) {
            var rh = row.u * unitH;
            if (ri) {
                rc.lineWidth = list[ri - 1].kind === 'head' ? lw * 1.8 : lw;
                rc.beginPath(); rc.moveTo(x, yy); rc.lineTo(x + w, yy); rc.stroke();
                rc.lineWidth = lw;
            }
            if (row.kind === 'sep') {
                seps.push({ text: up(row.label), x: x + w / 2, y: yy + rh / 2, w: w * 0.3, h: rh * 0.6 * sc });
            } else {
                for (var c = 0; c <= days; c++) {
                    var cx0 = c ? x + labelW + (c - 1) * colW : x, cw = c ? colW : labelW;
                    if (c) { rc.beginPath(); rc.moveTo(cx0, yy); rc.lineTo(cx0, yy + rh); rc.stroke(); }
                    var text = row.kind === 'head' ? (c ? DAY_NAMES[c - 1] : 'Tiết')
                        : c ? ((cells[row.s] || [])[row.i] || [])[c - 1] : String(row.i + 1);
                    (row.kind === 'head' ? heads : c ? subjects : labels).push({ text: up(text), x: cx0 + cw / 2, y: yy + rh / 2, w: cw - lw * 2 - 1, h: rh * (row.kind === 'head' ? 0.55 : 0.6) * sc, cell: [cx0, yy, cw, rh] });
                }
            }
            yy += rh;
        });
        [heads, labels, seps].concat(b.tiles ? [] : [subjects]).forEach(function (items) { fitAll(rc, items, font, R, FW, FH, s.spacing); });
        return b.tiles ? { items: subjects, line: lw } : null;
    }

    // ---------- QR plate ----------

    // QR modules (dark = relief) in a square of side q (mm) centered at cx, cy; finder patterns stay solid squares
    // (rounded for the round / dot styles) so phones always find them. An icon in the middle replaces the modules
    // under it (the server encodes with high error correction then).
    function drawQR(rc, s, cx, cy, q) {
        var rows = s.qr.rows || [], n = rows.length;
        if (!n) return;
        var quiet = Math.max(0, +s.qr.quiet || 0), mod = q / (n + 2 * quiet), x0 = cx - q / 2 + quiet * mod, y0 = cy - q / 2 + quiet * mod;
        var style = s.qr.style || 'square', icon = iconOf(s.icon), hole = icon ? Math.ceil(n * 0.24 / 2) * 2 + 1 : 0, h0 = (n - hole) / 2;
        function dark(x, y) { return x >= 0 && y >= 0 && x < n && y < n && rows[y].charAt(x) === '1'; }
        function finder(x, y) { return (x < 7 && y < 7) || (x >= n - 7 && y < 7) || (x < 7 && y >= n - 7); }
        function covered(x, y) { return hole && x >= h0 && x < h0 + hole && y >= h0 && y < h0 + hole; }
        rc.beginPath();
        for (var y = 0; y < n; y++) {
            for (var x = 0; x < n; x++) {
                if (!dark(x, y) || covered(x, y) || (style !== 'square' && finder(x, y))) continue;
                var px = x0 + x * mod, py = y0 + y * mod;
                if (style === 'dots') { circle(rc, px + mod / 2, py + mod / 2, mod * 0.43); continue; }
                if (style === 'round') {
                    circle(rc, px + mod / 2, py + mod / 2, mod * 0.5);
                    if (dark(x + 1, y) && !covered(x + 1, y) && !finder(x + 1, y)) rc.rect(px + mod / 2, py, mod, mod);
                    if (dark(x, y + 1) && !covered(x, y + 1) && !finder(x, y + 1)) rc.rect(px, py + mod / 2, mod, mod);
                    continue;
                }
                rc.rect(px - 0.01, py - 0.01, mod + 0.02, mod + 0.02);
            }
        }
        rc.fill();
        if (style !== 'square') {
            [[0, 0], [n - 7, 0], [0, n - 7]].forEach(function (f) {
                var fx = x0 + f[0] * mod, fy = y0 + f[1] * mod;
                rc.beginPath(); roundRect(rc, fx, fy, 7 * mod, 7 * mod, mod * 1.6); rc.fill();
                rc.save(); rc.globalCompositeOperation = 'destination-out';
                rc.beginPath(); roundRect(rc, fx + mod, fy + mod, 5 * mod, 5 * mod, mod * 1.1); rc.fill(); rc.restore();
                rc.beginPath(); roundRect(rc, fx + 2 * mod, fy + 2 * mod, 3 * mod, 3 * mod, mod * 0.8); rc.fill();
            });
        }
        if (icon) {
            var size = hole * mod * 0.82, ic = canvas(256, 256), icc = ic.getContext('2d');
            icc.setTransform(256, 0, 0, 256, 0, 0);
            icc.fillStyle = '#fff';
            icc.beginPath(); icon.draw(icc); icc.fill();
            if (icon.cut) { icc.globalCompositeOperation = 'destination-out'; icon.cut(icc); }
            rc.drawImage(ic, cx - size / 2, y0 + (n / 2) * mod - size / 2, size, size);
        }
    }

    // Builds the plate and relief masks for a spec. R = mask pixels per mm; frame = mask size in mm (centered).
    function masks(s) {
        var font = fontOf(s.font), L = s.length, H = Math.max(8, L * s.heightPct / 100), m = s.margin;
        // A QR plate takes its height from the code (square) and the caption band.
        var qrSide = 0, capH = 0, holeSideQ = 2 * HOLE_R + 2.5;
        if (s.qr) {
            var awq = L - 2 * m - (s.hole === 'left' ? holeSideQ : 0) - (s.border ? 3 : 0);
            qrSide = Math.max(5, awq * Math.max(0.4, Math.min(1, s.qr.scale || 1)));
            var cap = s.qr.caption === 'none' ? 0 : 1;
            capH = cap ? (s.text ? qrSide * 0.16 : 0) + (s.line2 ? qrSide * 0.09 : 0) : 0;
            H = 2 * m + qrSide + (capH ? capH + m * 0.4 : 0) + (s.hole === 'top1' || s.hole === 'top2' ? holeSideQ : 0) + (s.border ? 3 : 0);
        }
        var R = Math.min(9, (s.board ? 2000 : MAX_PX) / (L + 4)), w = Math.ceil((L + 4) * R), h = Math.ceil((H + 4) * R);
        var FW = w / R, FH = h / R;
        var relief = canvas(w, h), rc = relief.getContext('2d');
        var base = canvas(w, h), bc = base.getContext('2d');
        // mm, y down, origin at the plate center.
        rc.setTransform(R, 0, 0, R, FW / 2 * R, FH / 2 * R);
        bc.setTransform(R, 0, 0, R, FW / 2 * R, FH / 2 * R);
        rc.fillStyle = bc.fillStyle = rc.strokeStyle = '#fff';

        var vi = 'vi';
        // A board or a QR plate draws its own content instead of the text lines.
        var own = s.board || s.qr;
        var line1 = own ? '' : s.upper ? s.text.toLocaleUpperCase(vi) : s.text, line2 = own ? '' : s.upper ? s.line2.toLocaleUpperCase(vi) : s.line2;
        var icon = own ? null : iconOf(s.icon), nIcons = icon ? (s.iconSide === 'both' ? 2 : 1) : 0;

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
        else if (s.hole === 'top1' || (s.hole === 'top2' && (s.shape === 'outline' || s.board || s.qr))) { ah -= holeSide; cy = holeSide / 2; }
        else if (s.hole === 'top2') aw -= 2 * holeSide;
        if (s.border && s.shape !== 'outline') { aw -= 3; ah -= 3; }

        var tiles = s.board && aw > 0 && ah > 0 ? drawBoard(rc, s, font, { x: cx - aw / 2, y: cy - ah / 2, w: aw, h: ah }, R, FW, FH) : null;
        if (s.qr && aw > 0 && ah > 0) {
            // Code on top or below the caption band.
            var capTop = s.qr.caption === 'top', top0 = cy - ah / 2, qy = capTop && capH ? top0 + capH + m * 0.4 + qrSide / 2 : top0 + qrSide / 2;
            drawQR(rc, s, cx, qy, qrSide);
            if (capH) {
                var by = capTop ? top0 : top0 + qrSide + m * 0.4, t1h = s.text ? qrSide * 0.16 : 0, t2h = s.line2 ? qrSide * 0.09 : 0;
                var upq = function (t) { return s.upper ? String(t).toLocaleUpperCase(vi) : t; };
                if (s.text) fitText(rc, upq(s.text), font, cx, by + t1h / 2, aw, t1h * 0.85 * s.textScale, R, FW, FH, s.spacing);
                if (s.line2) fitText(rc, upq(s.line2), font, cx, by + t1h + t2h / 2, aw, t2h * 0.8 * s.textScale, R, FW, FH, s.spacing);
            }
        }
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

        // Removable tiles: a pocket per cell (cut into the plate), a tile a little smaller than its pocket, and the
        // tile's text, all on their own masks.
        var fp = null, ft = null, ftt = null;
        if (tiles && tiles.items.length) {
            var mk = function () { var cv = canvas(w, h), c = cv.getContext('2d'); c.setTransform(R, 0, 0, R, FW / 2 * R, FH / 2 * R); c.fillStyle = '#fff'; return { cv: cv, c: c }; };
            var pk = mk(), tl = mk(), tt = mk(), inset = tiles.line / 2 + 0.35, gap = 0.25;
            tiles.items.forEach(function (it) {
                var c = it.cell;
                pk.c.beginPath(); roundRect(pk.c, c[0] + inset, c[1] + inset, c[2] - 2 * inset, c[3] - 2 * inset, 0.8); pk.c.fill();
                tl.c.beginPath(); roundRect(tl.c, c[0] + inset + gap, c[1] + inset + gap, c[2] - 2 * (inset + gap), c[3] - 2 * (inset + gap), 0.6); tl.c.fill();
            });
            fitAll(tt.c, tiles.items.map(function (it) { return Object.assign({}, it, { w: it.cell[2] - 2 * (inset + gap) - 1.2, h: Math.min(it.h, it.cell[3] - 2 * (inset + gap) - 1.2) }); }), font, R, FW, FH, s.spacing);
            // Frame relief (grid lines) stays out of the pockets.
            rc.save(); rc.setTransform(1, 0, 0, 1, 0, 0); rc.globalCompositeOperation = 'destination-out'; rc.drawImage(pk.cv, 0, 0); rc.restore();
            fp = pk.c.getImageData(0, 0, w, h).data; ft = tl.c.getImageData(0, 0, w, h).data; ftt = tt.c.getImageData(0, 0, w, h).data;
        }

        var fb = bc.getImageData(0, 0, w, h).data, fr = rc.getImageData(0, 0, w, h).data;
        var n = w * h, B = new Float32Array(n), Rf = new Float32Array(n), tex = new Uint8Array(n * 4), any = false;
        var Pk = fp ? new Float32Array(n) : null, Tl = fp ? new Float32Array(n) : null, Tt = fp ? new Float32Array(n) : null, tex2 = fp ? new Uint8Array(n * 4) : null;
        var minX = w, maxX = -1, minY = h, maxY = -1;
        for (var i = 0, x = 0, y = 0; i < n; i++) {
            var b = fb[i * 4 + 3], r = fr[i * 4 + 3];
            B[i] = b / 255; Rf[i] = r / 255;
            tex[i * 4] = b; tex[i * 4 + 1] = r; tex[i * 4 + 3] = 255;
            if (fp) {
                var pa = fp[i * 4 + 3], ta = ft[i * 4 + 3], xa = ftt[i * 4 + 3];
                Pk[i] = pa / 255; Tl[i] = ta / 255; Tt[i] = xa / 255;
                tex[i * 4 + 2] = pa;
                tex2[i * 4] = ta; tex2[i * 4 + 1] = xa; tex2[i * 4 + 3] = 255;
            }
            if (r > 127) any = true;
            if (b > 127) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            if (++x === w) { x = 0; y++; }
        }
        return {
            w: w, h: h, R: R, FW: FW, FH: FH, base: B, relief: Rf, tex: tex, hasRelief: any, baseCanvas: base, reliefCanvas: relief,
            pocket: Pk, tile: Tl, tileText: Tt, tex2: tex2,
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

    // ---------- Keycap ----------

    // Approximate shapes of the common profiles (mm): height and top tilt per row R1..R4 (positive = back higher),
    // top inset from the base, top shifted back, dish (cylindrical / spherical) and its depth, side bulge.
    var PROFILES = {
        oem: { name: 'OEM', h: [11.9, 9.7, 9.2, 9.8], tilt: [9, 4, -1, -6], inset: 2.7, back: 0.8, dish: 'cyl', depth: 0.9, bulge: 0 },
        cherry: { name: 'Cherry', h: [9.4, 7.9, 7.3, 8.0], tilt: [8, 3, -2, -7], inset: 2.7, back: 0.6, dish: 'cyl', depth: 0.8, bulge: 0 },
        xda: { name: 'XDA', h: [9.1, 9.1, 9.1, 9.1], tilt: [0, 0, 0, 0], inset: 1.6, back: 0, dish: 'sph', depth: 1.0, bulge: 0.25 },
        dsa: { name: 'DSA', h: [7.6, 7.6, 7.6, 7.6], tilt: [0, 0, 0, 0], inset: 3.0, back: 0, dish: 'sph', depth: 1.0, bulge: 0.1 },
        sa: { name: 'SA', h: [14.6, 12.9, 12.9, 13.9], tilt: [12, 6, -1, -9], inset: 2.9, back: 0.3, dish: 'sph', depth: 1.3, bulge: 0.6 }
    };
    var KEY_UNIT = 19.05;

    function keycapShape(s) {
        var p = PROFILES[s.profile] || PROFILES.oem, row = Math.max(1, Math.min(4, (s.row | 0) || 3)) - 1;
        var u = Math.max(1, Math.min(10, +s.units || 1)), W0 = 18.1 + (u - 1) * KEY_UNIT, D0 = 18.1;
        return {
            p: p, u: u, W0: W0, D0: D0, W1: W0 - 2 * p.inset, D1: D0 - 2 * p.inset - 0.6, H: p.h[row],
            tilt: Math.tan(p.tilt[row] * Math.PI / 180), back: p.back, r0: 1.1, r1: 1.7
        };
    }

    // Height of the top (no legend) at x, y: tilt minus the dish, plus the homing bar of F / J.
    function keyTopZ(k, x, y, homing) {
        var dx = x / (k.W1 / 2), dy = (y - k.back) / (k.D1 / 2), dish;
        if (k.p.dish === 'cyl') dish = k.u < 1.75 ? Math.max(0, 1 - dx * dx) : Math.max(0, 1 - dy * dy) * 0.6;
        else {
            // Spherical; wide keys get a stadium-shaped dish.
            var ex = Math.max(0, Math.abs(x) - Math.max(0, k.W1 / 2 - k.D1 / 2)) / (k.D1 / 2);
            dish = Math.max(0, 1 - ex * ex - dy * dy);
        }
        var z = k.H + k.tilt * (y - k.back) - k.p.depth * dish;
        if (homing) {
            var by = (y - (k.back - k.D1 * 0.3)) / 0.45, bx = x / 2.2;
            z += 0.45 * Math.exp(-(bx * bx * bx * bx) - by * by);
        }
        return z;
    }

    // Rounded rectangle outline with a fixed number of points (same count for every ring of the loft).
    function ringPoints(w, d, r, cy) {
        var hw = w / 2 - r, hd = d / 2 - r, seg = 8, pts = [];
        [[hw, hd, 0], [-hw, hd, 0.5], [-hw, -hd, 1], [hw, -hd, 1.5]].forEach(function (c) {
            for (var i = 0; i <= seg; i++) {
                var a = (c[2] + i / seg * 0.5) * Math.PI;
                pts.push([c[0] + Math.cos(a) * r, cy + c[1] + Math.sin(a) * r]);
            }
        });
        return pts;
    }

    // Legend (G) and top shape (R) masks over the top rectangle; R = pixels per mm.
    function keycapMasks(s, k) {
        var R = Math.min(24, 1400 / k.W1), w = Math.max(8, Math.round(k.W1 * R)), h = Math.max(8, Math.round(k.D1 * R));
        var shape = canvas(w, h), sc = shape.getContext('2d');
        sc.fillStyle = '#fff';
        sc.setTransform(R, 0, 0, R, 0, 0);
        sc.beginPath(); roundRect(sc, 0, 0, k.W1, k.D1, k.r1); sc.fill();

        var legend = canvas(w, h), c = legend.getContext('2d', { willReadFrequently: true });
        c.fillStyle = c.strokeStyle = '#fff';
        c.textBaseline = 'alphabetic';
        c.textAlign = 'left';
        var font = fontOf(s.font), vi = 'vi', pad = 1.5 * R, scale = s.textScale || 1;
        var main = s.upper ? s.text.toLocaleUpperCase(vi) : s.text, sub = s.upper ? s.line2.toLocaleUpperCase(vi) : s.line2;
        var icon = iconOf(s.icon), only = icon && (!main || s.iconSide === 'only');

        // Text of px pixels (smaller if wider than maxW), x by align, y by valign (top / middle / bottom).
        function put(text, px, x, y, align, valign, maxW) {
            var t = measure(c, text, font, px, s.spacing);
            if (t.w > maxW) { px *= maxW / t.w; t = measure(c, text, font, px, s.spacing); }
            var x0 = align === 'center' ? x - t.w / 2 : align === 'right' ? x - t.w : x;
            var y0 = valign === 'top' ? y : valign === 'bottom' ? y - t.h : y - t.h / 2;
            t.draw(c, x0 + t.l, y0 + t.a);
            return { x: x0, w: t.w, h: t.h };
        }
        function putIcon(x, y, size) {
            var ic = canvas(Math.max(2, Math.ceil(size)), Math.max(2, Math.ceil(size))), icc = ic.getContext('2d');
            icc.setTransform(ic.width, 0, 0, ic.height, 0, 0);
            icc.fillStyle = icc.strokeStyle = '#fff';
            icc.beginPath(); icon.draw(icc); icc.fill();
            if (icon.cut) { icc.globalCompositeOperation = 'destination-out'; icon.cut(icc); }
            c.drawImage(ic, x, y, size, size);
        }

        if (only) {
            var size = Math.min(w, h) * 0.6 * scale;
            putIcon((w - size) / 2, (h - size) / 2, size);
        } else if (main || sub) {
            var big = h * (sub ? 0.34 : 0.48) * scale, small = h * 0.3 * scale, maxW = w - 2 * pad;
            var pos = s.legendPos || 'center', ix = 0;
            if (icon) {
                // Icon in front of the main legend.
                var isz = big * 0.9;
                ix = isz + big * 0.15;
                maxW -= ix;
            }
            var mx, my, align, valign;
            if (pos === 'tl') { mx = pad + ix; my = sub ? h - pad : pad; align = 'left'; valign = sub ? 'bottom' : 'top'; }
            else if (pos === 'bl') { mx = pad + ix; my = h - pad; align = 'left'; valign = 'bottom'; }
            else if (pos === 'tc') { mx = w / 2 + ix / 2; my = pad + (sub ? small + pad * 0.5 : 0); align = 'center'; valign = 'top'; }
            else { mx = w / 2 + ix / 2; my = h / 2 + (sub ? h * 0.14 : 0); align = 'center'; valign = 'middle'; }
            var placed = main ? put(main, big, mx, my, align, valign, maxW) : null;
            if (icon && placed) putIcon(placed.x - ix, (valign === 'top' ? my : valign === 'bottom' ? my - placed.h : my - placed.h / 2) + (placed.h - isz) / 2, isz);
            if (sub) {
                if (pos === 'tl' || pos === 'bl') put(sub, small, pad, pad, 'left', 'top', w - 2 * pad);
                else put(sub, small, w / 2, pos === 'tc' ? pad : h / 2 - h * 0.2, 'center', pos === 'tc' ? 'top' : 'middle', w - 2 * pad);
            }
        }

        var fs = sc.getImageData(0, 0, w, h).data, fl = c.getImageData(0, 0, w, h).data;
        var n = w * h, G = new Float32Array(n), tex = new Uint8Array(n * 4), any = false;
        for (var i = 0; i < n; i++) {
            var g = fl[i * 4 + 3];
            G[i] = g / 255;
            tex[i * 4] = fs[i * 4 + 3]; tex[i * 4 + 1] = g; tex[i * 4 + 3] = 255;
            if (g > 127) any = true;
        }
        return { w: w, h: h, R: R, G: G, tex: tex, any: any, FW: k.W1, FH: k.D1, baseCanvas: shape, reliefCanvas: legend };
    }

    // Keycap parts: sides (lofted from base to top), top (height field with the legend), bottom, stems and the
    // mark on the stem (cross of MX, bars of Choc, block of Alps).
    function keycapMesh(s) {
        var k = keycapShape(s), M = keycapMasks(s, k), out = {};
        var disp = s.style === 'raised' ? Math.min(1.5, s.relief) : s.style === 'engraved' ? -Math.min(1.2, s.relief) : 0;
        // Heights from a softened legend: a hard step in a height field shades as a jagged dark rim; the color edge
        // stays sharp (texture).
        var G = M.G;
        if (disp && M.any) {
            var rad = Math.max(1, Math.round(0.1 * M.R)), tmp = new Float32Array(G.length), out = new Float32Array(G.length);
            for (var pass = 0; pass < 2; pass++) {
                var src = pass ? tmp : G, dst = pass ? out : tmp;
                for (var yy = 0; yy < M.h; yy++) {
                    for (var xx = 0; xx < M.w; xx++) {
                        var sum = 0, cnt = 0;
                        for (var o = -rad; o <= rad; o++) {
                            var X = pass ? xx : xx + o, Y = pass ? yy + o : yy;
                            if (X >= 0 && Y >= 0 && X < M.w && Y < M.h) { sum += src[Y * M.w + X]; cnt++; }
                        }
                        dst[yy * M.w + xx] = sum / cnt;
                    }
                }
            }
            G = out;
        }
        function legendAt(x, y) {
            var fx = (x + k.W1 / 2) * M.R - 0.5, fy = (k.back + k.D1 / 2 - y) * M.R - 0.5;
            var x0 = Math.max(0, Math.min(M.w - 1, Math.floor(fx))), y0 = Math.max(0, Math.min(M.h - 1, Math.floor(fy)));
            var x1 = Math.min(M.w - 1, x0 + 1), y1 = Math.min(M.h - 1, y0 + 1), ax = Math.max(0, Math.min(1, fx - x0)), ay = Math.max(0, Math.min(1, fy - y0));
            var a = G[y0 * M.w + x0], b = G[y0 * M.w + x1], c = G[y1 * M.w + x0], d = G[y1 * M.w + x1];
            return (a * (1 - ax) + b * ax) * (1 - ay) + (c * (1 - ax) + d * ax) * ay;
        }
        function z(x, y) { return keyTopZ(k, x, y, s.homing) + (disp && M.any ? disp * legendAt(x, y) : 0); }

        // Top: a grid over the top rectangle, the corners cut by the R mask in the shader.
        var step = Math.max(0.08, k.W1 / 520), nx = Math.ceil(k.W1 / step), ny = Math.ceil(k.D1 / step);
        var gx = k.W1 / nx, gy = k.D1 / ny, Z = new Float32Array((nx + 1) * (ny + 1));
        for (var j = 0; j <= ny; j++) for (var i = 0; i <= nx; i++) Z[j * (nx + 1) + i] = z(-k.W1 / 2 + i * gx, k.back - k.D1 / 2 + j * gy);
        var top = new Float32Array(nx * ny * 36), q = 0;
        function tv(i, j) {
            var I = Math.max(1, Math.min(nx - 1, i)), J = Math.max(1, Math.min(ny - 1, j));
            var dzx = (Z[j * (nx + 1) + I + 1] - Z[j * (nx + 1) + I - 1]) / (2 * gx), dzy = (Z[(J + 1) * (nx + 1) + i] - Z[(J - 1) * (nx + 1) + i]) / (2 * gy);
            var l = Math.hypot(dzx, dzy, 1);
            top[q++] = -k.W1 / 2 + i * gx; top[q++] = k.back - k.D1 / 2 + j * gy; top[q++] = Z[j * (nx + 1) + i];
            top[q++] = -dzx / l; top[q++] = -dzy / l; top[q++] = 1 / l;
        }
        for (j = 0; j < ny; j++) for (i = 0; i < nx; i++) { tv(i, j); tv(i + 1, j); tv(i + 1, j + 1); tv(i, j); tv(i + 1, j + 1); tv(i, j + 1); }
        out.capTop = top;

        // Sides: rings from the base outline to the top outline.
        var rings = 12, base = ringPoints(k.W0, k.D0, k.r0, 0), topRing = ringPoints(k.W1, k.D1, k.r1, k.back), np = base.length, P = [];
        for (var r = 0; r <= rings; r++) {
            var t = r / rings, bulge = k.p.bulge * Math.sin(Math.PI * t), ring = [];
            var w = k.W0 + (k.W1 - k.W0) * t + 2 * bulge, d = k.D0 + (k.D1 - k.D0) * t + 2 * bulge, rr = k.r0 + (k.r1 - k.r0) * t;
            ringPoints(w, d, rr, k.back * t).forEach(function (pt, pi) {
                ring.push([pt[0], pt[1], t * keyTopZ(k, topRing[pi][0], topRing[pi][1], false)]);
            });
            P.push(ring);
        }
        var sides = [], cyAt = function (r) { return k.back * r / rings; };
        function sn(r, i) {
            var a = P[r][(i + 1) % np], b = P[r][(i - 1 + np) % np], c = P[Math.min(rings, r + 1)][i], d = P[Math.max(0, r - 1)][i];
            var n = unit(cross(sub(a, b), sub(c, d)));
            if (n[0] * P[r][i][0] + n[1] * (P[r][i][1] - cyAt(r)) < 0) n = [-n[0], -n[1], -n[2]];
            return n;
        }
        for (r = 0; r < rings; r++) {
            for (i = 0; i < np; i++) {
                var i2 = (i + 1) % np;
                [[r, i], [r, i2], [r + 1, i2], [r, i], [r + 1, i2], [r + 1, i]].forEach(function (v) {
                    var pt = P[v[0]][v[1]], n = sn(v[0], v[1]);
                    sides.push(pt[0], pt[1], pt[2], n[0], n[1], n[2]);
                });
            }
        }
        out.capSides = new Float32Array(sides);

        // Bottom: a fan of the base outline.
        var bottom = [];
        for (i = 0; i < np; i++) {
            var p1 = base[i], p2 = base[(i + 1) % np];
            bottom.push(0, 0, 0, 0, 0, -1, p2[0], p2[1], 0, 0, 0, -1, p1[0], p1[1], 0, 0, 0, -1);
        }
        out.capBottom = new Float32Array(bottom);

        // Stems under the cap; wide keys get stabilizer stems.
        var xs = [0];
        if (k.u >= 6) xs.push(-50, 50); else if (k.u >= 2) xs.push(-11.9, 11.9);
        var stem = [], mark = [], sh = 3.6;
        function add(arr, data) { for (var a = 0; a < data.length; a++) arr.push(data[a]); }
        xs.forEach(function (x0) {
            if (s.stem === 'choc' && x0 === 0) {
                add(stem, box(-3.6, -2.4, -1.5, 1.5, -3, 0)); add(stem, box(2.4, 3.6, -1.5, 1.5, -3, 0));
            } else if (s.stem === 'alps' && x0 === 0) {
                add(stem, box(-2.25, 2.25, -1.1, 1.1, -sh, 0));
            } else {
                var seg = 20, rad = 2.75;
                for (var a = 0; a < seg; a++) {
                    var a0 = a / seg * Math.PI * 2, a1 = (a + 1) / seg * Math.PI * 2, c0 = Math.cos(a0), s0 = Math.sin(a0), c1 = Math.cos(a1), s1 = Math.sin(a1);
                    add(stem, [x0 + c0 * rad, s0 * rad, 0, c0, s0, 0, x0 + c1 * rad, s1 * rad, 0, c1, s1, 0, x0 + c1 * rad, s1 * rad, -sh, c1, s1, 0,
                        x0 + c0 * rad, s0 * rad, 0, c0, s0, 0, x0 + c1 * rad, s1 * rad, -sh, c1, s1, 0, x0 + c0 * rad, s0 * rad, -sh, c0, s0, 0,
                        x0, 0, -sh, 0, 0, -1, x0 + c1 * rad, s1 * rad, -sh, 0, 0, -1, x0 + c0 * rad, s0 * rad, -sh, 0, 0, -1]);
                }
                add(mark, box(x0 - 2.05, x0 + 2.05, -0.65, 0.65, -sh - 0.05, -sh + 0.2));
                add(mark, box(x0 - 0.65, x0 + 0.65, -2.05, 2.05, -sh - 0.05, -sh + 0.2));
            }
        });
        out.stem = new Float32Array(stem);
        out.stemMark = new Float32Array(mark);

        var Hmax = k.H + Math.max(0, k.tilt * k.D1 / 2) + Math.max(0, disp);
        return {
            parts: out, M: M, k: k,
            dims: { length: Math.round(k.W0 * 10) / 10, height: Math.round(k.D0 * 10) / 10, depth: Math.round(Hmax * 10) / 10 },
            texRect: [-k.W1 / 2, k.back - k.D1 / 2, k.W1, k.D1],
            target: [0, 0, k.H * 0.45], radius: Math.hypot(k.W0 / 2, k.D0 / 2, k.H) * 1.05
        };
    }

    // ---------- WebGL ----------

    var VS = 'attribute vec3 aPos; attribute vec3 aNor; uniform mat4 uMVP; uniform mat3 uRot; uniform vec3 uTrans; uniform vec4 uTexRect;' +
        'varying vec3 vN; varying vec3 vP; varying vec2 vUV;' +
        'void main() { vec3 p = uRot * aPos + uTrans; vN = uRot * aNor; vP = p; vUV = (aPos.xy - uTexRect.xy) / uTexRect.zw; gl_Position = uMVP * vec4(p, 1.0); }';

    // uMode: 0 solid, 1 inside the plate mask, 2 inside the relief mask, 3 plate without relief (engraved / flush),
    // 4 keycap top: inside the shape mask, legend (G) in uColor2; 5 pocket floor (B). Pockets (B) are cut out of 1 and 3.
    var FS = 'precision mediump float; uniform vec3 uColor; uniform vec3 uColor2; uniform vec3 uLight; uniform vec3 uEye; uniform sampler2D uTex; uniform float uMode;' +
        'varying vec3 vN; varying vec3 vP; varying vec2 vUV;' +
        'void main() { vec4 m = texture2D(uTex, vec2(vUV.x, 1.0 - vUV.y));' +
        ' if (uMode > 0.5 && uMode < 1.5 && (m.r < 0.5 || m.b >= 0.5)) discard;' +
        ' if (uMode > 1.5 && uMode < 2.5 && m.g < 0.5) discard;' +
        ' if (uMode > 2.5 && uMode < 3.5 && (m.r < 0.5 || m.g >= 0.5 || m.b >= 0.5)) discard;' +
        ' if (uMode > 4.5 && m.b < 0.5) discard;' +
        ' vec3 col = uColor; if (uMode > 3.5 && uMode < 4.5) { if (m.r < 0.5) discard; if (m.g >= 0.5) col = uColor2; }' +
        ' vec3 n = normalize(vN); vec3 v = normalize(uEye - vP); float diff = max(dot(n, uLight), 0.0);' +
        ' float spec = pow(max(dot(n, normalize(uLight + v)), 0.0), 36.0); float sky = 0.5 + 0.5 * n.z;' +
        ' gl_FragColor = vec4(col * (0.34 + 0.52 * diff + 0.18 * sky) + vec3(0.09 * spec), 1.0); }';

    var PARTS = ['baseWalls', 'baseTop', 'baseBottom', 'reliefWalls', 'reliefTop', 'foot', 'capSides', 'capTop', 'capBottom', 'stem', 'stemMark',
        'pocketWalls', 'pocketFloor', 'tileWalls', 'tileTop', 'tileTextWalls', 'tileTextTop'];

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

        var g = { gl: gl, tex: gl.createTexture(), tex2: gl.createTexture(), counts: {} };
        PARTS.forEach(function (p) { g[p] = gl.createBuffer(); g.counts[p] = 0; });
        ['aPos', 'aNor'].forEach(function (n) { g[n] = gl.getAttribLocation(prog, n); });
        ['uMVP', 'uRot', 'uTrans', 'uTexRect', 'uColor', 'uColor2', 'uLight', 'uEye', 'uTex', 'uMode'].forEach(function (n) { g[n] = gl.getUniformLocation(prog, n); });
        [g.tex, g.tex2].forEach(function (t) {
            gl.bindTexture(gl.TEXTURE_2D, t);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        });
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
    var GEOMETRY = ['text', 'line2', 'font', 'upper', 'spacing', 'textScale', 'length', 'heightPct', 'thickness', 'shape', 'radius', 'margin', 'style', 'relief', 'border', 'hole', 'icon', 'iconSide', 'stand',
        'kind', 'board', 'profile', 'units', 'row', 'legendPos', 'homing', 'stem', 'qr'];

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

    View.prototype.home = function () {
        if (this.spec.kind === 'keycap') return { yaw: -0.5, pitch: 0.62 };
        return this.spec.stand ? { yaw: -0.4, pitch: 0.28 } : { yaw: -0.32, pitch: 0.95 };
    };

    // Straight at the face of the plate (a lying plate from above, a standing one from the front), no sway.
    View.prototype.face = function () {
        this.stopSway();
        this.yaw = 0;
        this.pitch = this.spec.kind === 'keycap' ? 1.52 : this.spec.stand ? Math.PI / 2 - TILT : 1.52;
        this.zoom = 1;
        this.request();
    };

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
        var standChanged = next.stand !== this.spec.stand || next.kind !== this.spec.kind;
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
        if (this.spec.kind === 'keycap') { this.buildKeycap(); return; }
        this.cap = null;
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
        // Standing: a slotted foot under the plate's lower edge, reaching back behind the leaning plate. The edge
        // sits half the plate height × cos(tilt) in front of the center (far for tall boards), so the foot starts there.
        var H = M.size[1], L = M.size[0], edge = -(H / 2) * Math.cos(TILT);
        upload(g, 'foot', s.stand ? box(-L * 0.36, L * 0.36, edge - T * 1.6, edge + Math.max(H * 0.42, 14), 0, Math.max(2.4, T * 0.9)) : null);
        ['capSides', 'capTop', 'capBottom', 'stem', 'stemMark'].forEach(function (p) { upload(g, p, null); });
        // Tiles sit in pockets as deep as the plate allows and stand out a little; their text follows the style.
        if (M.tile) {
            var depth = Math.max(0.8, Math.min(T - 1, 1.6)), pro = 1, tz = T + pro, tr = Math.min(s.relief, s.style === 'engraved' ? depth + pro - 0.4 : 5);
            this.tileGeo = { depth: depth, pro: pro };
            upload(g, 'pocketWalls', walls(M, M.pocket, T - depth, T, true));
            upload(g, 'pocketFloor', frameQuad(M, T - depth, 1));
            upload(g, 'tileWalls', walls(M, M.tile, T - depth + 0.02, tz, false));
            upload(g, 'tileTop', frameQuad(M, tz, 1));
            if (s.style === 'raised') { upload(g, 'tileTextWalls', walls(M, M.tileText, tz - 0.05, tz + tr, false)); upload(g, 'tileTextTop', frameQuad(M, tz + tr, 1)); }
            else if (s.style === 'engraved') { upload(g, 'tileTextWalls', walls(M, M.tileText, tz - tr, tz, true)); upload(g, 'tileTextTop', frameQuad(M, tz - tr, 1)); }
            else { upload(g, 'tileTextWalls', null); upload(g, 'tileTextTop', frameQuad(M, tz, 1)); }
            gl.bindTexture(gl.TEXTURE_2D, g.tex2);
            gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, M.w, M.h, 0, gl.RGBA, gl.UNSIGNED_BYTE, M.tex2);
            this.dims.depth = Math.round((T + pro + (s.style === 'raised' ? tr : 0)) * 10) / 10;
        } else {
            this.tileGeo = null;
            ['pocketWalls', 'pocketFloor', 'tileWalls', 'tileTop', 'tileTextWalls', 'tileTextTop'].forEach(function (p) { upload(g, p, null); });
        }
        this.texRect = [-M.FW / 2, -M.FH / 2, M.FW, M.FH];
        gl.bindTexture(gl.TEXTURE_2D, g.tex);
        gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, M.w, M.h, 0, gl.RGBA, gl.UNSIGNED_BYTE, M.tex);
    };

    View.prototype.buildKeycap = function () {
        var cap = this.cap = keycapMesh(this.spec), g = this.gl;
        this.M = cap.M;
        this.dims = cap.dims;
        this.radius = cap.radius;
        if (!g) return;
        var gl = g.gl;
        ['baseWalls', 'baseTop', 'baseBottom', 'reliefWalls', 'reliefTop', 'foot', 'pocketWalls', 'pocketFloor', 'tileWalls', 'tileTop', 'tileTextWalls', 'tileTextTop'].forEach(function (p) { upload(g, p, null); });
        Object.keys(cap.parts).forEach(function (p) { upload(g, p, cap.parts[p].length ? cap.parts[p] : null); });
        this.texRect = cap.texRect;
        gl.bindTexture(gl.TEXTURE_2D, g.tex);
        gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, cap.M.w, cap.M.h, 0, gl.RGBA, gl.UNSIGNED_BYTE, cap.M.tex);
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
        var plateRot = [1, 0, 0, 0, 1, 0, 0, 0, 1], plateTrans = [0, 0, 0], target = this.cap ? this.cap.target : [0, 0, s.thickness / 2];
        if (s.stand && !this.cap) {
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

        function part(name, color, mode, rot, trans, color2, tex) {
            if (!g.counts[name]) return;
            gl.bindTexture(gl.TEXTURE_2D, tex || g.tex);
            var c = rgb(color), c2 = rgb(color2 || color);
            gl.uniform3f(g.uColor, c[0], c[1], c[2]);
            gl.uniform3f(g.uColor2, c2[0], c2[1], c2[2]);
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
        if (this.cap) {
            var I = [1, 0, 0, 0, 1, 0, 0, 0, 1], O = [0, 0, 0];
            part('capSides', s.base, 0, I, O);
            part('capTop', s.base, 4, I, O, s.color);
            part('capBottom', s.base, 0, I, O);
            part('stem', s.base, 0, I, O);
            part('stemMark', '#20201f', 0, I, O);
            return;
        }
        var cut = s.style !== 'raised' && M.hasRelief;
        part('baseWalls', s.base, 0, plateRot, plateTrans);
        part('baseTop', s.base, cut ? 3 : 1, plateRot, plateTrans);
        part('baseBottom', s.base, 1, plateRot, plateTrans);
        part('reliefWalls', s.style === 'raised' ? s.color : s.base, 0, plateRot, plateTrans);
        part('reliefTop', s.color, 2, plateRot, plateTrans);
        part('foot', s.base, 0, [1, 0, 0, 0, 1, 0, 0, 0, 1], [0, 0, 0]);
        if (this.tileGeo) {
            part('pocketWalls', s.base, 0, plateRot, plateTrans);
            part('pocketFloor', s.base, 5, plateRot, plateTrans);
            // "Tách ô": the tiles float above their pockets (along the plate's up direction when standing).
            var lift = s.explode ? Math.max(8, this.radius * 0.08) : 0;
            var tTrans = [plateTrans[0] + plateRot[6] * lift, plateTrans[1] + plateRot[7] * lift, plateTrans[2] + plateRot[8] * lift];
            var tcut = s.style !== 'raised';
            part('tileWalls', s.tileColor, 0, plateRot, tTrans, null, g.tex2);
            part('tileTop', s.tileColor, tcut ? 3 : 1, plateRot, tTrans, null, g.tex2);
            part('tileTextWalls', s.style === 'raised' ? s.color : s.tileColor, 0, plateRot, tTrans, null, g.tex2);
            part('tileTextTop', s.color, 2, plateRot, tTrans, null, g.tex2);
            gl.bindTexture(gl.TEXTURE_2D, g.tex);
        }
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

    window.TTNameplate = { View: View, PROFILES: PROFILES, FONTS: FONTS, addFonts: addFonts, SHAPES: SHAPES, ICONS: ICONS, DEFAULTS: DEFAULTS, ensureFonts: ensureFonts, fontOf: fontOf, iconOf: iconOf };
})();

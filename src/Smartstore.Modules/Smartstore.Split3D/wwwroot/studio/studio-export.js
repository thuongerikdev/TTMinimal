/* TT Minimal print files: turns the print pieces of a design (TTNameplate.solids: the frame with its pockets and the
   content pieces, per color) into closed triangle meshes and writes them as 3MF (Bambu Studio, OrcaSlicer,
   PrusaSlicer), binary STL or GLB (Blender, with colors). Contours come from marching squares over the masks, are
   simplified, walled and capped by ear clipping. Used by the "File in 3D" card of the admin order page; no
   dependencies. */
(function () {
    'use strict';

    // ---------- Contours ----------

    // Marching squares, as walls() in studio-nameplate.js: edge pairs per corner case, 5 and 10 decided by the center.
    var CASES = [null, [3, 0], [0, 1], [3, 1], [1, 2], null, [0, 2], [3, 2], [2, 3], [0, 2], null, [1, 2], [1, 3], [0, 1], [3, 0], null];
    // Cell corners a, b, c, d (clockwise from the top left); edge k runs from corner k to corner k + 1.
    var CX = [0, 1, 1, 0], CY = [0, 0, 1, 1];

    // Closed contours of a mask (iso 0.5) as flat [x, y, …] arrays in mask pixels, each with the inside on its
    // left as seen on screen (counterclockwise outlines, clockwise holes once y points up). loop.fixed flags the
    // points around saddle cells, where two contours pass within a fraction of a pixel: simplify keeps them, so the
    // simplified contours cannot cross there.
    function contours(f, w, h) {
        var t = 0.5, next = new Map(), xs = new Map(), ys = new Map(), saddle = new Uint8Array(w * h);
        var px = [0, 0, 0, 0], py = [0, 0, 0, 0], id = [0, 0, 0, 0], v = [0, 0, 0, 0];
        for (var j = 0; j < h - 1; j++) {
            for (var i = 0; i < w - 1; i++) {
                var o = j * w + i, a = f[o], b = f[o + 1], c = f[o + w + 1], d = f[o + w];
                var idx = (a > t ? 1 : 0) | (b > t ? 2 : 0) | (c > t ? 4 : 0) | (d > t ? 8 : 0);
                if (idx === 0 || idx === 15) continue;
                var pairs = CASES[idx];
                if (!pairs) {
                    saddle[o] = 1;
                    var inside = (a + b + c + d) / 4 > t;
                    pairs = (idx === 5) === inside ? [0, 1, 2, 3] : [3, 0, 1, 2];
                }
                v[0] = a; v[1] = b; v[2] = c; v[3] = d;
                px[0] = i + (t - a) / (b - a); py[0] = j; id[0] = o * 2;
                px[1] = i + 1; py[1] = j + (t - b) / (c - b); id[1] = (o + 1) * 2 + 1;
                px[2] = i + (t - d) / (c - d); py[2] = j + 1; id[2] = (o + w) * 2;
                px[3] = i; py[3] = j + (t - a) / (d - a); id[3] = o * 2 + 1;
                for (var p = 0; p < pairs.length; p += 2) {
                    var e1 = pairs[p], e2 = pairs[p + 1], dx = px[e2] - px[e1], dy = py[e2] - py[e1];
                    // The corners from the end of edge e1 round to edge e2 lie on one side of the segment, all inside or
                    // all outside: the farthest one tells the side.
                    var best = 0, side = 0;
                    for (var k = (e1 + 1) % 4; ; k = (k + 1) % 4) {
                        var cr = dx * (j + CY[k] - py[e1]) - dy * (i + CX[k] - px[e1]);
                        if (Math.abs(cr) > Math.abs(best)) { best = cr; side = v[k] > t ? 1 : -1; }
                        if (k === e2) break;
                    }
                    // On screen (y down) a positive cross product is on the right: the inside has to be on the left.
                    var s0 = e1, s1 = e2;
                    if (best * side > 0) { s0 = e2; s1 = e1; }
                    next.set(id[s0], id[s1]);
                    xs.set(id[s0], px[s0]); ys.set(id[s0], py[s0]);
                    xs.set(id[s1], px[s1]); ys.set(id[s1], py[s1]);
                }
            }
        }
        // A point id is its edge: (cell index) * 2, + 1 for a vertical edge.
        function nearSaddle(key) {
            var o = Math.floor(key / 2), i0 = o % w, j0 = (o - i0) / w;
            for (var j = Math.max(0, j0 - 2); j <= Math.min(h - 2, j0 + 1); j++) {
                for (var i = Math.max(0, i0 - 2); i <= Math.min(w - 2, i0 + 1); i++) if (saddle[j * w + i]) return true;
            }
            return false;
        }
        var loops = [], seen = new Set();
        next.forEach(function (_, start) {
            if (seen.has(start)) return;
            var loop = [], fixed = [], key = start, guard = next.size + 1;
            while (key !== undefined && !seen.has(key) && guard--) {
                seen.add(key);
                loop.push(xs.get(key), ys.get(key));
                fixed.push(nearSaddle(key));
                key = next.get(key);
            }
            if (key === start && loop.length >= 6) { loop.fixed = fixed; loops.push(loop); }
        });
        return loops;
    }

    // Douglas–Peucker on a closed loop (tolerance in the loop's units), keeping the points flagged in fixed;
    // drops repeated points.
    function simplify(p, eps, fixed) {
        var n = p.length / 2, i;
        if (n < 4) return p;
        var far = 0, fd = -1;
        for (i = 1; i < n; i++) {
            var dd = (p[i * 2] - p[0]) * (p[i * 2] - p[0]) + (p[i * 2 + 1] - p[1]) * (p[i * 2 + 1] - p[1]);
            if (dd > fd) { fd = dd; far = i; }
        }
        var keep = new Uint8Array(n), stack = [], e2 = eps * eps, kept = [];
        keep[0] = keep[far] = 1;
        if (fixed) for (i = 0; i < n; i++) if (fixed[i]) keep[i] = 1;
        for (i = 0; i < n; i++) if (keep[i]) kept.push(i);
        for (i = 0; i < kept.length; i++) stack.push(kept[i], i + 1 < kept.length ? kept[i + 1] : n);
        while (stack.length) {
            var b = stack.pop(), a = stack.pop(), ax = p[a * 2], ay = p[a * 2 + 1], bx = p[(b % n) * 2], by = p[(b % n) * 2 + 1];
            var vx = bx - ax, vy = by - ay, len2 = vx * vx + vy * vy, worst = -1, at = -1;
            for (i = a + 1; i < b; i++) {
                var wx = p[i * 2] - ax, wy = p[i * 2 + 1] - ay, d2;
                if (len2 > 0) { var cr = vx * wy - vy * wx; d2 = cr * cr / len2; } else d2 = wx * wx + wy * wy;
                if (d2 > worst) { worst = d2; at = i; }
            }
            if (at >= 0 && worst > e2) { keep[at] = 1; stack.push(a, at, at, b); }
        }
        var out = [];
        for (i = 0; i < n; i++) {
            if (!keep[i]) continue;
            var l = out.length;
            if (l && Math.abs(out[l - 2] - p[i * 2]) < 1e-9 && Math.abs(out[l - 1] - p[i * 2 + 1]) < 1e-9) continue;
            out.push(p[i * 2], p[i * 2 + 1]);
        }
        return out;
    }

    function signedArea(p) {
        var s = 0;
        for (var i = 0, n = p.length, j = n - 2; i < n; j = i, i += 2) s += (p[j] - p[i]) * (p[i + 1] + p[j + 1]);
        return s / 2;
    }

    function inPolygon(p, x, y) {
        var c = false;
        for (var i = 0, n = p.length, j = n - 2; i < n; j = i, i += 2) {
            if ((p[i + 1] > y) !== (p[j + 1] > y) && x < (p[j] - p[i]) * (y - p[i + 1]) / (p[j + 1] - p[i + 1]) + p[i]) c = !c;
        }
        return c;
    }

    // ---------- Ear clipping with holes (the earcut algorithm, without its z-order speed-up) ----------

    function Node(i, x, y) { this.i = i; this.x = x; this.y = y; this.prev = this.next = null; this.steiner = false; }

    function earcut(data, holes) {
        var tris = [], outerLen = holes.length ? holes[0] * 2 : data.length;
        var outer = linkedList(data, 0, outerLen, true);
        if (!outer || outer.next === outer.prev) return tris;
        if (holes.length) outer = eliminateHoles(data, holes, outer);
        earcutLinked(outer, tris, 0);
        return tris;
    }

    function linkedList(data, start, end, clockwise) {
        var i, last = null;
        if (clockwise === (ringArea(data, start, end) > 0)) {
            for (i = start; i < end; i += 2) last = insertNode(i / 2, data[i], data[i + 1], last);
        } else {
            for (i = end - 2; i >= start; i -= 2) last = insertNode(i / 2, data[i], data[i + 1], last);
        }
        if (last && equals(last, last.next)) { removeNode(last); last = last.next; }
        return last;
    }

    function ringArea(data, start, end) {
        var s = 0;
        for (var i = start, j = end - 2; i < end; i += 2) { s += (data[j] - data[i]) * (data[i + 1] + data[j + 1]); j = i; }
        return s;
    }

    function filterPoints(start, end) {
        if (!start) return start;
        if (!end) end = start;
        var p = start, again;
        do {
            again = false;
            if (!p.steiner && (equals(p, p.next) || area(p.prev, p, p.next) === 0)) {
                removeNode(p);
                p = end = p.prev;
                if (p === p.next) break;
                again = true;
            } else p = p.next;
        } while (again || p !== end);
        return end;
    }

    function earcutLinked(ear, tris, pass) {
        if (!ear) return;
        var stop = ear, prev, next;
        while (ear.prev !== ear.next) {
            prev = ear.prev; next = ear.next;
            if (isEar(ear)) {
                tris.push(prev.i, ear.i, next.i);
                removeNode(ear);
                ear = next.next; stop = next.next;
                continue;
            }
            ear = next;
            if (ear === stop) {
                if (!pass) earcutLinked(filterPoints(ear), tris, 1);
                else if (pass === 1) earcutLinked(cureLocalIntersections(filterPoints(ear), tris), tris, 2);
                else if (pass === 2) splitEarcut(ear, tris);
                break;
            }
        }
    }

    function isEar(ear) {
        var a = ear.prev, b = ear, c = ear.next;
        if (area(a, b, c) >= 0) return false;
        var p = c.next;
        while (p !== a) {
            if (pointInTriangle(a.x, a.y, b.x, b.y, c.x, c.y, p.x, p.y) && area(p.prev, p, p.next) >= 0) return false;
            p = p.next;
        }
        return true;
    }

    function cureLocalIntersections(start, tris) {
        var p = start;
        do {
            var a = p.prev, b = p.next.next;
            if (!equals(a, b) && intersects(a, p, p.next, b) && locallyInside(a, b) && locallyInside(b, a)) {
                tris.push(a.i, p.i, b.i);
                removeNode(p); removeNode(p.next);
                p = start = b;
            }
            p = p.next;
        } while (p !== start);
        return filterPoints(p);
    }

    function splitEarcut(start, tris) {
        var a = start;
        do {
            var b = a.next.next;
            while (b !== a.prev) {
                if (a.i !== b.i && isValidDiagonal(a, b)) {
                    var c = splitPolygon(a, b);
                    a = filterPoints(a, a.next);
                    c = filterPoints(c, c.next);
                    earcutLinked(a, tris, 0);
                    earcutLinked(c, tris, 0);
                    return;
                }
                b = b.next;
            }
            a = a.next;
        } while (a !== start);
    }

    function eliminateHoles(data, holes, outer) {
        var queue = [], i, list;
        for (i = 0; i < holes.length; i++) {
            var start = holes[i] * 2, end = i < holes.length - 1 ? holes[i + 1] * 2 : data.length;
            list = linkedList(data, start, end, false);
            if (!list) continue;
            if (list === list.next) list.steiner = true;
            queue.push(getLeftmost(list));
        }
        queue.sort(function (a, b) { return a.x - b.x; });
        for (i = 0; i < queue.length; i++) outer = eliminateHole(queue[i], outer);
        return outer;
    }

    function eliminateHole(hole, outer) {
        var bridge = findHoleBridge(hole, outer);
        if (!bridge) return outer;
        var bridgeReverse = splitPolygon(bridge, hole);
        filterPoints(bridgeReverse, bridgeReverse.next);
        return filterPoints(bridge, bridge.next);
    }

    // David Eberly's algorithm: connects a hole to the outline at a point visible from the hole's leftmost vertex.
    function findHoleBridge(hole, outer) {
        var p = outer, hx = hole.x, hy = hole.y, qx = -Infinity, m = null;
        do {
            if (hy <= p.y && hy >= p.next.y && p.next.y !== p.y) {
                var x = p.x + (hy - p.y) * (p.next.x - p.x) / (p.next.y - p.y);
                if (x <= hx && x > qx) {
                    qx = x;
                    m = p.x < p.next.x ? p : p.next;
                    if (x === hx) return m;
                }
            }
            p = p.next;
        } while (p !== outer);
        if (!m) return null;
        var stop = m, mx = m.x, my = m.y, tanMin = Infinity;
        p = m;
        do {
            if (hx >= p.x && p.x >= mx && hx !== p.x &&
                pointInTriangle(hy < my ? hx : qx, hy, mx, my, hy < my ? qx : hx, hy, p.x, p.y)) {
                var tan = Math.abs(hy - p.y) / (hx - p.x);
                if (locallyInside(p, hole) && (tan < tanMin || (tan === tanMin && (p.x > m.x || (p.x === m.x && sectorContainsSector(m, p)))))) {
                    m = p; tanMin = tan;
                }
            }
            p = p.next;
        } while (p !== stop);
        return m;
    }

    function sectorContainsSector(m, p) { return area(m.prev, m, p.prev) < 0 && area(p.next, m, m.next) < 0; }

    function getLeftmost(start) {
        var p = start, left = start;
        do { if (p.x < left.x || (p.x === left.x && p.y < left.y)) left = p; p = p.next; } while (p !== start);
        return left;
    }

    function pointInTriangle(ax, ay, bx, by, cx, cy, px, py) {
        return (cx - px) * (ay - py) >= (ax - px) * (cy - py) && (ax - px) * (by - py) >= (bx - px) * (ay - py) && (bx - px) * (cy - py) >= (cx - px) * (by - py);
    }

    function isValidDiagonal(a, b) {
        return a.next.i !== b.i && a.prev.i !== b.i && !intersectsPolygon(a, b) &&
            (locallyInside(a, b) && locallyInside(b, a) && middleInside(a, b) && (area(a.prev, a, b.prev) || area(a, b.prev, b)) ||
                equals(a, b) && area(a.prev, a, a.next) > 0 && area(b.prev, b, b.next) > 0);
    }

    function area(p, q, r) { return (q.y - p.y) * (r.x - q.x) - (q.x - p.x) * (r.y - q.y); }
    function equals(p1, p2) { return p1.x === p2.x && p1.y === p2.y; }

    function intersects(p1, q1, p2, q2) {
        var o1 = sign(area(p1, q1, p2)), o2 = sign(area(p1, q1, q2)), o3 = sign(area(p2, q2, p1)), o4 = sign(area(p2, q2, q1));
        if (o1 !== o2 && o3 !== o4) return true;
        if (o1 === 0 && onSegment(p1, p2, q1)) return true;
        if (o2 === 0 && onSegment(p1, q2, q1)) return true;
        if (o3 === 0 && onSegment(p2, p1, q2)) return true;
        if (o4 === 0 && onSegment(p2, q1, q2)) return true;
        return false;
    }
    function onSegment(p, q, r) { return q.x <= Math.max(p.x, r.x) && q.x >= Math.min(p.x, r.x) && q.y <= Math.max(p.y, r.y) && q.y >= Math.min(p.y, r.y); }
    function sign(v) { return v > 0 ? 1 : v < 0 ? -1 : 0; }

    function intersectsPolygon(a, b) {
        var p = a;
        do {
            if (p.i !== a.i && p.next.i !== a.i && p.i !== b.i && p.next.i !== b.i && intersects(p, p.next, a, b)) return true;
            p = p.next;
        } while (p !== a);
        return false;
    }

    function locallyInside(a, b) {
        return area(a.prev, a, a.next) < 0
            ? area(a, b, a.next) >= 0 && area(a, a.prev, b) >= 0
            : area(a, b, a.prev) < 0 || area(a, a.next, b) < 0;
    }

    function middleInside(a, b) {
        var p = a, inside = false, px = (a.x + b.x) / 2, py = (a.y + b.y) / 2;
        do {
            if (((p.y > py) !== (p.next.y > py)) && p.next.y !== p.y && (px < (p.next.x - p.x) * (py - p.y) / (p.next.y - p.y) + p.x)) inside = !inside;
            p = p.next;
        } while (p !== a);
        return inside;
    }

    function splitPolygon(a, b) {
        var a2 = new Node(a.i, a.x, a.y), b2 = new Node(b.i, b.x, b.y), an = a.next, bp = b.prev;
        a.next = b; b.prev = a;
        a2.next = an; an.prev = a2;
        b2.next = a2; a2.prev = b2;
        bp.next = b2; b2.prev = bp;
        return b2;
    }

    function insertNode(i, x, y, last) {
        var p = new Node(i, x, y);
        if (!last) { p.prev = p; p.next = p; }
        else { p.next = last.next; p.prev = last; last.next.prev = p; last.next = p; }
        return p;
    }

    function removeNode(p) { p.next.prev = p.prev; p.prev.next = p.next; }

    // ---------- Meshes ----------

    // Contour tolerance in mask pixels: a few hundredths of a millimeter, far below what a printer resolves.
    var SIMPLIFY = 0.2;

    // Outlines (counterclockwise, mm, y up) of mask f with their holes: [{ outer, holes: [] }].
    function polygons(f, frame) {
        var R = frame.R, ox = frame.FW / 2, oy = frame.FH / 2, outers = [], holes = [];
        contours(f, frame.w, frame.h).forEach(function (loop) {
            var p = simplify(loop, SIMPLIFY, loop.fixed);
            if (p.length < 6) return;
            for (var i = 0; i < p.length; i += 2) { p[i] = (p[i] + 0.5) / R - ox; p[i + 1] = oy - (p[i + 1] + 0.5) / R; }
            var a = signedArea(p);
            if (Math.abs(a) < 1e-4) return;
            // Inside on the left as seen on screen: counterclockwise with y up, so a positive area is an outline.
            (a > 0 ? outers : holes).push({ p: p, area: Math.abs(a) });
        });
        var polys = outers.sort(function (a, b) { return a.area - b.area; }).map(function (o) { return { outer: o.p, holes: [] }; });
        holes.forEach(function (hl) {
            var x = hl.p[0], y = hl.p[1];
            for (var k = 0; k < polys.length; k++) {
                if (inPolygon(polys[k].outer, x, y)) { polys[k].holes.push(hl.p); return; }
            }
        });
        return polys;
    }

    // Triangles (flat array, 9 floats each) of mask f extruded from z0 to z1, moved by dy, dz.
    function extrude(out, f, frame, z0, z1, dy, dz) {
        dy = dy || 0; dz = dz || 0;
        z0 += dz; z1 += dz;
        polygons(f, frame).forEach(function (poly) {
            var data = poly.outer.slice(), starts = [];
            poly.holes.forEach(function (hl) { starts.push(data.length / 2); Array.prototype.push.apply(data, hl); });
            // Walls: the inside is on the left of every ring, so (a, b, b', a') faces out.
            [poly.outer].concat(poly.holes).forEach(function (r) {
                for (var i = 0, n = r.length; i < n; i += 2) {
                    var j = (i + 2) % n, ax = r[i], ay = r[i + 1] + dy, bx = r[j], by = r[j + 1] + dy;
                    out.push(ax, ay, z0, bx, by, z0, bx, by, z1, ax, ay, z0, bx, by, z1, ax, ay, z1);
                }
            });
            var t = earcut(data, starts);
            for (var k = 0; k < t.length; k += 3) {
                var a = t[k] * 2, b = t[k + 1] * 2, c = t[k + 2] * 2;
                var up = (data[b] - data[a]) * (data[c + 1] - data[a + 1]) - (data[b + 1] - data[a + 1]) * (data[c] - data[a]) > 0;
                if (!up) { var sw = b; b = c; c = sw; }
                out.push(data[a], data[a + 1] + dy, z1, data[b], data[b + 1] + dy, z1, data[c], data[c + 1] + dy, z1);
                out.push(data[a], data[a + 1] + dy, z0, data[c], data[c + 1] + dy, z0, data[b], data[b + 1] + dy, z0);
            }
        });
    }

    function boxTris(out, b) {
        var x0 = b[0], x1 = b[1], y0 = b[2], y1 = b[3], z0 = b[4], z1 = b[5];
        var v = [[x0, y0, z0], [x1, y0, z0], [x1, y1, z0], [x0, y1, z0], [x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]];
        [[0, 2, 1], [0, 3, 2], [4, 5, 6], [4, 6, 7], [0, 1, 5], [0, 5, 4], [1, 2, 6], [1, 6, 5], [2, 3, 7], [2, 7, 6], [3, 0, 4], [3, 4, 7]].forEach(function (t) {
            t.forEach(function (k) { out.push(v[k][0], v[k][1], v[k][2]); });
        });
    }

    /**
     * Print files of a design spec (options: see EXPORT in studio-nameplate.js). Resolves with
     * { dims, files: [{ key, title, color, parts: [{ name, color, group, shells: [Float32Array], count }] }] }: the
     * frame first, then the content, one file per color. Every solid is a closed shell of its own (the layers of a
     * frame with pockets touch but are not merged, which slicers handle as one body); every file is moved down to
     * stand on the build plate. P is TTNameplate.
     */
    function build(P, spec, options) {
        return P.solids(spec, options).then(function (r) {
            var parts = r.parts.map(function (p) {
                var shells = p.solids.map(function (sd) {
                    var out = [];
                    if (sd.box) boxTris(out, sd.box);
                    else extrude(out, sd.f, r.frame, sd.z0, sd.z1, sd.dy, 0);
                    return new Float32Array(out);
                }).filter(function (t) { return t.length; });
                return { name: p.name, color: p.color, file: p.file, group: p.group, shells: shells, count: shells.reduce(function (s, t) { return s + t.length / 9; }, 0) };
            }).filter(function (p) { return p.count; });

            var files = [], frame = parts.filter(function (p) { return p.file === 'frame'; }), colors = [];
            parts.forEach(function (p) { if (p.file === 'content' && colors.indexOf(p.color) < 0) colors.push(p.color); });
            // Nothing apart (a QR plate): the frame is the whole print.
            if (frame.length) files.push(colors.length ? { key: 'khung', title: 'Khung', color: frame[0].color, parts: frame } : { key: 'nguyen-khoi', title: 'Nguyên khối', color: frame[0].color, parts: frame });
            colors.forEach(function (c) {
                files.push({
                    key: colors.length > 1 ? 'noi-dung-' + c.slice(1).toLowerCase() : 'noi-dung', title: 'Nội dung' + (colors.length > 1 ? ' ' + c : ''), color: c,
                    parts: parts.filter(function (p) { return p.file === 'content' && p.color === c; })
                });
            });
            files.forEach(function (f) {
                var min = Infinity;
                f.parts.forEach(function (p) { p.shells.forEach(function (t) { for (var k = 2; k < t.length; k += 3) if (t[k] < min) min = t[k]; }); });
                if (min) f.parts.forEach(function (p) { p.shells.forEach(function (t) { for (var k = 2; k < t.length; k += 3) t[k] -= min; }); });
                f.count = f.parts.reduce(function (s, p) { return s + p.count; }, 0);
            });
            return { dims: r.dims, files: files };
        });
    }

    // ---------- Writers ----------

    function stl(mesh) {
        var n = mesh.parts.reduce(function (s, p) { return s + p.count; }, 0);
        var buf = new ArrayBuffer(84 + n * 50), dv = new DataView(buf), o = 84;
        var head = 'TT Minimal print file';
        for (var i = 0; i < head.length; i++) dv.setUint8(i, head.charCodeAt(i));
        dv.setUint32(80, n, true);
        mesh.parts.forEach(function (p) { p.shells.forEach(function (t) {
            for (var k = 0; k < t.length; k += 9) {
                var ux = t[k + 3] - t[k], uy = t[k + 4] - t[k + 1], uz = t[k + 5] - t[k + 2];
                var vx = t[k + 6] - t[k], vy = t[k + 7] - t[k + 1], vz = t[k + 8] - t[k + 2];
                var nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx, l = Math.hypot(nx, ny, nz) || 1;
                dv.setFloat32(o, nx / l, true); dv.setFloat32(o + 4, ny / l, true); dv.setFloat32(o + 8, nz / l, true);
                for (var q = 0; q < 9; q++) dv.setFloat32(o + 12 + q * 4, t[k + q], true);
                o += 50;
            }
        }); });
        return new Blob([buf], { type: 'model/stl' });
    }

    function xmlEsc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;'); }
    function num(v) { return String(Math.round(v * 10000) / 10000); }

    // One mesh object per shell (shared vertices), one object with them as components (parts) per group.
    function threeMF(mesh, title) {
        var colors = [], xml = [], groups = [], id = 2;
        mesh.parts.forEach(function (p) { if (colors.indexOf(p.color) < 0) colors.push(p.color); });
        xml.push('<?xml version="1.0" encoding="UTF-8"?>\n<model unit="millimeter" xml:lang="vi-VN" xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02">',
            '<metadata name="Title">' + xmlEsc(title) + '</metadata><metadata name="Application">TT Minimal</metadata><resources>',
            '<basematerials id="1">' + colors.map(function (c, i) { return '<base name="Màu ' + (i + 1) + '" displaycolor="' + c + 'FF"/>'; }).join('') + '</basematerials>');
        mesh.parts.forEach(function (p) { p.shells.forEach(function (t, si) {
            var map = new Map(), verts = [], tris = [], k;
            for (k = 0; k < t.length; k += 3) {
                var key = num(t[k]) + ' ' + num(t[k + 1]) + ' ' + num(t[k + 2]), vi = map.get(key);
                if (vi === undefined) { vi = verts.length; map.set(key, vi); verts.push('<vertex x="' + num(t[k]) + '" y="' + num(t[k + 1]) + '" z="' + num(t[k + 2]) + '"/>'); }
                tris.push(vi);
            }
            var tx = [];
            for (k = 0; k < tris.length; k += 3) {
                if (tris[k] === tris[k + 1] || tris[k + 1] === tris[k + 2] || tris[k] === tris[k + 2]) continue;
                tx.push('<triangle v1="' + tris[k] + '" v2="' + tris[k + 1] + '" v3="' + tris[k + 2] + '"/>');
            }
            var name = p.name + (p.shells.length > 1 ? ' ' + (si + 1) : '');
            xml.push('<object id="' + id + '" type="model" name="' + xmlEsc(name) + '" pid="1" pindex="' + colors.indexOf(p.color) + '"><mesh><vertices>',
                verts.join(''), '</vertices><triangles>', tx.join(''), '</triangles></mesh></object>');
            var g = groups.filter(function (x) { return x.key === p.group; })[0];
            if (!g) groups.push(g = { key: p.group, ids: [] });
            g.ids.push(id++);
        }); });
        var names = { main: title, tiles: 'Ô rời', foot: 'Chân đế' }, items = [];
        groups.forEach(function (g) {
            xml.push('<object id="' + id + '" type="model" name="' + xmlEsc(names[g.key] || g.key) + '"><components>'
                + g.ids.map(function (c) { return '<component objectid="' + c + '"/>'; }).join('') + '</components></object>');
            items.push('<item objectid="' + id++ + '"/>');
        });
        xml.push('</resources><build>', items.join(''), '</build></model>');
        var enc = new TextEncoder();
        return new Blob([zip([
            { name: '[Content_Types].xml', data: enc.encode('<?xml version="1.0" encoding="UTF-8"?>\n<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="model" ContentType="application/vnd.ms-package.3dmanufacturing-3dmodel+xml"/></Types>') },
            { name: '_rels/.rels', data: enc.encode('<?xml version="1.0" encoding="UTF-8"?>\n<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Target="/3D/3dmodel.model" Id="rel0" Type="http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel"/></Relationships>') },
            { name: '3D/3dmodel.model', data: enc.encode(xml.join('')) }
        ])], { type: 'model/3mf' });
    }

    // ---------- GLB (binary glTF 2.0) ----------

    // sRGB "#RRGGBB" to linear RGBA, as glTF expects base colors.
    function linearColor(hex) {
        return [1, 3, 5].map(function (i) {
            var c = parseInt(hex.substr(i, 2), 16) / 255;
            return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
        }).concat([1]);
    }

    // For Blender and other 3D apps (3MF colors are read by slicers only): one mesh per shell with its part's name
    // and color as a material, grouped per piece. Flat shaded (every triangle its own vertices and face normal).
    // glTF is in meters with y up: (x, y, z) mm becomes (x, z, -y) / 1000.
    function glb(mesh, title) {
        var colors = [], materials = [], meshes = [], nodes = [], accessors = [], views = [], chunks = [], offset = 0, groups = [];
        mesh.parts.forEach(function (p) {
            if (colors.indexOf(p.color) < 0) {
                colors.push(p.color);
                materials.push({ name: 'Màu ' + p.color, pbrMetallicRoughness: { baseColorFactor: linearColor(p.color), metallicFactor: 0, roughnessFactor: 0.6 } });
            }
        });
        function addView(arr) {
            views.push({ buffer: 0, byteOffset: offset, byteLength: arr.byteLength, target: 34962 });
            chunks.push(new Uint8Array(arr.buffer, arr.byteOffset, arr.byteLength));
            offset += arr.byteLength;
            return views.length - 1;
        }
        mesh.parts.forEach(function (p) {
            p.shells.forEach(function (t, si) {
                var n = t.length / 3, pos = new Float32Array(n * 3), nor = new Float32Array(n * 3);
                var min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
                for (var k = 0; k < t.length; k += 9) {
                    var ux = t[k + 3] - t[k], uy = t[k + 4] - t[k + 1], uz = t[k + 5] - t[k + 2];
                    var vx = t[k + 6] - t[k], vy = t[k + 7] - t[k + 1], vz = t[k + 8] - t[k + 2];
                    var nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx, l = Math.hypot(nx, ny, nz) || 1;
                    for (var q = 0; q < 9; q += 3) {
                        var o = k + q, x = t[o] / 1000, y = t[o + 2] / 1000, z = -t[o + 1] / 1000;
                        pos[o] = x; pos[o + 1] = y; pos[o + 2] = z;
                        nor[o] = nx / l; nor[o + 1] = nz / l; nor[o + 2] = -ny / l;
                        if (x < min[0]) min[0] = x; if (y < min[1]) min[1] = y; if (z < min[2]) min[2] = z;
                        if (x > max[0]) max[0] = x; if (y > max[1]) max[1] = y; if (z > max[2]) max[2] = z;
                    }
                }
                accessors.push({ bufferView: addView(pos), componentType: 5126, count: n, type: 'VEC3', min: min, max: max });
                accessors.push({ bufferView: addView(nor), componentType: 5126, count: n, type: 'VEC3' });
                var name = p.name + (p.shells.length > 1 ? ' ' + (si + 1) : '');
                meshes.push({ name: name, primitives: [{ attributes: { POSITION: accessors.length - 2, NORMAL: accessors.length - 1 }, material: colors.indexOf(p.color) }] });
                nodes.push({ name: name, mesh: meshes.length - 1 });
                var g = groups.filter(function (x) { return x.key === p.group; })[0];
                if (!g) groups.push(g = { key: p.group, children: [] });
                g.children.push(nodes.length - 1);
            });
        });
        var names = { main: title, tiles: 'Ô rời', foot: 'Chân đế' }, roots = groups.map(function (g) {
            nodes.push({ name: names[g.key] || g.key, children: g.children });
            return nodes.length - 1;
        });
        var json = {
            asset: { version: '2.0', generator: 'TT Minimal' },
            scene: 0, scenes: [{ name: title, nodes: roots }], nodes: nodes, meshes: meshes, materials: materials,
            accessors: accessors, bufferViews: views, buffers: [{ byteLength: offset }]
        };
        var jsonBytes = new TextEncoder().encode(JSON.stringify(json)), jsonLen = (jsonBytes.length + 3) & ~3, binLen = (offset + 3) & ~3;
        var out = new Uint8Array(12 + 8 + jsonLen + 8 + binLen), dv = new DataView(out.buffer);
        dv.setUint32(0, 0x46546C67, true); dv.setUint32(4, 2, true); dv.setUint32(8, out.length, true);
        dv.setUint32(12, jsonLen, true); dv.setUint32(16, 0x4E4F534A, true);
        out.set(jsonBytes, 20);
        for (var i = 20 + jsonBytes.length; i < 20 + jsonLen; i++) out[i] = 0x20;
        var b = 20 + jsonLen;
        dv.setUint32(b, binLen, true); dv.setUint32(b + 4, 0x004E4942, true);
        var o2 = b + 8;
        chunks.forEach(function (c) { out.set(c, o2); o2 += c.length; });
        return new Blob([out], { type: 'model/gltf-binary' });
    }

    // ---------- ZIP (stored, no compression) ----------

    var CRC = (function () {
        var t = new Uint32Array(256);
        for (var n = 0; n < 256; n++) { var c = n; for (var k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; }
        return t;
    })();
    function crc32(d) { var c = 0xFFFFFFFF; for (var i = 0; i < d.length; i++) c = CRC[(c ^ d[i]) & 0xFF] ^ (c >>> 8); return (c ^ 0xFFFFFFFF) >>> 0; }

    // files: [{ name, data: Uint8Array }] → Uint8Array of the archive (UTF-8 names).
    function zip(files) {
        var enc = new TextEncoder(), now = new Date(), parts = [], central = [], offset = 0;
        var time = (now.getHours() << 11) | (now.getMinutes() << 5) | (now.getSeconds() >> 1);
        var date = ((now.getFullYear() - 1980) << 9) | ((now.getMonth() + 1) << 5) | now.getDate();
        files.forEach(function (f) {
            var name = enc.encode(f.name), crc = crc32(f.data), size = f.data.length;
            var lh = new DataView(new ArrayBuffer(30));
            lh.setUint32(0, 0x04034b50, true); lh.setUint16(4, 20, true); lh.setUint16(6, 0x0800, true); lh.setUint16(8, 0, true);
            lh.setUint16(10, time, true); lh.setUint16(12, date, true); lh.setUint32(14, crc, true); lh.setUint32(18, size, true);
            lh.setUint32(22, size, true); lh.setUint16(26, name.length, true); lh.setUint16(28, 0, true);
            parts.push(new Uint8Array(lh.buffer), name, f.data);
            var ch = new DataView(new ArrayBuffer(46));
            ch.setUint32(0, 0x02014b50, true); ch.setUint16(4, 20, true); ch.setUint16(6, 20, true); ch.setUint16(8, 0x0800, true);
            ch.setUint16(10, 0, true); ch.setUint16(12, time, true); ch.setUint16(14, date, true); ch.setUint32(16, crc, true);
            ch.setUint32(20, size, true); ch.setUint32(24, size, true); ch.setUint16(28, name.length, true);
            ch.setUint32(42, offset, true);
            central.push(new Uint8Array(ch.buffer), name);
            offset += 30 + name.length + size;
        });
        var cdSize = central.reduce(function (s, a) { return s + a.length; }, 0), end = new DataView(new ArrayBuffer(22));
        end.setUint32(0, 0x06054b50, true); end.setUint16(8, files.length, true); end.setUint16(10, files.length, true);
        end.setUint32(12, cdSize, true); end.setUint32(16, offset, true);
        var all = parts.concat(central, [new Uint8Array(end.buffer)]), total = all.reduce(function (s, a) { return s + a.length; }, 0);
        var out = new Uint8Array(total), o = 0;
        all.forEach(function (a) { out.set(a, o); o += a.length; });
        return out;
    }

    // ---------- Admin order card ----------

    function slug(s) {
        return String(s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D')
            .replace(/[^A-Za-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 40) || 'thiet-ke';
    }

    function download(blob, name) {
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = name;
        document.body.appendChild(a);
        a.click();
        setTimeout(function () { URL.revokeObjectURL(a.href); a.remove(); }, 2000);
    }

    // One print file in the chosen format.
    function write(file, format, title) {
        return format === 'stl' ? stl(file) : format === 'glb' ? glb(file, title + ' - ' + file.title) : threeMF(file, title + ' - ' + file.title);
    }

    function zipBlob(entries) {
        return Promise.all(entries.map(function (e) { return e.blob.arrayBuffer(); })).then(function (bufs) {
            return new Blob([zip(entries.map(function (e, i) { return { name: e.name, data: new Uint8Array(bufs[i]) }; }))], { type: 'application/zip' });
        });
    }

    // Card of the admin order page (Views/Shared/Components/OrderPrintFiles): one row per order line with a design,
    // the print settings (clearance, floor, format; saved as defaults on request) and the downloads.
    function mount(card) {
        var cfg;
        try { cfg = JSON.parse(card.querySelector('script[data-tt-print-files]').textContent); } catch (e) { return; }
        var P = window.TTNameplate, msg = card.querySelector('[data-tt-msg]'), view = null, viewing = -1;
        if (!P) { msg.textContent = 'Không tải được bộ dựng 3D.'; return; }
        P.addFonts(cfg.fonts || []);

        function opt(name) { var el = card.querySelector('[data-tt-opt="' + name + '"]'); return el ? el.value : ''; }
        function options() { return { clearance: parseFloat(opt('clearance').replace(',', '.')), pocket: parseFloat(opt('pocket').replace(',', '.')) }; }
        function format() { return opt('format') || '3mf'; }
        function say(text, ok) { msg.textContent = text; msg.className = 'tt-pf-msg' + (ok ? ' is-ok' : text ? ' is-error' : ''); }
        function baseName(item) { return cfg.order + '-' + item.no + '-' + slug(item.text || item.name) + '-x' + item.qty; }
        function entries(item, m, which) {
            return m.files.filter(function (f) { return which === 'set' || (which === 'frame') === (f.key === 'khung' || f.key === 'nguyen-khoi'); }).map(function (f) {
                return { name: baseName(item) + '-' + f.key + '.' + format(), blob: write(f, format(), item.text || item.name), file: f };
            });
        }
        function summary(m) {
            return m.dims.length + ' × ' + m.dims.height + ' × ' + m.dims.depth + ' mm · '
                + m.files.map(function (f) { return f.title + ' ' + f.count.toLocaleString('vi-VN') + ' tam giác'; }).join(' · ');
        }

        var busy = false;
        function run(button, work) {
            if (busy) return;
            busy = true;
            var label = button.textContent;
            button.disabled = true;
            button.textContent = 'Đang dựng…';
            say('');
            // Lets the button repaint before the build blocks the page.
            setTimeout(function () {
                Promise.resolve().then(work).then(function (text) { say(text || '', true); }, function (err) {
                    say((err && err.message) || 'Không dựng được file.');
                }).then(function () { busy = false; button.disabled = false; button.textContent = label; });
            }, 30);
        }

        function saveSettings(button) {
            var token = card.querySelector('input[name="__RequestVerificationToken"]'), body = new URLSearchParams();
            body.set('clearance', opt('clearance').replace(',', '.'));
            body.set('pocket', opt('pocket').replace(',', '.'));
            body.set('format', format());
            if (token) body.set(token.name, token.value);
            button.disabled = true;
            fetch(cfg.saveUrl, { method: 'POST', credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' }, body: body })
                .then(function (res) { return res.ok ? res.json() : { success: false, message: 'Lỗi máy chủ (' + res.status + ').' }; })
                .then(function (r) { say(r.success ? 'Đã lưu làm mặc định.' : r.message || 'Không lưu được.', r.success); }, function () { say('Không lưu được.'); })
                .then(function () { button.disabled = false; });
        }

        card.addEventListener('click', function (e) {
            if (e.target.closest('[data-tt-save-settings]')) { saveSettings(e.target.closest('[data-tt-save-settings]')); return; }
            var b = e.target.closest('button[data-tt-file]');
            if (!b) return;
            var kind = b.getAttribute('data-tt-file'), item = cfg.items[+b.getAttribute('data-tt-item')];
            if (kind === 'view') {
                var stage = card.querySelector('[data-tt-stage]');
                if (viewing === +b.getAttribute('data-tt-item') && !stage.hidden) { stage.hidden = true; if (view) view.stopSway(); return; }
                viewing = +b.getAttribute('data-tt-item');
                stage.hidden = false;
                card.querySelector('[data-tt-stage-title]').textContent = item.no + '. ' + item.name + (item.text ? ' · ' + item.text : '');
                if (!view) view = new P.View(stage.querySelector('canvas'));
                view.set(item.spec).then(function (d) {
                    card.querySelector('[data-tt-stage-size]').textContent = d ? d.length + ' × ' + d.height + ' × ' + d.depth + ' mm' : '';
                });
                return;
            }
            if (kind === 'all') {
                // Every line in its own folder of the archive.
                run(b, function () {
                    var all = [], items = cfg.items.filter(function (x) { return x.spec; });
                    return items.reduce(function (p, it) {
                        return p.then(function () {
                            return build(P, it.spec, options()).then(function (m) {
                                entries(it, m, 'set').forEach(function (en) { all.push({ name: baseName(it) + '/' + en.name, blob: en.blob }); });
                            });
                        });
                    }, Promise.resolve()).then(function () { return zipBlob(all); }).then(function (z) {
                        download(z, 'don-' + cfg.order + '-file-in-3d.zip');
                        return 'Đã tải ' + all.length + ' file của ' + items.length + ' sản phẩm (.zip).';
                    });
                });
                return;
            }
            // frame, content (one file per color: several are zipped), set (all files of the line, zipped)
            run(b, function () {
                return build(P, item.spec, options()).then(function (m) {
                    var list = entries(item, m, kind);
                    if (!list.length) return 'Sản phẩm này không có phần ' + (kind === 'frame' ? 'khung' : 'nội dung') + ' riêng (cùng màu với khung). ' + summary(m);
                    if (list.length === 1) { download(list[0].blob, list[0].name); return 'Đã tải ' + list[0].name + ' · ' + summary(m); }
                    var name = baseName(item) + (kind === 'set' ? '' : '-' + (kind === 'frame' ? 'khung' : 'noi-dung')) + '.zip';
                    return zipBlob(list).then(function (z) { download(z, name); return 'Đã tải ' + name + ' (' + list.length + ' file) · ' + summary(m); });
                });
            });
        });
    }

    window.TTExport = { build: build, stl: stl, threeMF: threeMF, glb: glb, zip: zip, polygons: polygons, contours: contours, earcut: earcut };

    function init() { Array.prototype.forEach.call(document.querySelectorAll('[data-tt-print-card]'), mount); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();

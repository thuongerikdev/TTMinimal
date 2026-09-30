/* TT Minimal model reader: STL (binary/ASCII), OBJ and 3MF to a triangle soup with volume, area and bounds,
   plus a small canvas preview. No dependencies; loaded on demand by the price calculator in studio.js. */
(function () {
    'use strict';

    // ---------- Parsers: each returns { tris: Float32Array (9 floats per triangle), unit: mm per file unit } ----------

    function parseStl(buf) {
        var dv = new DataView(buf);
        if (buf.byteLength >= 84) {
            var n = dv.getUint32(80, true);
            if (84 + n * 50 === buf.byteLength) {
                var tris = new Float32Array(n * 9);
                for (var i = 0, o = 84; i < n; i++, o += 50) {
                    for (var k = 0; k < 9; k++) tris[i * 9 + k] = dv.getFloat32(o + 12 + k * 4, true);
                }
                return { tris: tris, unit: 1 };
            }
        }

        var text = new TextDecoder().decode(buf);
        var re = /vertex\s+(\S+)\s+(\S+)\s+(\S+)/g, m, out = [];
        while ((m = re.exec(text))) out.push(+m[1], +m[2], +m[3]);
        out.length -= out.length % 9;
        return { tris: new Float32Array(out), unit: 1 };
    }

    function parseObj(buf) {
        var lines = new TextDecoder().decode(buf).split('\n');
        var v = [], out = [];
        for (var i = 0; i < lines.length; i++) {
            var line = lines[i].trim();
            if (line.charCodeAt(0) === 118 && line.charCodeAt(1) === 32) { // "v "
                var p = line.split(/\s+/);
                v.push(+p[1], +p[2], +p[3]);
            } else if (line.charCodeAt(0) === 102 && line.charCodeAt(1) === 32) { // "f "
                var f = line.split(/\s+/), idx = [];
                for (var j = 1; j < f.length; j++) {
                    var n = parseInt(f[j], 10);
                    if (!isNaN(n)) idx.push(n < 0 ? v.length / 3 + n : n - 1);
                }
                for (var t = 1; t + 1 < idx.length; t++) {
                    var a = idx[0] * 3, b = idx[t] * 3, c = idx[t + 1] * 3;
                    out.push(v[a], v[a + 1], v[a + 2], v[b], v[b + 1], v[b + 2], v[c], v[c + 1], v[c + 2]);
                }
            }
        }
        return { tris: new Float32Array(out), unit: 1 };
    }

    // Minimal ZIP reader for 3MF: central directory + stored/deflate entries.
    function unzip(buf, filter) {
        var dv = new DataView(buf), u8 = new Uint8Array(buf), eocd = -1;
        for (var i = buf.byteLength - 22; i >= Math.max(0, buf.byteLength - 65557); i--) {
            if (dv.getUint32(i, true) === 0x06054b50) { eocd = i; break; }
        }
        if (eocd < 0) return Promise.reject(new Error('File 3MF bị hỏng (không đọc được nội dung nén).'));

        var count = dv.getUint16(eocd + 10, true), p = dv.getUint32(eocd + 16, true), jobs = [];
        var decoder = new TextDecoder();
        for (var e = 0; e < count; e++) {
            if (dv.getUint32(p, true) !== 0x02014b50) break;
            var method = dv.getUint16(p + 10, true), size = dv.getUint32(p + 20, true);
            var nameLen = dv.getUint16(p + 28, true), extraLen = dv.getUint16(p + 30, true), commentLen = dv.getUint16(p + 32, true);
            var local = dv.getUint32(p + 42, true);
            var name = decoder.decode(u8.subarray(p + 46, p + 46 + nameLen));
            p += 46 + nameLen + extraLen + commentLen;
            if (!filter(name)) continue;

            var start = local + 30 + dv.getUint16(local + 26, true) + dv.getUint16(local + 28, true);
            var data = u8.subarray(start, start + size);
            jobs.push(inflate(method, data).then((function (n) { return function (bytes) { return { name: n, text: decoder.decode(bytes) }; }; })(name)));
        }
        return Promise.all(jobs);
    }

    function inflate(method, data) {
        if (method === 0) return Promise.resolve(data);
        if (method !== 8) return Promise.reject(new Error('File 3MF dùng kiểu nén chưa hỗ trợ.'));
        if (typeof DecompressionStream === 'undefined') return Promise.reject(new Error('Trình duyệt quá cũ để đọc 3MF. Hãy xuất file STL.'));
        var stream = new Blob([data]).stream().pipeThrough(new DecompressionStream('deflate-raw'));
        return new Response(stream).arrayBuffer().then(function (b) { return new Uint8Array(b); });
    }

    var UNITS = { micron: 0.001, millimeter: 1, centimeter: 10, inch: 25.4, foot: 304.8, meter: 1000 };

    function parse3mf(buf) {
        return unzip(buf, function (n) { return /\.model$/i.test(n); }).then(function (files) {
            if (!files.length) throw new Error('Không tìm thấy mô hình trong file 3MF.');

            var objects = {}, root = null, unit = 1;
            files.forEach(function (f) {
                var xml = new DOMParser().parseFromString(f.text, 'application/xml');
                var model = xml.documentElement, path = '/' + f.name.replace(/^\//, '');
                if (!model || model.nodeName === 'parsererror') return;

                var objs = model.getElementsByTagName('object');
                for (var i = 0; i < objs.length; i++) {
                    var o = objs[i], entry = { mesh: null, comps: [] };
                    var mesh = o.getElementsByTagName('mesh')[0];
                    if (mesh) {
                        var vs = mesh.getElementsByTagName('vertex'), ts = mesh.getElementsByTagName('triangle');
                        var v = new Float64Array(vs.length * 3);
                        for (var k = 0; k < vs.length; k++) {
                            v[k * 3] = +vs[k].getAttribute('x'); v[k * 3 + 1] = +vs[k].getAttribute('y'); v[k * 3 + 2] = +vs[k].getAttribute('z');
                        }
                        var t = new Uint32Array(ts.length * 3);
                        for (k = 0; k < ts.length; k++) {
                            t[k * 3] = +ts[k].getAttribute('v1'); t[k * 3 + 1] = +ts[k].getAttribute('v2'); t[k * 3 + 2] = +ts[k].getAttribute('v3');
                        }
                        entry.mesh = { v: v, t: t };
                    }
                    var comps = o.getElementsByTagName('component');
                    for (k = 0; k < comps.length; k++) {
                        entry.comps.push({ id: comps[k].getAttribute('objectid'), path: attrLocal(comps[k], 'path') || path, m: matrix(comps[k].getAttribute('transform')) });
                    }
                    objects[path + '#' + o.getAttribute('id')] = entry;
                }

                var build = model.getElementsByTagName('build')[0];
                if (build && (!root || /3dmodel\.model$/i.test(path))) {
                    root = { path: path, items: build.getElementsByTagName('item') };
                    unit = UNITS[model.getAttribute('unit') || 'millimeter'] || 1;
                }
            });
            if (!root) throw new Error('File 3MF không có vật thể nào để in.');

            var out = [];
            function emit(key, m, depth) {
                var o = objects[key];
                if (!o || depth > 16) return;
                if (o.mesh) {
                    var v = o.mesh.v, t = o.mesh.t;
                    for (var i = 0; i < t.length; i++) {
                        var j = t[i] * 3, x = v[j], y = v[j + 1], z = v[j + 2];
                        out.push(x * m[0] + y * m[3] + z * m[6] + m[9], x * m[1] + y * m[4] + z * m[7] + m[10], x * m[2] + y * m[5] + z * m[8] + m[11]);
                    }
                }
                o.comps.forEach(function (c) { emit(c.path + '#' + c.id, compose(c.m, m), depth + 1); });
            }
            for (var i = 0; i < root.items.length; i++) {
                var item = root.items[i];
                emit((attrLocal(item, 'path') || root.path) + '#' + item.getAttribute('objectid'), matrix(item.getAttribute('transform')), 0);
            }
            return { tris: new Float32Array(out), unit: unit };
        });
    }

    function attrLocal(el, name) {
        for (var i = 0; i < el.attributes.length; i++) if (el.attributes[i].localName === name) return el.attributes[i].value;
        return null;
    }

    // 3MF transforms are 3x4 row-major, applied to row vectors: p' = [x y z 1] * M.
    function matrix(s) {
        var m = s ? s.trim().split(/\s+/).map(Number) : [];
        return m.length === 12 ? m : [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0];
    }

    // Apply a, then b.
    function compose(a, b) {
        var r = new Array(12);
        for (var row = 0; row < 4; row++) {
            for (var col = 0; col < 3; col++) {
                r[row * 3 + col] = a[row * 3] * b[col] + a[row * 3 + 1] * b[3 + col] + a[row * 3 + 2] * b[6 + col] + (row === 3 ? b[9 + col] : 0);
            }
        }
        return r;
    }

    // ---------- Measurements ----------

    function measure(tris, unit) {
        var n = tris.length / 9, vol = 0, area = 0;
        var min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
        for (var i = 0; i < n; i++) {
            var o = i * 9;
            var ax = tris[o], ay = tris[o + 1], az = tris[o + 2];
            var bx = tris[o + 3], by = tris[o + 4], bz = tris[o + 5];
            var cx = tris[o + 6], cy = tris[o + 7], cz = tris[o + 8];
            // Signed tetrahedron volume against the origin (divergence theorem).
            vol += (ax * (by * cz - bz * cy) - ay * (bx * cz - bz * cx) + az * (bx * cy - by * cx)) / 6;
            var ux = bx - ax, uy = by - ay, uz = bz - az, vx = cx - ax, vy = cy - ay, vz = cz - az;
            var nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            area += Math.sqrt(nx * nx + ny * ny + nz * nz) / 2;
            for (var k = 0; k < 9; k += 3) {
                for (var a = 0; a < 3; a++) {
                    var val = tris[o + k + a];
                    if (val < min[a]) min[a] = val;
                    if (val > max[a]) max[a] = val;
                }
            }
        }
        var u = unit || 1;
        return {
            tris: tris, count: n, unit: u,
            volume: Math.abs(vol) * u * u * u,         // mm³
            area: area * u * u,                        // mm²
            size: [0, 1, 2].map(function (a) { return n ? (max[a] - min[a]) * u : 0; }),  // mm
            center: [0, 1, 2].map(function (a) { return (max[a] + min[a]) / 2; })
        };
    }

    function read(file) {
        var ext = (file.name.split('.').pop() || '').toLowerCase();
        if (['stl', 'obj', '3mf'].indexOf(ext) < 0) {
            return Promise.reject(new Error('Tự cân hỗ trợ STL, OBJ, 3MF. File ' + ext.toUpperCase() + ' vẫn gửi báo giá được bình thường.'));
        }
        return file.arrayBuffer().then(function (buf) {
            return ext === 'stl' ? parseStl(buf) : ext === 'obj' ? parseObj(buf) : parse3mf(buf);
        }).then(function (r) {
            if (!r.tris.length) throw new Error('Không đọc được tam giác nào trong file.');
            return measure(r.tris, r.unit);
        });
    }

    // ---------- Preview: flat-shaded orthographic render, drag to rotate ----------

    var MAX_PREVIEW_TRIS = 60000;

    function View(canvas) {
        this.canvas = canvas;
        this.yaw = -0.6;
        this.pitch = 0.5;
        this.color = [128, 229, 203];
        this.zoom = 1;
        this.mesh = null;
        this.spinning = false;
        var self = this, drag = null;

        canvas.addEventListener('pointerdown', function (e) {
            self.stopSpin();
            drag = { x: e.clientX, y: e.clientY, yaw: self.yaw, pitch: self.pitch };
            canvas.setPointerCapture(e.pointerId);
        });
        canvas.addEventListener('wheel', function (e) {
            e.preventDefault();
            self.stopSpin();
            self.zoom = Math.max(0.4, Math.min(6, self.zoom * Math.exp(-e.deltaY * 0.0015)));
            self.request();
        }, { passive: false });
        if ('ResizeObserver' in window) new ResizeObserver(function () { self.request(); }).observe(canvas);
        canvas.addEventListener('pointermove', function (e) {
            if (!drag) return;
            self.yaw = drag.yaw + (e.clientX - drag.x) * 0.012;
            self.pitch = Math.max(-1.5, Math.min(1.5, drag.pitch + (e.clientY - drag.y) * 0.012));
            self.request();
        });
        ['pointerup', 'pointercancel'].forEach(function (t) { canvas.addEventListener(t, function () { drag = null; }); });
    }

    View.prototype.set = function (mesh) {
        // Preview a subset of large meshes; the measurements always use every triangle.
        var n = mesh.count, step = Math.max(1, Math.ceil(n / MAX_PREVIEW_TRIS)), m = Math.ceil(n / step);
        var src = mesh.tris, pts = new Float32Array(m * 9), c = mesh.center, r = 0;
        for (var i = 0, j = 0; i < n; i += step, j++) {
            for (var k = 0; k < 9; k += 3) {
                var x = src[i * 9 + k] - c[0], y = src[i * 9 + k + 1] - c[1], z = src[i * 9 + k + 2] - c[2];
                pts[j * 9 + k] = x; pts[j * 9 + k + 1] = y; pts[j * 9 + k + 2] = z;
                r = Math.max(r, x * x + y * y + z * z);
            }
        }
        this.mesh = { pts: pts, n: m, radius: Math.sqrt(r) || 1, order: new Uint32Array(m), depth: new Float32Array(m) };
        this.reset();
    };

    View.prototype.reset = function () {
        this.yaw = -0.6; this.pitch = 0.5; this.zoom = 1;
        this.request();
        this.startSpin();
    };

    // Slow turntable until the visitor grabs the model. Skipped for heavy meshes and reduced motion.
    View.prototype.startSpin = function () {
        var self = this, last = 0;
        if (this.spinning || !this.mesh || this.mesh.n > 25000) return;
        if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
        this.spinning = true;
        (function tick(now) {
            if (!self.spinning) return;
            if (last) { self.yaw += Math.min(now - last, 50) * 0.0004; self.draw(); }
            last = now;
            requestAnimationFrame(tick);
        })(0);
    };

    View.prototype.stopSpin = function () { this.spinning = false; };

    View.prototype.request = function () {
        var self = this;
        if (this.pending) return;
        this.pending = true;
        requestAnimationFrame(function () { self.pending = false; self.draw(); });
    };

    View.prototype.draw = function () {
        var cv = this.canvas, mesh = this.mesh;
        var dpr = Math.min(window.devicePixelRatio || 1, 2), w = cv.clientWidth, h = cv.clientHeight;
        if (!w || !h || !mesh) return;
        if (cv.width !== Math.round(w * dpr) || cv.height !== Math.round(h * dpr)) { cv.width = Math.round(w * dpr); cv.height = Math.round(h * dpr); }
        var ctx = cv.getContext('2d');
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, w, h);

        var cy = Math.cos(this.yaw), sy = Math.sin(this.yaw), cp = Math.cos(this.pitch), sp = Math.sin(this.pitch);
        var scale = Math.min(w, h) * 0.44 * this.zoom / mesh.radius, ox = w / 2, oy = h / 2;
        var pts = mesh.pts, n = mesh.n, P = new Float32Array(n * 9);

        // Z up (as in slicers): rotate around Z by yaw, tilt around X by pitch; view along +Y.
        for (var i = 0; i < n * 3; i++) {
            var x = pts[i * 3], y = pts[i * 3 + 1], z = pts[i * 3 + 2];
            var x1 = x * cy - y * sy, y1 = x * sy + y * cy;
            P[i * 3] = x1; P[i * 3 + 1] = y1 * cp - z * sp; P[i * 3 + 2] = y1 * sp + z * cp;
        }
        for (i = 0; i < n; i++) {
            mesh.order[i] = i;
            mesh.depth[i] = P[i * 9 + 1] + P[i * 9 + 4] + P[i * 9 + 7];
        }
        var depth = mesh.depth;
        var order = Array.prototype.slice.call(mesh.order).sort(function (a, b) { return depth[b] - depth[a]; });

        var col = this.color, lx = -0.45, ly = -0.55, lz = 0.7;
        for (var q = 0; q < n; q++) {
            var o = order[q] * 9;
            var ux = P[o + 3] - P[o], uy = P[o + 4] - P[o + 1], uz = P[o + 5] - P[o + 2];
            var vx = P[o + 6] - P[o], vy = P[o + 7] - P[o + 1], vz = P[o + 8] - P[o + 2];
            var nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            var len = Math.sqrt(nx * nx + ny * ny + nz * nz) || 1;
            var shade = 0.42 + 0.58 * Math.abs((nx * lx + ny * ly + nz * lz) / len);
            ctx.fillStyle = 'rgb(' + Math.round(col[0] * shade) + ',' + Math.round(col[1] * shade) + ',' + Math.round(col[2] * shade) + ')';
            ctx.strokeStyle = ctx.fillStyle;
            ctx.beginPath();
            ctx.moveTo(ox + P[o] * scale, oy - P[o + 2] * scale);
            ctx.lineTo(ox + P[o + 3] * scale, oy - P[o + 5] * scale);
            ctx.lineTo(ox + P[o + 6] * scale, oy - P[o + 8] * scale);
            ctx.closePath();
            ctx.fill();
            ctx.stroke(); // hides hairline gaps between triangles
        }
    };

    window.TTMesh = { read: read, View: View };
})();

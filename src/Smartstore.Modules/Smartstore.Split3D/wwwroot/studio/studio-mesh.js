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

    // ---------- Preview: WebGL (every triangle, depth buffer), Canvas 2D fallback; drag to rotate ----------

    var MAX_GL_TRIS = 4000000;     // above this, every n-th triangle is drawn (GPU memory)
    var MAX_2D_TRIS = 60000;       // Canvas 2D fallback only
    var LIGHT = (function () { var l = [-0.45, 0.7, -0.55], n = Math.hypot(l[0], l[1], l[2]); return [l[0] / n, l[1] / n, l[2] / n]; })();

    var VS = 'attribute vec3 aPos; attribute vec3 aNor;' +
        'uniform mat3 uRot; uniform vec3 uCenter; uniform vec3 uScale; uniform vec2 uPan; varying vec3 vN;' +
        'void main() { vec3 p = uRot * (aPos - uCenter); vN = uRot * aNor; gl_Position = vec4(p * uScale + vec3(uPan, 0.0), 1.0); }';

    // Two-sided flat lighting: key light plus a soft fill from below, so no face turns black.
    var FS = 'precision mediump float; uniform vec3 uColor; uniform vec3 uLight; varying vec3 vN;' +
        'void main() { vec3 n = normalize(vN); float key = abs(dot(n, uLight)); float fill = 0.5 + 0.5 * n.y * sign(dot(n, uLight) + 0.0001);' +
        ' float s = 0.34 + 0.56 * key + 0.1 * fill; gl_FragColor = vec4(uColor * s, 1.0); }';

    function createGL(canvas) {
        var gl = null;
        try { gl = canvas.getContext('webgl', { antialias: true, alpha: true, premultipliedAlpha: true }) || canvas.getContext('experimental-webgl'); } catch (e) { gl = null; }
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

        return {
            gl: gl,
            aPos: gl.getAttribLocation(prog, 'aPos'),
            aNor: gl.getAttribLocation(prog, 'aNor'),
            uRot: gl.getUniformLocation(prog, 'uRot'),
            uCenter: gl.getUniformLocation(prog, 'uCenter'),
            uScale: gl.getUniformLocation(prog, 'uScale'),
            uPan: gl.getUniformLocation(prog, 'uPan'),
            uColor: gl.getUniformLocation(prog, 'uColor'),
            uLight: gl.getUniformLocation(prog, 'uLight'),
            pos: gl.createBuffer(),
            nor: gl.createBuffer(),
            count: 0
        };
    }

    function View(canvas) {
        this.canvas = canvas;
        this.yaw = -0.6;
        this.pitch = 0.5;
        this.color = [128, 229, 203];
        this.zoom = 1;
        this.panX = 0;  // CSS px
        this.panY = 0;
        this.mesh = null;
        this.spinning = false;
        this.gl = createGL(canvas);
        var self = this, drag = null;

        // Axis gizmo (X red, Y green, Z blue) in the bottom-left corner, on its own canvas above the model.
        this.axes = document.createElement('canvas');
        this.axes.className = 'tt-axes';
        this.axes.setAttribute('aria-hidden', 'true');
        if (canvas.parentNode) canvas.parentNode.appendChild(this.axes);

        // One pointer: left button rotates, right / middle button or Shift pans. Two fingers: pinch to zoom and pan.
        var pointers = {};
        function pinch() {
            var ids = Object.keys(pointers);
            if (ids.length < 2) return null;
            var a = pointers[ids[0]], b = pointers[ids[1]];
            return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2, d: Math.hypot(a.x - b.x, a.y - b.y) || 1 };
        }
        function begin(e) {
            var p = pinch();
            drag = p
                ? { pinch: true, x: p.x, y: p.y, d: p.d, zoom: self.zoom, panX: self.panX, panY: self.panY }
                : { pan: e.button === 1 || e.button === 2 || e.shiftKey, x: e.clientX, y: e.clientY, yaw: self.yaw, pitch: self.pitch, panX: self.panX, panY: self.panY };
        }
        canvas.addEventListener('pointerdown', function (e) {
            self.stopSpin();
            pointers[e.pointerId] = { x: e.clientX, y: e.clientY };
            begin(e);
            canvas.setPointerCapture(e.pointerId);
        });
        canvas.addEventListener('contextmenu', function (e) { e.preventDefault(); });
        canvas.addEventListener('wheel', function (e) {
            e.preventDefault();
            self.stopSpin();
            self.zoomAt(self.zoom * Math.exp(-e.deltaY * 0.0015), e.clientX, e.clientY);
        }, { passive: false });
        if ('ResizeObserver' in window) new ResizeObserver(function () { self.request(); }).observe(canvas);
        canvas.addEventListener('pointermove', function (e) {
            if (!drag || !pointers[e.pointerId]) return;
            pointers[e.pointerId] = { x: e.clientX, y: e.clientY };
            if (drag.pinch) {
                var p = pinch();
                if (!p) return;
                self.zoom = Math.max(0.4, Math.min(20, drag.zoom * p.d / drag.d));
                self.panX = drag.panX + p.x - drag.x; self.panY = drag.panY + p.y - drag.y;
            } else if (drag.pan) {
                self.panX = drag.panX + e.clientX - drag.x; self.panY = drag.panY + e.clientY - drag.y;
            } else {
                self.yaw = drag.yaw + (e.clientX - drag.x) * 0.01;
                self.pitch = Math.max(-1.5, Math.min(1.5, drag.pitch + (e.clientY - drag.y) * 0.01));
            }
            self.request();
        });
        ['pointerup', 'pointercancel'].forEach(function (t) {
            canvas.addEventListener(t, function (e) {
                delete pointers[e.pointerId];
                // Lifting one finger of a pinch continues as a rotation from where the other finger is.
                if (Object.keys(pointers).length) { var id = Object.keys(pointers)[0]; begin({ clientX: pointers[id].x, clientY: pointers[id].y, button: 0 }); }
                else drag = null;
            });
        });
    }

    // Zooms keeping the model point under the cursor in place.
    View.prototype.zoomAt = function (zoom, clientX, clientY) {
        zoom = Math.max(0.4, Math.min(20, zoom));
        var r = this.canvas.getBoundingClientRect(), k = zoom / this.zoom;
        var cx = clientX - r.left - r.width / 2, cy = clientY - r.top - r.height / 2;
        this.panX = cx - (cx - this.panX) * k;
        this.panY = cy - (cy - this.panY) * k;
        this.zoom = zoom;
        this.request();
    };

    View.prototype.set = function (mesh) {
        var n = mesh.count, src = mesh.tris, c = mesh.center, r = 0;
        var step = Math.max(1, Math.ceil(n / (this.gl ? MAX_GL_TRIS : MAX_2D_TRIS))), m = Math.ceil(n / step);

        var pts = step === 1 && this.gl ? src : new Float32Array(m * 9);
        for (var i = 0, j = 0; i < n; i += step, j++) {
            for (var k = 0; k < 9; k += 3) {
                var x = src[i * 9 + k], y = src[i * 9 + k + 1], z = src[i * 9 + k + 2];
                if (pts !== src) { pts[j * 9 + k] = x; pts[j * 9 + k + 1] = y; pts[j * 9 + k + 2] = z; }
                x -= c[0]; y -= c[1]; z -= c[2];
                var d = x * x + y * y + z * z;
                if (d > r) r = d;
            }
        }
        this.mesh = { pts: pts, n: m, center: c, radius: Math.sqrt(r) || 1 };

        if (this.gl) {
            // Flat shading: every vertex of a triangle gets the face normal.
            var nor = new Float32Array(m * 9);
            for (i = 0; i < m; i++) {
                var o = i * 9;
                var ux = pts[o + 3] - pts[o], uy = pts[o + 4] - pts[o + 1], uz = pts[o + 5] - pts[o + 2];
                var vx = pts[o + 6] - pts[o], vy = pts[o + 7] - pts[o + 1], vz = pts[o + 8] - pts[o + 2];
                var nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                var len = Math.sqrt(nx * nx + ny * ny + nz * nz) || 1;
                nx /= len; ny /= len; nz /= len;
                for (k = 0; k < 9; k += 3) { nor[o + k] = nx; nor[o + k + 1] = ny; nor[o + k + 2] = nz; }
            }
            var g = this.gl, gl = g.gl;
            gl.bindBuffer(gl.ARRAY_BUFFER, g.pos); gl.bufferData(gl.ARRAY_BUFFER, pts, gl.STATIC_DRAW);
            gl.bindBuffer(gl.ARRAY_BUFFER, g.nor); gl.bufferData(gl.ARRAY_BUFFER, nor, gl.STATIC_DRAW);
            g.count = m * 3;
        } else {
            this.mesh.order = new Uint32Array(m);
            this.mesh.depth = new Float32Array(m);
        }
        this.reset();
    };

    View.prototype.reset = function () {
        this.yaw = -0.6; this.pitch = 0.5; this.zoom = 1; this.panX = this.panY = 0;
        this.request();
        this.startSpin();
    };

    // Slow turntable until the visitor grabs the model. Canvas 2D fallback spins only small meshes.
    View.prototype.startSpin = function () {
        var self = this, last = 0;
        if (this.spinning || !this.mesh || (!this.gl && this.mesh.n > 20000)) return;
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
        if (this.gl) this.drawGL(w, h); else this.draw2D(w, h, dpr);
        this.drawAxes(dpr);
    };

    var AXES = [['X', [1, 0, 0], '#e5484d'], ['Y', [0, 1, 0], '#2b9a4a'], ['Z', [0, 0, 1], '#2f6fe0']];

    View.prototype.drawAxes = function (dpr) {
        var cv = this.axes, size = 92, R = this.rotation(), c = size / 2, len = 30;
        if (cv.width !== size * dpr) { cv.width = cv.height = size * dpr; }
        var ctx = cv.getContext('2d');
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, size, size);

        // Far axes first, so the axis pointing at the viewer is drawn on top.
        var axes = AXES.map(function (a) {
            var v = a[1];
            return {
                name: a[0], color: a[2],
                x: R[0][0] * v[0] + R[0][1] * v[1] + R[0][2] * v[2],
                y: R[1][0] * v[0] + R[1][1] * v[1] + R[1][2] * v[2],
                depth: R[2][0] * v[0] + R[2][1] * v[1] + R[2][2] * v[2]
            };
        }).sort(function (a, b) { return b.depth - a.depth; });

        ctx.lineCap = 'round';
        ctx.font = '900 12px "Be Vietnam Pro", Arial, sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        axes.forEach(function (a) {
            var ex = c + a.x * len, ey = c - a.y * len;
            ctx.globalAlpha = a.depth > 0.35 ? 0.45 : 1; // axes pointing away fade a little
            ctx.strokeStyle = '#20201f'; ctx.lineWidth = 6;
            ctx.beginPath(); ctx.moveTo(c, c); ctx.lineTo(ex, ey); ctx.stroke();
            ctx.strokeStyle = a.color; ctx.lineWidth = 3;
            ctx.beginPath(); ctx.moveTo(c, c); ctx.lineTo(ex, ey); ctx.stroke();
            var lx = c + a.x * (len + 10), ly = c - a.y * (len + 10);
            ctx.fillStyle = a.color;
            ctx.beginPath(); ctx.arc(lx, ly, 8, 0, Math.PI * 2); ctx.fill();
            ctx.strokeStyle = '#20201f'; ctx.lineWidth = 1.5; ctx.stroke();
            ctx.fillStyle = '#fff';
            ctx.fillText(a.name, lx, ly + 0.5);
        });
        ctx.globalAlpha = 1;
        ctx.fillStyle = '#20201f';
        ctx.beginPath(); ctx.arc(c, c, 3, 0, Math.PI * 2); ctx.fill();
    };

    // Z up (as in slicers): rotate around Z by yaw, tilt around X by pitch, look along +Y.
    // Rows map a model vector to (screen x, screen up, depth away from the viewer).
    View.prototype.rotation = function () {
        var cy = Math.cos(this.yaw), sy = Math.sin(this.yaw), cp = Math.cos(this.pitch), sp = Math.sin(this.pitch);
        return [[cy, -sy, 0], [sp * sy, sp * cy, cp], [cp * sy, cp * cy, -sp]];
    };

    View.prototype.drawGL = function (w, h) {
        var g = this.gl, gl = g.gl, R = this.rotation(), c = this.mesh.center, col = this.color;
        var s = 0.88 * this.zoom / this.mesh.radius;

        gl.viewport(0, 0, this.canvas.width, this.canvas.height);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
        gl.enable(gl.DEPTH_TEST);

        // GLSL matrices are column-major.
        gl.uniformMatrix3fv(g.uRot, false, [R[0][0], R[1][0], R[2][0], R[0][1], R[1][1], R[2][1], R[0][2], R[1][2], R[2][2]]);
        gl.uniform3f(g.uCenter, c[0], c[1], c[2]);
        gl.uniform3f(g.uScale, s * Math.min(1, h / w), s * Math.min(1, w / h), 0.95 / this.mesh.radius);
        gl.uniform2f(g.uPan, 2 * this.panX / w, -2 * this.panY / h);
        gl.uniform3f(g.uColor, col[0] / 255, col[1] / 255, col[2] / 255);
        gl.uniform3f(g.uLight, LIGHT[0], LIGHT[1], LIGHT[2]);

        gl.bindBuffer(gl.ARRAY_BUFFER, g.pos);
        gl.enableVertexAttribArray(g.aPos);
        gl.vertexAttribPointer(g.aPos, 3, gl.FLOAT, false, 0, 0);
        gl.bindBuffer(gl.ARRAY_BUFFER, g.nor);
        gl.enableVertexAttribArray(g.aNor);
        gl.vertexAttribPointer(g.aNor, 3, gl.FLOAT, false, 0, 0);
        gl.drawArrays(gl.TRIANGLES, 0, g.count);
    };

    // Fallback without WebGL: painter's algorithm on a subset of the triangles.
    View.prototype.draw2D = function (w, h, dpr) {
        var mesh = this.mesh, ctx = this.canvas.getContext('2d'), R = this.rotation(), c = mesh.center;
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, w, h);

        var scale = Math.min(w, h) * 0.44 * this.zoom / mesh.radius, ox = w / 2 + this.panX, oy = h / 2 + this.panY;
        var pts = mesh.pts, n = mesh.n, P = new Float32Array(n * 9);
        for (var i = 0; i < n * 3; i++) {
            var x = pts[i * 3] - c[0], y = pts[i * 3 + 1] - c[1], z = pts[i * 3 + 2] - c[2];
            P[i * 3] = R[0][0] * x + R[0][1] * y;
            P[i * 3 + 1] = R[1][0] * x + R[1][1] * y + R[1][2] * z;
            P[i * 3 + 2] = R[2][0] * x + R[2][1] * y + R[2][2] * z;
        }
        for (i = 0; i < n; i++) {
            mesh.order[i] = i;
            mesh.depth[i] = P[i * 9 + 2] + P[i * 9 + 5] + P[i * 9 + 8];
        }
        var depth = mesh.depth;
        var order = Array.prototype.slice.call(mesh.order).sort(function (a, b) { return depth[b] - depth[a]; });

        var col = this.color;
        for (var q = 0; q < n; q++) {
            var o = order[q] * 9;
            var ux = P[o + 3] - P[o], uy = P[o + 4] - P[o + 1], uz = P[o + 5] - P[o + 2];
            var vx = P[o + 6] - P[o], vy = P[o + 7] - P[o + 1], vz = P[o + 8] - P[o + 2];
            var nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            var len = Math.sqrt(nx * nx + ny * ny + nz * nz) || 1;
            var shade = 0.42 + 0.58 * Math.abs((nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]) / len);
            ctx.fillStyle = 'rgb(' + Math.round(col[0] * shade) + ',' + Math.round(col[1] * shade) + ',' + Math.round(col[2] * shade) + ')';
            ctx.strokeStyle = ctx.fillStyle;
            ctx.beginPath();
            ctx.moveTo(ox + P[o] * scale, oy - P[o + 1] * scale);
            ctx.lineTo(ox + P[o + 3] * scale, oy - P[o + 4] * scale);
            ctx.lineTo(ox + P[o + 6] * scale, oy - P[o + 7] * scale);
            ctx.closePath();
            ctx.fill();
            ctx.stroke(); // hides hairline gaps between triangles
        }
    };

    window.TTMesh = { read: read, View: View };
})();

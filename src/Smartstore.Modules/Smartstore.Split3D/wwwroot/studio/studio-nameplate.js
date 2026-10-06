/* TT Minimal name plate preview: raised text on a rounded plate, drawn with WebGL. The text is rasterized with the
   page's own font (Vietnamese diacritics included) and turned into 3D: marching squares on the glyph mask give the
   side walls, the mask itself (alpha test) is the top face, so no triangulation and no font files are needed.
   No dependencies; loaded on demand by studio.js on the name plate product page. */
(function () {
    'use strict';

    var FONT_FAMILY = '"Be Vietnam Pro", Arial, sans-serif';
    var PLATE_T = 3;       // plate thickness, mm
    var RELIEF = 1.6;      // raised text height, mm
    var BEVEL = 0.7;       // chamfer on the plate's top edge, mm
    var RATIO = 0.3;       // plate height / length
    var MAX_PX = 1400;     // raster width of the text (contour detail)
    var FOV = 30 * Math.PI / 180;
    var YAW = -0.32, PITCH = 0.95;

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

    // ---------- Geometry: interleaved buffers of position (3) + normal (3) ----------

    function Buf() { this.a = []; }
    Buf.prototype.v = function (x, y, z, nx, ny, nz) { this.a.push(x, y, z, nx, ny, nz); };
    Buf.prototype.quad = function (p, q, r, s) { this.v.apply(this, p); this.v.apply(this, q); this.v.apply(this, r); this.v.apply(this, p); this.v.apply(this, r); this.v.apply(this, s); };

    // Rounded rectangle outline, counter-clockwise, with outward normals; inset keeps the corner centers.
    function outline(L, H, r, inset) {
        var hw = L / 2 - r, hh = H / 2 - r, rr = r - inset, seg = 10, pts = [];
        [[hw, hh, 0], [-hw, hh, 0.5], [-hw, -hh, 1], [hw, -hh, 1.5]].forEach(function (c) {
            for (var k = 0; k <= seg; k++) {
                var a = (c[2] + k / seg * 0.5) * Math.PI, nx = Math.cos(a), ny = Math.sin(a);
                pts.push([c[0] + nx * rr, c[1] + ny * rr, nx, ny]);
            }
        });
        return pts;
    }

    function plate(L, H) {
        var r = Math.min(H * 0.22, 6), outer = outline(L, H, r, 0), inner = outline(L, H, r, BEVEL), b = new Buf();
        var zb = PLATE_T - BEVEL, k = Math.SQRT1_2;
        for (var i = 0; i < outer.length; i++) {
            var j = (i + 1) % outer.length, o1 = outer[i], o2 = outer[j], i1 = inner[i], i2 = inner[j];
            b.quad([o1[0], o1[1], 0, o1[2], o1[3], 0], [o2[0], o2[1], 0, o2[2], o2[3], 0], [o2[0], o2[1], zb, o2[2], o2[3], 0], [o1[0], o1[1], zb, o1[2], o1[3], 0]);
            b.quad([o1[0], o1[1], zb, o1[2] * k, o1[3] * k, k], [o2[0], o2[1], zb, o2[2] * k, o2[3] * k, k], [i2[0], i2[1], PLATE_T, o2[2] * k, o2[3] * k, k], [i1[0], i1[1], PLATE_T, o1[2] * k, o1[3] * k, k]);
            b.v(0, 0, PLATE_T, 0, 0, 1); b.v(i1[0], i1[1], PLATE_T, 0, 0, 1); b.v(i2[0], i2[1], PLATE_T, 0, 0, 1);
            b.v(0, 0, 0, 0, 0, -1); b.v(o2[0], o2[1], 0, 0, 0, -1); b.v(o1[0], o1[1], 0, 0, 0, -1);
        }
        return new Float32Array(b.a);
    }

    // The text as a coverage mask, scaled to fit the plate with a margin; R = mask pixels per mm.
    function rasterize(text, L, H) {
        var margin = Math.max(4, H * 0.17), aw = L - 2 * margin, ah = H - 2 * margin, ref = 100;
        var c = document.createElement('canvas'), ctx = c.getContext('2d', { willReadFrequently: true });
        ctx.font = '900 ' + ref + 'px ' + FONT_FAMILY;
        var m = ctx.measureText(text);
        var left = m.actualBoundingBoxLeft || 0, right = m.actualBoundingBoxRight || m.width;
        var asc = m.actualBoundingBoxAscent || ref * 0.75, desc = m.actualBoundingBoxDescent || ref * 0.25;
        var bw = left + right, bh = asc + desc;
        if (!(bw > 0.5 && bh > 0.5)) return null;

        var mm = Math.min(aw / bw, ah / bh), R = Math.min(14, MAX_PX / (bw * mm)), k = mm * R, pad = 3;
        c.width = Math.ceil(bw * k) + 2 * pad;
        c.height = Math.ceil(bh * k) + 2 * pad;
        ctx.font = '900 ' + (ref * k) + 'px ' + FONT_FAMILY;
        ctx.textBaseline = 'alphabetic';
        ctx.textAlign = 'left';
        ctx.fillStyle = '#fff';
        ctx.fillText(text, pad + left * k, pad + asc * k);

        var data = ctx.getImageData(0, 0, c.width, c.height).data, f = new Float32Array(c.width * c.height);
        for (var i = 0; i < f.length; i++) f[i] = data[i * 4 + 3] / 255;
        return { canvas: c, f: f, w: c.width, h: c.height, R: R, width: bw * mm, height: bh * mm };
    }

    // Marching squares (iso 0.5) over the mask: every contour segment becomes a wall quad. Cell edges are shared
    // by neighbouring cells, so normals summed per edge give smooth shading along the curves.
    // Pairs of cell edges (0 top, 1 right, 2 bottom, 3 left) per corner case; 5 and 10 are decided by the center.
    var CASES = [null, [3, 0], [0, 1], [3, 1], [1, 2], null, [0, 2], [3, 2], [2, 3], [0, 2], null, [1, 2], [1, 3], [0, 1], [3, 0], null];

    function walls(r) {
        var f = r.f, w = r.w, h = r.h, t = 0.5, segs = [], acc = new Map();
        var px = new Float64Array(4), py = new Float64Array(4), id = new Float64Array(4);

        function add(key, nx, ny) {
            var n = acc.get(key);
            if (n) { n[0] += nx; n[1] += ny; } else acc.set(key, [nx, ny]);
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

        // Mask pixel (x + 0.5, y + 0.5) is the sample, the mask is centered on the plate; y goes up in mm.
        var out = new Float32Array(segs.length / 6 * 36), z0 = PLATE_T - 0.05, z1 = PLATE_T + RELIEF, n = 0;
        function vert(x, y, key, z) {
            var nn = acc.get(key), l = Math.hypot(nn[0], nn[1]) || 1;
            out[n++] = (x + 0.5 - w / 2) / r.R; out[n++] = (h / 2 - y - 0.5) / r.R; out[n++] = z;
            out[n++] = nn[0] / l; out[n++] = -nn[1] / l; out[n++] = 0;
        }
        for (var s = 0; s < segs.length; s += 6) {
            vert(segs[s], segs[s + 1], segs[s + 2], z0); vert(segs[s + 3], segs[s + 4], segs[s + 5], z0); vert(segs[s + 3], segs[s + 4], segs[s + 5], z1);
            vert(segs[s], segs[s + 1], segs[s + 2], z0); vert(segs[s + 3], segs[s + 4], segs[s + 5], z1); vert(segs[s], segs[s + 1], segs[s + 2], z1);
        }
        return out;
    }

    // ---------- WebGL ----------

    var VS = 'attribute vec3 aPos; attribute vec3 aNor; uniform mat4 uMVP; uniform vec4 uTexRect;' +
        'varying vec3 vN; varying vec3 vP; varying vec2 vUV;' +
        'void main() { vN = aNor; vP = aPos; vUV = (aPos.xy - uTexRect.xy) / uTexRect.zw; gl_Position = uMVP * vec4(aPos, 1.0); }';

    // Key light from the upper left of the viewer, sky fill from above, a small highlight; the text top face
    // discards everything outside the glyph mask.
    var FS = 'precision mediump float; uniform vec3 uColor; uniform vec3 uLight; uniform vec3 uEye; uniform sampler2D uTex; uniform float uUseTex;' +
        'varying vec3 vN; varying vec3 vP; varying vec2 vUV;' +
        'void main() { if (uUseTex > 0.5 && texture2D(uTex, vec2(vUV.x, 1.0 - vUV.y)).a < 0.5) discard;' +
        ' vec3 n = normalize(vN); vec3 v = normalize(uEye - vP); float diff = max(dot(n, uLight), 0.0);' +
        ' float spec = pow(max(dot(n, normalize(uLight + v)), 0.0), 36.0); float sky = 0.5 + 0.5 * n.z;' +
        ' gl_FragColor = vec4(uColor * (0.3 + 0.55 * diff + 0.2 * sky) + vec3(0.09 * spec), 1.0); }';

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

        var g = { gl: gl, plate: gl.createBuffer(), walls: gl.createBuffer(), top: gl.createBuffer(), tex: gl.createTexture(), counts: {} };
        ['aPos', 'aNor'].forEach(function (n) { g[n] = gl.getAttribLocation(prog, n); });
        ['uMVP', 'uTexRect', 'uColor', 'uLight', 'uEye', 'uTex', 'uUseTex'].forEach(function (n) { g[n] = gl.getUniformLocation(prog, n); });
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
        gl.bufferData(gl.ARRAY_BUFFER, data, gl.STATIC_DRAW);
        g.counts[name] = data.length / 6;
    }

    // ---------- Math ----------

    function sub(a, b) { return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]; }
    function dot(a, b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
    function cross(a, b) { return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]; }
    function unit(a) { var l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; }

    // ---------- View: drag to rotate, Ctrl + wheel (or any wheel while opts.wheel() is true) to zoom ----------

    function View(canvas, opts) {
        this.canvas = canvas;
        this.opts = opts || {};
        this.gl = createGL(canvas);
        this.spec = { text: '', base: '#ffffff', color: '#20201f', length: 100 };
        this.dims = null;
        this.mask = null;
        this.zoom = 1;
        this.seq = 0;
        this.reset(true);

        var self = this, drag = null;
        canvas.addEventListener('pointerdown', function (e) {
            if (e.button !== 0) return;
            self.stopSway();
            drag = { x: e.clientX, y: e.clientY, yaw: self.yaw, pitch: self.pitch };
            canvas.setPointerCapture(e.pointerId);
        });
        canvas.addEventListener('pointermove', function (e) {
            if (!drag) return;
            self.yaw = drag.yaw - (e.clientX - drag.x) * 0.01;
            self.pitch = Math.max(-0.15, Math.min(1.52, drag.pitch + (e.clientY - drag.y) * 0.01));
            self.request();
        });
        ['pointerup', 'pointercancel'].forEach(function (t) { canvas.addEventListener(t, function () { drag = null; }); });
        canvas.addEventListener('dblclick', function () { self.reset(); });
        canvas.addEventListener('wheel', function (e) {
            if (!(e.ctrlKey || e.metaKey || (self.opts.wheel && self.opts.wheel()))) return;
            e.preventDefault();
            self.stopSway();
            self.zoom = Math.max(0.6, Math.min(6, self.zoom * Math.exp(-e.deltaY * 0.0015)));
            self.request();
        }, { passive: false });
        if ('ResizeObserver' in window) new ResizeObserver(function () { self.request(); }).observe(canvas);
    }

    View.prototype.reset = function (quiet) {
        this.yaw = YAW; this.pitch = PITCH; this.zoom = 1;
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
                self.yaw = YAW + 0.38 * Math.sin((now - t0) * 0.0007);
                self.draw();
            }
            requestAnimationFrame(tick);
        })(0);
    };

    View.prototype.stopSway = function () { this.swaying = false; };

    /**
     * Shows a plate: { text, base (CSS color of the plate), color (CSS color of the text), length (mm) }.
     * Colors apply at once; text and length rebuild the mesh after the font is ready.
     */
    View.prototype.set = function (spec) {
        var old = this.spec, self = this;
        this.spec = {
            text: String(spec.text == null ? old.text : spec.text).replace(/\s+/g, ' ').trim(),
            base: spec.base || old.base,
            color: spec.color || old.color,
            length: +spec.length > 0 ? +spec.length : old.length
        };
        if (this.dims && this.spec.text === old.text && this.spec.length === old.length) { this.request(); return Promise.resolve(this.dims); }

        var seq = ++this.seq, text = this.spec.text, font = '900 40px ' + FONT_FAMILY;
        var ready = document.fonts && document.fonts.load ? document.fonts.load(font, text || 'A').catch(function () { }) : Promise.resolve();
        return ready.then(function () {
            if (seq !== self.seq) return self.dims;
            self.build();
            self.request();
            return self.dims;
        });
    };

    View.prototype.build = function () {
        var L = this.spec.length, H = Math.round(L * RATIO), mask = this.spec.text ? rasterize(this.spec.text, L, H) : null;
        this.mask = mask;
        this.dims = { length: L, height: H, depth: +(PLATE_T + (mask ? RELIEF : 0)).toFixed(1), textWidth: mask ? mask.width : 0, textHeight: mask ? mask.height : 0 };
        this.radius = Math.hypot(L / 2, H / 2);
        var g = this.gl;
        if (!g) return;
        var gl = g.gl;
        upload(g, 'plate', plate(L, H));
        if (mask) {
            upload(g, 'walls', walls(mask));
            var x = mask.w / 2 / mask.R, y = mask.h / 2 / mask.R;
            upload(g, 'top', new Float32Array([-x, -y, PLATE_T + RELIEF, 0, 0, 1, x, -y, PLATE_T + RELIEF, 0, 0, 1, x, y, PLATE_T + RELIEF, 0, 0, 1,
                -x, -y, PLATE_T + RELIEF, 0, 0, 1, x, y, PLATE_T + RELIEF, 0, 0, 1, -x, y, PLATE_T + RELIEF, 0, 0, 1]));
            this.texRect = [-x, -y, 2 * x, 2 * y];
            gl.bindTexture(gl.TEXTURE_2D, g.tex);
            gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, mask.canvas);
        } else {
            g.counts.walls = g.counts.top = 0;
        }
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
        var g = this.gl, gl = g.gl, self = this;
        // Distance that fits the plate across, and its tilted, swaying outline from top to bottom.
        var tanH = Math.tan(FOV / 2), d = this.radius * Math.max(0.9 / aspect, 0.85) / tanH / this.zoom;
        var cp = Math.cos(this.pitch), sp = Math.sin(this.pitch), cy = Math.cos(this.yaw), sy = Math.sin(this.yaw);
        var target = [0, 0, PLATE_T / 2], eye = [d * cp * sy, -d * cp * cy, PLATE_T / 2 + d * sp];
        var f = unit(sub(target, eye)), s = unit(cross(f, [0, 0, 1])), u = cross(s, f);
        var near = Math.max(1, d - this.radius * 1.5), far = d + this.radius * 1.5, ff = 1 / tanH;
        // Projection * view, column-major.
        var A = (far + near) / (near - far), B = 2 * far * near / (near - far);
        var se = -dot(s, eye), ue = -dot(u, eye), fe = dot(f, eye);
        var mvp = [
            ff / aspect * s[0], ff * u[0], -A * f[0], f[0],
            ff / aspect * s[1], ff * u[1], -A * f[1], f[1],
            ff / aspect * s[2], ff * u[2], -A * f[2], f[2],
            ff / aspect * se, ff * ue, A * fe + B, -fe
        ];
        var light = unit([-0.35 * s[0] + 0.75 * u[0] - 0.55 * f[0], -0.35 * s[1] + 0.75 * u[1] - 0.55 * f[1], -0.35 * s[2] + 0.75 * u[2] - 0.55 * f[2]]);

        gl.viewport(0, 0, this.canvas.width, this.canvas.height);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
        gl.enable(gl.DEPTH_TEST);
        gl.uniformMatrix4fv(g.uMVP, false, mvp);
        gl.uniform3fv(g.uLight, light);
        gl.uniform3fv(g.uEye, eye);
        gl.uniform4fv(g.uTexRect, this.texRect || [0, 0, 1, 1]);

        function part(name, color, tex) {
            if (!g.counts[name]) return;
            var c = rgb(color);
            gl.uniform3f(g.uColor, c[0], c[1], c[2]);
            gl.uniform1f(g.uUseTex, tex ? 1 : 0);
            gl.bindBuffer(gl.ARRAY_BUFFER, g[name]);
            gl.enableVertexAttribArray(g.aPos);
            gl.vertexAttribPointer(g.aPos, 3, gl.FLOAT, false, 24, 0);
            gl.enableVertexAttribArray(g.aNor);
            gl.vertexAttribPointer(g.aNor, 3, gl.FLOAT, false, 24, 12);
            gl.drawArrays(gl.TRIANGLES, 0, g.counts[name]);
        }
        part('plate', self.spec.base);
        part('walls', self.spec.color);
        part('top', self.spec.color, true);
    };

    // Fallback without WebGL: the plate seen from the front.
    View.prototype.draw2D = function (w, h, dpr) {
        var ctx = this.canvas.getContext('2d'), D = this.dims, k = Math.min(w * 0.86 / D.length, h * 0.7 / D.height);
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, w, h);
        var pw = D.length * k, ph = D.height * k, x = (w - pw) / 2, y = (h - ph) / 2, r = Math.min(D.height * 0.22, 6) * k;
        ctx.beginPath();
        if (ctx.roundRect) ctx.roundRect(x, y, pw, ph, r); else ctx.rect(x, y, pw, ph);
        ctx.fillStyle = this.spec.base; ctx.fill();
        ctx.lineWidth = 2; ctx.strokeStyle = '#20201f'; ctx.stroke();
        if (this.mask) {
            var m = this.mask, s = k / m.R;
            var tint = document.createElement('canvas');
            tint.width = m.w; tint.height = m.h;
            var tc = tint.getContext('2d');
            tc.drawImage(m.canvas, 0, 0);
            tc.globalCompositeOperation = 'source-in';
            tc.fillStyle = this.spec.color; tc.fillRect(0, 0, m.w, m.h);
            ctx.drawImage(tint, w / 2 - m.w * s / 2, h / 2 - m.h * s / 2, m.w * s, m.h * s);
        }
    };

    window.TTNameplate = { View: View };
})();

/* TT Minimal design panels: the options of each customizable product kind (name plate, class board, keycap, QR plate), its
   presets, the readable summary that travels with the order and the spec drawn by studio-nameplate.js.
   Panel renders the tabs and applies the choices; studio.js supplies the page context (colors, length, radios).
   No dependencies; loaded on demand by studio.js next to the renderer. */
(function () {
    'use strict';

    function esc(s) { return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;'); }
    function norm(s) { return String(s || '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/\s+/g, ' ').trim(); }
    function mm(v) { return String(Math.round(v * 10) / 10).replace('.', ',') + ' mm'; }
    function pct(v) { return Math.round(v * 100) + '%'; }
    function nameOf(list, key) { var x = list.filter(function (i) { return String(i.key || i[0]) === String(key); })[0]; return x ? (x.name || x[1]) : key; }
    function omit(o, keys) { var r = Object.assign({}, o); keys.forEach(function (k) { delete r[k]; }); return r; }

    // ---------- Building blocks; the data-* attributes are read by Panel ----------

    var ui = {
        seg: function (key, items, value, num) {
            return '<div class="tt-np-seg" role="group">' + items.map(function (i) {
                return '<button type="button" data-set="' + key + '" data-val="' + esc(i[0]) + '"' + (num ? ' data-num' : '') + ' aria-pressed="' + (String(value) === String(i[0])) + '">' + i[1] + '</button>';
            }).join('') + '</div>';
        },
        // redraw: the panel is drawn again when the slider is released (it changes the panel itself).
        slider: function (key, label, min, max, step, value, fmt, redraw) {
            return '<label class="tt-np-range"><span>' + label + ' <b data-out="' + key + '">' + fmt(value) + '</b></span>'
                + '<input type="range" class="skip-pd-ajax-update" data-range="' + key + '"' + (redraw ? ' data-redraw' : '') + ' min="' + min + '" max="' + max + '" step="' + step + '" value="' + value + '" /></label>';
        },
        check: function (key, label, on, disabled) {
            return '<label class="tt-np-check"><input type="checkbox" class="skip-pd-ajax-update" data-check="' + key + '"' + (on ? ' checked' : '') + (disabled ? ' disabled' : '') + ' /> ' + label + '</label>';
        },
        text: function (key, label, value, placeholder, max, hint) {
            return '<label class="tt-np-field"><span>' + label + (hint ? ' <small>' + hint + '</small>' : '') + '</span>'
                + '<input type="text" class="tt-input skip-pd-ajax-update" maxlength="' + (max || 60) + '" data-text="' + key + '" value="' + esc(value) + '" placeholder="' + esc(placeholder || '') + '" /></label>';
        },
        area: function (key, label, value, placeholder, rows, hint) {
            return '<label class="tt-np-field"><span>' + label + (hint ? ' <small>' + hint + '</small>' : '') + '</span>'
                + '<textarea class="tt-input skip-pd-ajax-update" rows="' + (rows || 6) + '" data-text="' + key + '" placeholder="' + esc(placeholder || '') + '">' + esc(value) + '</textarea></label>';
        },
        label: function (t) { return '<div class="tt-np-label">' + t + '</div>'; },
        // Numbered step of the quick design ("1 Chọn loại").
        step: function (n, t) { return '<div class="tt-np-step"><i>' + n + '</i>' + t + '</div>'; },
        // Slot the page fills with the text field (see ctx.mountText).
        textSlot: function (hint) { return '<div class="tt-np-textslot" data-np-text' + (hint ? ' data-placeholder="' + esc(hint) + '"' : '') + '></div>'; },
        note: function (t) { return '<p class="tt-np-note">' + t + '</p>'; },
        grid: function (html) { return '<div class="tt-np-grid">' + html + '</div>'; },
        fonts: function (P, d, kind) {
            return '<div class="tt-np-fonts">' + P.FONTS.filter(function (f) { return !f.kinds || f.kinds.indexOf(kind) >= 0; }).map(function (f) {
                return '<button type="button" class="tt-np-font" data-set="font" data-val="' + f.key + '" aria-pressed="' + (d.font === f.key) + '" title="' + esc(f.name) + '" style="font-family:\'' + f.family + '\';font-weight:' + f.weight + '">' + esc(f.name) + '</button>';
            }).join('') + '</div>';
        },
        icons: function (P, d) {
            return '<div class="tt-np-icons"><button type="button" class="tt-np-icon" data-set="icon" data-val="" aria-pressed="' + !d.icon + '" title="Không">∅</button>'
                + P.ICONS.map(function (i) { return '<button type="button" class="tt-np-icon" data-set="icon" data-val="' + i.key + '" aria-pressed="' + (d.icon === i.key) + '" title="' + i.name + '"><canvas data-icon="' + i.key + '" width="44" height="44"></canvas></button>'; }).join('')
                + '</div>';
        },
        types: function (types, d) {
            return '<div class="tt-np-types">' + types.map(function (t) {
                return '<button type="button" class="tt-np-type" data-type="' + t.key + '" aria-pressed="' + (d.type === t.key) + '"><b>' + t.name + '</b><small>' + t.hint + '</small></button>';
            }).join('') + '</div>';
        }
    };

    var STYLES = [['raised', 'Chữ nổi'], ['engraved', 'Chữ chìm'], ['flush', 'Phẳng (in màu)']];
    var HOLES = [['none', 'Không'], ['left', 'Bên trái'], ['top1', '1 lỗ trên'], ['top2', '2 lỗ trên']];
    var STANDS = [['false', 'Nằm / treo'], ['true', 'Đứng có chân đế']];

    // Fields of a plate spec (name plate and class board) taken from the design.
    function plateFields(d) {
        return omit(d, ['type', 'mode', 'days', 'am', 'pm', 'cells', 'rows', 'groups', 'seats', 'teacher', 'names', 'line', 'tileMode', 'spare', 'heading', 'colors', 'locked', 'showContent',
            'qType', 'qText', 'ssid', 'password', 'security', 'hidden', 'bank', 'account', 'amount', 'ecc', 'qrStyle', 'quiet', 'qrScale', 'caption', 'matrix', 'qrError']);
    }

    function plateSummary(d, P, L) {
        return 'Đế: ' + nameOf(P.SHAPES.concat([{ key: 'scallop', name: 'Viền gợn sóng' }]), d.shape) + ', cao ' + mm(L * d.heightPct / 100) + ', dày ' + mm(d.thickness) + ', lề ' + mm(d.margin)
            + (d.shape === 'rounded' || d.shape === 'tag' ? ', bo góc ' + mm(d.radius) : '');
    }

    // ---------- Name plate ----------

    var nameplate = {
        item: 'bảng tên',
        empty: 'Nhập tên muốn in để xem trước 3D',
        // Quick design: type, font and text are all most customers need; the tabs below hold the advanced options.
        quick: function (d, P) {
            return ui.step(1, 'Chọn loại') + ui.types(this.types, d)
                + ui.step(2, 'Chọn phông chữ') + ui.fonts(P, d, 'nameplate')
                + ui.step(3, 'Nhập tên muốn in') + ui.textSlot();
        },
        advanced: 'Kiểu chữ nổi / chìm, dòng chữ thứ 2, hình dạng đế, lỗ móc, biểu tượng, chân đứng',
        tabs: ['Chữ', 'Đế', 'Thêm'],
        types: [
            { key: 'desk', name: 'Bảng tên để bàn', hint: 'Có chân đứng', set: { shape: 'rounded', stand: true, hole: 'none', border: false, heightPct: 30, thickness: 4 } },
            { key: 'keychain', name: 'Móc khoá', hint: 'Đế ôm chữ, lỗ móc', set: { shape: 'outline', stand: false, hole: 'left', border: false, heightPct: 32, margin: 3, thickness: 3 } },
            { key: 'door', name: 'Bảng treo cửa', hint: 'Viền nổi, 2 lỗ treo', set: { shape: 'rounded', stand: false, hole: 'top2', border: true, heightPct: 40, thickness: 3 } },
            { key: 'badge', name: 'Tag tên cài áo', hint: 'Mỏng, bo tròn', set: { shape: 'pill', stand: false, hole: 'none', border: false, heightPct: 28, thickness: 2.5 } },
            { key: 'luggage', name: 'Thẻ treo hành lý', hint: 'Góc vát, lỗ dây', set: { shape: 'tag', stand: false, hole: 'left', border: true, heightPct: 45, thickness: 3 } }
        ],
        fresh: function (P) {
            return Object.assign(omit(P.DEFAULTS, ['text', 'base', 'color', 'length', 'board']), { kind: 'nameplate', type: 'desk' }, this.types[0].set);
        },
        formats: { heightPct: function (v, ctx) { return mm(ctx.plateLength() * v / 100); } },
        onSet: function (key, val, patch) { if (key === 'shape' && val === 'outline') patch.border = false; },
        pane: function (i, d, P, ctx) {
            var f = this.formats;
            if (i === 0) {
                return ui.text('line2', 'Dòng chữ thứ 2', d.line2, 'VD: Trưởng phòng kinh doanh', 40, '(chức vụ, số điện thoại… không bắt buộc)')
                    + ui.label('Kiểu chữ') + ui.seg('style', STYLES, d.style)
                    + ui.grid(ui.slider('relief', d.style === 'engraved' ? 'Độ sâu' : 'Độ nổi', 0.6, 3, 0.2, d.relief, mm)
                        + ui.slider('textScale', 'Cỡ chữ', 0.5, 1, 0.05, d.textScale, pct)
                        + ui.slider('spacing', 'Giãn chữ', -0.05, 0.4, 0.01, d.spacing, pct)
                        + ui.check('upper', 'VIẾT HOA TOÀN BỘ', d.upper));
            }
            if (i === 1) {
                return ui.label('Hình dạng đế') + ui.seg('shape', P.SHAPES.map(function (s) { return [s.key, s.name]; }), d.shape)
                    + ui.grid(ui.slider('heightPct', 'Chiều cao đế', 15, 70, 1, d.heightPct, function (v) { return f.heightPct(v, ctx); })
                        + ui.slider('margin', 'Lề quanh chữ (thu hẹp / nới khuôn)', 1.5, 12, 0.5, d.margin, mm)
                        + ui.slider('thickness', 'Độ dày đế', 2, 6, 0.5, d.thickness, mm)
                        + (d.shape === 'rounded' || d.shape === 'tag' ? ui.slider('radius', 'Bo góc', 0, 15, 0.5, d.radius, mm) : '')
                        + ui.check('border', 'Viền nổi quanh bảng', d.border, d.shape === 'outline'))
                    + ui.note('Chiều dài đế kéo ở mục <b>Chiều dài</b> cạnh giá sản phẩm.');
            }
            return ui.label('Biểu tượng') + ui.icons(P, d)
                + (d.icon ? ui.label('Vị trí biểu tượng') + ui.seg('iconSide', [['left', 'Trái'], ['right', 'Phải'], ['both', 'Hai bên']], d.iconSide) : '')
                + ui.label('Lỗ móc / lỗ treo (Ø4 mm)') + ui.seg('hole', HOLES, d.hole)
                + ui.label('Kiểu đặt') + ui.seg('stand', STANDS, d.stand);
        },
        // "Loại: Móc khoá · Phông: Pacifico · Chữ nổi 1,6 mm · Đế: Ôm theo chữ, cao 30 mm, dày 3 mm, lề 3 mm · Lỗ móc: bên trái"
        summary: function (d, P, ctx) {
            var parts = ['Loại: ' + nameOf(this.types, d.type), 'Phông: ' + P.fontOf(d.font).name + (d.upper ? ' (IN HOA)' : '')];
            if (d.line2) parts.push('Dòng 2: ' + d.line2);
            parts.push(nameOf(STYLES, d.style) + (d.style === 'flush' ? '' : ' ' + mm(d.relief)));
            if (d.textScale !== 1) parts.push('Cỡ chữ ' + pct(d.textScale));
            if (d.spacing) parts.push('Giãn chữ ' + pct(d.spacing));
            parts.push(plateSummary(d, P, ctx.plateLength()));
            if (d.border) parts.push('Viền nổi');
            if (d.hole !== 'none') parts.push('Lỗ móc: ' + nameOf(HOLES, d.hole).toLowerCase());
            if (d.icon) parts.push('Biểu tượng: ' + P.iconOf(d.icon).name + ' (' + nameOf([['left', 'trái'], ['right', 'phải'], ['both', 'hai bên']], d.iconSide) + ')');
            if (d.stand) parts.push('Có chân đứng');
            return parts.join(' · ');
        },
        spec: function (d, row, ctx) {
            return Object.assign(plateFields(d), {
                kind: 'nameplate', board: null, text: row.text, length: ctx.plateLength(),
                base: ctx.color('base', row, '#ffffff'), color: ctx.color('text', row, '#20201f')
            });
        }
    };

    // ---------- Class board: timetable / seating chart in one product ----------

    var DAYS = [['5', 'Thứ 2 – 6'], ['6', 'Thứ 2 – 7'], ['7', 'Cả tuần']];
    var DAY_NAMES = ['Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7', 'CN'];
    var TEACHER = [['left', 'Bên trái'], ['right', 'Bên phải'], ['none', 'Không vẽ']];
    var PERIODS = [0, 1, 2, 3, 4, 5].map(function (n) { return [String(n), n ? String(n) : 'Không']; });
    var BOARD_MODES = [['timetable', 'Thời khoá biểu'], ['seating', 'Sơ đồ lớp']];
    var HEADINGS = { timetable: 'Thời khoá biểu', seating: 'Sơ đồ lớp' };
    // Tile choice of the product (priced option "Kiểu ô"): text printed in place or removable tiles.
    var TILE_MODES = [['fixed', 'Chữ in liền'], ['press', 'Ô rời – khớp ấn'], ['magnet', 'Ô rời – nam châm']];
    var TILE_RE = /^kieu o$/;
    function tileModeOf(text) { var n = norm(text); return /nam cham/.test(n) ? 'magnet' : /khop an|roi/.test(n) ? 'press' : 'fixed'; }
    function tileMode(d, ctx) { var r = ctx.radio(TILE_RE); return r ? tileModeOf(r) : d.tileMode; }

    // Filament colors the studio prints boards in.
    var BOARD_COLORS = [
        ['#ffffff', 'Trắng'], ['#fff4dc', 'Kem'], ['#9e9e9a', 'Xám'], ['#20201f', 'Đen'], ['#1d1d24', 'Đen tím'],
        ['#ff67bc', 'Hồng'], ['#9b6bf2', 'Tím'], ['#ede4ff', 'Tím nhạt'], ['#2f6fe4', 'Xanh dương'], ['#5ac8fa', 'Xanh trời'],
        ['#80e5cb', 'Mint'], ['#6cc644', 'Xanh lá'], ['#dff3d2', 'Xanh lá nhạt'], ['#ffc93c', 'Vàng'], ['#ff7a1a', 'Cam'], ['#e5484d', 'Đỏ'],
        ['#f2f2ef', 'Trắng ngà'], ['#e0e0dc', 'Xám nhạt'], ['#3a3a46', 'Xám đậm'], ['#eef2f8', 'Xanh xám nhạt'], ['#1f4fb5', 'Xanh đậm'], ['#5b34b8', 'Tím đậm'], ['#2f7d1e', 'Xanh lá đậm']
    ];
    function colorName(hex) { return hex ? nameOf(BOARD_COLORS, hex) : 'không'; }

    // Board looks (mẫu). colors: plate (base), frame (null: none), days (accent), session labels (am, pm), heading and
    // name (ink), printed cells (cell, cellInk), removable tiles (tile, tileInk), stickers (deco1, deco2).
    // head: heading on a pill ('banner') or plain; day: day headers as pills, numbers in circles or text.
    var THEMES = [
        { key: 'pastel', name: 'Pastel tím', hint: 'Viền gợn sóng, tim & sao', shape: 'scallop', head: 'banner', headFont: 'titanone', day: 'text', deco: ['heart', 'star'],
            colors: { base: '#ffffff', frame: '#9b6bf2', accent: '#9b6bf2', am: '#9b6bf2', pm: '#ff67bc', ink: '#9b6bf2', cell: '#ede4ff', cellInk: '#5b34b8', tile: '#2f6fe4', tileInk: '#ffffff', deco1: '#ff67bc', deco2: '#ffc93c' } },
        { key: 'gamer', name: 'Gamer', hint: 'Trắng – đen – cam', shape: 'rounded', head: 'plain', headFont: 'be', day: 'circle', deco: ['gear', 'bolt'],
            colors: { base: '#f2f2ef', frame: '#20201f', accent: '#20201f', am: '#20201f', pm: '#ff7a1a', ink: '#20201f', cell: '#e0e0dc', cellInk: '#20201f', tile: '#ff7a1a', tileInk: '#ffffff', deco1: '#20201f', deco2: '#ff7a1a' } },
        { key: 'panda', name: 'Gấu trúc', hint: 'Xanh lá tre, gấu trúc', shape: 'rounded', head: 'plain', headFont: 'titanone', day: 'pill', deco: ['panda', 'leaf'],
            colors: { base: '#ffffff', frame: '#6cc644', accent: '#6cc644', am: '#6cc644', pm: '#6cc644', ink: '#2f7d1e', cell: '#dff3d2', cellInk: '#2f7d1e', tile: '#6cc644', tileInk: '#ffffff', deco1: '#20201f', deco2: '#6cc644' } },
        { key: 'space', name: 'Vũ trụ', hint: 'Nền đen, sao & tên lửa', shape: 'rounded', head: 'plain', headFont: 'titanone', day: 'text', deco: ['rocket', 'planet'], scatter: true,
            colors: { base: '#1d1d24', frame: null, accent: '#ede4ff', am: '#9b6bf2', pm: '#ff7a1a', ink: '#ede4ff', cell: '#3a3a46', cellInk: '#ffffff', tile: '#9b6bf2', tileInk: '#ffffff', deco1: '#e5484d', deco2: '#ffc93c' } },
        { key: 'classic', name: 'Cổ điển', hint: 'Số ngày tròn, Sáng xanh – Chiều đỏ', shape: 'rounded', head: 'plain', headFont: 'be', day: 'circle', deco: [],
            colors: { base: '#ffffff', frame: null, accent: '#ff7a1a', am: '#2f6fe4', pm: '#e5484d', ink: '#20201f', cell: '#eef2f8', cellInk: '#1f4fb5', tile: '#2f6fe4', tileInk: '#ffffff' } }
    ];
    function themeOf(key) { return THEMES.filter(function (t) { return t.key === key; })[0] || THEMES[0]; }
    // Text color that reads on a fill.
    function inkOn(hex) {
        var c = String(hex || '#ffffff').replace('#', ''), v = [0, 2, 4].map(function (i) { return parseInt(c.substr(i, 2), 16) || 0; });
        return 0.299 * v[0] + 0.587 * v[1] + 0.114 * v[2] > 165 ? '#20201f' : '#ffffff';
    }
    // Colors the renderer paints the board with (TTNameplate boardLook).
    function boardTheme(d) {
        var t = themeOf(d.theme), c = d.colors;
        return {
            frame: c.frame || null, head: t.head, headFont: t.headFont, headColor: t.head === 'banner' ? c.accent : c.ink, headInk: inkOn(c.accent), nameInk: c.ink,
            day: t.day, dayColor: c.accent, dayInk: inkOn(c.accent), am: c.am, pm: c.pm, amInk: inkOn(c.am), pmInk: inkOn(c.pm),
            cell: c.cell, cellInk: c.cellInk, scatter: t.scatter ? [c.ink, c.pm, c.deco2 || c.accent] : null,
            deco: (t.deco || []).map(function (icon, i) { return { icon: icon, color: c['deco' + (i + 1)] || c.accent }; })
        };
    }
    // Small drawing of a look for its card.
    function miniBoard(t) {
        var c = t.colors, cells = '';
        for (var i = 0; i < 12; i++) cells += '<i></i>';
        return '<span class="tt-np-mini' + (t.shape === 'scallop' ? ' is-wavy' : '') + '" style="--b:' + c.base + ';--f:' + (c.frame || c.base) + ';--a:' + c.accent + ';--m:' + c.am + ';--p:' + c.pm + ';--c:' + c.cell + ';--k:' + c.ink + '">'
            + '<span class="tt-np-mini-h"></span><span class="tt-np-mini-s"><i></i><i></i></span><span class="tt-np-mini-g">' + cells + '</span></span>';
    }
    function swatches(key, value, none) {
        return '<div class="tt-np-swatches">' + (none ? '<button type="button" class="tt-np-sw is-none" data-set="' + key + '" data-val="none" aria-pressed="' + !value + '" title="Không có"></button>' : '')
            + BOARD_COLORS.map(function (c) {
                return '<button type="button" class="tt-np-sw" data-set="' + key + '" data-val="' + c[0] + '" aria-pressed="' + (value === c[0]) + '" title="' + c[1] + '" style="--sw:' + c[0] + '"></button>';
            }).join('') + '</div>';
    }
    var COLOR_ROLES = [['base', 'Nền bảng'], ['frame', 'Viền khung', true], ['ink', 'Tiêu đề & tên'], ['accent', 'Hàng ngày (Thứ 2…)'], ['am', 'Nhãn buổi sáng'],
        ['pm', 'Nhãn buổi chiều'], ['cell', 'Ô môn học / chỗ ngồi'], ['cellInk', 'Chữ trong ô']];

    var classboard = {
        item: 'bảng',
        empty: 'Nhập tên để xem trước 3D',
        textHint: 'VD: Vũ Lan Anh · Lớp 6A1',
        // Quick design: the look, the board kind, the name, the content. A board of a fixed look (a ready-made board
        // of the "Thời khoá biểu" category) only asks for the name; its content is optional.
        quick: function (d, P, ctx) {
            if (d.locked) {
                return ui.step(1, 'Nhập tên muốn in lên bảng') + ui.textSlot(this.textHint)
                    + ui.note('Bảng in đúng mẫu trong ảnh, có sẵn khung ô trống. Muốn in sẵn tên môn học vào từng ô thì điền ở dưới (không bắt buộc).')
                    + '<div class="tt-np-more' + (d.showContent ? ' is-open' : '') + '"><button type="button" class="tt-np-more-btn" data-set="showContent" data-val="' + !d.showContent + '" aria-expanded="' + !!d.showContent + '">'
                    + (d.showContent ? 'Ẩn phần nội dung' : '+ Điền sẵn môn học vào bảng') + '</button>'
                    + (d.showContent ? this.content(d, ctx) : '') + '</div>'
                    + (ctx.customUrl ? ui.note('Muốn đổi màu, bố cục hay làm sơ đồ lớp? <a href="' + esc(ctx.customUrl) + '">Tự thiết kế bảng của bạn →</a>') : '');
            }
            return ui.step(1, 'Chọn mẫu') + '<div class="tt-np-themes">' + THEMES.map(function (t) {
                return '<button type="button" class="tt-np-theme" data-set="theme" data-val="' + t.key + '" aria-pressed="' + (d.theme === t.key) + '">' + miniBoard(t)
                    + '<b>' + t.name + '</b><small>' + t.hint + '</small></button>';
            }).join('') + '</div>'
                + ui.step(2, 'Loại bảng') + ui.seg('mode', BOARD_MODES, d.mode)
                + ui.step(3, 'Tên trên bảng') + ui.textSlot(this.textHint)
                + ui.text('heading', 'Dòng tiêu đề', d.heading, HEADINGS[d.mode], 40, '(để trống nếu không cần)')
                + ui.step(4, d.mode === 'seating' ? 'Chỗ ngồi & tên học sinh' : 'Môn học từng tiết') + this.content(d, ctx);
        },
        advanced: 'Đổi màu từng phần, ô rời tháo lắp, phông chữ, kích thước, lỗ treo / chân đứng',
        advancedFor: function (d) { return !d.locked; },
        tabs: ['Màu sắc', 'Kiểu ô', 'Chữ', 'Kích thước & treo'],
        // Timetable or seating content, shared by the quick design of both board kinds.
        content: function (d) {
            if (d.mode === 'seating') {
                var seats = d.rows * d.groups * d.seats, filled = String(d.names || '').split(/\r?\n/).filter(function (n) { return n.trim(); }).length;
                return ui.grid(ui.slider('rows', 'Số hàng bàn', 1, 8, 1, d.rows, String)
                        + ui.slider('groups', 'Số dãy bàn', 1, 5, 1, d.groups, String)
                        + ui.slider('seats', 'Chỗ mỗi bàn', 1, 3, 1, d.seats, String))
                    + ui.label('Bàn giáo viên') + ui.seg('teacher', TEACHER, d.teacher)
                    + ui.area('names', 'Danh sách học sinh', d.names, 'Nguyễn Văn An\nTrần Thị Bình\n…', 7,
                        '(mỗi dòng 1 bạn, từ bàn đầu, trái sang phải; dòng trống = chỗ trống)')
                    + ui.note('<b data-seat-count>' + filled + '</b> học sinh · ' + seats + ' chỗ. Copy cả cột tên trong Excel rồi dán vào ô trên là xong.');
            }
            var html = ui.label('Học các ngày') + ui.seg('days', DAYS, d.days, true)
                + ui.label('Số tiết buổi sáng') + ui.seg('am', PERIODS, d.am, true)
                + ui.label('Số tiết buổi chiều') + ui.seg('pm', PERIODS, d.pm, true);
            ['am', 'pm'].forEach(function (s) {
                if (!d[s]) return;
                html += ui.label(s === 'am' ? 'Buổi sáng' : 'Buổi chiều') + '<div class="tt-np-tablewrap"><table class="tt-np-table"><thead><tr><th>Tiết</th>'
                    + DAY_NAMES.slice(0, d.days).map(function (n) { return '<th>' + n + '</th>'; }).join('') + '</tr></thead><tbody>'
                    + d.cells[s].map(function (row, r) {
                        return '<tr><th>' + (r + 1) + '</th>' + row.slice(0, d.days).map(function (v, c) {
                            return '<td><input type="text" class="skip-pd-ajax-update" maxlength="24" data-cell="' + s + '|' + r + '|' + c + '" value="' + esc(v) + '" placeholder="…" aria-label="' + DAY_NAMES[c] + ' tiết ' + (r + 1) + '" /></td>';
                        }).join('') + '</tr>';
                    }).join('') + '</tbody></table></div>';
            });
            return html + ui.note('Có thể để trống rồi tự viết sau. Gõ tên môn vào từng ô, hoặc copy cả bảng trong Excel rồi dán vào ô đầu tiên.');
        },
        fresh: function (P, ctx) {
            var locked = ctx && ctx.theme ? themeOf(ctx.theme) : null;
            var d = Object.assign(omit(P.DEFAULTS, ['text', 'base', 'color', 'length', 'board', 'theme', 'tileInk']), {
                kind: 'classboard', font: 'be', margin: 6, thickness: 3, shape: 'rounded', radius: 6, relief: 1, style: 'raised',
                mode: 'timetable', heading: HEADINGS.timetable, days: 6, am: 5, pm: 4, cells: { am: [], pm: [] },
                rows: 5, groups: 4, seats: 2, teacher: 'left', names: '', line: 0.8,
                tileMode: 'fixed', spare: '', explode: false, stand: false, hole: 'top2', border: false, heightPct: 74,
                locked: !!locked, showContent: false
            });
            this.applyTheme(d, locked || THEMES[0]);
            this.normalize(d);
            return d;
        },
        applyTheme: function (d, t) {
            d.theme = t.key;
            d.colors = Object.assign({}, t.colors);
            d.shape = t.shape || 'rounded';
            d.margin = t.scatter ? 9 : 6;
        },
        // Keeps the timetable arrays as large as the chosen days and periods (cells typed before are kept).
        normalize: function (d) {
            ['am', 'pm'].forEach(function (s) {
                var list = (d.cells[s] || []).slice(0, Math.max(0, d[s]));
                while (list.length < d[s]) list.push([]);
                d.cells[s] = list.map(function (row) { row = (row || []).slice(0, 7); while (row.length < 7) row.push(''); return row; });
            });
        },
        formats: { heightPct: function (v, ctx) { return mm(ctx.plateLength() * v / 100); }, line: mm },
        onSet: function (key, val, patch, d, ctx) {
            // The tile choice is a priced product option: pick it there (the price follows).
            if (key === 'tileMode') ctx.pickRadio(TILE_RE, function (text) { return tileModeOf(text) === val; });
            if (key === 'theme') {
                var look = {};
                this.applyTheme(look, themeOf(val));
                Object.assign(patch, look);
            }
            // A heading still at its default follows the board kind.
            if (key === 'mode' && (!d.heading || d.heading === HEADINGS[d.mode])) patch.heading = HEADINGS[val];
            // "color:base" → one color of the look.
            if (key.indexOf('color:') === 0) {
                patch.colors = Object.assign({}, d.colors);
                patch.colors[key.slice(6)] = val === 'none' ? null : val;
                delete patch[key];
            }
        },
        pane: function (i, d, P, ctx) {
            var f = this.formats, c = d.colors;
            if (i === 0) {
                return COLOR_ROLES.map(function (r) { return ui.label(r[1]) + swatches('color:' + r[0], c[r[0]], r[2]); }).join('')
                    + ui.note('Mỗi phần in một màu nhựa riêng. Chọn lại mẫu ở bước 1 để về màu gốc của mẫu.');
            }
            if (i === 1) {
                var tm = tileMode(d, ctx);
                return ui.label('Kiểu ô') + ui.seg('tileMode', TILE_MODES, tm)
                    + (tm === 'fixed' ? ui.note('Chữ in liền vào từng ô trên bảng. Muốn đổi môn / đổi chỗ được thì chọn <b>ô rời</b>: mỗi ô là một miếng riêng có chữ, cắm vào hốc trên bảng, rút ra lắp lại được (giá theo lựa chọn Kiểu ô).')
                        : ui.label('Màu ô rời') + swatches('color:tile', c.tile) + ui.label('Chữ trên ô rời') + swatches('color:tileInk', c.tileInk)
                            + ui.grid(ui.check('explode', 'Xem các ô tách khỏi bảng', d.explode))
                            + ui.area('spare', 'Ô thêm để thay đổi', d.spare, 'Tin học\nÂm nhạc\n…', 3, '(mỗi dòng 1 ô, in kèm để thay khi đổi môn / đổi chỗ; không bắt buộc)'));
            }
            if (i === 2) {
                return ui.text('line2', 'Dòng phụ dưới tên', d.line2, 'VD: Năm học 2026 – 2027 · GVCN: Cô Lan', 70, '(không bắt buộc)')
                    + ui.label('Phông chữ trong ô') + ui.fonts(P, d, 'classboard')
                    + ui.label('Kiểu chữ') + ui.seg('style', STYLES, d.style)
                    + ui.grid(ui.slider('relief', d.style === 'engraved' ? 'Độ sâu' : 'Độ nổi', 0.4, 2.4, 0.2, d.relief, mm)
                        + ui.slider('textScale', 'Cỡ chữ trong ô', 0.5, 1, 0.05, d.textScale, pct)
                        + ui.check('upper', 'VIẾT HOA TOÀN BỘ', d.upper));
            }
            return ui.label('Viền bảng') + ui.seg('shape', [['rounded', 'Bo góc'], ['scallop', 'Gợn sóng'], ['rect', 'Vuông góc']], d.shape)
                + ui.grid(ui.slider('heightPct', 'Chiều cao bảng', 40, 100, 1, d.heightPct, function (v) { return f.heightPct(v, ctx); })
                    + ui.slider('margin', 'Lề quanh nội dung', 3, 15, 0.5, d.margin, mm)
                    + ui.slider('thickness', 'Độ dày đế', 2, 6, 0.5, d.thickness, mm)
                    + (d.shape === 'rect' ? '' : ui.slider('radius', 'Bo góc', 0, 20, 0.5, d.radius, mm)))
                + ui.label('Lỗ treo (Ø4 mm)') + ui.seg('hole', [['none', 'Không'], ['top1', '1 lỗ trên'], ['top2', '2 lỗ trên']], d.hole)
                + ui.label('Kiểu đặt') + ui.seg('stand', STANDS, d.stand)
                + ui.note('Chiều dài bảng kéo ở mục <b>Chiều dài</b> cạnh giá sản phẩm.');
        },
        // Typing in a timetable cell.
        cell: function (d, el) {
            var p = el.getAttribute('data-cell').split('|');
            d.cells[p[0]][+p[1]][+p[2]] = el.value;
        },
        // Cells copied from Excel / Google Sheets fill the table from the cell they are pasted into.
        paste: function (d, el, text) {
            if (!/[\t\n]/.test(text.trim())) return false;
            var p = el.getAttribute('data-cell').split('|'), s = p[0], r0 = +p[1], c0 = +p[2];
            text.replace(/\r/g, '').replace(/\n$/, '').split('\n').forEach(function (line, i) {
                var row = d.cells[s][r0 + i];
                if (!row) return;
                line.split('\t').forEach(function (v, j) { if (c0 + j < d.days) row[c0 + j] = v.trim(); });
            });
            return true;
        },
        summary: function (d, P, ctx) {
            var t = themeOf(d.theme), c = d.colors, changed = Object.keys(t.colors).some(function (k) { return (t.colors[k] || null) !== (c[k] || null); });
            var parts = ['Mẫu: ' + t.name + (changed ? ' (đã đổi màu)' : ''), 'Loại: ' + nameOf(BOARD_MODES, d.mode) + (d.stand ? ' để bàn' : '')];
            if (d.heading) parts.push('Tiêu đề: ' + d.heading);
            if (d.line2) parts.push('Dòng phụ: ' + d.line2);
            if (changed || !d.locked) {
                parts.push('Màu: nền ' + colorName(c.base) + ', viền ' + colorName(c.frame) + ', chữ ' + colorName(c.ink) + ', ngày ' + colorName(c.accent)
                    + ', sáng ' + colorName(c.am) + ', chiều ' + colorName(c.pm) + ', ô ' + colorName(c.cell) + ' / chữ ô ' + colorName(c.cellInk));
            }
            parts.push('Phông ô: ' + P.fontOf(d.font).name + (d.upper ? ' (IN HOA)' : ''), nameOf(STYLES, d.style) + (d.style === 'flush' ? '' : ' ' + mm(d.relief)));
            parts.push(plateSummary(d, P, ctx.plateLength()));
            if (d.hole !== 'none') parts.push('Lỗ treo: ' + nameOf(HOLES, d.hole).toLowerCase());
            if (d.stand) parts.push('Có chân đứng');
            var tm = tileMode(d, ctx);
            if (tm !== 'fixed') {
                parts.push('Ô rời ' + (tm === 'magnet' ? 'nam châm' : 'khớp ấn') + ', màu ô ' + colorName(c.tile) + ', chữ ' + colorName(c.tileInk));
                var spare = String(d.spare || '').split(/\r?\n/).map(function (x) { return x.trim(); }).filter(Boolean);
                if (spare.length) parts.push('Ô thêm: ' + spare.join(', '));
            }
            var lines = [parts.join(' · ')];
            if (d.mode === 'seating') {
                lines.push('Sơ đồ lớp: ' + d.rows + ' hàng × ' + d.groups + ' dãy × ' + d.seats + ' chỗ, bàn GV ' + nameOf(TEACHER, d.teacher).toLowerCase());
                var names = String(d.names || '').split(/\r?\n/), seat = 0;
                for (var r = 0; r < d.rows; r++) {
                    var row = [];
                    for (var g = 0; g < d.groups * d.seats; g++) row.push((names[seat++] || '').trim() || '–');
                    lines.push('Hàng ' + (r + 1) + ': ' + row.join(', '));
                }
            } else {
                var any = false;
                ['am', 'pm'].forEach(function (s) {
                    if (!d[s]) return;
                    for (var col = 0; col < d.days; col++) {
                        var subjects = d.cells[s].map(function (rw) { return (rw[col] || '').trim(); });
                        if (subjects.some(Boolean)) any = true;
                        lines.push((s === 'am' ? 'Sáng ' : 'Chiều ') + DAY_NAMES[col] + ': ' + subjects.map(function (x) { return x || '–'; }).join(', '));
                    }
                });
                // An empty timetable: only its size matters.
                if (!any) lines = [lines[0], 'Thời khoá biểu trống: ' + d.days + ' ngày, ' + d.am + ' tiết sáng, ' + d.pm + ' tiết chiều'];
            }
            return lines.join('\n');
        },
        spec: function (d, row, ctx) {
            var c = d.colors, tm = tileMode(d, ctx);
            return Object.assign(plateFields(d), {
                kind: 'classboard', text: row.text, length: ctx.plateLength(), base: c.base, color: c.ink, border: false,
                tileColor: c.tile, tileInk: c.tileInk, explode: !!d.explode && tm !== 'fixed', theme: boardTheme(d),
                board: { mode: d.mode, heading: d.heading, days: d.days, am: d.am, pm: d.pm, cells: d.cells, rows: d.rows, groups: d.groups, seats: d.seats, teacher: d.teacher, names: d.names, line: d.line, tiles: tm !== 'fixed' }
            });
        }
    };

    // ---------- Keycap ----------

    // Width in units from a size option ("2,25u (Enter, Shift trái)").
    function unitsOf(text) { var m = String(text || '').match(/(\d+(?:[.,]\d+)?)\s*u\b/i); return m ? parseFloat(m[1].replace(',', '.')) : 1; }
    function profileOf(text) {
        var n = norm(text);
        return /cherry/.test(n) ? 'cherry' : /xda/.test(n) ? 'xda' : /dsa/.test(n) ? 'dsa' : /^sa\b/.test(n) ? 'sa' : 'oem';
    }
    var LEGEND_POS = [['center', 'Giữa'], ['tl', 'Góc trên trái'], ['tc', 'Trên giữa'], ['bl', 'Góc dưới trái']];
    var KEY_STYLES = [['raised', 'In nổi'], ['engraved', 'Khắc chìm'], ['flush', 'In màu phẳng']];
    var STEMS = [['mx', 'MX (dấu +)'], ['choc', 'Kailh Choc'], ['alps', 'Alps']];
    var SIZE_RE = /kich co phim|kich thuoc phim/;

    var keycap = {
        item: 'keycap',
        empty: 'Nhập ký tự để xem trước keycap 3D',
        tabs: ['Loại', 'Ký tự', 'Phím', 'Thêm'],
        types: [
            { key: 'alpha', name: 'Phím chữ / số', hint: '1u, ký tự giữa', units: 1, set: { legendPos: 'center', iconSide: 'left' } },
            { key: 'mod', name: 'Phím chức năng', hint: 'Ctrl, Alt, Tab… 1,25–1,5u', units: 1.25, set: { legendPos: 'bl', iconSide: 'left' } },
            { key: 'enter', name: 'Enter / Shift', hint: '2,25u, có chân cân bằng', units: 2.25, set: { legendPos: 'bl', iconSide: 'left' } },
            { key: 'space', name: 'Phím cách', hint: '6,25u', units: 6.25, set: { legendPos: 'center', iconSide: 'left' } },
            { key: 'logo', name: 'Phím logo / artisan', hint: 'Biểu tượng to giữa phím', units: 1, set: { legendPos: 'center', iconSide: 'only', icon: 'heart' } }
        ],
        fresh: function (P) {
            return Object.assign(omit(P.DEFAULTS, ['text', 'base', 'color', 'length', 'board']), {
                kind: 'keycap', type: 'alpha', font: 'be', style: 'raised', relief: 0.5, textScale: 1, upper: true,
                legendPos: 'center', row: 3, homing: false, stem: 'mx', icon: '', iconSide: 'left'
            });
        },
        // A preset also picks its size in the product options (that option sets the price).
        apply: function (preset, ctx) {
            if (preset.units) ctx.pickRadio(SIZE_RE, function (text) { return unitsOf(text) === preset.units; });
        },
        pane: function (i, d, P, ctx) {
            if (i === 0) return ui.types(this.types, d) + ui.note('Chọn loại phím: kích cỡ phím ở cột thông tin đổi theo (giá theo kích cỡ). Sau đó chỉnh ký tự, phím, biểu tượng ở các tab bên cạnh.');
            if (i === 1) {
                return ui.text('line2', 'Ký tự phụ', d.line2, 'VD: ! trên phím 1', 12, '(ký tự Shift, không bắt buộc)')
                    + ui.label('Vị trí ký tự') + ui.seg('legendPos', LEGEND_POS, d.legendPos)
                    + ui.label('Phông chữ') + ui.fonts(P, d, 'keycap')
                    + ui.label('Kiểu in ký tự') + ui.seg('style', KEY_STYLES, d.style)
                    + ui.grid((d.style === 'flush' ? '' : ui.slider('relief', d.style === 'engraved' ? 'Độ sâu' : 'Độ nổi', 0.2, 1.2, 0.1, d.relief, mm))
                        + ui.slider('textScale', 'Cỡ ký tự', 0.4, 1.3, 0.05, d.textScale, pct)
                        + ui.check('upper', 'VIẾT HOA', d.upper));
            }
            if (i === 2) {
                var profile = ctx.radio(/^profile$/), size = ctx.radio(SIZE_RE);
                return ui.note('Profile <b>' + esc(profile || 'OEM') + '</b> · kích cỡ <b>' + esc(size || '1u') + '</b> — chọn ở cột thông tin bên cạnh (đổi giá).')
                    + ui.label('Hàng phím (độ nghiêng mặt phím)') + ui.seg('row', [['1', 'R1 – hàng số'], ['2', 'R2'], ['3', 'R3 – hàng giữa'], ['4', 'R4 – hàng dưới']], d.row, true)
                    + ui.label('Chân phím') + ui.seg('stem', STEMS, d.stem)
                    + ui.grid(ui.check('homing', 'Gờ định vị (phím F / J)', d.homing))
                    + ui.note('XDA và DSA có mặt phím bằng nhau ở mọi hàng; OEM, Cherry, SA nghiêng theo hàng.');
            }
            return ui.label('Biểu tượng trên phím') + ui.icons(P, d)
                + (d.icon ? ui.label('Cách đặt') + ui.seg('iconSide', [['left', 'Cạnh ký tự'], ['only', 'Chỉ biểu tượng']], d.iconSide) : '');
        },
        summary: function (d, P, ctx) {
            var parts = ['Loại: ' + nameOf(this.types, d.type)];
            if (d.line2) parts.push('Ký tự phụ: ' + d.line2);
            parts.push('Vị trí: ' + nameOf(LEGEND_POS, d.legendPos).toLowerCase(), 'Phông: ' + P.fontOf(d.font).name + (d.upper ? ' (IN HOA)' : ''));
            parts.push(nameOf(KEY_STYLES, d.style) + (d.style === 'flush' ? '' : ' ' + mm(d.relief)));
            if (d.textScale !== 1) parts.push('Cỡ ký tự ' + pct(d.textScale));
            parts.push('Hàng R' + d.row, 'Chân ' + nameOf(STEMS, d.stem));
            if (d.homing) parts.push('Gờ định vị');
            if (d.icon) parts.push('Biểu tượng: ' + P.iconOf(d.icon).name + (d.iconSide === 'only' ? ' (thay ký tự)' : ''));
            return parts.join(' · ');
        },
        spec: function (d, row, ctx) {
            return Object.assign(omit(d, ['type']), {
                kind: 'keycap', board: null, text: row.text,
                profile: profileOf(ctx.radio(/^profile$/)), units: unitsOf(ctx.radio(SIZE_RE)),
                base: ctx.color('base', row, '#f4f1e8'), color: ctx.color('text', row, '#20201f')
            });
        }
    };

    // ---------- QR plate ----------

    var QR_TYPES = [['url', 'Link'], ['wifi', 'WiFi'], ['bank', 'Chuyển khoản'], ['zalo', 'Zalo'], ['phone', 'Điện thoại'], ['text', 'Văn bản']];
    var QR_CONTENT = ['qType', 'qText', 'ssid', 'password', 'security', 'hidden', 'bank', 'account', 'amount', 'ecc', 'icon'];
    var QR_DOTS = [['square', 'Vuông'], ['round', 'Bo tròn'], ['dots', 'Chấm tròn']];
    // 3D styles of the code (qrLevels in studio-nameplate.js) and the values each one starts with.
    var QR_RELIEFS = [
        { key: 'flat', name: 'Phẳng', hint: 'Nổi đều, dễ quét nhất', set: { relief: 0.8 } },
        { key: 'pyramid', name: 'Kim tự tháp', hint: 'Bậc vuông, khối to thì cao', set: { qrHeight: 6, tiers: 8, multi: false } },
        { key: 'terrace', name: 'Ruộng bậc thang', hint: 'Bậc tròn, nhiều tông màu', set: { qrHeight: 6, tiers: 8, multi: true } },
        { key: 'river', name: 'Dòng sông', hint: 'Bờ uốn lượn từng lớp', set: { qrHeight: 2, tiers: 4, bankWidth: 0.2, multi: true } },
        { key: 'hills', name: 'Đồi', hint: 'Vòm tròn mềm mại', set: { qrHeight: 4, multi: false } }
    ];

    function qrContentFields(d) {
        if (d.qType === 'wifi') {
            return ui.text('ssid', 'Tên WiFi', d.ssid, 'VD: TT Minimal', 32)
                + ui.text('password', 'Mật khẩu', d.password, '', 63)
                + ui.label('Bảo mật') + ui.seg('security', [['WPA', 'WPA / WPA2'], ['WEP', 'WEP'], ['nopass', 'Không mật khẩu']], d.security)
                + ui.grid(ui.check('hidden', 'WiFi ẩn', d.hidden));
        }
        if (d.qType === 'bank') {
            return ui.text('bank', 'Ngân hàng', d.bank, 'VD: MB, Vietcombank, Techcombank', 40)
                + ui.text('account', 'Số tài khoản', d.account, '', 30)
                + ui.text('amount', 'Số tiền', d.amount, 'Để trống: người quét tự nhập', 12, '(VNĐ, không bắt buộc)')
                + ui.text('qText', 'Nội dung chuyển khoản', d.qText, 'Không bắt buộc', 40);
        }
        if (d.qType === 'zalo' || d.qType === 'phone') return ui.text('qText', 'Số điện thoại', d.qText, 'VD: 0333 424 766', 20);
        if (d.qType === 'url') return ui.text('qText', 'Đường link', d.qText, 'VD: facebook.com/TT.minimal', 300);
        return ui.area('qText', 'Nội dung', d.qText, '', 4);
    }

    var qr = {
        item: 'bảng QR',
        empty: 'Nhập nội dung mã QR (bước 1) để xem trước 3D',
        // Quick design: what the code holds, its 3D style and height. Everything else has a sensible default.
        quick: function (d) {
            var flat = d.qrRelief === 'flat';
            return ui.step(1, 'Mã QR chứa gì?') + ui.seg('qType', QR_TYPES, d.qType) + qrContentFields(d) + this.statusLine(d)
                + ui.step(2, 'Chọn kiểu 3D')
                + '<div class="tt-np-types">' + QR_RELIEFS.map(function (r) {
                    return '<button type="button" class="tt-np-type" data-set="qrRelief" data-val="' + r.key + '" aria-pressed="' + (d.qrRelief === r.key) + '"><b>' + r.name + '</b><small>' + r.hint + '</small></button>';
                }).join('') + '</div>'
                + ui.step(3, flat ? 'Độ nổi của mã' : 'Độ cao & màu')
                + (flat
                    ? ui.slider('relief', 'Độ nổi', 0.4, 2, 0.2, d.relief, mm)
                    : ui.slider('qrHeight', 'Độ cao tối đa', 1, 10, 0.5, d.qrHeight, mm)
                        + ui.label('Màu mã') + ui.seg('multi', [['false', 'Một màu'], ['true', 'Nhiều tông (đổi màu theo tầng)']], String(d.multi)))
                + ui.note('Chữ dưới mã nhập ở ô <b>Chữ trên bảng</b>, kích thước chọn bằng thanh <b>chiều dài</b> cạnh giá.'
                    + (flat ? '' : ' Kiểu 3D quét tốt nhất khi nhìn thẳng.'));
        },
        advanced: 'Để bàn / treo / móc khoá, kiểu điểm mã, biểu tượng giữa, số bậc, chữ & đế',
        tabs: ['Kiểu đặt', 'Mã QR', 'Chữ & đế'],
        types: [
            { key: 'desk', name: 'Để bàn', hint: 'Có chân đứng', set: { stand: true, hole: 'none', shape: 'rounded', margin: 4 } },
            { key: 'wall', name: 'Treo / dán tường', hint: '2 lỗ treo', set: { stand: false, hole: 'top2', shape: 'rounded', margin: 4 } },
            { key: 'lay', name: 'Đặt nằm / dán', hint: 'Tấm phẳng, không lỗ', set: { stand: false, hole: 'none', shape: 'rounded', margin: 4 } },
            { key: 'keychain', name: 'Móc khoá QR', hint: 'Nhỏ, lỗ móc', set: { stand: false, hole: 'left', shape: 'rounded', margin: 2.5 } }
        ],
        fresh: function (P) {
            return Object.assign(omit(P.DEFAULTS, ['text', 'base', 'color', 'length', 'board', 'qr']), {
                kind: 'qr', type: 'desk', font: 'be', style: 'raised', relief: 0.8, margin: 4, thickness: 3, radius: 4, border: false,
                qType: 'url', qText: '', ssid: '', password: '', security: 'WPA', hidden: false, bank: '', account: '', amount: '',
                ecc: 'M', qrStyle: 'square', quiet: 1, qrScale: 1, caption: 'bottom', icon: '', matrix: null, qrError: '',
                qrRelief: 'flat', qrHeight: 6, tiers: 8, bankWidth: 0.2, multi: false
            }, this.types[0].set);
        },
        formats: { quiet: function (v) { return v + ' ô'; }, qrScale: pct, tiers: String, bankWidth: function (v) { return pct(v) + ' ô'; } },
        // A 3D style starts from its own height / tiers / shades.
        onSet: function (key, val, patch) {
            if (key !== 'qrRelief') return;
            var r = QR_RELIEFS.filter(function (x) { return x.key === val; })[0];
            if (r) Object.assign(patch, r.set);
        },
        pane: function (i, d, P) {
            if (i === 0) return ui.types(this.types, d);
            if (i === 1) {
                var r = d.qrRelief;
                return ui.label('Kiểu điểm mã') + ui.seg('qrStyle', QR_DOTS, d.qrStyle)
                    + (r === 'pyramid' || r === 'terrace' || r === 'river'
                        ? ui.grid(ui.slider('tiers', r === 'river' ? 'Số lớp' : 'Số bậc tối đa', 2, r === 'river' ? 8 : 16, 1, d.tiers, String)
                            + (r === 'river' ? ui.slider('bankWidth', 'Độ rộng bờ', 0.1, 0.4, 0.05, d.bankWidth, this.formats.bankWidth) : ''))
                        : '')
                    + ui.label('Độ chịu lỗi') + ui.seg('ecc', [['L', 'Thấp – ít điểm'], ['M', 'Vừa'], ['Q', 'Khá'], ['H', 'Cao']], d.icon ? 'H' : d.ecc)
                    + ui.grid(ui.slider('qrScale', 'Cỡ mã trên bảng', 0.5, 1, 0.05, d.qrScale, pct) + ui.slider('quiet', 'Lề trắng quanh mã', 0, 4, 1, d.quiet, this.formats.quiet))
                    + ui.label('Biểu tượng giữa mã') + ui.icons(P, d)
                    + ui.note('Có biểu tượng giữa thì mã tự dùng độ chịu lỗi Cao (mã dày điểm hơn). Mã càng nhiều điểm thì bảng càng phải to.')
                    + '<p class="tt-np-note tt-qr-status" data-qr-status>' + this.status(d) + '</p>';
            }
            return ui.label('Chữ') + ui.seg('caption', [['bottom', 'Dưới mã'], ['top', 'Trên mã'], ['none', 'Không chữ']], d.caption)
                + (d.caption === 'none' ? '' : ui.text('line2', 'Dòng chữ nhỏ', d.line2, 'VD: Mật khẩu: 12345678', 50, '(không bắt buộc)'))
                + ui.label('Phông chữ') + ui.fonts(P, d, 'qr')
                + ui.label('Kiểu in chữ') + ui.seg('style', STYLES, d.style)
                + ui.grid((d.qrRelief === 'flat' ? ui.slider('relief', d.style === 'engraved' ? 'Độ sâu' : 'Độ nổi', 0.4, 2, 0.2, d.relief, mm) : '')
                    + ui.slider('textScale', 'Cỡ chữ', 0.5, 1, 0.05, d.textScale, pct)
                    + ui.check('upper', 'VIẾT HOA', d.upper))
                + ui.label('Đế') + ui.seg('shape', [['rounded', 'Bo góc'], ['rect', 'Vuông góc'], ['oval', 'Oval']], d.shape)
                + ui.grid(ui.slider('margin', 'Lề', 2, 10, 0.5, d.margin, mm)
                    + ui.slider('thickness', 'Độ dày đế', 2, 6, 0.5, d.thickness, mm)
                    + (d.shape === 'rounded' ? ui.slider('radius', 'Bo góc', 0, 12, 0.5, d.radius, mm) : '')
                    + ui.check('border', 'Viền nổi', d.border))
                + ui.label('Lỗ treo / móc (Ø4 mm)') + ui.seg('hole', HOLES, d.hole)
                + ui.label('Kiểu đặt') + ui.seg('stand', STANDS, d.stand);
        },
        statusLine: function (d) {
            return '<p class="tt-np-note tt-qr-status" data-qr-status>' + this.status(d) + '</p>'
                + '<button type="button" class="tt-np-action" data-action="top">Nhìn thẳng để quét thử bằng điện thoại</button>';
        },
        status: function (d) {
            if (d.qrError) return '⚠ ' + esc(d.qrError);
            return d.matrix ? 'Mã QR ' + d.matrix.length + ' × ' + d.matrix.length + ' điểm' + (d.matrix.length > 45 ? ' — nhiều điểm, nên chọn bảng từ 12 cm' : '') : 'Đang tạo mã…';
        },
        // The matrix comes from the server (studio/qr) whenever the content changes; debounced while typing.
        after: function (panel, patch) {
            var d = panel.d, ctx = panel.ctx, self = this;
            if (!patch.init && !QR_CONTENT.some(function (k) { return k in patch; })) return;
            clearTimeout(this.timer);
            this.timer = setTimeout(function () {
                var body = new URLSearchParams({
                    Type: d.qType, Text: d.qText || '', Ssid: d.ssid || '', Password: d.password || '', Security: d.security, Hidden: d.hidden ? 'true' : 'false',
                    Bank: d.bank || '', Account: d.account || '', Amount: String(d.amount || '').replace(/\D/g, ''), Ecc: d.icon ? 'H' : d.ecc
                });
                var seq = self.seq = (self.seq || 0) + 1;
                fetch(ctx.qrUrl, { method: 'POST', credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' }, body: body })
                    .then(function (res) { return res.json(); })
                    .then(function (json) {
                        if (seq !== self.seq) return;
                        d.matrix = json && json.rows ? json.rows : null;
                        d.qrError = json && json.error ? json.error : '';
                        Array.prototype.forEach.call(panel.el.querySelectorAll('[data-qr-status]'), function (el) { el.innerHTML = self.status(d); });
                        panel.onChange({});
                    }, function () {
                        d.qrError = 'Không tạo được mã, hãy thử lại.';
                        Array.prototype.forEach.call(panel.el.querySelectorAll('[data-qr-status]'), function (el) { el.innerHTML = self.status(d); });
                    });
            }, patch.init ? 0 : 350);
        },
        summary: function (d, P) {
            var content = {
                wifi: 'WiFi «' + d.ssid + '»' + (d.security === 'nopass' ? ', không mật khẩu' : ', mật khẩu «' + d.password + '» (' + d.security + ')') + (d.hidden ? ', WiFi ẩn' : ''),
                bank: 'Chuyển khoản ' + d.bank + ' – STK ' + d.account + (d.amount ? ', số tiền ' + d.amount : '') + (d.qText ? ', nội dung «' + d.qText + '»' : ''),
                zalo: 'Zalo ' + d.qText, phone: 'Gọi ' + d.qText, url: 'Link ' + d.qText, text: 'Văn bản «' + d.qText + '»'
            }[d.qType];
            var relief = d.qrRelief === 'flat' ? 'phẳng, nổi ' + mm(d.relief)
                : nameOf(QR_RELIEFS, d.qrRelief).toLowerCase() + ', cao ' + mm(d.qrHeight)
                    + (d.qrRelief === 'pyramid' || d.qrRelief === 'terrace' ? ', tối đa ' + d.tiers + ' bậc' : '')
                    + (d.qrRelief === 'river' ? ', ' + d.tiers + ' lớp, bờ ' + pct(d.bankWidth) + ' ô' : '')
                    + (d.multi ? ', nhiều tông màu (đổi màu theo tầng)' : ', một màu');
            var parts = ['Loại: ' + nameOf(this.types, d.type), 'Mã QR: ' + content, 'Kiểu 3D: ' + relief, 'Kiểu điểm: ' + nameOf(QR_DOTS, d.qrStyle).toLowerCase()
                + ', chịu lỗi ' + (d.icon ? 'H' : d.ecc) + ', cỡ mã ' + pct(d.qrScale)];
            if (d.icon) parts.push('Biểu tượng giữa: ' + P.iconOf(d.icon).name);
            parts.push('Chữ ' + nameOf([['bottom', 'dưới mã'], ['top', 'trên mã'], ['none', 'không có']], d.caption) + (d.line2 ? ', dòng nhỏ: ' + d.line2 : ''));
            parts.push('Phông: ' + P.fontOf(d.font).name, nameOf(STYLES, d.style) + (d.style === 'flush' ? '' : ' ' + mm(d.relief)));
            parts.push('Đế: ' + nameOf(P.SHAPES, d.shape) + ', dày ' + mm(d.thickness) + ', lề ' + mm(d.margin));
            if (d.border) parts.push('Viền nổi');
            if (d.hole !== 'none') parts.push('Lỗ: ' + nameOf(HOLES, d.hole).toLowerCase());
            if (d.stand) parts.push('Có chân đứng');
            return parts.join(' · ');
        },
        spec: function (d, row, ctx) {
            return Object.assign(plateFields(d), {
                kind: 'qr', board: null, text: d.caption === 'none' ? '' : row.text, line2: d.caption === 'none' ? '' : d.line2, length: ctx.plateLength(),
                base: ctx.color('base', row, '#ffffff'), color: ctx.color('text', row, '#20201f'),
                qr: {
                    rows: d.matrix || [], quiet: d.quiet, style: d.qrStyle, scale: d.qrScale, caption: d.caption,
                    relief3d: d.qrRelief, height: d.qrHeight, tiers: d.tiers, bank: d.bankWidth, multi: d.multi
                }
            });
        }
    };

    var KINDS = { nameplate: nameplate, classboard: classboard, keycap: keycap, qr: qr };

    // ---------- Panel ----------

    /**
     * Design panel of one product kind inside el. ctx: { P (renderer), plateLength(), color(role, row, fallback),
     * radio(titleRegex) → checked option text, pickRadio(titleRegex, test), mountText(slot) → puts the product's text
     * field into the quick design }. onChange(info) after every change;
     * info.reset when the view should go back to its home angle.
     */
    function Panel(el, kindKey, ctx, onChange) {
        var self = this;
        this.el = el;
        this.kind = KINDS[kindKey] || nameplate;
        this.ctx = ctx;
        this.onChange = onChange || function () { };
        this.tab = 0;
        this.d = this.kind.fresh(ctx.P, ctx);
        if (this.kind.after) this.kind.after(this, { init: true });

        function fmt(key) { var f = self.kind.formats && self.kind.formats[key]; return f ? function (v) { return f(v, ctx); } : key === 'textScale' || key === 'spacing' ? pct : /^(rows|groups|seats|am|pm)$/.test(key) ? String : mm; }

        el.addEventListener('click', function (e) {
            var t = e.target.closest('[data-tab]'), b = e.target.closest('[data-set]'), type = e.target.closest('[data-type]');
            if (t) { self.tab = +t.getAttribute('data-tab'); self.render(); return; }
            if (e.target.closest('[data-adv]')) { self.adv = !self.adv; self.render(); return; }
            if (type) {
                var preset = self.kind.types.filter(function (x) { return x.key === type.getAttribute('data-type'); })[0];
                if (self.kind.apply) self.kind.apply(preset, ctx);
                self.set(Object.assign({ type: preset.key }, preset.set), true, { reset: true });
                return;
            }
            if (b) {
                var key = b.getAttribute('data-set'), val = b.getAttribute('data-val'), patch = {};
                patch[key] = val === 'true' ? true : val === 'false' ? false : b.hasAttribute('data-num') ? +val : val;
                if (self.kind.onSet) self.kind.onSet(key, val, patch, self.d, ctx);
                self.set(patch, true);
                return;
            }
            var action = e.target.closest('[data-action]');
            if (action) { self.onChange({ view: action.getAttribute('data-action') }); return; }
            if (e.target.closest('[data-design-reset]')) { self.d = self.kind.fresh(ctx.P, ctx); self.tab = 0; self.set({ init: true }, true, { reset: true }); delete self.d.init; }
        });
        el.addEventListener('input', function (e) {
            var r = e.target.getAttribute('data-range'), tx = e.target.getAttribute('data-text'), patch = {};
            if (r) {
                patch[r] = parseFloat(e.target.value);
                var out = el.querySelector('[data-out="' + r + '"]');
                if (out) out.textContent = fmt(r)(patch[r]);
                self.set(patch, false);
            } else if (tx) {
                patch[tx] = e.target.value;
                self.set(patch, false);
                var count = el.querySelector('[data-seat-count]');
                if (count && tx === 'names') count.textContent = e.target.value.split(/\r?\n/).filter(function (n) { return n.trim(); }).length;
            } else if (e.target.hasAttribute('data-cell') && self.kind.cell) {
                self.kind.cell(self.d, e.target);
                self.set({}, false);
            }
        });
        el.addEventListener('change', function (e) {
            var c = e.target.getAttribute('data-check');
            if (c) { var patch = {}; patch[c] = e.target.checked; self.set(patch, false); }
            else if (e.target.hasAttribute('data-redraw')) self.render();
        });
        el.addEventListener('paste', function (e) {
            if (!e.target.hasAttribute('data-cell') || !self.kind.paste) return;
            var text = (e.clipboardData || window.clipboardData).getData('text') || '';
            if (self.kind.paste(self.d, e.target, text)) { e.preventDefault(); self.set({}, true); }
        });
        // The panel sits inside the product form: Enter must not submit it.
        el.addEventListener('keydown', function (e) { if (e.key === 'Enter' && e.target.matches('input')) e.preventDefault(); });
    }

    Panel.prototype.set = function (patch, redraw, info) {
        Object.assign(this.d, patch);
        delete this.d.init;
        if (this.kind.normalize) this.kind.normalize(this.d);
        if (this.kind.after) this.kind.after(this, patch);
        if (redraw) this.render();
        this.onChange(info || {});
    };

    Panel.prototype.render = function () {
        var el = this.el, P = this.ctx.P, kind = this.kind, d = this.d, tab = this.tab;
        var active = document.activeElement && el.contains(document.activeElement) ? document.activeElement : null;
        var focusSel = active && (active.hasAttribute('data-np-quick') ? '[data-np-quick]' : active.getAttribute('data-text') ? '[data-text="' + active.getAttribute('data-text') + '"]' : active.getAttribute('data-cell') ? '[data-cell="' + active.getAttribute('data-cell') + '"]' : null);
        var tabs = '<div class="tt-np-tabs" role="tablist">'
            + kind.tabs.map(function (t, i) { return '<button type="button" role="tab" data-tab="' + i + '" aria-selected="' + (tab === i) + '">' + t + '</button>'; }).join('')
            + '</div><div class="tt-np-pane">' + kind.pane(tab, d, P, this.ctx) + '</div>';
        // Kinds with a quick design show it first; their tabs fold away under "Tuỳ chỉnh nâng cao".
        if (kind.quick && kind.advancedFor && !kind.advancedFor(d)) {
            tabs = '<div class="tt-np-quick">' + kind.quick(d, P, this.ctx) + '</div>';
        } else if (kind.quick) {
            tabs = '<div class="tt-np-quick">' + kind.quick(d, P, this.ctx) + '</div>'
                + '<div class="tt-np-adv' + (this.adv ? ' is-open' : '') + '"><button type="button" class="tt-np-adv-btn" data-adv aria-expanded="' + !!this.adv + '">'
                + '<b>Tuỳ chỉnh nâng cao</b><small>' + (this.adv ? 'Không bắt buộc — để nguyên là studio làm theo mẫu chuẩn' : kind.advanced) + '</small><i aria-hidden="true"></i></button>'
                + (this.adv ? '<div class="tt-np-adv-body">' + tabs + '</div>' : '') + '</div>';
        }
        el.innerHTML = tabs
            + '<div class="tt-np-panel-foot"><button type="button" class="tt-textlink" data-design-reset>Đặt lại thiết kế</button>'
            + '<span>Lựa chọn thiết kế được gửi kèm đơn hàng.</span></div>';
        Array.prototype.forEach.call(el.querySelectorAll('canvas[data-icon]'), function (cv) {
            var c = cv.getContext('2d'), icon = P.iconOf(cv.getAttribute('data-icon'));
            c.setTransform(36, 0, 0, 36, 4, 4);
            c.fillStyle = '#20201f';
            c.beginPath(); icon.draw(c); c.fill();
            if (icon.cut) { c.globalCompositeOperation = 'destination-out'; icon.cut(c); }
        });
        var slot = el.querySelector('[data-np-text]');
        if (slot && this.ctx.mountText) this.ctx.mountText(slot);
        if (focusSel) {
            var f = el.querySelector(focusSel);
            if (f) { f.focus(); if (f.setSelectionRange) f.setSelectionRange(f.value.length, f.value.length); }
        }
    };

    /** Live labels that depend on the page (e.g. the plate height in mm follows the length). */
    Panel.prototype.refresh = function () {
        var out = this.el.querySelector('[data-out="heightPct"]'), f = this.kind.formats && this.kind.formats.heightPct;
        if (out && f) out.textContent = f(this.d.heightPct, this.ctx);
    };

    Panel.prototype.spec = function (row) { return this.kind.spec(this.d, row, this.ctx); };
    Panel.prototype.summary = function () { return this.kind.summary(this.d, this.ctx.P, this.ctx); };

    window.TTDesigns = { Panel: Panel, kinds: KINDS };
})();

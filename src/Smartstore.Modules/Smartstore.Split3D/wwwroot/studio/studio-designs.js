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
        return omit(d, ['type', 'mode', 'days', 'am', 'pm', 'cells', 'rows', 'groups', 'seats', 'teacher', 'names', 'line', 'tileMode', 'spare',
            'qType', 'qText', 'ssid', 'password', 'security', 'hidden', 'bank', 'account', 'amount', 'ecc', 'qrStyle', 'quiet', 'qrScale', 'caption', 'matrix', 'qrError']);
    }

    function plateSummary(d, P, L) {
        return 'Đế: ' + nameOf(P.SHAPES, d.shape) + ', cao ' + mm(L * d.heightPct / 100) + ', dày ' + mm(d.thickness) + ', lề ' + mm(d.margin)
            + (d.shape === 'rounded' || d.shape === 'tag' ? ', bo góc ' + mm(d.radius) : '');
    }

    // ---------- Name plate ----------

    var nameplate = {
        item: 'bảng tên',
        empty: 'Nhập tên muốn in để xem trước 3D',
        tabs: ['Loại', 'Chữ', 'Đế', 'Thêm'],
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
            if (i === 0) return ui.types(this.types, d) + ui.note('Chọn loại để có sẵn kiểu đế phù hợp, sau đó chỉnh tiếp ở các tab Chữ · Đế · Thêm.');
            if (i === 1) {
                return ui.text('line2', 'Dòng chữ thứ 2', d.line2, 'VD: Trưởng phòng kinh doanh', 40, '(chức vụ, số điện thoại… không bắt buộc)')
                    + ui.label('Phông chữ') + ui.fonts(P, d, 'nameplate')
                    + ui.label('Kiểu chữ') + ui.seg('style', STYLES, d.style)
                    + ui.grid(ui.slider('relief', d.style === 'engraved' ? 'Độ sâu' : 'Độ nổi', 0.6, 3, 0.2, d.relief, mm)
                        + ui.slider('textScale', 'Cỡ chữ', 0.5, 1, 0.05, d.textScale, pct)
                        + ui.slider('spacing', 'Giãn chữ', -0.05, 0.4, 0.01, d.spacing, pct)
                        + ui.check('upper', 'VIẾT HOA TOÀN BỘ', d.upper));
            }
            if (i === 2) {
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
    var BOARD_SHAPES = ['rounded', 'rect', 'pill', 'oval'];
    // Tile choice of the product (priced option "Kiểu ô"): text printed in place or removable tiles.
    var TILE_MODES = [['fixed', 'Chữ in liền'], ['press', 'Ô rời – khớp ấn'], ['magnet', 'Ô rời – nam châm']];
    var TILE_RE = /^kieu o$/;
    var TILE_COLORS = [['#ffffff', 'Trắng'], ['#20201f', 'Đen'], ['#ff67bc', 'Hồng'], ['#80e5cb', 'Mint'], ['#ffe348', 'Vàng']];
    function tileModeOf(text) { var n = norm(text); return /nam cham/.test(n) ? 'magnet' : /khop an|roi/.test(n) ? 'press' : 'fixed'; }
    function tileMode(d, ctx) { var r = ctx.radio(TILE_RE); return r ? tileModeOf(r) : d.tileMode; }

    var classboard = {
        item: 'bảng',
        empty: 'Nhập tên lớp / tiêu đề để xem trước 3D',
        tabs: ['Loại', 'Nội dung', 'Chữ', 'Đế'],
        types: [
            { key: 'tkb', name: 'Thời khoá biểu', hint: 'Treo tường, 2 lỗ treo', set: { mode: 'timetable', stand: false, hole: 'top2', border: true, heightPct: 70 } },
            { key: 'sodo', name: 'Sơ đồ lớp', hint: 'Chỗ ngồi học sinh, treo tường', set: { mode: 'seating', stand: false, hole: 'top2', border: true, heightPct: 70 } },
            { key: 'tkb-desk', name: 'Thời khoá biểu để bàn', hint: 'Có chân đứng', set: { mode: 'timetable', stand: true, hole: 'none', border: true, heightPct: 62 } },
            { key: 'sodo-desk', name: 'Sơ đồ lớp để bàn GV', hint: 'Có chân đứng', set: { mode: 'seating', stand: true, hole: 'none', border: true, heightPct: 62 } }
        ],
        fresh: function (P) {
            var d = Object.assign(omit(P.DEFAULTS, ['text', 'base', 'color', 'length', 'board']), {
                kind: 'classboard', type: 'tkb', font: 'be', margin: 6, thickness: 3, shape: 'rounded', radius: 4, relief: 1, style: 'raised',
                mode: 'timetable', days: 6, am: 5, pm: 0, cells: { am: [], pm: [] },
                rows: 5, groups: 4, seats: 2, teacher: 'left', names: '', line: 0.8,
                tileMode: 'fixed', tileColor: '#ffffff', spare: '', explode: false
            }, this.types[0].set);
            this.normalize(d);
            return d;
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
        // Board kind or stand changed outside the presets: the type follows, so the summary names the right board.
        onSet: function (key, val, patch, d, ctx) {
            // The tile choice is a priced product option: pick it there (the price follows).
            if (key === 'tileMode') ctx.pickRadio(TILE_RE, function (text) { return tileModeOf(text) === val; });
            if (key !== 'mode' && key !== 'stand') return;
            var mode = key === 'mode' ? val : d.mode, stand = key === 'stand' ? patch.stand : d.stand;
            patch.type = (mode === 'seating' ? 'sodo' : 'tkb') + (stand ? '-desk' : '');
        },
        pane: function (i, d, P, ctx) {
            var f = this.formats;
            if (i === 0) return ui.types(this.types, d) + ui.note('Một sản phẩm cho cả hai: bảng <b>thời khoá biểu</b> hoặc <b>sơ đồ chỗ ngồi</b> của lớp. Tiêu đề (VD: Lớp 6A1) nhập ở ô bên cạnh giá; nội dung bảng nhập ở tab <b>Nội dung</b>.');
            if (i === 1) {
                var tm = tileMode(d, ctx);
                var html = ui.label('Loại bảng') + ui.seg('mode', [['timetable', 'Thời khoá biểu'], ['seating', 'Sơ đồ lớp']], d.mode)
                    + ui.label('Kiểu ô') + ui.seg('tileMode', TILE_MODES, tm)
                    + (tm === 'fixed' ? ui.note('Chữ in liền vào bảng. Muốn đổi môn / đổi chỗ được thì chọn <b>ô rời</b>: mỗi ô là một miếng vuông riêng có chữ, cắm vào hốc trên bảng, rút ra lắp lại được (giá theo lựa chọn Kiểu ô).')
                        : ui.label('Màu ô rời') + ui.seg('tileColor', TILE_COLORS, d.tileColor)
                            + ui.grid(ui.check('explode', 'Xem các ô tách khỏi bảng', d.explode))
                            + ui.area('spare', 'Ô thêm để thay đổi', d.spare, 'Tin học\nÂm nhạc\n…', 3, '(mỗi dòng 1 ô, in kèm để thay khi đổi môn / đổi chỗ; không bắt buộc)'))
                    + ui.text('line2', 'Dòng phụ dưới tiêu đề', d.line2, 'VD: Năm học 2026 – 2027 · GVCN: Cô Lan', 70, '(không bắt buộc)');
                if (d.mode === 'seating') {
                    var seats = d.rows * d.groups * d.seats, filled = String(d.names || '').split(/\r?\n/).filter(function (n) { return n.trim(); }).length;
                    return html + ui.grid(ui.slider('rows', 'Số hàng bàn', 1, 8, 1, d.rows, String)
                            + ui.slider('groups', 'Số dãy bàn', 1, 5, 1, d.groups, String)
                            + ui.slider('seats', 'Chỗ mỗi bàn', 1, 3, 1, d.seats, String))
                        + ui.label('Bàn giáo viên') + ui.seg('teacher', TEACHER, d.teacher)
                        + ui.area('names', 'Danh sách học sinh', d.names, 'Nguyễn Văn An\nTrần Thị Bình\n…', 8,
                            '(mỗi dòng 1 bạn, theo thứ tự từ bàn đầu, trái sang phải; dòng trống = chỗ trống)')
                        + ui.note('<b data-seat-count>' + filled + '</b> học sinh · ' + seats + ' chỗ. Có thể copy cả cột tên trong Excel rồi dán vào ô trên.');
                }
                html += ui.label('Số ngày') + ui.seg('days', DAYS, d.days, true)
                    + ui.grid(ui.slider('am', 'Số tiết buổi sáng', 0, 5, 1, d.am, String, true) + ui.slider('pm', 'Số tiết buổi chiều', 0, 5, 1, d.pm, String, true));
                ['am', 'pm'].forEach(function (s) {
                    if (!d[s]) return;
                    html += ui.label(s === 'am' ? 'Buổi sáng' : 'Buổi chiều') + '<div class="tt-np-tablewrap"><table class="tt-np-table"><thead><tr><th>Tiết</th>'
                        + DAY_NAMES.slice(0, d.days).map(function (n) { return '<th>' + n + '</th>'; }).join('') + '</tr></thead><tbody>'
                        + d.cells[s].map(function (row, r) {
                            return '<tr><th>' + (r + 1) + '</th>' + row.slice(0, d.days).map(function (v, c) {
                                return '<td><input type="text" class="skip-pd-ajax-update" maxlength="24" data-cell="' + s + '|' + r + '|' + c + '" value="' + esc(v) + '" aria-label="' + DAY_NAMES[c] + ' tiết ' + (r + 1) + '" /></td>';
                            }).join('') + '</tr>';
                        }).join('') + '</tbody></table></div>';
                });
                return html + ui.note('Gõ tên môn vào từng ô, hoặc copy cả bảng trong Excel rồi dán vào ô đầu tiên.');
            }
            if (i === 2) {
                return ui.label('Phông chữ') + ui.fonts(P, d, 'classboard')
                    + ui.label('Kiểu chữ & đường kẻ') + ui.seg('style', STYLES, d.style)
                    + ui.grid(ui.slider('relief', d.style === 'engraved' ? 'Độ sâu' : 'Độ nổi', 0.4, 2.4, 0.2, d.relief, mm)
                        + ui.slider('line', 'Độ dày đường kẻ', 0.4, 2, 0.1, d.line, mm)
                        + ui.slider('textScale', 'Cỡ chữ trong ô', 0.5, 1, 0.05, d.textScale, pct)
                        + ui.check('upper', 'VIẾT HOA TOÀN BỘ', d.upper));
            }
            return ui.label('Hình dạng đế') + ui.seg('shape', P.SHAPES.filter(function (s) { return BOARD_SHAPES.indexOf(s.key) >= 0; }).map(function (s) { return [s.key, s.name]; }), d.shape)
                + ui.grid(ui.slider('heightPct', 'Chiều cao bảng', 40, 100, 1, d.heightPct, function (v) { return f.heightPct(v, ctx); })
                    + ui.slider('margin', 'Lề quanh nội dung', 3, 15, 0.5, d.margin, mm)
                    + ui.slider('thickness', 'Độ dày đế', 2, 6, 0.5, d.thickness, mm)
                    + (d.shape === 'rounded' ? ui.slider('radius', 'Bo góc', 0, 20, 0.5, d.radius, mm) : '')
                    + ui.check('border', 'Viền nổi quanh bảng', d.border))
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
            var parts = ['Loại: ' + nameOf(this.types, d.type)];
            if (d.line2) parts.push('Dòng phụ: ' + d.line2);
            parts.push('Phông: ' + P.fontOf(d.font).name + (d.upper ? ' (IN HOA)' : ''), nameOf(STYLES, d.style) + (d.style === 'flush' ? '' : ' ' + mm(d.relief)) + ', kẻ ' + mm(d.line));
            parts.push(plateSummary(d, P, ctx.plateLength()));
            if (d.border) parts.push('Viền nổi');
            if (d.hole !== 'none') parts.push('Lỗ treo: ' + nameOf(HOLES, d.hole).toLowerCase());
            if (d.stand) parts.push('Có chân đứng');
            var tm = tileMode(d, ctx);
            if (tm !== 'fixed') {
                parts.push('Ô rời ' + (tm === 'magnet' ? 'nam châm' : 'khớp ấn') + ', màu ô ' + nameOf(TILE_COLORS, d.tileColor));
                var spare = String(d.spare || '').split(/\r?\n/).map(function (t) { return t.trim(); }).filter(Boolean);
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
                ['am', 'pm'].forEach(function (s) {
                    if (!d[s]) return;
                    for (var c = 0; c < d.days; c++) {
                        lines.push((s === 'am' ? 'Sáng ' : 'Chiều ') + DAY_NAMES[c] + ': ' + d.cells[s].map(function (row) { return (row[c] || '').trim() || '–'; }).join(', '));
                    }
                });
            }
            return lines.join('\n');
        },
        spec: function (d, row, ctx) {
            return Object.assign(plateFields(d), {
                kind: 'classboard', text: row.text, length: ctx.plateLength(),
                base: ctx.color('base', row, '#ffffff'), color: ctx.color('text', row, '#20201f'),
                tileColor: d.tileColor, explode: !!d.explode && tileMode(d, ctx) !== 'fixed',
                board: { mode: d.mode, days: d.days, am: d.am, pm: d.pm, cells: d.cells, rows: d.rows, groups: d.groups, seats: d.seats, teacher: d.teacher, names: d.names, line: d.line, tiles: tileMode(d, ctx) !== 'fixed' }
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

    var qr = {
        item: 'bảng QR',
        empty: 'Nhập nội dung mã QR (tab Nội dung) để xem trước 3D',
        tabs: ['Loại', 'Nội dung', 'Mã QR', 'Chữ & đế'],
        types: [
            { key: 'wifi', name: 'Bảng WiFi để bàn', hint: 'Quét là vào mạng', set: { qType: 'wifi', stand: true, hole: 'none', shape: 'rounded' } },
            { key: 'bank', name: 'Bảng chuyển khoản', hint: 'VietQR mọi ngân hàng', set: { qType: 'bank', stand: true, hole: 'none', shape: 'rounded' } },
            { key: 'menu', name: 'Menu / website', hint: 'Link quán, fanpage', set: { qType: 'url', stand: true, hole: 'none', shape: 'rounded' } },
            { key: 'wall', name: 'Bảng treo / dán tường', hint: '2 lỗ treo', set: { qType: 'url', stand: false, hole: 'top2', shape: 'rounded' } },
            { key: 'keychain', name: 'Móc khoá QR', hint: 'Nhỏ, lỗ móc', set: { qType: 'zalo', stand: false, hole: 'left', shape: 'rounded', margin: 2.5 } }
        ],
        fresh: function (P) {
            return Object.assign(omit(P.DEFAULTS, ['text', 'base', 'color', 'length', 'board', 'qr']), {
                kind: 'qr', type: 'wifi', font: 'be', style: 'raised', relief: 0.8, margin: 4, thickness: 3, radius: 4, border: false,
                qType: 'wifi', qText: '', ssid: '', password: '', security: 'WPA', hidden: false, bank: '', account: '', amount: '',
                ecc: 'M', qrStyle: 'square', quiet: 1, qrScale: 1, caption: 'bottom', icon: '', matrix: null, qrError: ''
            }, this.types[0].set);
        },
        formats: { quiet: function (v) { return v + ' ô'; }, qrScale: pct },
        pane: function (i, d, P, ctx) {
            if (i === 0) return ui.types(this.types, d) + ui.note('Chọn loại rồi nhập nội dung ở tab <b>Nội dung</b>. Chữ hiện dưới mã (VD: Quét để kết nối WiFi) nhập ở ô cạnh giá.');
            var status = '<p class="tt-np-note tt-qr-status" data-qr-status>' + this.status(d) + '</p>';
            if (i === 1) {
                var f = ui.label('Mã QR chứa') + ui.seg('qType', QR_TYPES, d.qType);
                if (d.qType === 'wifi') {
                    f += ui.text('ssid', 'Tên WiFi', d.ssid, 'VD: TT Minimal', 32)
                        + ui.text('password', 'Mật khẩu', d.password, '', 63)
                        + ui.label('Bảo mật') + ui.seg('security', [['WPA', 'WPA / WPA2'], ['WEP', 'WEP'], ['nopass', 'Không mật khẩu']], d.security)
                        + ui.grid(ui.check('hidden', 'WiFi ẩn', d.hidden));
                } else if (d.qType === 'bank') {
                    f += ui.text('bank', 'Ngân hàng', d.bank, 'VD: MB, Vietcombank, Techcombank', 40)
                        + ui.text('account', 'Số tài khoản', d.account, '', 30)
                        + ui.text('amount', 'Số tiền', d.amount, 'Để trống: người quét tự nhập', 12, '(VNĐ, không bắt buộc)')
                        + ui.text('qText', 'Nội dung chuyển khoản', d.qText, 'Không bắt buộc', 40);
                } else if (d.qType === 'zalo' || d.qType === 'phone') {
                    f += ui.text('qText', 'Số điện thoại', d.qText, 'VD: 0333 424 766', 20);
                } else if (d.qType === 'url') {
                    f += ui.text('qText', 'Đường link', d.qText, 'VD: facebook.com/TT.minimal', 300);
                } else {
                    f += ui.area('qText', 'Nội dung', d.qText, '', 4);
                }
                return f + status + '<button type="button" class="tt-np-action" data-action="top">Nhìn thẳng để quét thử bằng điện thoại</button>'
                    + ui.note('Mã đậm trên nền sáng (chọn màu nền sáng, màu chữ đậm) thì điện thoại mới quét được.');
            }
            if (i === 2) {
                return ui.label('Kiểu điểm mã') + ui.seg('qrStyle', [['square', 'Vuông'], ['round', 'Bo tròn'], ['dots', 'Chấm tròn']], d.qrStyle)
                    + ui.label('Độ chịu lỗi') + ui.seg('ecc', [['L', 'Thấp – ít điểm'], ['M', 'Vừa'], ['Q', 'Khá'], ['H', 'Cao']], d.icon ? 'H' : d.ecc)
                    + ui.grid(ui.slider('qrScale', 'Cỡ mã trên bảng', 0.5, 1, 0.05, d.qrScale, pct) + ui.slider('quiet', 'Lề trắng quanh mã', 0, 4, 1, d.quiet, this.formats.quiet))
                    + ui.label('Biểu tượng giữa mã') + ui.icons(P, d)
                    + ui.note('Có biểu tượng giữa thì mã tự dùng độ chịu lỗi Cao (mã dày điểm hơn). Mã càng nhiều điểm thì bảng càng phải to.') + status;
            }
            return ui.label('Chữ') + ui.seg('caption', [['bottom', 'Dưới mã'], ['top', 'Trên mã'], ['none', 'Không chữ']], d.caption)
                + (d.caption === 'none' ? '' : ui.text('line2', 'Dòng chữ nhỏ', d.line2, 'VD: Mật khẩu: 12345678', 50, '(không bắt buộc)'))
                + ui.label('Phông chữ') + ui.fonts(P, d, 'qr')
                + ui.label('Kiểu in') + ui.seg('style', STYLES, d.style)
                + ui.grid(ui.slider('relief', d.style === 'engraved' ? 'Độ sâu' : 'Độ nổi', 0.4, 2, 0.2, d.relief, mm)
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
        summary: function (d, P, ctx) {
            var content = {
                wifi: 'WiFi «' + d.ssid + '»' + (d.security === 'nopass' ? ', không mật khẩu' : ', mật khẩu «' + d.password + '» (' + d.security + ')') + (d.hidden ? ', WiFi ẩn' : ''),
                bank: 'Chuyển khoản ' + d.bank + ' – STK ' + d.account + (d.amount ? ', số tiền ' + d.amount : '') + (d.qText ? ', nội dung «' + d.qText + '»' : ''),
                zalo: 'Zalo ' + d.qText, phone: 'Gọi ' + d.qText, url: 'Link ' + d.qText, text: 'Văn bản «' + d.qText + '»'
            }[d.qType];
            var parts = ['Loại: ' + nameOf(this.types, d.type), 'Mã QR: ' + content, 'Kiểu điểm: ' + nameOf([['square', 'vuông'], ['round', 'bo tròn'], ['dots', 'chấm tròn']], d.qrStyle)
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
                qr: { rows: d.matrix || [], quiet: d.quiet, style: d.qrStyle, scale: d.qrScale, caption: d.caption }
            });
        }
    };

    var KINDS = { nameplate: nameplate, classboard: classboard, keycap: keycap, qr: qr };

    // ---------- Panel ----------

    /**
     * Design panel of one product kind inside el. ctx: { P (renderer), plateLength(), color(role, row, fallback),
     * radio(titleRegex) → checked option text, pickRadio(titleRegex, test) }. onChange(info) after every change;
     * info.reset when the view should go back to its home angle.
     */
    function Panel(el, kindKey, ctx, onChange) {
        var self = this;
        this.el = el;
        this.kind = KINDS[kindKey] || nameplate;
        this.ctx = ctx;
        this.onChange = onChange || function () { };
        this.tab = 0;
        this.d = this.kind.fresh(ctx.P);
        if (this.kind.after) this.kind.after(this, { init: true });

        function fmt(key) { var f = self.kind.formats && self.kind.formats[key]; return f ? function (v) { return f(v, ctx); } : key === 'textScale' || key === 'spacing' ? pct : /^(rows|groups|seats|am|pm)$/.test(key) ? String : mm; }

        el.addEventListener('click', function (e) {
            var t = e.target.closest('[data-tab]'), b = e.target.closest('[data-set]'), type = e.target.closest('[data-type]');
            if (t) { self.tab = +t.getAttribute('data-tab'); self.render(); return; }
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
            if (e.target.closest('[data-design-reset]')) { self.d = self.kind.fresh(ctx.P); self.tab = 0; self.set({ init: true }, true, { reset: true }); delete self.d.init; }
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
        var focusSel = active && (active.getAttribute('data-text') ? '[data-text="' + active.getAttribute('data-text') + '"]' : active.getAttribute('data-cell') ? '[data-cell="' + active.getAttribute('data-cell') + '"]' : null);
        el.innerHTML = '<div class="tt-np-tabs" role="tablist">'
            + kind.tabs.map(function (t, i) { return '<button type="button" role="tab" data-tab="' + i + '" aria-selected="' + (tab === i) + '">' + t + '</button>'; }).join('')
            + '</div><div class="tt-np-pane">' + kind.pane(tab, d, P, this.ctx) + '</div>'
            + '<div class="tt-np-panel-foot"><button type="button" class="tt-textlink" data-design-reset>Đặt lại thiết kế</button>'
            + '<span>Lựa chọn thiết kế được gửi kèm đơn hàng.</span></div>';
        Array.prototype.forEach.call(el.querySelectorAll('canvas[data-icon]'), function (cv) {
            var c = cv.getContext('2d'), icon = P.iconOf(cv.getAttribute('data-icon'));
            c.setTransform(36, 0, 0, 36, 4, 4);
            c.fillStyle = '#20201f';
            c.beginPath(); icon.draw(c); c.fill();
            if (icon.cut) { c.globalCompositeOperation = 'destination-out'; icon.cut(c); }
        });
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

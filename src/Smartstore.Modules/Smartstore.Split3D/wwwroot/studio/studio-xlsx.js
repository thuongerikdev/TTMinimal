/* TT Minimal — minimal Excel (.xlsx) reader and writer for the name list import. Loaded on demand by studio.js.
   Reads the first worksheet into rows of strings; writes one sheet with a bold header and dropdown columns.
   Reading needs DecompressionStream (all current browsers); writing stores the files uncompressed. */
(function () {
    'use strict';

    // ---------- ZIP ----------

    var CRC_TABLE = (function () {
        var t = new Uint32Array(256);
        for (var n = 0; n < 256; n++) {
            var c = n;
            for (var k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1;
            t[n] = c >>> 0;
        }
        return t;
    })();

    function crc32(bytes) {
        var c = 0xFFFFFFFF;
        for (var i = 0; i < bytes.length; i++) c = CRC_TABLE[(c ^ bytes[i]) & 0xFF] ^ (c >>> 8);
        return (c ^ 0xFFFFFFFF) >>> 0;
    }

    function inflate(bytes) {
        if (typeof DecompressionStream !== 'function') {
            return Promise.reject(new Error('Trình duyệt này không đọc được file Excel — hãy cập nhật trình duyệt hoặc copy bảng rồi dán vào ô Nội dung.'));
        }
        var stream = new Blob([bytes]).stream().pipeThrough(new DecompressionStream('deflate-raw'));
        return new Response(stream).arrayBuffer().then(function (buf) { return new Uint8Array(buf); });
    }

    // Returns { 'path': Promise<Uint8Array> } for every entry, from the central directory.
    function unzip(buf) {
        var bytes = new Uint8Array(buf), view = new DataView(buf), eocd = -1;
        for (var i = bytes.length - 22; i >= Math.max(0, bytes.length - 65557); i--) {
            if (view.getUint32(i, true) === 0x06054b50) { eocd = i; break; }
        }
        if (eocd < 0) throw new Error('File không phải Excel .xlsx.');
        var count = view.getUint16(eocd + 10, true), p = view.getUint32(eocd + 16, true), files = {};
        var dec = new TextDecoder();
        for (var n = 0; n < count; n++) {
            if (view.getUint32(p, true) !== 0x02014b50) break;
            var method = view.getUint16(p + 10, true), size = view.getUint32(p + 20, true);
            var nameLen = view.getUint16(p + 28, true), extraLen = view.getUint16(p + 30, true), commentLen = view.getUint16(p + 32, true);
            var local = view.getUint32(p + 42, true), name = dec.decode(bytes.subarray(p + 46, p + 46 + nameLen));
            var start = local + 30 + view.getUint16(local + 26, true) + view.getUint16(local + 28, true);
            var data = bytes.subarray(start, start + size);
            files[name] = { method: method, data: data };
            p += 46 + nameLen + extraLen + commentLen;
        }
        return {
            text: function (name) {
                var f = files[name];
                if (!f) return Promise.resolve(null);
                if (f.method !== 0 && f.method !== 8) return Promise.reject(new Error('File Excel dùng kiểu nén không hỗ trợ.'));
                return (f.method === 8 ? inflate(f.data) : Promise.resolve(f.data)).then(function (b) { return dec.decode(b); });
            }
        };
    }

    function zip(entries) {
        var enc = new TextEncoder(), parts = [], central = [], offset = 0;
        entries.forEach(function (e) {
            var name = enc.encode(e.name), data = enc.encode(e.text), crc = crc32(data);
            var head = new DataView(new ArrayBuffer(30));
            head.setUint32(0, 0x04034b50, true); head.setUint16(4, 20, true); head.setUint16(6, 0x0800, true);
            head.setUint32(14, crc, true); head.setUint32(18, data.length, true); head.setUint32(22, data.length, true);
            head.setUint16(26, name.length, true);
            parts.push(head, name, data);
            var dir = new DataView(new ArrayBuffer(46));
            dir.setUint32(0, 0x02014b50, true); dir.setUint16(4, 20, true); dir.setUint16(6, 20, true); dir.setUint16(8, 0x0800, true);
            dir.setUint32(16, crc, true); dir.setUint32(20, data.length, true); dir.setUint32(24, data.length, true);
            dir.setUint16(28, name.length, true); dir.setUint32(42, offset, true);
            central.push(dir, name);
            offset += 30 + name.length + data.length;
        });
        var size = central.reduce(function (s, x) { return s + x.byteLength; }, 0);
        var end = new DataView(new ArrayBuffer(22));
        end.setUint32(0, 0x06054b50, true); end.setUint16(8, entries.length, true); end.setUint16(10, entries.length, true);
        end.setUint32(12, size, true); end.setUint32(16, offset, true);
        return new Blob(parts.concat(central, [end]), { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    }

    // ---------- read ----------

    function xml(text) { return new DOMParser().parseFromString(text, 'application/xml'); }
    function byTag(node, tag) { return Array.prototype.slice.call(node.getElementsByTagNameNS('*', tag)); }
    function textOf(node) { return byTag(node, 't').map(function (t) { return t.textContent; }).join(''); }

    // "BC12" -> 54 (zero-based column)
    function colIndex(ref) {
        var m = /^[A-Z]+/.exec(ref || ''), n = 0;
        if (!m) return -1;
        for (var i = 0; i < m[0].length; i++) n = n * 26 + m[0].charCodeAt(i) - 64;
        return n - 1;
    }

    function read(file) {
        return file.arrayBuffer().then(function (buf) {
            var z = unzip(buf);
            return Promise.all([z.text('xl/workbook.xml'), z.text('xl/_rels/workbook.xml.rels'), z.text('xl/sharedStrings.xml')]).then(function (r) {
                // First sheet of the workbook, via its relationship.
                var path = 'xl/worksheets/sheet1.xml';
                if (r[0] && r[1]) {
                    var sheet = byTag(xml(r[0]), 'sheet')[0], rid = sheet && (sheet.getAttribute('r:id') || sheet.getAttributeNS('http://schemas.openxmlformats.org/officeDocument/2006/relationships', 'id'));
                    byTag(xml(r[1]), 'Relationship').forEach(function (rel) {
                        if (rel.getAttribute('Id') !== rid) return;
                        var target = rel.getAttribute('Target').replace(/^\//, '');
                        path = target.indexOf('xl/') === 0 ? target : 'xl/' + target;
                    });
                }
                var shared = r[2] ? byTag(xml(r[2]), 'si').map(textOf) : [];
                return z.text(path).then(function (text) {
                    if (!text) throw new Error('Không tìm thấy trang tính trong file.');
                    return byTag(xml(text), 'row').map(function (row) {
                        var out = [];
                        byTag(row, 'c').forEach(function (c, i) {
                            var ci = colIndex(c.getAttribute('r')), type = c.getAttribute('t'), v = byTag(c, 'v')[0], val;
                            if (ci < 0) ci = i;
                            if (type === 's') val = v ? shared[parseInt(v.textContent, 10)] || '' : '';
                            else if (type === 'inlineStr') val = textOf(c);
                            else if (type === 'b') val = v && v.textContent === '1' ? 'TRUE' : 'FALSE';
                            else val = v ? v.textContent : '';
                            while (out.length < ci) out.push('');
                            out[ci] = val;
                        });
                        return out;
                    });
                });
            });
        });
    }

    // ---------- write ----------

    function esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;'); }
    function colName(i) { var s = ''; i++; while (i) { var m = (i - 1) % 26; s = String.fromCharCode(65 + m) + s; i = (i - m - 1) / 26; } return s; }

    // opts: { sheet, rows: [[...]], widths: [chars], lists: { colIndex: ['A', 'B'] }, listRows }
    // The first row is the bold, frozen header; `lists` become dropdowns below it.
    function write(opts) {
        var rows = opts.rows, last = opts.listRows || 500;
        var sheetXml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            + '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
            + '<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>'
            + '<cols>' + (opts.widths || []).map(function (w, i) { return '<col min="' + (i + 1) + '" max="' + (i + 1) + '" width="' + w + '" customWidth="1"/>'; }).join('') + '</cols>'
            + '<sheetData>' + rows.map(function (row, r) {
                return '<row r="' + (r + 1) + '">' + row.map(function (v, c) {
                    var ref = colName(c) + (r + 1), style = r === 0 ? ' s="1"' : '';
                    if (v === '' || v == null) return r === 0 ? '<c r="' + ref + '"' + style + '/>' : '';
                    if (typeof v === 'number') return '<c r="' + ref + '"' + style + '><v>' + v + '</v></c>';
                    return '<c r="' + ref + '" t="inlineStr"' + style + '><is><t xml:space="preserve">' + esc(v) + '</t></is></c>';
                }).join('') + '</row>';
            }).join('') + '</sheetData>';
        var lists = Object.keys(opts.lists || {});
        if (lists.length) {
            sheetXml += '<dataValidations count="' + lists.length + '">' + lists.map(function (c) {
                var col = colName(+c);
                return '<dataValidation type="list" allowBlank="1" showErrorMessage="0" sqref="' + col + '2:' + col + last + '">'
                    + '<formula1>"' + esc(opts.lists[c].join(',').replace(/"/g, '')) + '"</formula1></dataValidation>';
            }).join('') + '</dataValidations>';
        }
        sheetXml += '</worksheet>';

        return zip([
            { name: '[Content_Types].xml', text: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
                + '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/>'
                + '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'
                + '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>'
                + '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>' },
            { name: '_rels/.rels', text: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
                + '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>' },
            { name: 'xl/workbook.xml', text: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
                + '<sheets><sheet name="' + esc((opts.sheet || 'Sheet1').slice(0, 31)) + '" sheetId="1" r:id="rId1"/></sheets></workbook>' },
            { name: 'xl/_rels/workbook.xml.rels', text: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
                + '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>'
                + '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>' },
            { name: 'xl/styles.xml', text: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
                + '<fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>'
                + '<fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill>'
                + '<fill><patternFill patternType="solid"><fgColor rgb="FFFFE348"/><bgColor indexed="64"/></patternFill></fill></fills>'
                + '<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>'
                + '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>'
                + '<cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/></cellXfs>'
                + '<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>'
                + '</styleSheet>' },
            { name: 'xl/worksheets/sheet1.xml', text: sheetXml }
        ]);
    }

    window.TTXlsx = { read: read, write: write };
})();

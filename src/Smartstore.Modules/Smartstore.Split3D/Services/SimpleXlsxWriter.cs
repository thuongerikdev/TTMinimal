using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Minimal dependency-free XLSX writer: one sheet, bold frozen header row, auto filter,
/// strings and numbers. Enough for license reports without pulling in a spreadsheet library.
/// </summary>
public static partial class SimpleXlsxWriter
{
    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F]")]
    private static partial Regex InvalidXmlCharsRegex();

    public static byte[] Write(string sheetName, IReadOnlyList<string> headers, IEnumerable<object[]> rows, IReadOnlyList<double> columnWidths = null)
    {
        Guard.NotEmpty(sheetName);
        Guard.NotNull(headers);
        Guard.NotNull(rows);

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            AddEntry(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                "</Types>");

            AddEntry(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>");

            AddEntry(zip, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                $"<sheets><sheet name=\"{Escape(sheetName.Truncate(31))}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                "</workbook>");

            AddEntry(zip, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                "</Relationships>");

            // Style 0 = default, 1 = bold header with fill, 2 = thousands separator number.
            AddEntry(zip, "xl/styles.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font></fonts>" +
                "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF176B87\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
                "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"3\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>" +
                "<xf numFmtId=\"3\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs>" +
                "</styleSheet>");

            var sheet = new StringBuilder();
            sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sheet.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sheet.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");

            if (columnWidths?.Count > 0)
            {
                sheet.Append("<cols>");
                for (var i = 0; i < columnWidths.Count; i++)
                {
                    sheet.Append(CultureInfo.InvariantCulture, $"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{columnWidths[i]}\" customWidth=\"1\"/>");
                }
                sheet.Append("</cols>");
            }

            sheet.Append("<sheetData>");
            AppendRow(sheet, 1, headers.Cast<object>().ToArray(), headerStyle: true);

            var rowIndex = 1;
            foreach (var row in rows)
            {
                AppendRow(sheet, ++rowIndex, row, headerStyle: false);
            }

            sheet.Append("</sheetData>");
            sheet.Append(CultureInfo.InvariantCulture, $"<autoFilter ref=\"A1:{ColumnName(headers.Count)}{Math.Max(rowIndex, 1)}\"/>");
            sheet.Append("</worksheet>");

            AddEntry(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
        }

        return stream.ToArray();
    }

    private static void AppendRow(StringBuilder sb, int rowIndex, object[] values, bool headerStyle)
    {
        sb.Append(CultureInfo.InvariantCulture, $"<row r=\"{rowIndex}\">");

        for (var i = 0; i < values.Length; i++)
        {
            var reference = ColumnName(i + 1) + rowIndex.ToString(CultureInfo.InvariantCulture);
            var value = values[i];

            if (!headerStyle && value is decimal or int or long or double)
            {
                var number = Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                sb.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" s=\"2\"><v>{number}</v></c>");
            }
            else
            {
                var text = Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                sb.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" s=\"{(headerStyle ? 1 : 0)}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{text}</t></is></c>");
            }
        }

        sb.Append("</row>");
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        while (index > 0)
        {
            var remainder = (index - 1) % 26;
            name = (char)('A' + remainder) + name;
            index = (index - 1) / 26;
        }

        return name;
    }

    private static string Escape(string value)
        => SecurityElement.Escape(InvalidXmlCharsRegex().Replace(value, string.Empty));

    private static void AddEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}

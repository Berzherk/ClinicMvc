using ClinicMvc.Models;
using ClosedXML.Excel;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace ClinicMvc.Services;

/// <summary>
/// Ги генерира извозните датотеки за термини - Excel преку ClosedXML,
/// PDF преку PdfSharpCore. И двете враќаат готови бајтови кои контролерот
/// ги враќа директно како File резултат (без привремени датотеки на диск).
///
/// Забелешка за изборот на PDF библиотека: првично беше користен QuestPDF, кој
/// зависи од native SkiaSharp датотеки и предизвикуваше целосно паѓање на процесот
/// на некои Windows конфигурации. Потоа беше пробан iText7, кој прави background
/// мрежен повик за лиценца/телеметрија при секое генерирање - ако тој повик не
/// успее чисто (ограничена мрежа, firewall), исклучокот се случува на background
/// thread и го гаси целиот .NET процес (надвор од дофат на ASP.NET Core error handling).
/// PdfSharpCore е чиста managed библиотека, без native зависности и без каква било
/// мрежна комуникација - најбезбеден избор за овој сценарио.
/// </summary>
public class ExportService : IExportService
{
    public byte[] ExportAppointmentsToExcel(IEnumerable<Appointment> appointments)
    {
        using var workbook  = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Термини");

        string[] headers = { "Датум", "Време", "Пациент", "Лекар", "Специјалност", "Статус", "Белешки" };
        for (var i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#212529");
            worksheet.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }

        var row = 2;
        foreach (var a in appointments)
        {
            worksheet.Cell(row, 1).Value = a.AppointmentDate.ToString("dd.MM.yyyy");
            worksheet.Cell(row, 2).Value = a.AppointmentTime.ToString(@"hh\:mm");
            worksheet.Cell(row, 3).Value = a.PatientName;
            worksheet.Cell(row, 4).Value = a.DoctorName;
            worksheet.Cell(row, 5).Value = a.DoctorSpecialty;
            worksheet.Cell(row, 6).Value = a.Status;
            worksheet.Cell(row, 7).Value = a.Notes;
            row++;
        }

        // Фиксни, разумни ширини по колона - AdjustToContents() ја прави Белешки колоната
        // бескрајно широка ако некоја белешка е долга. Наместо тоа, Белешки колоната
        // е ограничена и текстот се пренесува во нов ред (WrapText) во таа ќелија.
        worksheet.Column(1).Width = 12; // Датум
        worksheet.Column(2).Width = 8;  // Време
        worksheet.Column(3).Width = 20; // Пациент
        worksheet.Column(4).Width = 20; // Лекар
        worksheet.Column(5).Width = 16; // Специјалност
        worksheet.Column(6).Width = 12; // Статус
        worksheet.Column(7).Width = 45; // Белешки - фиксна ширина + wrap наместо бескрајно раширување

        var dataRange = worksheet.Range(1, 1, row - 1, headers.Length);
        dataRange.Style.Alignment.WrapText = true;
        dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        worksheet.Rows().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportAppointmentsToPdf(IEnumerable<Appointment> appointments)
    {
        var document = new PdfDocument();

        var titleFont  = new XFont("Arial", 16, XFontStyle.Bold);
        var headerFont = new XFont("Arial", 10, XFontStyle.Bold);
        var cellFont   = new XFont("Arial", 9, XFontStyle.Regular);
        var footerFont = new XFont("Arial", 7, XFontStyle.Regular);

        double margin = 30;
        const double lineHeight = 12;
        const double minRowHeight = 20;
        const double cellPadding = 3;

        string[] headers   = { "Датум", "Време", "Пациент", "Лекар", "Специјалност", "Статус", "Белешки" };
        double[] colWidths = { 0.10, 0.08, 0.15, 0.15, 0.14, 0.10, 0.28 };

        // Ги дели зборовите на редови кои се вклопуваат во дадена ширина - спречува
        // текстот (особено долги белешки) да истече надвор од страницата.
        List<string> WrapText(XGraphics gfx, string text, XFont font, double maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                lines.Add(string.Empty);
                return lines;
            }

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var currentLine = string.Empty;

            foreach (var word in words)
            {
                var testLine = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                var width = gfx.MeasureString(testLine, font).Width;

                if (width > maxWidth && currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine);
            }

            return lines.Count == 0 ? new List<string> { string.Empty } : lines;
        }

        // Креира нова страница и враќа сè потребно за цртање на неа
        (PdfPage Page, XGraphics Graphics, double PageWidth, double[] ColX) NewPage()
        {
            var newPage = document.AddPage();
            newPage.Orientation = PdfSharpCore.PageOrientation.Landscape;
            newPage.Size = PdfSharpCore.PageSize.A4;

            var gfx = XGraphics.FromPdfPage(newPage);
            var pw = newPage.Width - (2 * margin);

            var cols = new double[headers.Length];
            var cx = margin;
            for (var i = 0; i < headers.Length; i++)
            {
                cols[i] = cx;
                cx += pw * colWidths[i];
            }

            return (newPage, gfx, pw, cols);
        }

        void DrawHeaderRow(XGraphics gfx, double headerY, double pageWidth, double[] colX)
        {
            gfx.DrawRectangle(XBrushes.Black, margin, headerY, pageWidth, minRowHeight);
            for (var i = 0; i < headers.Length; i++)
            {
                gfx.DrawString(headers[i], headerFont, XBrushes.White,
                    new XRect(colX[i] + cellPadding, headerY + cellPadding, pageWidth * colWidths[i] - (2 * cellPadding), minRowHeight - (2 * cellPadding)),
                    XStringFormats.TopLeft);
            }
        }

        var (page, graphics, pageWidth, colX) = NewPage();
        double y = margin;

        // Наслов - само на првата страница
        graphics.DrawString("Листа на термини", titleFont, XBrushes.Black,
            new XRect(margin, y, pageWidth, 25), XStringFormats.TopLeft);
        y += 35;

        DrawHeaderRow(graphics, y, pageWidth, colX);
        y += minRowHeight;

        foreach (var a in appointments)
        {
            string[] values =
            {
                a.AppointmentDate.ToString("dd.MM.yyyy"),
                a.AppointmentTime.ToString(@"hh\:mm"),
                a.PatientName ?? "",
                a.DoctorName ?? "",
                a.DoctorSpecialty ?? "",
                a.Status,
                a.Notes ?? ""
            };

            // Секоја колона се пренесува во редови (wrap) според сопствената ширина -
            // висината на редот на табелата е онолку колку што бара НАЈвисоката колона
            var wrappedColumns = new List<string>[values.Length];
            var maxLines = 1;
            for (var i = 0; i < values.Length; i++)
            {
                var colWidth = pageWidth * colWidths[i] - (2 * cellPadding);
                wrappedColumns[i] = WrapText(graphics, values[i], cellFont, colWidth);
                maxLines = Math.Max(maxLines, wrappedColumns[i].Count);
            }

            var rowHeight = Math.Max(minRowHeight, (maxLines * lineHeight) + (2 * cellPadding));

            // Ако нема простор до дното на страницата, отвори нова страница со ново заглавие
            if (y + rowHeight > page.Height - margin)
            {
                graphics.Dispose();
                (page, graphics, pageWidth, colX) = NewPage();
                y = margin;
                DrawHeaderRow(graphics, y, pageWidth, colX);
                y += minRowHeight;

                // Пресметката на wrap зависи од graphics инстанцата - повтори ја за новата страница
                maxLines = 1;
                for (var i = 0; i < values.Length; i++)
                {
                    var colWidth = pageWidth * colWidths[i] - (2 * cellPadding);
                    wrappedColumns[i] = WrapText(graphics, values[i], cellFont, colWidth);
                    maxLines = Math.Max(maxLines, wrappedColumns[i].Count);
                }
                rowHeight = Math.Max(minRowHeight, (maxLines * lineHeight) + (2 * cellPadding));
            }

            graphics.DrawRectangle(XPens.LightGray, XBrushes.White, margin, y, pageWidth, rowHeight);

            for (var i = 0; i < values.Length; i++)
            {
                var lineY = y + cellPadding;
                foreach (var line in wrappedColumns[i])
                {
                    graphics.DrawString(line, cellFont, XBrushes.Black,
                        new XRect(colX[i] + cellPadding, lineY, pageWidth * colWidths[i] - (2 * cellPadding), lineHeight),
                        XStringFormats.TopLeft);
                    lineY += lineHeight;
                }
            }

            y += rowHeight;
        }

        graphics.DrawString($"Генерирано на {DateTime.Now:dd.MM.yyyy HH:mm}", footerFont, XBrushes.Gray,
            new XRect(margin, page.Height - margin + 5, pageWidth, 15), XStringFormats.TopLeft);
        graphics.Dispose();

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }
}

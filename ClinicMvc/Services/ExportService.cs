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

        worksheet.Columns().AdjustToContents();

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
        const double rowHeight = 20;

        string[] headers   = { "Датум", "Време", "Пациент", "Лекар", "Специјалност", "Статус", "Белешки" };
        double[] colWidths = { 0.10, 0.08, 0.16, 0.16, 0.15, 0.10, 0.25 };

        // Креира нова страница и враќа сè потребно за цртање на неа (страница, графика, координати на колони)
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
            gfx.DrawRectangle(XBrushes.Black, margin, headerY, pageWidth, rowHeight);
            for (var i = 0; i < headers.Length; i++)
            {
                gfx.DrawString(headers[i], headerFont, XBrushes.White,
                    new XRect(colX[i] + 3, headerY + 3, pageWidth * colWidths[i] - 6, rowHeight - 6),
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
        y += rowHeight;

        foreach (var a in appointments)
        {
            // Ако нема простор до дното, затвори ја тековната графика и отвори нова страница
            if (y + rowHeight > page.Height - margin)
            {
                graphics.Dispose();
                (page, graphics, pageWidth, colX) = NewPage();
                y = margin;
                DrawHeaderRow(graphics, y, pageWidth, colX);
                y += rowHeight;
            }

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

            graphics.DrawRectangle(XPens.LightGray, XBrushes.White, margin, y, pageWidth, rowHeight);
            for (var i = 0; i < values.Length; i++)
            {
                graphics.DrawString(values[i], cellFont, XBrushes.Black,
                    new XRect(colX[i] + 3, y + 3, pageWidth * colWidths[i] - 6, rowHeight - 6),
                    XStringFormats.TopLeft);
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

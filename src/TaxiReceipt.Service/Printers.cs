using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.Versioning;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace TaxiReceipt.Service;

/// <summary>The slip the original printed with Stimulsoft: 8 x 4 cm, the person's full name.</summary>
public static class ReceiptLayout
{
    public const float WidthMm = 80;
    public const float HeightMm = 40;
    public const string Font = "Tahoma"; // ships with Windows and shapes Persian text

    public static string PersianDate(DateTime d)
    {
        var pc = new System.Globalization.PersianCalendar();
        return $"{pc.GetYear(d):0000}/{pc.GetMonth(d):00}/{pc.GetDayOfMonth(d):00} {d:HH:mm}";
    }
}

/// <summary>Writes each receipt as a PDF into a folder (a print queue, an archive, or a demo).</summary>
public sealed class PdfReceiptPrinter(ReceiptOptions options) : IReceiptPrinter
{
    static PdfReceiptPrinter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // QuestPDF only uses fonts it is given. Tahoma (regular + bold) comes
        // with every Windows install, so it's registered from there rather
        // than shipping a font file.
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var file in new[] { "tahoma.ttf", "tahomabd.ttf" })
        {
            var path = Path.Combine(fonts, file);
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                FontManager.RegisterFontFromStream(stream);
            }
        }
    }

    public Task PrintAsync(Punch punch, DateOnly night, CancellationToken ct)
    {
        Directory.CreateDirectory(options.PdfFolder);
        var file = Path.Combine(options.PdfFolder, $"{night:yyyy-MM-dd}_{punch.PersonId}_{punch.Id}.pdf");
        // Rendered to a temp file and moved: a failed render leaves no empty PDF.
        var tmp = file + ".tmp";
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(ReceiptLayout.WidthMm, ReceiptLayout.HeightMm, Unit.Millimetre);
            page.Margin(3, Unit.Millimetre);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(t => t.FontFamily(ReceiptLayout.Font).FontSize(9));
            page.Content().Column(col =>
            {
                col.Item().AlignCenter().Text(options.Title).FontSize(8).FontColor(Colors.Grey.Darken2);
                col.Item().PaddingVertical(2, Unit.Millimetre).AlignCenter().Text(punch.FullName).FontSize(14).Bold();
                col.Item().AlignCenter().Text(ReceiptLayout.PersianDate(punch.PunchTime)).FontSize(8);
            });
        })).GeneratePdf(tmp);
        File.Move(tmp, file, overwrite: true);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Sends the slip straight to a Windows printer. The printer has to be
/// installed for the account the service runs as (LocalSystem doesn't see a
/// user's own printers), and Microsoft doesn't support System.Drawing printing
/// from a service — it works with a local printer, but "Pdf" is the safe default.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsReceiptPrinter(ReceiptOptions options) : IReceiptPrinter
{
    public Task PrintAsync(Punch punch, DateOnly night, CancellationToken ct)
    {
        using var doc = new PrintDocument();
        if (!string.IsNullOrWhiteSpace(options.PrinterName)) doc.PrinterSettings.PrinterName = options.PrinterName;
        if (!doc.PrinterSettings.IsValid) throw new InvalidOperationException($"Printer not found: {options.PrinterName}");
        doc.DocumentName = $"TaxiReceipt {punch.Id}";
        doc.PrintController = new StandardPrintController(); // no "Printing..." dialog
        // Hundredths of an inch.
        doc.DefaultPageSettings.PaperSize = new PaperSize("Receipt 80x40", 315, 157);
        doc.DefaultPageSettings.Margins = new Margins(10, 10, 10, 10);
        doc.PrintPage += (_, e) =>
        {
            var g = e.Graphics!;
            var area = e.MarginBounds;
            var rtl = new StringFormat(StringFormatFlags.DirectionRightToLeft) { Alignment = StringAlignment.Center };
            using var small = new Font(ReceiptLayout.Font, 7);
            using var big = new Font(ReceiptLayout.Font, 13, FontStyle.Bold);
            g.DrawString(options.Title, small, Brushes.Black, new RectangleF(area.X, area.Y, area.Width, 25), rtl);
            g.DrawString(punch.FullName, big, Brushes.Black, new RectangleF(area.X, area.Y + 35, area.Width, 50), rtl);
            g.DrawString(ReceiptLayout.PersianDate(punch.PunchTime), small, Brushes.Black,
                new RectangleF(area.X, area.Bottom - 25, area.Width, 25), rtl);
        };
        doc.Print();
        return Task.CompletedTask;
    }
}

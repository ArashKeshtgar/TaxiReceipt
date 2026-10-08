using System.ComponentModel.DataAnnotations;

namespace TaxiReceipt.Service;

/// <summary>Settings from the "TaxiReceipt" section of appsettings.json.</summary>
public sealed class ReceiptOptions
{
    public const string Section = "TaxiReceipt";

    /// <summary>The attendance-clock database (punch tables + Persons).</summary>
    [Required]
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// The clock system writes one punch table per Persian month: "C" + yyyyMM
    /// in the Persian calendar, e.g. C140507. {0} is replaced with yyyyMM.
    /// </summary>
    [Required, RegularExpression(@"^[A-Za-z_]*\{0\}[A-Za-z_]*$")]
    public string TableNamePattern { get; set; } = "C{0}";

    [Range(1, 3600)]
    public int PollSeconds { get; set; } = 5;

    /// <summary>Receipts are only printed for punches in this window, which may cross midnight.</summary>
    public TimeOnly ActiveFrom { get; set; } = new(21, 0);
    public TimeOnly ActiveUntil { get; set; } = new(6, 0);

    /// <summary>One receipt per person per night, however many times they punch.</summary>
    public bool OncePerPersonPerNight { get; set; } = true;

    /// <summary>"Pdf" writes each receipt to PdfFolder; "Printer" sends it to PrinterName.</summary>
    [Required, RegularExpression("^(Pdf|Printer)$")]
    public string Output { get; set; } = "Pdf";

    public string PdfFolder { get; set; } = "receipts";

    /// <summary>Printer name as Windows shows it; empty = the service account's default printer.</summary>
    public string PrinterName { get; set; } = "";

    /// <summary>Where the last processed punch is remembered across restarts.</summary>
    [Required]
    public string StateFile { get; set; } = "state.json";

    /// <summary>Printed under the name, e.g. the hospital's name.</summary>
    public string Title { get; set; } = "رسید تاکسی";
}

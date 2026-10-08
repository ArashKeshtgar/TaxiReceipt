using Microsoft.Extensions.Options;
using TaxiReceipt.Service;

var builder = Host.CreateApplicationBuilder(args);

// As a Windows Service: SCM start/stop, the content root is the exe's folder
// (not System32), and logs also go to the Windows Event Log. From a console
// it runs exactly the same way, which is how it's developed and tested.
builder.Services.AddWindowsService(o => o.ServiceName = "TaxiReceipt");

builder.Services.AddOptions<ReceiptOptions>()
    .Bind(builder.Configuration.GetSection(ReceiptOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ReceiptOptions>>().Value);

// Relative paths in the settings are relative to the service's own folder.
builder.Services.PostConfigure<ReceiptOptions>(o =>
{
    var root = builder.Environment.ContentRootPath;
    o.PdfFolder = Path.GetFullPath(o.PdfFolder, root);
    o.StateFile = Path.GetFullPath(o.StateFile, root);
});

builder.Services.AddSingleton<IPunchSource>(sp => new SqlPunchSource(sp.GetRequiredService<ReceiptOptions>().ConnectionString));
builder.Services.AddSingleton(sp => new StateStore(sp.GetRequiredService<ReceiptOptions>().StateFile));
builder.Services.AddSingleton<IReceiptPrinter>(sp =>
{
    var o = sp.GetRequiredService<ReceiptOptions>();
    return o.Output == "Printer" && OperatingSystem.IsWindows()
        ? new WindowsReceiptPrinter(o)
        : new PdfReceiptPrinter(o);
});
builder.Services.AddSingleton<ReceiptProcessor>();
builder.Services.AddHostedService<Worker>();

builder.Build().Run();

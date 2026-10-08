namespace TaxiReceipt.Service;

/// <summary>
/// Polls every PollSeconds. A failed pass (database down, printer offline)
/// is logged and retried on the next tick instead of stopping the service —
/// the watermark makes the retry pick up exactly where it stopped.
/// </summary>
public sealed class Worker(ReceiptProcessor processor, ReceiptOptions options, ILogger<Worker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("TaxiReceipt watching punches {From}-{Until}, output {Output}, every {Seconds}s",
            options.ActiveFrom, options.ActiveUntil, options.Output, options.PollSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.PollSeconds));
        var failing = false;
        do
        {
            try
            {
                await processor.RunOnceAsync(DateTime.Now, stoppingToken);
                if (failing) log.LogInformation("Recovered");
                failing = false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Logged once per outage, not every few seconds.
                if (!failing) log.LogError(ex, "Polling failed; retrying every {Seconds}s", options.PollSeconds);
                failing = true;
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

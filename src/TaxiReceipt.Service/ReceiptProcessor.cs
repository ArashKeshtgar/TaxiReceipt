namespace TaxiReceipt.Service;

public interface IReceiptPrinter
{
    Task PrintAsync(Punch punch, DateOnly night, CancellationToken ct);
}

/// <summary>
/// One polling pass: read the punches added since the last pass, print a
/// receipt for each one inside the night window (once per person per night),
/// and move the watermark past everything read — in the window or not — so a
/// daytime punch is never printed later in the evening.
/// </summary>
public sealed class ReceiptProcessor(
    ReceiptOptions options,
    IPunchSource source,
    IReceiptPrinter printer,
    StateStore store,
    ILogger<ReceiptProcessor> log)
{
    public async Task<int> RunOnceAsync(DateTime now, CancellationToken ct)
    {
        var table = NightWindow.TableName(options.TableNamePattern, now);
        var state = store.Load();
        if (!await source.TableExistsAsync(table, ct))
        {
            // Watching from before the table exists: every row it gets is new.
            if (state?.Table != table)
            {
                store.Save(new ReceiptState { Table = table, LastId = 0, Night = state?.Night, PrintedThisNight = state?.PrintedThisNight ?? [] });
            }
            log.LogDebug("Punch table {Table} doesn't exist yet", table);
            return 0;
        }

        if (state is null)
        {
            // First start: everything already in the table happened before
            // the service was watching. Same choice the original made.
            state = new ReceiptState { Table = table, LastId = await source.MaxIdAsync(table, ct) };
            store.Save(state);
            log.LogInformation("Started watching {Table} after punch {LastId}", table, state.LastId);
            return 0;
        }
        if (state.Table != table)
        {
            // A new Persian month: the clock system started a new table, so
            // every row in it is new. (The original fixed the table name
            // once at start-up and kept reading last month's.)
            log.LogInformation("New month table {Table} (was {Old})", table, state.Table);
            state.Table = table;
            state.LastId = 0;
        }

        var printed = 0;
        foreach (var punch in await source.PunchesAfterAsync(table, state.LastId, ct))
        {
            if (NightWindow.IsActive(TimeOnly.FromDateTime(punch.PunchTime), options.ActiveFrom, options.ActiveUntil))
            {
                var night = NightWindow.NightOf(punch.PunchTime, options.ActiveFrom, options.ActiveUntil);
                if (state.Night != night)
                {
                    state.Night = night;
                    state.PrintedThisNight.Clear();
                }
                if (!options.OncePerPersonPerNight || state.PrintedThisNight.Add(punch.PersonId))
                {
                    await printer.PrintAsync(punch, night, ct);
                    printed++;
                    log.LogInformation("Receipt for {Name} (person {PersonId}, punch {Id} at {Time})",
                        punch.FullName, punch.PersonId, punch.Id, punch.PunchTime);
                }
            }
            // Saved after each punch: if printing the next one fails, this
            // one is not printed twice when the pass is retried.
            state.LastId = punch.Id;
            store.Save(state);
        }
        store.Save(state);
        return printed;
    }
}

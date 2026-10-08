using Microsoft.Extensions.Logging.Abstractions;
using TaxiReceipt.Service;

namespace TaxiReceipt.Service.Tests;

public sealed class ReceiptProcessorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("taxireceipt-").FullName;
    private readonly FakeSource _source = new();
    private readonly FakePrinter _printer = new();
    private readonly ReceiptOptions _options = new();

    private static readonly DateTime Night = new(2026, 10, 8, 22, 0, 0); // table C140507

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private ReceiptProcessor Processor() =>
        new(_options, _source, _printer, new StateStore(Path.Combine(_dir, "state.json")), NullLogger<ReceiptProcessor>.Instance);

    private Task<int> Run(DateTime now) => Processor().RunOnceAsync(now, CancellationToken.None);

    [Fact]
    public async Task First_start_skips_what_is_already_there_then_prints_new_night_punches()
    {
        _source.Add("C140507", 1, Night.AddHours(-1));
        Assert.Equal(0, await Run(Night));

        _source.Add("C140507", 2, Night.AddMinutes(5));
        _source.Add("C140507", 3, Night.AddMinutes(6)); // two at once: COUNT(*) logic printed one
        Assert.Equal(2, await Run(Night.AddMinutes(10)));
        Assert.Equal([2, 3], _printer.Printed.Select(p => p.PersonId));
    }

    [Fact]
    public async Task Daytime_punches_are_never_printed_later_in_the_evening()
    {
        await Run(Night);
        _source.Add("C140507", 7, new DateTime(2026, 10, 8, 14, 0, 0));
        await Run(new DateTime(2026, 10, 8, 14, 1, 0));
        Assert.Equal(0, await Run(Night.AddMinutes(1)));
        Assert.Empty(_printer.Printed);
    }

    [Fact]
    public async Task One_receipt_per_person_per_night_unless_turned_off()
    {
        await Run(Night);
        _source.Add("C140507", 5, Night.AddMinutes(1));
        _source.Add("C140507", 5, Night.AddHours(4)); // 02:00, same night
        Assert.Equal(1, await Run(Night.AddHours(5)));

        _source.Add("C140507", 5, Night.AddDays(1)); // next night
        Assert.Equal(1, await Run(Night.AddDays(1).AddMinutes(1)));

        _options.OncePerPersonPerNight = false;
        _source.Add("C140507", 5, Night.AddDays(1).AddMinutes(30));
        Assert.Equal(1, await Run(Night.AddDays(1).AddHours(1)));
        Assert.Equal(3, _printer.Printed.Count);
    }

    [Fact]
    public async Task A_restart_neither_reprints_nor_misses()
    {
        await Run(Night);
        _source.Add("C140507", 1, Night.AddMinutes(1));
        await Run(Night.AddMinutes(2));
        _source.Add("C140507", 2, Night.AddMinutes(3)); // arrives while the service is stopped
        Assert.Equal(1, await Run(Night.AddMinutes(4))); // a new processor = a restart
        Assert.Equal([1, 2], _printer.Printed.Select(p => p.PersonId));
    }

    [Fact]
    public async Task A_new_persian_month_switches_to_the_new_table_from_its_first_row()
    {
        var lastNightOfMonth = new DateTime(2026, 10, 22, 22, 0, 0); // 1405/07/30
        await Run(lastNightOfMonth);
        _source.Add("C140507", 1, lastNightOfMonth.AddMinutes(1));
        Assert.Equal(1, await Run(lastNightOfMonth.AddMinutes(2)));

        var afterMidnight = new DateTime(2026, 10, 23, 1, 0, 0); // 1405/08/01, same night
        _source.Add("C140508", 1, afterMidnight);              // already printed tonight
        _source.Add("C140508", 2, afterMidnight.AddMinutes(1));
        Assert.Equal(1, await Run(afterMidnight.AddMinutes(2)));
        Assert.Equal([1, 2], _printer.Printed.Select(p => p.PersonId));
    }

    [Fact]
    public async Task Started_before_the_month_table_exists_then_prints_its_first_rows()
    {
        Assert.Equal(0, await Run(Night));
        _source.Add("C140507", 1, Night.AddMinutes(1));
        Assert.Equal(1, await Run(Night.AddMinutes(2)));
    }

    [Fact]
    public async Task A_failed_print_is_retried_without_reprinting_the_ones_before_it()
    {
        await Run(Night);
        _source.Add("C140507", 1, Night.AddMinutes(1));
        _source.Add("C140507", 2, Night.AddMinutes(2));
        _printer.FailFor = 2;
        await Assert.ThrowsAsync<IOException>(() => Run(Night.AddMinutes(3)));

        _printer.FailFor = null;
        Assert.Equal(1, await Run(Night.AddMinutes(4)));
        Assert.Equal([1, 2], _printer.Printed.Select(p => p.PersonId));
    }

    private sealed class FakeSource : IPunchSource
    {
        private readonly Dictionary<string, List<Punch>> _tables = [];
        private long _nextId = 100;

        public void Add(string table, int personId, DateTime time)
        {
            if (!_tables.TryGetValue(table, out var rows)) _tables[table] = rows = [];
            rows.Add(new Punch(++_nextId, personId, $"Person {personId}", time));
        }

        public Task<bool> TableExistsAsync(string table, CancellationToken ct) => Task.FromResult(_tables.ContainsKey(table));

        public Task<long> MaxIdAsync(string table, CancellationToken ct) =>
            Task.FromResult(_tables[table].Select(p => p.Id).DefaultIfEmpty(0).Max());

        public Task<IReadOnlyList<Punch>> PunchesAfterAsync(string table, long afterId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Punch>>(_tables[table].Where(p => p.Id > afterId).OrderBy(p => p.Id).ToList());
    }

    private sealed class FakePrinter : IReceiptPrinter
    {
        public List<Punch> Printed { get; } = [];
        public int? FailFor { get; set; }

        public Task PrintAsync(Punch punch, DateOnly night, CancellationToken ct)
        {
            if (punch.PersonId == FailFor) throw new IOException("printer offline");
            Printed.Add(punch);
            return Task.CompletedTask;
        }
    }
}

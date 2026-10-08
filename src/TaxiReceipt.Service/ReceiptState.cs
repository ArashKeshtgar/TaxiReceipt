using System.Text.Json;

namespace TaxiReceipt.Service;

/// <summary>
/// What has been processed so far. The original compared COUNT(*) of the
/// month's table with the last count, which missed punches that arrived
/// together and reprinted after every restart; a watermark on the punch Id,
/// saved to disk, does neither.
/// </summary>
public sealed class ReceiptState
{
    public string? Table { get; set; }
    public long LastId { get; set; }
    public DateOnly? Night { get; set; }
    public HashSet<int> PrintedThisNight { get; set; } = [];
}

public sealed class StateStore(string path)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public ReceiptState? Load()
    {
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<ReceiptState>(File.ReadAllText(path), Json);
    }

    /// <summary>Written to a temp file and moved over, so a crash never leaves half a file.</summary>
    public void Save(ReceiptState state)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, Json));
        File.Move(tmp, path, overwrite: true);
    }
}

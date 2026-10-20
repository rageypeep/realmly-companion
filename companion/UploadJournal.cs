using System.Text.Json;

namespace RealmlyCompanion;

internal sealed record UploadAttempt(DateTimeOffset At, string Client, int Characters, int Records, string Outcome, bool Succeeded = false, bool Automatic = false);

internal sealed class UploadJournal
{
    public List<string> UploadedRecords { get; set; } = [];
    public List<UploadAttempt> History { get; set; } = [];

    public void Record(UploadPreview preview, ClientKind client, UploadResult? result, bool automatic = false)
    {
        var successful = result?.Ok == true;
        if (successful) UploadedRecords = UploadedRecords.Concat(preview.RecordKeys).Distinct(StringComparer.Ordinal).ToList();
        var outcome = !successful ? "Failed - not marked as uploaded; retry when ready."
            : result!.Duplicate || (result.Batch && result.ImportedCount == 0) ? "Already on record."
            : $"Accepted by Realmly ({result.LocationCount} discoveries reported).";
        History.Insert(0, new(DateTimeOffset.UtcNow, client.ToString(), preview.Characters.Length, preview.Records, outcome, successful, automatic));
        History = History.Take(20).ToList();
    }

    public static UploadJournal Load(string path)
    {
        if (!File.Exists(path)) return new();
        var journal = JsonSerializer.Deserialize<UploadJournal>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("The local upload history could not be read.");
        journal.UploadedRecords ??= [];
        journal.History = (journal.History ?? []).Take(20).ToList();
        return journal;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this));
        File.Move(temporary, path, overwrite: true);
    }

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Realmly", "upload-history.json");
}

using System.Runtime.InteropServices;

namespace RealmlyCompanion;

internal enum DiagnosticOperation { General, AddonCheck, AddonInstall, Upload, Pairing, CompanionCheck, CompanionUpdate, Preview, Settings, History, AutomaticUpload }
internal sealed record SafeDiagnosticEvent(DateTimeOffset At, DiagnosticOperation Operation, string Category);

internal sealed class DiagnosticLog
{
    private readonly List<SafeDiagnosticEvent> _events = [];
    public IReadOnlyList<SafeDiagnosticEvent> Events => _events;
    public void Record(DiagnosticOperation operation, Exception error)
    {
        // Never retain Message, StackTrace, Data, URLs or exception type names from arbitrary code.
        var category = error switch
        {
            RealmlyApiException api => api.StatusCode switch
            { 401 or 403 => "Sign-in or permission required", 413 => "Upload too large", 429 => "Rate limited", >= 500 => "Server error", _ => "Request rejected" },
            HttpRequestException => "Network connection failed",
            TaskCanceledException => "Timed out or cancelled",
            OperationCanceledException => "Cancelled",
            UnauthorizedAccessException => "File permission denied",
            IOException => "File read/write failed",
            System.Text.Json.JsonException => "Unreadable JSON response or local data",
            InvalidOperationException or ArgumentException => "Validation or operation state failed",
            _ => "Unexpected operation failure"
        };
        if (_events.LastOrDefault() is { } last && last.Operation == operation && last.Category == category && DateTimeOffset.UtcNow - last.At < TimeSpan.FromMinutes(5)) return;
        _events.Add(new(DateTimeOffset.UtcNow, operation, category));
        if (_events.Count > 8) _events.RemoveAt(0);
    }
}

internal sealed record DiagnosticSnapshot(ClientKind Client, AddonVersion? Installed, AddonVersion? Latest,
    WowRunState GameState, bool GameFolderPresent, bool SavedFilePresent, bool Connected,
    int Setups, bool Tray, bool Notifications, int? PreviewRecords, int? PreviewCharacters, bool? LastUploadSucceeded,
    bool AutoUpload = false, AutoUploadState AutoState = AutoUploadState.Off);

internal static class DiagnosticReport
{
    private static string YesNo(bool value) => value ? "yes" : "no";
    private static string SafeVersion(AddonVersion? version) => version?.Number.ToString() ?? "not available";
    public static string Build(DiagnosticSnapshot snapshot, DiagnosticLog log)
    {
        var lines = new List<string>
        {
            "REALMLY COMPANION DIAGNOSTICS",
            "Created (UTC): " + DateTimeOffset.UtcNow.ToString("u"),
            "Companion: " + typeof(DiagnosticReport).Assembly.GetName().Version?.ToString(3),
            "Distribution: " + CompanionDistribution.Name,
            "Windows version: " + Environment.OSVersion.Version,
            "Runtime: .NET " + Environment.Version + " / " + RuntimeInformation.ProcessArchitecture,
            "Client: " + snapshot.Client,
            "Installed addon: " + SafeVersion(snapshot.Installed),
            "Latest checked addon: " + SafeVersion(snapshot.Latest),
            "WoW state: " + snapshot.GameState,
            "Game folder present: " + YesNo(snapshot.GameFolderPresent),
            "SavedVariables file present: " + YesNo(snapshot.SavedFilePresent),
            "Account connected: " + YesNo(snapshot.Connected),
            "Saved setups: " + snapshot.Setups,
            "Tray enabled: " + YesNo(snapshot.Tray),
            "Update notifications enabled: " + YesNo(snapshot.Tray && snapshot.Notifications),
            "Preview records: " + (snapshot.PreviewRecords?.ToString() ?? "not checked"),
            "Preview characters: " + (snapshot.PreviewCharacters?.ToString() ?? "not checked"),
            "Last local upload succeeded: " + (snapshot.LastUploadSucceeded is bool success ? YesNo(success) : "no history"),
            "Automatic uploads for selected setup: " + YesNo(snapshot.AutoUpload) + " / " + snapshot.AutoState,
            "", "Recent errors (safe categories only; this app session):"
        };
        lines.AddRange(log.Events.Count == 0 ? ["None recorded."] : log.Events.Select(item => $"{item.At:u} / {item.Operation} / {item.Category}"));
        lines.Add("");
        lines.Add("Excludes paths, setup/character names, tokens, file contents and raw error messages. This report is never sent automatically.");
        return string.Join(Environment.NewLine, lines);
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RealmlyCompanion;

internal sealed class CompanionSettings
{
    private const string FileName = "RealmlyCompanion.json";

    public string? GameRoot { get; set; }
    public string? SavedVariablesPath { get; set; }
    public ClientKind Client { get; set; } = ClientKind.Retail;
    public string? ProtectedAccessToken { get; set; }
    public bool CloseToTray { get; set; }
    public bool NotifyUpdates { get; set; }
    public List<WowInstallation> Installations { get; set; } = [];
    public string? ActiveInstallationId { get; set; }
    public Dictionary<ClientKind, string> LastInstallationByClient { get; set; } = [];

    public WowInstallation EnsureInstallation()
    {
        Installations ??= [];
        LastInstallationByClient ??= [];
        Installations = Installations.Where(item => item is not null && Enum.IsDefined(item.Client)).ToList();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in Installations)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id)) { item.Id = Guid.NewGuid().ToString("N"); ids.Add(item.Id); }
            if (string.IsNullOrWhiteSpace(item.Name)) item.Name = item.Client.ToString();
        }
        if (Installations.Count == 0)
            Installations.Add(new() { Name = Client.ToString(), Client = Enum.IsDefined(Client) ? Client : ClientKind.Retail, GameRoot = GameRoot, SavedVariablesPath = SavedVariablesPath });
        var active = Installations.FirstOrDefault(item => item.Id == ActiveInstallationId) ?? Installations[0];
        Remember(active);
        return active;
    }

    public void Remember(WowInstallation installation)
    {
        ActiveInstallationId = installation.Id;
        LastInstallationByClient[installation.Client] = installation.Id;
        Client = installation.Client;
        GameRoot = installation.GameRoot;
        SavedVariablesPath = installation.SavedVariablesPath;
    }

    public WowInstallation ForClient(ClientKind client, string? defaultRoot)
    {
        LastInstallationByClient.TryGetValue(client, out var lastId);
        var installation = Installations.FirstOrDefault(item => item.Id == lastId && item.Client == client)
            ?? Installations.FirstOrDefault(item => item.Client == client);
        if (installation is not null) return installation;
        installation = new() { Name = client.ToString(), Client = client, GameRoot = defaultRoot };
        Installations.Add(installation);
        return installation;
    }

    public WowInstallation SaveAs(WowInstallation source, string name)
    {
        var copy = new WowInstallation { Name = ValidateName(name, source.Client), Client = source.Client,
            GameRoot = source.GameRoot, SavedVariablesPath = source.SavedVariablesPath };
        Installations.Add(copy);
        return copy;
    }

    public void Rename(WowInstallation installation, string name) => installation.Name = ValidateName(name, installation.Client, installation.Id);

    private string ValidateName(string name, ClientKind client, string? exceptId = null)
    {
        var clean = name.Trim();
        if (clean.Length is 0 or > 60 || clean.Any(char.IsControl)) throw new InvalidOperationException("Choose a setup name of 1 to 60 characters.");
        if (Installations.Any(item => item.Id != exceptId && item.Client == client && string.Equals(item.Name, clean, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("That client already has a setup with this name.");
        return clean;
    }

    public WowInstallation Forget(WowInstallation installation)
    {
        if (Installations.Count <= 1) throw new InvalidOperationException("Keep at least one saved setup.");
        Installations.Remove(installation);
        return Installations.FirstOrDefault(item => item.Client == installation.Client) ?? Installations[0];
    }

    [JsonIgnore]
    public string? AccessToken
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ProtectedAccessToken)) return null;
            try
            {
                var encrypted = Convert.FromBase64String(ProtectedAccessToken);
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser));
            }
            catch (CryptographicException) { return null; }
            catch (FormatException) { return null; }
        }
        set
        {
            ProtectedAccessToken = string.IsNullOrWhiteSpace(value)
                ? null
                : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
        }
    }

    public static CompanionSettings Load()
    {
        try
        {
            var path = SettingsPath();
            return File.Exists(path)
                ? JsonSerializer.Deserialize<CompanionSettings>(File.ReadAllText(path)) ?? new CompanionSettings()
                : new CompanionSettings();
        }
        catch (JsonException) { return new CompanionSettings(); }
        catch (IOException) { return new CompanionSettings(); }
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(SettingsPath())!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath(), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string SettingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Realmly", FileName);
}

internal sealed class WowInstallation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public ClientKind Client { get; set; }
    public string? GameRoot { get; set; }
    public string? SavedVariablesPath { get; set; }
    public bool AutoUpload { get; set; }
    public override string ToString() => string.Equals(Name, Client.ToString(), StringComparison.OrdinalIgnoreCase) ? Name : $"{Name} / {Client}";
}

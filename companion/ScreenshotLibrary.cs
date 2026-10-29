using System.Globalization;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Text.RegularExpressions;

namespace RealmlyCompanion;

internal sealed record ScreenshotCapture(string Id, string AdventurerId, string CharacterName, string Realm,
    DateTime LocalTime, string Day, string Title)
{
    public override string ToString() => $"{CharacterName} / {Realm}: {Title} ({LocalTime:yyyy-MM-dd HH:mm:ss})";
}
internal sealed class LocalScreenshot(string path, DateTime? clock, ScreenshotCapture[] matches)
{
    public string Path { get; } = path;
    public DateTime? Clock { get; } = clock;
    public ScreenshotCapture[] Matches { get; } = matches;
    public ScreenshotCapture? Capture { get; set; } = matches.Length == 1 ? matches[0] : null;
    public bool Uploaded { get; set; }
}
internal static partial class ScreenshotLibrary
{
    public const int MaximumFiles = 2000;
    public const int MaximumBytes = 12 * 1024 * 1024;
    public static string Folder(string root, ClientKind client) => Path.Combine(root, WowFiles.ClientFolder(client), "Screenshots");
    [GeneratedRegex(@"^WoWScrnShot_(\d{6})_(\d{6})\.(?:jpe?g|png|bmp)$", RegexOptions.IgnoreCase)]
    private static partial Regex FilenamePattern();
    public static DateTime? FilenameClock(string path)
    {
        var match = FilenamePattern().Match(Path.GetFileName(path));
        if (!match.Success) return null;
        var date = match.Groups[1].Value;var time = match.Groups[2].Value;
        return DateTime.TryParseExact($"20{date[4..]}-{date[..2]}-{date[2..4]}T{time[..2]}:{time[2..4]}:{time[4..]}",
            "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? DateTime.SpecifyKind(value, DateTimeKind.Unspecified) : null;
    }
    public static ScreenshotCapture[] Captures(string source, ClientKind client)
    {
        if (Encoding.UTF8.GetByteCount(source) > 2 * 1024 * 1024) throw new InvalidOperationException("Realmly.lua exceeds 2 MB. Use the addon's date export or reduce saved history.");
        var db = new SavedVariablesReader(source).Read();
        if (db.GetValueOrDefault("client") is string kind && !string.Equals(kind, client.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This Realmly.lua belongs to the other game client. Choose the matching setup.");
        var captures = new List<ScreenshotCapture>();
        if (db.GetValueOrDefault("profiles") is not Dictionary<string, object?> profiles) return [];
        foreach (var profile in profiles.Values.OfType<Dictionary<string, object?>>())
        {
            if (profile.GetValueOrDefault("character") is not Dictionary<string, object?> character ||
                profile.GetValueOrDefault("locations") is not Dictionary<string, object?> locations ||
                profile.GetValueOrDefault("adventurerId") is not string adventurer) continue;
            var latest = new Dictionary<string, Dictionary<string, object?>>();
            foreach (var row in locations.OrderBy(pair => int.TryParse(pair.Key, out var n) ? n : 0).Select(pair => pair.Value).OfType<Dictionary<string, object?>>())
                if (row.GetValueOrDefault("id") is string id) latest[id] = row;
            foreach (var row in latest.Values)
            {
                if (row.GetValueOrDefault("screenshot") is not Dictionary<string, object?> shot ||
                    shot.GetValueOrDefault("status") as string != "succeeded" ||
                    shot.GetValueOrDefault("requestedAtLocal") is not string local ||
                    !DateTime.TryParseExact(local, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var clock) ||
                    shot.GetValueOrDefault("events") is not Dictionary<string, object?> events) continue;
                var milestone = events.OrderBy(pair => int.TryParse(pair.Key, out var n) ? n : 0).Select(pair => pair.Value).OfType<Dictionary<string, object?>>().FirstOrDefault();
                if (milestone?.GetValueOrDefault("name") is not string title ||
                    milestone.GetValueOrDefault("capturedAt") is not string at ||
                    !DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var utc)) continue;
                captures.Add(new((string)row["id"]!, adventurer, character.GetValueOrDefault("name") as string ?? "Unknown",
                    character.GetValueOrDefault("realm") as string ?? "Unknown", DateTime.SpecifyKind(clock, DateTimeKind.Unspecified), utc.UtcDateTime.ToString("yyyy-MM-dd"), title));
            }
        }
        return captures.ToArray();
    }
    // Uploads can retain the website's established ID after a local addon migration.
    // The caller supplies only characters returned for its paired account.
    public static ScreenshotCharacter? ResolveCaptureCharacter(ScreenshotCapture capture, IEnumerable<ScreenshotCharacter> ownedCharacters, ClientKind client)
    {
        var sameGame = ownedCharacters.Where(c => string.Equals(c.GameVersion, client.ToString(), StringComparison.OrdinalIgnoreCase)).ToArray();
        var exact = sameGame.Where(c => c.AdventurerId == capture.AdventurerId).ToArray();
        if (exact.Length > 0) return exact.Length == 1 ? exact[0] : null;
        var identity = sameGame.Where(c => string.Equals(c.CharacterName?.Trim(), capture.CharacterName.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.RealmName?.Trim() ?? "", capture.Realm.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        return identity.Length == 1 ? identity[0] : null;
    }
    public static LocalScreenshot[] Scan(string folder, ScreenshotCapture[] captures, CancellationToken ct)
    {
        if (!Directory.Exists(folder)) return [];
        // No recursive walks, watchers or reading image bytes during discovery. Stop oversized directories.
        var files = new List<string>();
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            ct.ThrowIfCancellationRequested();
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
            if (!new[] {".jpg", ".jpeg", ".png", ".bmp"}.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;
            if (files.Count == MaximumFiles) throw new InvalidOperationException($"This folder contains more than {MaximumFiles:N0} screenshots. Choose a smaller folder to browse.");
            files.Add(file);
        }
        return files.Select(file => {
            var time = FilenameClock(file);
            var matches = time.HasValue ? captures.Where(c => Math.Abs((c.LocalTime - time.Value).TotalSeconds) <= 5).ToArray() : [];
            return new LocalScreenshot(file, time, matches);
        }).OrderByDescending(file => file.Clock ?? File.GetLastWriteTime(file.Path)).ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static byte[] ReadImageBytes(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length > MaximumBytes) throw new InvalidOperationException("Choose a screenshot under 12 MB.");
        using var output = new MemoryStream();
        var buffer = new byte[81920];int count;
        while ((count = stream.Read(buffer)) > 0)
        {
            if (output.Length + count > MaximumBytes) throw new InvalidOperationException("The screenshot grew beyond 12 MB. Try again after it finishes saving.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    public static Bitmap Preview(byte[] bytes, int maxSize = 1200)
    {
        using var input = new MemoryStream(bytes);
        using var original = Image.FromStream(input, false, true);
        if ((long)original.Width * original.Height > 25_000_000) throw new InvalidOperationException("Use a screenshot under 25 megapixels.");
        var scale = Math.Min(1, (double)maxSize / Math.Max(original.Width, original.Height));
        var bitmap = new Bitmap(Math.Max(1, (int)(original.Width * scale)), Math.Max(1, (int)(original.Height * scale)));
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(original, new Rectangle(Point.Empty, bitmap.Size));return bitmap;
    }
    public static string UploadImage(byte[] bytes)
    {
        using var image = Preview(bytes, 2048);
        var codec = ImageCodecInfo.GetImageEncoders().Single(c => c.FormatID == ImageFormat.Jpeg.Guid);
        foreach (var quality in new long[] {88, 75, 60})
        {
            using var output = new MemoryStream();using var options = new EncoderParameters(1);
            options.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            image.Save(output, codec, options);
            if (output.Length <= 2 * 1024 * 1024) return "data:image/jpeg;base64," + Convert.ToBase64String(output.ToArray());
        }
        throw new InvalidOperationException("This screenshot is still too large after resizing. Choose a smaller image.");
    }
}

using System.Text.RegularExpressions;

namespace RealmlyCompanion;

internal sealed record AddonVersion(string Display, Version Number)
{
    public static AddonVersion? FromToc(string source)
    {
        foreach (var line in source.Split('\n'))
        {
            var match = Regex.Match(line.Trim(), @"^##\s*Version:\s*(.+)$", RegexOptions.IgnoreCase);
            if (!match.Success) continue;
            var display = match.Groups[1].Value.Trim();
            var number = Regex.Match(display, @"^(\d+)\.(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:[-+][\w.-]+)?$");
            if (!number.Success) return null;
            var normalized = string.Join(".", Enumerable.Range(1, 4)
                .Select(index => number.Groups[index].Success ? number.Groups[index].Value : "0"));
            return Version.TryParse(normalized, out var version) ? new AddonVersion(display, version) : null;
        }
        return null;
    }
}

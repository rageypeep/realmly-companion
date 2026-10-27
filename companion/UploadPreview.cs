using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace RealmlyCompanion;

internal sealed record CharacterPreview(string Name, string Realm, int Records, int NewRecords, string Dates);
internal sealed record UploadPreview(string Source, string Hash, string[] RecordKeys, CharacterPreview[] Characters)
{
    public int Records => Characters.Sum(character => character.Records);
    public int NewRecords => Characters.Sum(character => character.NewRecords);
    public string Summary => string.Join(Environment.NewLine, Characters.Select(character =>
        $"{character.Name} - {character.Realm}: {character.Records} records ({character.NewRecords} new locally)\n  {character.Dates}"));

    public static UploadPreview Parse(string source, IEnumerable<string> uploaded)
    {
        if (Encoding.UTF8.GetByteCount(source) > 2 * 1024 * 1024)
            throw new InvalidOperationException("Realmly.lua is larger than the 2 MB upload limit.");
        var database = new SavedVariablesReader(source).Read();
        if (database.GetValueOrDefault("profiles") is not Dictionary<string, object?> profiles)
            throw new InvalidOperationException("This file has no Realmly character profiles. Choose WTF / Account / ... / SavedVariables / Realmly.lua.");
        var known = uploaded.ToHashSet(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var characters = new List<CharacterPreview>();
        foreach (var profile in profiles.Values.OfType<Dictionary<string, object?>>())
        {
            if (profile.GetValueOrDefault("character") is not Dictionary<string, object?> character ||
                profile.GetValueOrDefault("locations") is not Dictionary<string, object?> locations ||
                profile.GetValueOrDefault("adventurerId") is not string adventurer || string.IsNullOrWhiteSpace(adventurer)) continue;
            var records = locations.Where(pair => int.TryParse(pair.Key, out var index) && index > 0)
                .Select(pair => pair.Value).OfType<Dictionary<string, object?>>().ToArray();
            if (records.Length == 0) continue;
            var newCount = 0;
            foreach (var record in records)
            {
                // Stable record IDs are namespaced by adventurer; retain only hashes locally.
                if (record.GetValueOrDefault("id") is not string id || string.IsNullOrWhiteSpace(id))
                    throw new InvalidOperationException("A record has no ID. Update the addon and save your data again.");
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(adventurer + "\0" + id)));
                if (keys.Add(key) && !known.Contains(key)) newCount++;
            }
            var dates = records.Select(record => record.GetValueOrDefault("capturedAt") as string)
                .Where(date => DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
                .Select(date => DateTimeOffset.Parse(date!, CultureInfo.InvariantCulture).UtcDateTime.ToString("yyyy-MM-dd"))
                .Distinct().Order().ToArray();
            var range = dates.Length == 0 ? "Adventure dates unavailable" : dates.Length == 1 ? dates[0] + " (UTC)" : $"{dates[0]} to {dates[^1]} (UTC)";
            characters.Add(new(character.GetValueOrDefault("name") as string ?? "Unknown character",
                character.GetValueOrDefault("realm") as string ?? "Unknown realm", records.Length, newCount, range));
        }
        if (characters.Count == 0) throw new InvalidOperationException("No recorded discoveries yet. Play with Realmly enabled, then log out or /reload.");
        return new(source, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))), keys.ToArray(), characters.OrderBy(character => character.Name).ToArray());
    }
}

// Data-only Lua reader, deliberately excluding calls, expressions and executable code.
internal sealed class SavedVariablesReader(string source)
{
    private readonly string _source = source.TrimStart('\uFEFF');
    private const int MaximumTableFields = 250_000;
    private int _index, _entries;
    public Dictionary<string, object?> Read()
    {
        var variable = Identifier();
        if (variable is not ("RealmlyDB" or "WoWForeverAtlasDB" or "WoWForeverAtlasRetailDB")) Fail();
        Expect('=');
        var value = Value(0) as Dictionary<string, object?>;
        Skip();
        if (_index != _source.Length || value is null) Fail();
        return value!;
    }
    private char Current => _index < _source.Length ? _source[_index] : '\0';
    private void Fail() => throw new InvalidOperationException("This is not a valid Realmly SavedVariables data file. Choose the saved Realmly.lua, not the addon source.");
    private bool Consume(char value) { if (Current != value) return false; _index++; return true; }
    private void Expect(char value) { Skip(); if (!Consume(value)) Fail(); }
    private void Skip()
    {
        while (_index < _source.Length)
        {
            if (char.IsWhiteSpace(Current)) { _index++; continue; }
            if (!_source.AsSpan(_index).StartsWith("--")) break;
            if (_source.AsSpan(_index).StartsWith("--[["))
            {
                var end = _source.IndexOf("]]", _index + 4, StringComparison.Ordinal);
                _index = end < 0 ? _source.Length : end + 2;
            }
            else { var end = _source.IndexOf('\n', _index); _index = end < 0 ? _source.Length : end + 1; }
        }
    }
    private string Identifier()
    {
        Skip(); var start = _index;
        if (!(char.IsAsciiLetter(Current) || Current == '_')) Fail();
        _index++;
        while (char.IsAsciiLetterOrDigit(Current) || Current == '_') _index++;
        return _source[start.._index];
    }
    private object? Value(int depth)
    {
        Skip(); if (depth > 32) Fail();
        if (Consume('{'))
        {
            var result = new Dictionary<string, object?>(); var next = 1;
            while (true)
            {
                Skip(); if (Consume('}')) return result;
                if (++_entries > MaximumTableFields)
                    throw new InvalidOperationException("This SavedVariables file exceeds the preview complexity limit (250,000 table fields).");
                string? key = null;
                if (Consume('[')) { key = Convert.ToString(Value(depth + 1), CultureInfo.InvariantCulture); Expect(']'); Expect('='); }
                else if (char.IsAsciiLetter(Current) || Current == '_')
                {
                    var checkpoint = _index; var candidate = Identifier(); Skip();
                    if (Consume('=')) key = candidate; else _index = checkpoint;
                }
                result[key ?? (next++).ToString(CultureInfo.InvariantCulture)] = Value(depth + 1);
                Skip(); if (Consume(',') || Consume(';')) continue;
                if (Current != '}') Fail();
            }
        }
        if (Current is '\'' or '"') return String();
        if (Current == '-' || char.IsAsciiDigit(Current))
        {
            var start = _index++;
            while (char.IsAsciiDigit(Current) || Current is '.' or 'e' or 'E' or '+' or '-') _index++;
            if (!double.TryParse(_source[start.._index], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) Fail();
            return number;
        }
        return Identifier() switch { "true" => true, "false" => false, "nil" => null, _ => throw new InvalidOperationException("Executable Lua is not accepted. Choose the SavedVariables file.") };
    }
    private string String()
    {
        var quote = _source[_index++]; var output = new StringBuilder();
        while (_index < _source.Length)
        {
            var value = _source[_index++]; if (value == quote) return output.ToString();
            if (value != '\\') { output.Append(value); continue; }
            if (_index >= _source.Length) Fail();
            value = _source[_index++];
            if (char.IsAsciiDigit(value))
            {
                var digits = value.ToString();
                while (digits.Length < 3 && char.IsAsciiDigit(Current)) digits += _source[_index++];
                var code = int.Parse(digits, CultureInfo.InvariantCulture); if (code > 255) Fail();
                output.Append((char)code); continue;
            }
            output.Append(value switch { 'n' => '\n', 'r' => '\r', 't' => '\t', 'a' => '\a', 'b' => '\b', 'f' => '\f', 'v' => '\v', '\\' => '\\', '"' => '"', '\'' => '\'', '\n' => '\n', _ => throw new InvalidOperationException("Unsupported SavedVariables string escape.") });
        }
        Fail(); return "";
    }
}

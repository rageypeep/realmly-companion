using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RealmlyCompanion;

internal sealed class RealmlyApi
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;

    public RealmlyApi(HttpClient? http = null) => _http = http ?? new HttpClient
        { BaseAddress = new Uri("https://realmly.me/"), Timeout = TimeSpan.FromSeconds(90) };

    public async Task<PairingRequest> StartPairingAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsync("api/companion/requests", new StringContent("{}", Encoding.UTF8, "application/json"), cancellationToken);
        return await Read<PairingRequest>(response, cancellationToken);
    }

    public async Task<PairingStatus> PollPairingAsync(PairingRequest pairing, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/companion/requests/{Uri.EscapeDataString(pairing.RequestId)}");
        request.Headers.Add("X-Realmly-Companion-Secret", pairing.PollSecret);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Accepted) return new PairingStatus(false, null);
        return await Read<PairingStatus>(response, cancellationToken);
    }

    public async Task<UploadResult> UploadAsync(string savedVariablesPath, string accessToken, CancellationToken cancellationToken)
    {
        var source = await File.ReadAllTextAsync(savedVariablesPath, cancellationToken);
        return await UploadSourceAsync(source, accessToken, cancellationToken);
    }

    public async Task<UploadResult> UploadSourceAsync(string source, string accessToken, CancellationToken cancellationToken)
    {
        if (Encoding.UTF8.GetByteCount(source) > 2 * 1024 * 1024) throw new InvalidOperationException("Realmly.lua is larger than the 2 MB upload limit.");
        var payload = JsonSerializer.Serialize(new { savedVariables = source });
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/collector-uploads")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        return await Read<UploadResult>(response, cancellationToken);
    }

    public Task<ScreenshotCharacters> ScreenshotCharactersAsync(string token, CancellationToken ct) =>
        ScreenshotRequestAsync<ScreenshotCharacters>("api/companion/screenshot-characters", token, null, ct);

    public Task<ScreenshotDays> ScreenshotDaysAsync(string slug, string token, CancellationToken ct) =>
        ScreenshotRequestAsync<ScreenshotDays>($"api/my/characters/{Uri.EscapeDataString(slug)}/screenshot-days", token, null, ct);

    public Task<ScreenshotMatchResponse> ScreenshotMatchesAsync(string slug, string day, string filename, string token, CancellationToken ct) =>
        ScreenshotRequestAsync<ScreenshotMatchResponse>($"api/my/characters/{Uri.EscapeDataString(slug)}/days/{Uri.EscapeDataString(day)}/screenshot-matches?filename={Uri.EscapeDataString(filename)}", token, null, ct);

    public Task<ScreenshotUploadResult> UploadScreenshotAsync(string slug, string day, string image, string? milestoneId, string token, CancellationToken ct) =>
        ScreenshotRequestAsync<ScreenshotUploadResult>($"api/my/characters/{Uri.EscapeDataString(slug)}/days/{Uri.EscapeDataString(day)}/screenshots", token, new {image, milestoneId}, ct);

    private async Task<T> ScreenshotRequestAsync<T>(string url, string token, object? body, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, deadline.Token);
        return await Read<T>(response, deadline.Token);
    }

    public async Task<CompanionRelease> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync($"downloads/RealmlyCompanion-release.json?check={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", cancellationToken);
        return await Read<CompanionRelease>(response, cancellationToken);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            try { throw new RealmlyApiException((int)response.StatusCode, JsonSerializer.Deserialize<ApiError>(text, Json)?.Error ?? $"Realmly returned {(int)response.StatusCode}."); }
            catch (JsonException) { throw new RealmlyApiException((int)response.StatusCode, $"Realmly returned {(int)response.StatusCode}."); }
        }
        return JsonSerializer.Deserialize<T>(text, Json) ?? throw new InvalidOperationException("Realmly returned an unreadable response.");
    }
}

internal sealed record PairingRequest(string RequestId, string PollSecret, string VerificationUri, int ExpiresIn);
internal sealed record PairingStatus(bool Approved, string? AccessToken);
internal sealed record UploadResult(bool Ok, bool Duplicate, bool Batch, int ImportedCount, int DuplicateCount, int LocationCount, string? CharacterName, string? ProfileUrl);
internal sealed record CompanionRelease(string Version, string DownloadUrl, string? Notes, string? Sha256 = null);
internal sealed record ApiError(string? Error);
internal sealed class RealmlyApiException(int statusCode, string message) : InvalidOperationException(message)
{
    public int StatusCode { get; } = statusCode;
}

internal sealed record ScreenshotCharacter(string Slug, string AdventurerId, string CharacterName, string RealmName, string? GameVersion)
{
    public override string ToString() => $"{CharacterName} / {RealmName} ({GameVersion ?? "unknown"})";
}
internal sealed record ScreenshotCharacters(ScreenshotCharacter[] Characters);
internal sealed record ScreenshotDays(string[] Days);
internal sealed record ScreenshotCandidate(string Id, string Title, string RequestedAtLocal);
internal sealed record ScreenshotMatchResponse(ScreenshotCandidate[] Matches, ScreenshotCandidate[] Candidates);
internal sealed record ScreenshotUploadResult(bool Ok, string Id);

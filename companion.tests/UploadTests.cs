using RealmlyCompanion;

internal static class UploadTests
{
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    public const string Source = """
            RealmlyDB = {
              ["profiles"] = {
                ["Test-Realm"] = {
                  adventurerId = "retail-test", character = {name="Test", realm="Realm", race="Human"},
                  locations = {
                    {id="one", capturedAt="2026-10-18T23:00:00Z", name="Herb", scopes={{mapId=1, x=0.5, y=0.5}}}, -- [1]
                    {id="two", capturedAt="2026-10-19T23:00:00Z", name="Fishing", notes="escaped \"quote\""},
                  }, milestones={fishing=true, unknown=nil},
                },
                Other={adventurerId="other",character={name="Elf", realm="Realm"},locations={[2]={id="one",capturedAt="unknown"}}},
              },
            }
            """;
    public static void Run()
    {
        const string source = Source;
        var preview = UploadPreview.Parse("\uFEFF-- saved data\n" + source, []);
        using (var http = new HttpClient(new SnapshotHandler(preview.Source)) { BaseAddress = new Uri("https://realmly.me/") })
        {
            var result = new RealmlyApi(http).UploadSourceAsync(preview.Source, "fixture-token", default).GetAwaiter().GetResult();
            Assert(result.Ok && result.LocationCount == 3, "Snapshot API response");
        }
        Assert(preview.Records == 3 && preview.NewRecords == 3 && preview.Characters.Length == 2, "Multi-character preview counts");
        Assert(preview.Summary.Contains("2026-10-18 to 2026-10-19 (UTC)") && preview.Summary.Contains("Adventure dates unavailable"), "Preview date range");
        var journal = new UploadJournal();
        journal.Record(preview, ClientKind.Retail, null);
        Assert(journal.UploadedRecords.Count == 0 && journal.History[0].Outcome.Contains("Failed"), "Failure incorrectly marked uploaded");
        journal.Record(preview, ClientKind.Retail, new(true, false, true, 2, 0, 3, null, null));
        Assert(UploadPreview.Parse(source, journal.UploadedRecords).NewRecords == 0, "Successful local record detection");
        var changed = source.Replace("id=\"two\"", "id=\"three\"");
        Assert(UploadPreview.Parse(changed, journal.UploadedRecords).NewRecords == 1, "One appended/replaced record");
        journal.Record(preview, ClientKind.Retail, new(true, true, true, 0, 2, 3, null, null));
        Assert(journal.History[0].Outcome == "Already on record.", "Duplicate upload outcome");
        for (var i = 0; i < 25; i++) journal.Record(preview, ClientKind.Forever, null);
        Assert(journal.History.Count == 20, "Bounded history");
        var directory = Path.Combine(Path.GetTempPath(), "realmly-journal-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "history.json");
        try
        {
            journal.Save(path); journal.Save(path);
            var loaded = UploadJournal.Load(path);
            Assert(loaded.History.Count == 20 && loaded.UploadedRecords.Count == 3, "Journal persistence");
            Assert(!File.ReadAllText(path).Contains("retail-test") && !File.ReadAllText(path).Contains("capturedAt"), "Journal contains raw source/identity");
        }
        finally { if (File.Exists(path)) File.Delete(path); if (Directory.Exists(directory)) Directory.Delete(directory); }
        foreach (var bad in new[] { "", "RealmlyDB={}; print('bad')", "RealmlyDB={profiles={x=os.execute('bad')}}", "NotRealmly={}", "RealmlyDB={profiles={}}", source[..^4], source.Replace("id=\"one\"", "other=\"one\""), new string('x', 2 * 1024 * 1024 + 1), "RealmlyDB=" + new string('{', 40) })
        {
            try { UploadPreview.Parse(bad, []); throw new Exception("Malformed/executable file accepted"); }
            catch (InvalidOperationException) { }
        }
        // A realistic accumulated history remains well below the byte limit,
        // yet nested scope/metadata tables exceed the old 60,000-field ceiling.
        var records = string.Join(",", Enumerable.Range(1, 3000).Select(index =>
            "{id=\"record-" + index + "\",capturedAt=\"2026-10-27T12:00:00Z\",name=\"Herb\",zone=\"Forest\",scopes={{mapId=50,x=0.5,y=0.5}}," +
            string.Join(",", Enumerable.Range(1, 16).Select(field => "metadata" + field + "=1")) + "}"));
        var largeHistory = "RealmlyDB={profiles={Test={adventurerId=\"retail-history\",character={name=\"Test\",realm=\"Realm\"},locations={" + records + "}}}}";
        var grownPreview = UploadPreview.Parse(largeHistory, []);
        Assert(grownPreview.Records == 3000 && grownPreview.NewRecords == 3000, "A valid growing history was mistaken for an invalid file");
        var largeAgain = UploadPreview.Parse(largeHistory, grownPreview.RecordKeys);
        Assert(largeAgain.Records == 3000 && largeAgain.NewRecords == 0, "Large-history deduplication changed");
        try {
            new SavedVariablesReader("RealmlyDB={extra={" + string.Join(",", Enumerable.Repeat("1", 250001)) + "}}").Read();
            throw new Exception("Unbounded SavedVariables complexity accepted");
        } catch (InvalidOperationException error) {
            Assert(error.Message.Contains("complexity limit"), "Complexity limit still reports a valid file as addon source");
        }
        Console.WriteLine("PASS safe SavedVariables preview, characters/dates, per-adventurer IDs, local deltas, duplicate/failure history and persistence");
    }

    private sealed class SnapshotHandler(string expected) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert(request.RequestUri!.AbsolutePath == "/api/collector-uploads" && request.Method == HttpMethod.Post, "Upload route changed");
            Assert(request.Headers.Authorization?.Parameter == "fixture-token", "Upload authentication changed");
            using var json = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert(json.RootElement.GetProperty("savedVariables").GetString() == expected, "Reviewed snapshot was altered");
            return new(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true,\"batch\":true,\"importedCount\":2,\"locationCount\":3}") };
        }
    }
}

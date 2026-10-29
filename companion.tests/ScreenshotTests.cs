using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RealmlyCompanion;

internal static class ScreenshotTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private)!.GetValue(owner)!;
    private static void Pump(Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(10); }
        Check(task.IsCompleted, "Screenshot operation did not finish"); task.GetAwaiter().GetResult();
    }
    internal const string Source = """
        RealmlyDB={client="retail",profiles={one={adventurerId="retail-fixture",character={name="Fixture",realm="Realm"},locations={
        {id="capture",screenshot={status="succeeded",requestedAtLocal="2026-10-29T00:00:02",events={{name="Level 40!",capturedAt="2026-10-28T23:00:02Z"}}}},
        {id="failed",screenshot={status="failed",requestedAtLocal="2026-10-29T00:00:02",events={{name="Bad",capturedAt="2026-10-28T23:00:02Z"}}}}
        }}}}
        """;
    public static void Run(string output)
    {
        Check(ScreenshotLibrary.FilenameClock("WoWScrnShot_102926_000004.jpg") == new DateTime(2026,10,29,0,0,4), "Local filename clock");
        Check(ScreenshotLibrary.FilenameClock("WoWScrnShot_022900_010203.png") == new DateTime(2000,2,29,1,2,3), "20YY leap date");
        Check(ScreenshotLibrary.FilenameClock("WoWScrnShot_023026_120001.jpg") is null && ScreenshotLibrary.FilenameClock("renamed.jpg") is null, "Invalid/renamed filenames");
        var captures = ScreenshotLibrary.Captures(Source, ClientKind.Retail);
        Check(captures.Length == 1 && captures[0].Day == "2026-10-28" && captures[0].Title == "Level 40!", "Successful capture and recorded UTC day");
        try { ScreenshotLibrary.Captures(Source,ClientKind.Forever);throw new Exception("Client mismatch accepted"); } catch (InvalidOperationException) { }
        try { ScreenshotLibrary.Captures("RealmlyDB={}; print('bad')",ClientKind.Retail);throw new Exception("Executable Lua accepted"); } catch (InvalidOperationException) { }
        var root = Path.Combine(Path.GetTempPath(), "realmly-screenshot-tests-" + Guid.NewGuid().ToString("N"));
        var folder = ScreenshotLibrary.Folder(root,ClientKind.Retail);Directory.CreateDirectory(folder);
        Check(!folder.Equals(ScreenshotLibrary.Folder(root,ClientKind.Forever)), "Game folders mixed");
        var path = Path.Combine(folder,"WoWScrnShot_102926_000004.jpg");
        var lua = Path.Combine(root,"Realmly.lua");File.WriteAllText(lua,Source);
        try
        {
            using (var image = new Bitmap(1600,900)) { using var graphics = Graphics.FromImage(image);graphics.Clear(Color.FromArgb(35,65,55));graphics.DrawString("REALMLY TEST / LEVEL 40",new Font("Segoe UI",32),Brushes.Gold,100,400);image.Save(path,System.Drawing.Imaging.ImageFormat.Jpeg); }
            var original = File.ReadAllBytes(path);
            var files = ScreenshotLibrary.Scan(folder,captures,default);Check(files.Length == 1 && files[0].Capture?.AdventurerId == "retail-fixture", "Unique +/-5 second match");
            Check(ScreenshotLibrary.Scan(folder,[captures[0],captures[0] with {AdventurerId="another",Id="another"}],default)[0].Capture is null, "Ambiguity guessed");
            Check(ScreenshotLibrary.Scan(folder,[captures[0] with {LocalTime=captures[0].LocalTime.AddMinutes(1)}],default)[0].Capture is null, "Distant timestamp matched");
            var data = ScreenshotLibrary.UploadImage(original);Check(data.StartsWith("data:image/jpeg;base64,") && data.Length < 2_800_000, "Upload image encoding");
            Check(File.ReadAllBytes(path).SequenceEqual(original), "Original image changed");
            using var handler = new ScreenshotHandler();using var http = new HttpClient(handler) { BaseAddress = new Uri("https://realmly.me/") };
            using var form = new ScreenshotManagerForm(root,ClientKind.Retail,lua,"fixture-token",new RealmlyApi(http),message=>message.Contains("2026-10-28") && message.Contains("Fixture"));
            form.StartPosition = FormStartPosition.Manual;form.Location = new Point(-30000,-30000);form.Show();
            Pump(WaitUntil(()=>Field<DataGridView>(form,"_grid").Rows.Count==1 && Field<Button>(form,"_upload").Enabled));
            var grid = Field<DataGridView>(form,"_grid");grid.Rows[0].Cells[0].Value = true;
            Pump((Task)typeof(ScreenshotManagerForm).GetMethod("UploadAsync",Private)!.Invoke(form,null)!);
            Check(handler.Uploads==1 && grid.Rows[0].Cells[0].Value is false, "Selected private upload and success state");
            grid.Rows[0].Cells[0].Value = true;
            Pump((Task)typeof(ScreenshotManagerForm).GetMethod("UploadAsync",Private)!.Invoke(form,null)!);
            Check(handler.Uploads==1 && Field<Label>(form,"_status").Text.Contains("already uploaded"), "Duplicate during visit not blocked");
            foreach (var size in new[] { new Size(1180,820),new Size(940,680) })
            {
                form.ClientSize=size;Application.DoEvents();
                using var bitmap = new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,$"companion-screenshots-{size.Width}.png"));
                var status = Field<Label>(form,"_status");Check(status.Bottom<=status.Parent!.ClientSize.Height,"Screenshot status clipped");
            }
            Check(File.ReadAllBytes(path).SequenceEqual(original),"Uploading modified original");form.Close();
            using var rejected = new ScreenshotManagerForm(root,ClientKind.Retail,lua,"fixture-token",new RealmlyApi(http),_=>true);
            rejected.StartPosition=FormStartPosition.Manual;rejected.Location=new Point(-30000,-30000);rejected.Show();
            Pump(WaitUntil(()=>Field<DataGridView>(rejected,"_grid").Rows.Count==1 && Field<Button>(rejected,"_upload").Enabled));
            var rejectedGrid=Field<DataGridView>(rejected,"_grid");rejectedGrid.Rows[0].Cells[0].Value=true;handler.RejectUploads=true;
            Pump((Task)typeof(ScreenshotManagerForm).GetMethod("UploadAsync",Private)!.Invoke(rejected,null)!);
            Check(rejectedGrid.Rows[0].Cells[0].Value is true && Field<Label>(rejected,"_status").Text.Contains("Fixture rejection"),"Rejected upload lost selection/error");
            Check(File.ReadAllBytes(path).SequenceEqual(original),"Rejected upload changed original");
            var cancelled=(Task)typeof(ScreenshotManagerForm).GetMethod("UploadAsync",Private)!.Invoke(rejected,null)!;
            Field<CancellationTokenSource>(rejected,"_work").Cancel();Pump(cancelled);
            Check(rejectedGrid.Rows[0].Cells[0].Value is true && handler.Uploads==1 && !Field<bool>(rejected,"_busy"),"Cancellation lost selection or left manager busy");
            rejected.Close();
            Console.WriteLine("PASS screenshot filename/UTC matching, client isolation, ambiguity, safe image conversion, bearer API upload, duplicate guard and window layouts");
        }
        finally
        {
            // Only the explicitly created fixture files/directories are removed.
            File.Delete(path);File.Delete(lua);Directory.Delete(folder);Directory.Delete(Path.GetDirectoryName(folder)!);Directory.Delete(root);
        }
    }
    private static async Task WaitUntil(Func<bool> ready) { var until=DateTime.UtcNow.AddSeconds(10);while(!ready()&&DateTime.UtcNow<until)await Task.Delay(20);Check(ready(),"Manager did not load"); }
    private sealed class ScreenshotHandler : HttpMessageHandler
    {
        public int Uploads;
        public bool RejectUploads;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Check(request.Headers.Authorization?.ToString()=="Bearer fixture-token","Screenshot token missing");var url=request.RequestUri!.AbsolutePath;object response;
            if(url.EndsWith("screenshot-characters"))response=new {characters=new[]{new {slug="fixture",adventurerId="retail-fixture",characterName="Fixture",realmName="Realm",gameVersion="retail"},new {slug="other",adventurerId="forever-fixture",characterName="Other",realmName="Realm",gameVersion="forever"}}};
            else if(url.EndsWith("screenshot-days"))response=new {days=new[]{"2026-10-28"}};
            else if(url.EndsWith("screenshot-matches"))response=new {matches=new[]{new {id="capture",title="Level 40!",requestedAtLocal="2026-10-29T00:00:02"}},candidates=Array.Empty<object>()};
            else if(request.Method==HttpMethod.Post && url.EndsWith("screenshots"))
            {
                using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                Check(json.RootElement.GetProperty("milestoneId").GetString()=="capture","Capture id lost");
                Check(url.Contains("/fixture/days/2026-10-28/"),"Local date used as UTC adventure date");
                Check(!json.RootElement.TryGetProperty("isPublic",out _),"Upload implicitly shared");
                if(RejectUploads)return new HttpResponseMessage(HttpStatusCode.BadRequest){Content=new StringContent("{\"error\":\"Fixture rejection\"}",Encoding.UTF8,"application/json")};
                Uploads++;response=new {ok=true,id="fixture-image"};
            }
            else throw new Exception("Unexpected screenshot request: "+url);
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(response),Encoding.UTF8,"application/json")};
        }
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.Win32;
using Windows.Media.Control;
using IcalCalendar = Ical.Net.Calendar;

namespace FocusGlance;

/// <summary>
/// Settings are a free-form JSON object owned by the UI (defaults live in ui/common.js).
/// The host only reads the few keys it needs itself.
/// </summary>
public sealed class SettingsStore
{
    public JsonObject Data { get; private set; } = new();

    public static string DataDir
    {
        get
        {
            // FOCUSGLANCE_DATA=<folder> keeps a separate profile (demos, testing)
            var d = Environment.GetEnvironmentVariable("FOCUSGLANCE_DATA") is { Length: > 0 } custom ? custom
                  : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusGlance");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    public static string SoundsDir { get { var d = Path.Combine(DataDir, "sounds"); Directory.CreateDirectory(d); return d; } }

    // A settings file next to the exe wins (portable mode); otherwise per-user AppData.
    static string PortablePath => Path.Combine(AppContext.BaseDirectory, "Focus-with-a-glance.settings.json");
    static string UserPath => Path.Combine(DataDir, "settings.json");
    static string ActivePath => File.Exists(PortablePath) ? PortablePath : UserPath;

    public static SettingsStore Load()
    {
        var s = new SettingsStore();
        try
        {
            if (File.Exists(ActivePath) && JsonNode.Parse(File.ReadAllText(ActivePath)) is JsonObject o)
                s.Data = Migrate(o);
        }
        catch { }
        return s;
    }

    // v2.0 used PascalCase properties; carry them over once.
    static JsonObject Migrate(JsonObject o)
    {
        if (!o.ContainsKey("Name") && !o.ContainsKey("Use24h")) return o;
        var map = new Dictionary<string, string>
        {
            ["Name"] = "name", ["IcalUrls"] = "icalUrls", ["Use24h"] = "use24h", ["ShowSeconds"] = "showSeconds",
            ["StartWithWindows"] = "startWithWindows", ["LyricsPinned"] = "pinned", ["Tint"] = "tint",
        };
        var n = new JsonObject();
        foreach (var (k, v) in o) n[map.TryGetValue(k, out var nk) ? nk : k] = v?.DeepClone();
        return n;
    }

    public void Save()
    {
        try { File.WriteAllText(ActivePath, Data.ToJsonString(new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
    }

    public void Replace(JsonObject o) { Data = o; Save(); }

    public void Patch(JsonObject p)
    {
        foreach (var (k, v) in p) Data[k] = v?.DeepClone();
        Save();
    }

    T Get<T>(string key, T def)
    {
        try { var n = Data[key]; return n == null ? def : n.GetValue<T>(); } catch { return def; }
    }

    public bool StartWithWindows => Get("startWithWindows", true);
    public string Monitor => Get("monitor", "");
    public string Position => Get("position", "center");
    public bool Pinned => Get("pinned", false);

    public List<string> IcalUrls
    {
        get
        {
            try { return Data["icalUrls"]?.AsArray().Select(x => x?.GetValue<string>() ?? "").Where(x => x.Length > 0).ToList() ?? new(); }
            catch { return new(); }
        }
    }
}

static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "FocusWithAGlance";
    const string TaskId = "FocusGlanceStartup"; // must match <uap5:StartupTask TaskId> in Package.appxmanifest

    public static void Apply(bool enabled)
    {
        if (AppInfo.IsPackaged) { _ = ApplyPackaged(enabled); return; }
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (enabled && Environment.ProcessPath is string exe)
                key.SetValue(ValueName, $"\"{exe}\"");
            else if (!enabled && key.GetValue(ValueName) != null)
                key.DeleteValue(ValueName, false);
        }
        catch { }
    }

    // Store (MSIX) apps can't use the Run key; they use a declared startup task instead.
    static async Task ApplyPackaged(bool enabled)
    {
        try
        {
            var task = await Windows.ApplicationModel.StartupTask.GetAsync(TaskId);
            if (enabled && task.State == Windows.ApplicationModel.StartupTaskState.Disabled)
                await task.RequestEnableAsync();
            else if (!enabled && task.State == Windows.ApplicationModel.StartupTaskState.Enabled)
                task.Disable();
        }
        catch { }
    }
}

sealed class MediaService
{
    readonly Control ui;
    readonly Action<object> post;
    GlobalSystemMediaTransportControlsSessionManager? mgr;
    GlobalSystemMediaTransportControlsSession? cur;
    readonly System.Windows.Forms.Timer poll = new() { Interval = 1500 };
    string artKey = "";
    string? artData;
    string lastJson = "";
    bool busy;

    public MediaService(Control ui, Action<object> post) { this.ui = ui; this.post = post; }

    public async Task InitAsync()
    {
        mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        mgr.CurrentSessionChanged += (_, _) => ui.BeginInvoke(Hook);
        mgr.SessionsChanged += (_, _) => ui.BeginInvoke(Hook);
        Hook();
        poll.Tick += async (_, _) => await PushAsync();
        poll.Start();
    }

    void Hook()
    {
        var s = mgr?.GetCurrentSession();
        if (!ReferenceEquals(s, cur))
        {
            if (cur != null)
            {
                cur.MediaPropertiesChanged -= OnProps;
                cur.PlaybackInfoChanged -= OnPlayback;
                cur.TimelinePropertiesChanged -= OnTimeline;
            }
            cur = s;
            if (cur != null)
            {
                cur.MediaPropertiesChanged += OnProps;
                cur.PlaybackInfoChanged += OnPlayback;
                cur.TimelinePropertiesChanged += OnTimeline;
            }
        }
        _ = PushAsync();
    }

    void OnProps(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e) => ui.BeginInvoke(() => _ = PushAsync());
    void OnPlayback(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e) => ui.BeginInvoke(() => _ = PushAsync());
    void OnTimeline(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e) => ui.BeginInvoke(() => _ = PushAsync());

    public void ForceResend() { lastJson = ""; _ = PushAsync(); }

    public async Task PushAsync()
    {
        if (busy) return;
        busy = true;
        try
        {
            var s = cur;
            if (s == null) { Send(new { t = "media", has = false }); return; }
            var props = await s.TryGetMediaPropertiesAsync();
            var pb = s.GetPlaybackInfo();
            var tl = s.GetTimelineProperties();
            string key = (props?.Title ?? "") + "|" + (props?.Artist ?? "");
            if (key != artKey)
            {
                artKey = key; artData = null;
                if (props?.Thumbnail != null)
                {
                    try
                    {
                        using var st = await props.Thumbnail.OpenReadAsync();
                        using var ns = st.AsStreamForRead();
                        using var ms = new MemoryStream();
                        await ns.CopyToAsync(ms);
                        if (ms.Length > 0 && ms.Length < 2_000_000)
                            artData = $"data:{(string.IsNullOrEmpty(st.ContentType) ? "image/png" : st.ContentType)};base64,{Convert.ToBase64String(ms.ToArray())}";
                    }
                    catch { }
                }
            }
            bool playing = pb?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            Send(new
            {
                t = "media",
                has = !string.IsNullOrEmpty(props?.Title),
                app = s.SourceAppUserModelId ?? "",
                title = props?.Title ?? "",
                artist = props?.Artist ?? "",
                album = props?.AlbumTitle ?? "",
                art = artData,
                playing,
                pos = tl.Position.TotalSeconds,
                dur = (tl.EndTime - tl.StartTime).TotalSeconds,
                upd = tl.LastUpdatedTime.ToUnixTimeMilliseconds(),
            });
        }
        catch { }
        finally { busy = false; }
    }

    void Send(object o)
    {
        var j = JsonSerializer.Serialize(o);
        if (j == lastJson) return;
        lastJson = j;
        post(o);
    }

    public async Task Command(string c)
    {
        var s = cur;
        if (s == null) return;
        try
        {
            switch (c)
            {
                case "toggle": await s.TryTogglePlayPauseAsync(); break;
                case "next": await s.TrySkipNextAsync(); break;
                case "prev": await s.TrySkipPreviousAsync(); break;
            }
        }
        catch { }
        await Task.Delay(300);
        await PushAsync();
    }
}

sealed class CalendarService
{
    readonly HttpClient http;
    readonly Action<object> post;
    List<string> urls = new();
    System.Threading.Timer? timer;
    object? last;

    public CalendarService(HttpClient http, Action<object> post) { this.http = http; this.post = post; }

    public void Start(IEnumerable<string> list)
    {
        urls = list.Select(u => u.Trim()).Where(u => u.Length > 0).ToList();
        timer?.Dispose();
        timer = new System.Threading.Timer(_ => _ = Refresh(), null, 0, 10 * 60 * 1000);
    }

    public void Resend() { if (last != null) post(last); }

    static long Ms(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    async Task Refresh()
    {
        var events = new List<object>();
        var seen = new HashSet<string>();
        int errors = 0;
        var from = DateTime.UtcNow.AddHours(-12);
        var to = DateTime.UtcNow.AddHours(36);
        foreach (var raw in urls)
        {
            try
            {
                var u = raw.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase) ? "https://" + raw[9..] : raw;
                var ics = await http.GetStringAsync(u);
                var cal = IcalCalendar.Load(ics);
                var account = cal.Properties.FirstOrDefault(p => p.Name == "X-WR-CALNAME")?.Value?.ToString() ?? "Calendar";
                foreach (var occ in cal.GetOccurrences(new CalDateTime(from, "UTC"), new CalDateTime(to, "UTC")))
                {
                    if (occ.Source is not CalendarEvent ev || ev.IsAllDay) continue;
                    if (string.Equals(ev.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase)) continue;
                    var conf = ev.Properties.FirstOrDefault(p => p.Name == "X-GOOGLE-CONFERENCE")?.Value?.ToString();
                    var text = $"{ev.Description} {ev.Location} {ev.Url} {conf}";
                    if (!text.Contains("meet.google.com", StringComparison.OrdinalIgnoreCase)) continue;
                    var s = occ.Period.StartTime.AsUtc;
                    var e = occ.Period.EndTime?.AsUtc ?? s + ev.Duration;
                    var title = ev.Summary ?? "Meeting";
                    if (!seen.Add(title + "|" + s.Ticks)) continue;
                    events.Add(new { title, start = Ms(s), end = Ms(e), account });
                }
            }
            catch { errors++; }
        }
        last = new { t = "meet", events, configured = urls.Count, errors };
        post(last);
    }
}

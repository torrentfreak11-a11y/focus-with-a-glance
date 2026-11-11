using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace FocusGlance;

static class Program
{
    /// <summary>Started by the app itself after its browser engine died: skip the greeting.</summary>
    public static bool Restarted { get; private set; }

    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(false, "FocusWithAGlance.SingleInstance.v2");
        bool owned;
        // after a self-restart, give the previous copy a moment to exit
        try { owned = mutex.WaitOne(args.Contains("--restart") ? 15000 : 0); }
        catch (AbandonedMutexException) { owned = true; }
        if (!owned) return;
        Restarted = args.Contains("--restart");
        // never vanish silently: log anything unexpected and keep running when it is safe to
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => GlanceForm.Crash("ui", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => GlanceForm.Crash("fatal", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { GlanceForm.Crash("task", e.Exception); e.SetObserved(); };
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new GlanceForm());
    }
}

/// <summary>Shared WebView2 plumbing for the widget and the settings window.</summary>
static class Web
{
    public static readonly string[] AllowedHosts =
    {
        "ipwho.is", "ipapi.co", "api.aladhan.com", "api.open-meteo.com", "geocoding-api.open-meteo.com",
        "open.er-api.com", "lrclib.net",
    };

    public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";

    static Task<CoreWebView2Environment>? env;
    public static string UiDir { get; private set; } = "";

    public static Task<CoreWebView2Environment> Env() => env ??= CoreWebView2Environment.CreateAsync(
        null, Path.Combine(SettingsStore.DataDir, "webview"),
        new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));

    /// <summary>Extracts the embedded ui/* files (or uses FOCUSGLANCE_UI=&lt;folder&gt; while developing).</summary>
    public static void PrepareUi()
    {
        var dev = Environment.GetEnvironmentVariable("FOCUSGLANCE_UI");
        if (!string.IsNullOrEmpty(dev) && Directory.Exists(dev)) { UiDir = dev; return; }
        UiDir = Path.Combine(SettingsStore.DataDir, "ui");
        Directory.CreateDirectory(UiDir);
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith("ui/")))
        {
            using var s = asm.GetManifestResourceStream(name)!;
            using var f = File.Create(Path.Combine(UiDir, name[3..]));
            s.CopyTo(f);
        }
    }

    public static void Configure(CoreWebView2 core)
    {
        core.SetVirtualHostNameToFolderMapping("glance.local", UiDir, CoreWebView2HostResourceAccessKind.Deny);
        core.SetVirtualHostNameToFolderMapping("sounds.glance.local", SettingsStore.SoundsDir, CoreWebView2HostResourceAccessKind.Allow);
        var st = core.Settings;
        st.AreDefaultContextMenusEnabled = false;
        st.AreDevToolsEnabled = Environment.GetEnvironmentVariable("FOCUSGLANCE_DEVTOOLS") == "1";
        st.IsZoomControlEnabled = false;
        st.AreBrowserAcceleratorKeysEnabled = false;
        st.IsStatusBarEnabled = false;
        st.IsPinchZoomEnabled = false;
        st.IsSwipeNavigationEnabled = false;
        core.NewWindowRequested += (_, a) => a.Handled = true;
    }

    public static async Task Fetch(HttpClient http, int id, string url, Action<object> post)
    {
        try
        {
            var uri = new Uri(url);
            if (uri.Scheme != "https" || !AllowedHosts.Contains(uri.Host)) throw new InvalidOperationException("blocked host");
            using var r = await http.GetAsync(uri);
            var body = await r.Content.ReadAsStringAsync();
            post(new { t = "fetch", id, ok = r.IsSuccessStatusCode, status = (int)r.StatusCode, body });
        }
        catch (Exception ex)
        {
            post(new { t = "fetch", id, ok = false, status = 0, body = ex.Message });
        }
    }

    public static void Send(WebView2 web, object msg)
    {
        try { web.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(msg)); } catch { }
    }
}

sealed class GlanceForm : Form
{
    // Logical (96-dpi) size of the overlay window. Everything outside the visible glass is click-through.
    const int LogicalW = 460, LogicalH = 240;
    // FOCUSGLANCE_ZOOM=2.5 renders the whole widget larger (crisp promo recordings)
    static readonly float Zoom = float.TryParse(Environment.GetEnvironmentVariable("FOCUSGLANCE_ZOOM"),
        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z) && z is >= 1 and <= 4 ? z : 1f;

    readonly WebView2 web = new();
    readonly System.Windows.Forms.Timer cursorTimer = new() { Interval = 30 };
    readonly System.Windows.Forms.Timer volumeTimer = new() { Interval = 120 };
    readonly NotifyIcon tray = new();
    readonly ToolStripMenuItem autostartItem = new("Start with Windows") { CheckOnClick = true };
    public readonly HttpClient Http;
    public readonly SettingsStore Store = SettingsStore.Load();
    readonly VolumeWatcher volume = new();
    MediaService? media;
    CalendarService? calendar;
    SettingsWindow? settingsWindow;
    FullscreenWatcher? fullscreen;
    BluetoothWatcher? bluetooth;
    public bool Pharaoh { get; private set; }
    bool hiddenForFullscreen;

    bool ready, clickThrough = true, cursorInside, focusable;
    int lastCx = int.MinValue, lastCy, lastVol = -1, ticks;
    bool lastMuted;
    float scale = 1f;

    public GlanceForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        Text = "Focus with a glance";

        web.Dock = DockStyle.Fill;
        web.DefaultBackgroundColor = Color.Transparent;
        Controls.Add(web);

        Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        Http.DefaultRequestHeaders.UserAgent.ParseAdd($"FocusWithAGlance/{Web.Version} (Windows desktop widget)");

        BuildTray();
        cursorTimer.Tick += (_, _) => PollCursor();
        volumeTimer.Tick += (_, _) => PollVolume();
        SystemEvents.DisplaySettingsChanged += (_, _) => BeginInvoke(PlaceWindow);
        Startup.Apply(Store.StartWithWindows);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST
                        | Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetLayeredWindowAttributes(Handle, 0, 255, Native.LWA_ALPHA);
        var m = new Native.MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        Native.DwmExtendFrameIntoClientArea(Handle, ref m);
        int noRound = 1; // DWMWCP_DONOTROUND
        Native.DwmSetWindowAttribute(Handle, 33, ref noRound, sizeof(int));
        PlaceWindow();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        PlaceWindow();
    }

    void PlaceWindow()
    {
        var screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == Store.Monitor) ?? Screen.PrimaryScreen ?? Screen.AllScreens[0];
        scale = DeviceDpi / 96f * Zoom;
        int w = (int)Math.Round(LogicalW * scale), h = (int)Math.Round(LogicalH * scale);
        var b = screen.Bounds;
        if (Store.Position == "taskbar")
        {
            // a pill on the left end of the taskbar; cards open upwards from it
            var wa = screen.WorkingArea;
            taskbarH = wa.Bottom < b.Bottom ? (b.Bottom - wa.Bottom) / scale : 48;
            Bounds = new Rectangle(b.Left, b.Bottom - h, w, h);
        }
        else
        {
            int x = Store.Position switch
            {
                "left" => b.Left,
                "right" => b.Right - w,
                _ => b.Left + (b.Width - w) / 2,
            };
            Bounds = new Rectangle(x, b.Top, w, h);
        }
        if (ready) Post(new { t = "place", tb = Math.Round(taskbarH) });
    }
    double taskbarH = 48;

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        PlaceWindow();
        try
        {
            Web.PrepareUi();
            await web.EnsureCoreWebView2Async(await Web.Env());
            var core = web.CoreWebView2;
            Web.Configure(core);
            if (Zoom != 1f) web.ZoomFactor = Zoom;
            core.WebMessageReceived += OnWebMessage;
            core.ProcessFailed += OnWebProcessFailed;
            core.Navigate("https://glance.local/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Focus with a glance needs the Microsoft Edge WebView2 Runtime.\n\n" + ex.Message,
                "Focus with a glance", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        cursorTimer.Start();
        volumeTimer.Start();
        try { media = new MediaService(this, Post); await media.InitAsync(); } catch { media = null; }
        calendar = new CalendarService(Http, Post);
        calendar.Start(Store.IcalUrls);

        fullscreen = new FullscreenWatcher(CurrentScreen);
        fullscreen.Changed += on => { if (IsHandleCreated) BeginInvoke(() => OnFullscreen(on)); };
        fullscreen.Start();
        bluetooth = new BluetoothWatcher(Post);
        bluetooth.Start();
        Pharaoh = await Licenses.HasPharaohAsync(Handle);
        SendLicense();
    }

    Screen CurrentScreen() => Screen.AllScreens.FirstOrDefault(s => s.DeviceName == Store.Monitor) ?? Screen.PrimaryScreen ?? Screen.AllScreens[0];

    // Games, full-screen videos and presentations: fade the widget away and let every click through.
    void OnFullscreen(bool on)
    {
        bool hide = on && Store.AutoHide;
        if (hide == hiddenForFullscreen) return;
        hiddenForFullscreen = hide;
        Log("fullscreen " + (hide ? "hide" : "show"));
        if (hide) SetClickThrough(true);
        Post(new { t = "fs", v = hide });
    }

    void SendLicense()
    {
        Post(new { t = "lic", pharaoh = Pharaoh, store = AppInfo.IsPackaged });
        settingsWindow?.PushInit();
    }

    public async void BuyPharaoh()
    {
        if (await Licenses.BuyPharaohAsync(Handle)) Pharaoh = true;
        else Pharaoh = await Licenses.HasPharaohAsync(Handle);
        SendLicense();
    }

    // The page's renderer can die (driver resets, sleep/resume, low memory). Bring it back instead of leaving an empty window.
    void OnWebProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        Log($"webview process failed: {e.ProcessFailedKind} {e.Reason}");
        ready = false;
        if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
        {
            // the whole browser is gone: start a fresh copy of the app
            try
            {
                if (Environment.ProcessPath is string exe)
                {
                    tray.Visible = false;
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Arguments = "--restart" });
                }
            }
            catch { }
            Application.Exit();
            return;
        }
        BeginInvoke(() => { try { web.CoreWebView2?.Reload(); } catch { } });
    }

    public static void Crash(string kind, Exception? ex)
    {
        try
        {
            var path = Path.Combine(SettingsStore.DataDir, "crash.log");
            if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024) File.Delete(path);
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{kind}] v{Web.Version}\n{ex}\n\n");
        }
        catch { }
    }

    public void Post(object msg)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Post(msg)); return; }
        if (!ready) return;
        Web.Send(web, msg);
    }

    void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        JsonNode? n;
        try { n = JsonNode.Parse(e.WebMessageAsJson); } catch { return; }
        if (n == null) return;
        switch ((string?)n["t"])
        {
            case "ready":
                ready = true;
                // placement, license and restart state first: the settings message starts the widget
                Post(new { t = "place", tb = Math.Round(taskbarH), restarted = Program.Restarted });
                Post(new { t = "lic", pharaoh = Pharaoh, store = AppInfo.IsPackaged });
                SendSettings();
                lastVol = -1; PollVolume();
                media?.ForceResend();
                calendar?.Resend();
                Post(new { t = "fs", v = hiddenForFullscreen });
                break;
            case "hit":
                if (!focusable && !hiddenForFullscreen) SetClickThrough(!(bool)n["v"]!);
                break;
            case "buy":
                BuyPharaoh();
                break;
            case "fetch":
                _ = Web.Fetch(Http, (int)n["id"]!, (string)n["url"]!, Post);
                break;
            case "media":
                if (media != null) _ = media.Command((string)n["cmd"]!);
                break;
            case "patch":
                if (n["patch"] is JsonObject p)
                {
                    Store.Patch(p.DeepClone().AsObject());
                    settingsWindow?.PushInit();
                }
                break;
            case "focus":
                SetFocusable((bool)n["v"]!);
                break;
            case "log":
                Log("js " + (string?)n["m"]);
                break;
            case "settings":
                OpenSettings((string?)n["section"]);
                break;
        }
    }

    static readonly string? LogPath = Environment.GetEnvironmentVariable("FOCUSGLANCE_LOG");
    public static bool Logging => LogPath != null;
    public static void Log(string s)
    {
        if (LogPath == null) return;
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {s}\n"); } catch { }
    }

    void SendSettings() =>Post(new { t = "settings", data = Store.Data });

    /// <summary>Called by the settings window on every change (live preview).</summary>
    public void ApplySettings(JsonObject data)
    {
        string oldIcal = string.Join("\n", Store.IcalUrls), oldPlace = Store.Monitor + "|" + Store.Position;
        Store.Replace(data);
        Startup.Apply(Store.StartWithWindows);
        autostartItem.Checked = Store.StartWithWindows;
        if (oldPlace != Store.Monitor + "|" + Store.Position) PlaceWindow();
        if (oldIcal != string.Join("\n", Store.IcalUrls)) calendar?.Start(Store.IcalUrls);
        SendSettings();
    }

    void SetClickThrough(bool on)
    {
        if (on == clickThrough || !IsHandleCreated) return;
        clickThrough = on;
        if (on) Native.SetExStyle(Handle, Native.WS_EX_TRANSPARENT, 0);
        else Native.SetExStyle(Handle, 0, Native.WS_EX_TRANSPARENT);
    }

    void SetFocusable(bool on)
    {
        if (on == focusable) return;
        focusable = on;
        if (on)
        {
            Native.SetExStyle(Handle, 0, Native.WS_EX_NOACTIVATE | Native.WS_EX_TRANSPARENT);
            clickThrough = false;
            Native.ForceForeground(Handle);
            web.Focus();
        }
        else
        {
            Native.SetExStyle(Handle, Native.WS_EX_NOACTIVATE, 0);
        }
    }

    void PollCursor()
    {
        if (!ready) return;
        Native.GetCursorPos(out var p);
        var r = Bounds;
        if (r.Contains(p.X, p.Y))
        {
            int cx = (int)((p.X - r.Left) / scale), cy = (int)((p.Y - r.Top) / scale);
            if (cx != lastCx || cy != lastCy || !cursorInside)
            {
                lastCx = cx; lastCy = cy;
                Post(new { t = "cursor", x = cx, y = cy, inside = true });
            }
            cursorInside = true;
        }
        else if (cursorInside)
        {
            cursorInside = false;
            lastCx = int.MinValue;
            Post(new { t = "cursor", x = -1, y = -1, inside = false });
            if (!focusable) SetClickThrough(true);
        }
    }

    void PollVolume()
    {
        if (!ready) return;
        // stay above other topmost windows: every ~5s, or twice a second in the taskbar (clicking the taskbar raises it)
        if (++ticks % (Store.Position == "taskbar" ? 4 : 40) == 0)
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        var v = volume.Read();
        if (v == null) return;
        if (v.Value.level != lastVol || v.Value.muted != lastMuted)
        {
            lastVol = v.Value.level; lastMuted = v.Value.muted;
            Post(new { t = "vol", v = lastVol, muted = lastMuted });
        }
    }

    // ---------- tray ----------
    void BuildTray()
    {
        tray.Icon = MakeTrayIcon();
        tray.Text = "Focus with a glance";
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings(null));
        autostartItem.Checked = Store.StartWithWindows;
        autostartItem.CheckedChanged += (_, _) =>
        {
            if (Store.StartWithWindows == autostartItem.Checked) return;
            Store.Patch(new JsonObject { ["startWithWindows"] = autostartItem.Checked });
            Startup.Apply(Store.StartWithWindows);
            settingsWindow?.PushInit();
        };
        menu.Items.Add(autostartItem);
        menu.Items.Add("Reload widget", null, (_, _) => { if (web.CoreWebView2 != null) { ready = false; web.CoreWebView2.Reload(); } });
        if (!AppInfo.IsPackaged && AppInfo.DonateUrl.Length > 0)
            menu.Items.Add("Support the project ♥", null, (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(AppInfo.DonateUrl) { UseShellExecute = true }); } catch { }
            });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => { tray.Visible = false; Application.Exit(); });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => OpenSettings(null);
        tray.Visible = true;
    }

    static Icon MakeTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var fill = new SolidBrush(Color.FromArgb(235, 22, 26, 38));
            g.FillEllipse(fill, 1, 1, 30, 30);
            using var rim = new Pen(Color.FromArgb(200, 190, 225, 255), 1.6f);
            g.DrawEllipse(rim, 2, 2, 28, 28);
            using var hand = new Pen(Color.White, 2.6f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLine(hand, 16, 16, 16, 8);
            g.DrawLine(hand, 16, 16, 22, 19);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    void OpenSettings(string? section)
    {
        if (settingsWindow == null || settingsWindow.IsDisposed)
        {
            settingsWindow = new SettingsWindow(this, section);
            settingsWindow.FormClosed += (_, _) => settingsWindow = null;
            settingsWindow.Show();
        }
        else
        {
            if (settingsWindow.WindowState == FormWindowState.Minimized) settingsWindow.WindowState = FormWindowState.Normal;
            settingsWindow.ShowSection(section);
        }
        settingsWindow.Activate();
        Native.ForceForeground(settingsWindow.Handle);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        tray.Visible = false;
        tray.Dispose();
        base.OnFormClosed(e);
    }
}

/// <summary>Glass-styled settings window (ui/settings.html). Every change is applied live.</summary>
sealed class SettingsWindow : Form
{
    const int LogicalW = 800, LogicalH = 600;
    readonly GlanceForm owner;
    readonly WebView2 web = new();
    readonly string? startSection;
    bool ready;

    public SettingsWindow(GlanceForm owner, string? section)
    {
        this.owner = owner;
        startSection = section;
        Text = "Focus with a glance — Settings";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        try { if (Environment.ProcessPath is string p) Icon = Icon.ExtractAssociatedIcon(p); } catch { }
        web.Dock = DockStyle.Fill;
        web.DefaultBackgroundColor = Color.Transparent;
        Controls.Add(web);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        float s = DeviceDpi / 96f;
        ClientSize = new Size((int)(LogicalW * s), (int)(LogicalH * s));
        MinimumSize = new Size((int)(640 * s), (int)(460 * s));
        int on = 1;
        Native.DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int));           // dark title bar
        int backdrop = 3;                                                          // DWMSBT_TRANSIENTWINDOW (acrylic)
        Native.DwmSetWindowAttribute(Handle, 38, ref backdrop, sizeof(int));
        var m = new Native.MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        Native.DwmExtendFrameIntoClientArea(Handle, ref m);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        int w = Math.Min(Width, wa.Width - 40), h = Math.Min(Height, wa.Height - 40);
        Bounds = new Rectangle(wa.Left + (wa.Width - w) / 2, wa.Top + (wa.Height - h) / 2, w, h);
        try
        {
            await web.EnsureCoreWebView2Async(await Web.Env());
            var core = web.CoreWebView2;
            Web.Configure(core);
            core.WebMessageReceived += OnWebMessage;
            core.Navigate("https://glance.local/settings.html" + (startSection != null ? "#" + Uri.EscapeDataString(startSection) : ""));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text);
            Close();
        }
    }

    void Post(object msg)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Post(msg)); return; }
        if (ready) Web.Send(web, msg);
    }

    public void PushInit() => Post(new
    {
        t = "init",
        data = owner.Store.Data,
        version = Web.Version,
        store = AppInfo.IsPackaged,
        pharaoh = owner.Pharaoh,
        links = new
        {
            repo = AppInfo.RepoUrl,
            donate = AppInfo.IsPackaged ? "" : AppInfo.DonateUrl,
            privacy = AppInfo.PrivacyUrl,
        },
        screens = Screen.AllScreens.Select((s, i) => new
        {
            id = s.DeviceName,
            name = $"Display {i + 1} · {s.Bounds.Width}×{s.Bounds.Height}" + (s.Primary ? " (main)" : ""),
            primary = s.Primary,
        }),
    });

    public void ShowSection(string? section) { if (section != null) Post(new { t = "section", id = section }); }

    void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        JsonNode? n;
        try { n = JsonNode.Parse(e.WebMessageAsJson); } catch { return; }
        if (n == null) return;
        switch ((string?)n["t"])
        {
            case "ready":
                ready = true;
                PushInit();
                break;
            case "save":
                if (n["data"] is JsonObject d) owner.ApplySettings(d.DeepClone().AsObject());
                break;
            case "fetch":
                _ = Web.Fetch(owner.Http, (int)n["id"]!, (string)n["url"]!, Post);
                break;
            case "pick-sound":
                PickSound();
                break;
            case "buy":
                owner.BuyPharaoh();
                break;
            case "open":
                var url = (string?)n["url"];
                if (url != null && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
                break;
            case "close":
                Close();
                break;
        }
    }

    void PickSound()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Choose an adhan sound",
            Filter = "Audio files|*.mp3;*.m4a;*.aac;*.wav;*.ogg;*.opus|All files|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            foreach (var old in Directory.GetFiles(SettingsStore.SoundsDir, "adhan.*")) File.Delete(old);
            var name = "adhan" + ext;
            File.Copy(dlg.FileName, Path.Combine(SettingsStore.SoundsDir, name), true);
            Post(new { t = "picked", file = name, label = Path.GetFileName(dlg.FileName) });
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
    }
}

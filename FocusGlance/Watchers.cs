using System.Diagnostics;
using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Services.Store;

namespace FocusGlance;

/// <summary>Reports when a game, a full-screen video or a presentation takes over the screen.</summary>
sealed class FullscreenWatcher
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 800 };
    readonly Func<Screen> screen;
    public bool Active { get; private set; }
    public event Action<bool>? Changed;

    public FullscreenWatcher(Func<Screen> screen)
    {
        this.screen = screen;
        timer.Tick += (_, _) => Check();
    }

    public void Start() => timer.Start();

    void Check()
    {
        bool now = Detect();
        if (now == Active) return;
        Active = now;
        Changed?.Invoke(now);
    }

    bool Detect()
    {
        try
        {
            // 3 = D3D full screen (games), 4 = presentation mode, 2 = busy (another app is full screen)
            if (Native.SHQueryUserNotificationState(out int s) == 0 && (s == 3 || s == 4)) return true;
            var fg = Native.Foreground();
            if (fg == IntPtr.Zero || Native.WindowProcessId(fg) == Environment.ProcessId) return false;
            var cls = new StringBuilder(64);
            Native.GetClassName(fg, cls, cls.Capacity);
            var c = cls.ToString();
            if (c is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Windows.UI.Core.CoreWindow") return false;
            if (!Native.GetWindowRect(fg, out var r)) return false;
            var b = screen().Bounds;
            // borderless full screen (browsers' video full screen, most games) covers the whole monitor
            return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
        }
        catch { return false; }
    }
}

/// <summary>Announces Bluetooth devices as they connect, with a best-effort battery level.</summary>
sealed class BluetoothWatcher
{
    const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2"; // DEVPKEY_Bluetooth_Battery
    readonly Action<object> post;
    readonly List<DeviceWatcher> watchers = new();
    readonly HashSet<string> connected = new();

    public BluetoothWatcher(Action<object> post) => this.post = post;

    public void Start()
    {
        try
        {
            Watch(BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected), le: false);
            Watch(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected), le: true);
        }
        catch (Exception ex) { GlanceForm.Log("bt start " + ex.Message); }
    }

    void Watch(string aqs, bool le)
    {
        var w = DeviceInformation.CreateWatcher(aqs, new[] { "System.Devices.Aep.ContainerId" }, DeviceInformationKind.AssociationEndpoint);
        bool wready = false;   // each watcher first lists what is already connected
        w.Added += (s, d) =>
        {
            lock (connected) if (!connected.Add(d.Id)) return;
            var container = d.Properties.TryGetValue("System.Devices.Aep.ContainerId", out var c) && c is Guid g ? g : Guid.Empty;
            if (wready) _ = Announce(d.Id, d.Name, le, container, probe: false);
            else if (GlanceForm.Logging) _ = Announce(d.Id, d.Name, le, container, probe: true);   // already connected at startup: log only
        };
        w.Removed += (_, d) =>
        {
            lock (connected) connected.Remove(d.Id);
            if (wready) post(new { t = "bt", id = d.Id, on = false });
        };
        w.Updated += (_, _) => { };
        w.EnumerationCompleted += (_, _) => wready = true;
        w.Start();
        watchers.Add(w);
    }

    async Task Announce(string id, string name, bool le, Guid container, bool probe)
    {
        string kind = "other"; BluetoothLEDevice? led = null;
        try
        {
            if (le)
            {
                led = await BluetoothLEDevice.FromIdAsync(id);
                if (led != null) { name = Nice(led.Name, name); kind = KindFromAppearance(led.Appearance?.Category ?? 0); }
            }
            else
            {
                using var bd = await BluetoothDevice.FromIdAsync(id);
                if (bd != null) { name = Nice(bd.Name, name); kind = KindFromClass(bd.ClassOfDevice); }
            }
        }
        catch { }
        if (probe)
        {
            int? pb = await PnpBattery(container) ?? (led != null ? await GattBattery(led) : null);
            GlanceForm.Log($"bt present: {name} ({(le ? "LE" : "classic")}, {kind}) battery={(pb?.ToString() ?? "n/a")}");
            led?.Dispose();
            return;
        }
        post(new { t = "bt", id, on = true, name, kind, battery = (int?)null });
        // headsets report their battery a few seconds after connecting
        for (int i = 0; i < 4; i++)
        {
            await Task.Delay(i == 0 ? 1500 : 2500);
            int? b = await PnpBattery(container) ?? (led != null ? await GattBattery(led) : null);
            if (b != null) { post(new { t = "bt", id, on = true, name, kind, battery = b, update = true }); break; }
        }
        led?.Dispose();
    }

    static string Nice(string a, string b) => string.IsNullOrWhiteSpace(a) ? b : a;

    static async Task<int?> PnpBattery(Guid container)
    {
        if (container == Guid.Empty) return null;
        try
        {
            var list = await Windows.Devices.Enumeration.Pnp.PnpObject.FindAllAsync(
                Windows.Devices.Enumeration.Pnp.PnpObjectType.Device, new[] { BatteryKey },
                $"System.Devices.ContainerId:=\"{{{container}}}\"");
            foreach (var o in list)
                if (o.Properties.TryGetValue(BatteryKey, out var v) && v is byte level) return level;
        }
        catch { }
        return null;
    }

    static async Task<int?> GattBattery(BluetoothLEDevice d)
    {
        try
        {
            var svc = await d.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Uncached);
            if (svc.Status != GattCommunicationStatus.Success || svc.Services.Count == 0) return null;
            using var s = svc.Services[0];
            var ch = await s.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Uncached);
            if (ch.Status != GattCommunicationStatus.Success || ch.Characteristics.Count == 0) return null;
            var r = await ch.Characteristics[0].ReadValueAsync(BluetoothCacheMode.Uncached);
            if (r.Status != GattCommunicationStatus.Success) return null;
            var reader = Windows.Storage.Streams.DataReader.FromBuffer(r.Value);
            return reader.ReadByte();
        }
        catch { return null; }
    }

    static string KindFromClass(BluetoothClassOfDevice? c)
    {
        if (c == null) return "other";
        return c.MajorClass switch
        {
            BluetoothMajorClass.AudioVideo => c.MinorClass is BluetoothMinorClass.AudioVideoLoudspeaker or BluetoothMinorClass.AudioVideoPortableAudio ? "speaker" : "headphones",
            BluetoothMajorClass.Phone => "phone",
            BluetoothMajorClass.Computer => "computer",
            BluetoothMajorClass.Peripheral => c.MinorClass switch
            {
                BluetoothMinorClass.PeripheralJoystick or BluetoothMinorClass.PeripheralGamepad => "gamepad",
                _ => ((int)c.RawValue & 0x80) != 0 ? "mouse" : ((int)c.RawValue & 0x40) != 0 ? "keyboard" : "other",
            },
            BluetoothMajorClass.Wearable => "watch",
            _ => "other",
        };
    }

    static string KindFromAppearance(ushort category) => category switch
    {
        0x0F => "mouse",     // HID (refined below by the UI from the name when needed)
        0x03 => "watch",
        0x01 => "phone",
        0x02 => "computer",
        0x29 or 0x21 => "speaker",
        0x25 or 0x24 => "headphones",
        _ => "other",
    };
}

/// <summary>
/// Today's screen time: every 5 s, the app in front gets 5 s — unless nobody has touched the PC for 2 minutes.
/// Kept only in %LocalAppData%\FocusGlance\screentime.json; never sent anywhere.
/// </summary>
sealed class ScreenTime
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 5000 };
    readonly string path = Path.Combine(SettingsStore.DataDir, "screentime.json");
    readonly Dictionary<string, string> names = new();
    string day = DateTime.Today.ToString("yyyy-MM-dd");
    Dictionary<string, int> apps = new();
    int ticks;
    public bool Enabled { get; set; } = true;

    public ScreenTime()
    {
        try
        {
            if (File.Exists(path) && System.Text.Json.JsonSerializer.Deserialize<Saved>(File.ReadAllText(path)) is { } s && s.Day == day)
                apps = s.Apps ?? new();
        }
        catch { }
        timer.Tick += (_, _) => Tick();
    }

    public void Start() => timer.Start();

    void Tick()
    {
        if (!Enabled) return;
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (today != day) { day = today; apps.Clear(); }
        if (Native.IdleMs() > 120_000) return;
        var fg = Native.Foreground(); if (fg == IntPtr.Zero) return;
        var name = AppName(Native.WindowProcessId(fg)); if (name == null) return;
        apps[name] = apps.GetValueOrDefault(name) + 5;
        if (++ticks % 12 == 0) Save();   // once a minute
    }

    string? AppName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            var exe = p.ProcessName;
            if (exe is "explorer" or "ShellExperienceHost" or "SearchHost" or "StartMenuExperienceHost" or "LockApp" or "Focus-with-a-glance") return null;
            if (names.TryGetValue(exe, out var n)) return n;
            string? desc = null;
            try { desc = p.MainModule?.FileVersionInfo.FileDescription; } catch { }
            n = string.IsNullOrWhiteSpace(desc) ? exe : desc.Trim();
            names[exe] = n;
            return n;
        }
        catch { return null; }
    }

    void Save()
    {
        try { File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new Saved { Day = day, Apps = apps })); } catch { }
    }

    public object Snapshot()
    {
        var top = apps.OrderByDescending(kv => kv.Value).FirstOrDefault();
        return new { t = "st", total = apps.Values.Sum(), top = top.Key, topSecs = top.Value };
    }

    sealed class Saved { public string Day { get; set; } = ""; public Dictionary<string, int>? Apps { get; set; } }
}

/// <summary>Microsoft Store add-on ("Pharaonic pack"). Only the Store build can buy or own it.</summary>
static class Licenses
{
    /// <summary>The add-on's "Product ID" (in-app offer token) as created in Partner Center.</summary>
    public const string PharaohToken = "pharaoh";

    public static async Task<bool> HasPharaohAsync(IntPtr owner)
    {
#if OWNER
        await Task.CompletedTask;
        return true;   // the developer's own copy (built with -p:OwnerBuild=true), never published
#endif
#if DEBUG
        if (Environment.GetEnvironmentVariable("FOCUSGLANCE_PHARAOH") == "1") return true;
#endif
        if (!AppInfo.IsPackaged) return false;
        try
        {
            var ctx = StoreContext.GetDefault();
            WinRT.Interop.InitializeWithWindow.Initialize(ctx, owner);
            var lic = await ctx.GetAppLicenseAsync();
            return lic.AddOnLicenses.Values.Any(l => l.IsActive && string.Equals(l.InAppOfferToken, PharaohToken, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) { GlanceForm.Log("license " + ex.Message); return false; }
    }

    /// <summary>Opens the Store purchase dialog. Returns true when the add-on is owned afterwards.</summary>
    public static async Task<bool> BuyPharaohAsync(IntPtr owner)
    {
        if (!AppInfo.IsPackaged) return false;
        try
        {
            var ctx = StoreContext.GetDefault();
            WinRT.Interop.InitializeWithWindow.Initialize(ctx, owner);
            var products = await ctx.GetAssociatedStoreProductsAsync(new[] { "Durable" });
            var p = products.Products.Values.FirstOrDefault(x => string.Equals(x.InAppOfferToken, PharaohToken, StringComparison.OrdinalIgnoreCase));
            if (p == null) return false;
            var r = await p.RequestPurchaseAsync();
            return r.Status is StorePurchaseStatus.Succeeded or StorePurchaseStatus.AlreadyPurchased;
        }
        catch (Exception ex) { GlanceForm.Log("purchase " + ex.Message); return false; }
    }
}

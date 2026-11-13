<div align="center">

# Focus with a glance

**A liquid-glass notch for Windows: the time, your next prayer, meetings, music and more, one glance away.**

English · العربية — free and open source

**[⬇ Download the latest version](https://github.com/torrentfreak11-a11y/focus-with-a-glance/releases/latest)**

<img src="docs/screenshots/1-hero.jpg" alt="Focus with a glance: the full clock card in liquid glass at the top of the screen" width="900">

</div>

---

Focus with a glance sits at the top of your screen like a small glass notch. It stays out of the way: clicks
pass straight through it, and it only grows when you hover over it. Open it to flip between cards with the
arrows.

## Features

| | |
|---|---|
| 🕰️ **Clock** | Compact clock that enlarges on hover, 12/24-hour, optional seconds |
| 🕌 **Prayer times** | Auto location or city search, every major calculation method, Hanafi Asr, per-prayer minute tuning |
| 🔔 **Adhan & iqama** | Glowing alert at prayer time with a chime or your own adhan recording, then an iqama countdown |
| 🌙 **Ramadan mode** | Iftar and suhoor countdowns and the fasting day, automatic during Ramadan |
| 📅 **Hijri date** | Umm al-Qura and three other calendars, Arabic or English, four styles, ±2 day moon-sighting adjustment |
| 💱 **Currency** | Live rate between any two of ~160 currencies with a quick converter |
| ⛅ **Weather** | Current conditions and a 6-day forecast, with a live weather scene inside the glass |
| 🌍 **World clock** | Up to three time zones |
| ⏳ **Countdown** | Your own dates, plus Ramadan, Eid, Arafah and the Islamic New Year |
| 🎯 **Focus timer** | Pomodoro sessions with short and long breaks |
| 📹 **Google Meet** | Countdown to your next meeting from any number of Google calendars (iCal links) |
| 🎵 **Media** | Mini player for whatever is playing, with synced lyrics you can pin |
| 🔊 **Volume** | A small overlay when you change the volume |
| 🎨 **Look & position** | Accent and glass colors, tint, top left/center/right, **or in the taskbar**, any display |
| 🖱️ **Click-through** | It never blocks your work: clicks pass straight through everywhere except the glass itself |
| ⛅ **Live weather** | Animated sun, moon, clouds, rain, snow, fog, thunder and wind right beside the clock |
| 🎧 **Bluetooth** | Headphones and other devices show their name, battery and volume as they connect |
| 🎮 **Stays out of the way** | Hides automatically during games, full-screen video and presentations |
| 🗓️ **Calendars** | Gregorian, Hijri, Chinese lunar — and Coptic in the Pharaonic pack |
| 𓋹 **Pharaonic pack** | Store add-on: Coptic calendar, sand glass and sand animations, gold icons, avatars, your name in a cartouche, a temple gate opened by the ankh |
| 🌅 **Welcome card** | For 3 seconds at Windows start: greeting, weather, the next hours, what's playing and today's screen time |
| ⚽ **Live football** | Scores, live minute and scorers for the competitions and teams you follow, with goal alerts (GitHub version) |
| 🔔 **Gentle sounds** | An airy adhan chime, an optional chime on the hour, and severe-weather alerts |

The whole interface, settings included, is available in **English, Arabic and Chinese**, with full right-to-left layout
for Arabic. The first run asks for your language and sets things up for it.

## Screenshots

| | |
|---|---|
| ![Liquid motion: drip, land, split, merge](docs/screenshots/2-liquid.jpg) | ![Prayer times, adhan alert and iqama countdown](docs/screenshots/3-prayer.jpg) |
| ![Ramadan mode, Hijri date and countdowns](docs/screenshots/4-ramadan.jpg) | ![The cards in Arabic, right to left](docs/screenshots/5-arabic.jpg) |
| ![Media player with synced lyrics](docs/screenshots/6-music.jpg) | ![Meeting countdown, focus timer, world clock, weather](docs/screenshots/7-workday.jpg) |
| ![The compact clock, volume overlay and meeting pill](docs/screenshots/8-invisible.jpg) | ![One notch replaces eight apps](docs/screenshots/9-replaces.jpg) |
| ![Click-through, animated weather and Bluetooth battery](docs/screenshots/11-smart.jpg) | ![English, Arabic and Chinese](docs/screenshots/12-languages.jpg) |
| ![In the taskbar](docs/screenshots/13-taskbar.jpg) | ![The Pharaonic pack](docs/screenshots/10-pharaoh.jpg) |

## Install

Download `Focus-with-a-glance.exe` from [Releases](https://github.com/torrentfreak11-a11y/focus-with-a-glance/releases/latest) and run it. It is a single
portable file; nothing is installed. Windows SmartScreen may warn about an unknown publisher the first time;
choose *More info → Run anyway*.

Requires Windows 10 (2004) or Windows 11, with the Microsoft Edge WebView2 Runtime (already part of Windows 11).

**Portable mode:** create an empty `Focus-with-a-glance.settings.json` next to the exe, and settings are kept
there instead of in `%LocalAppData%\FocusGlance`.

## Privacy

No account, no analytics, no tracking. The widget talks directly to a few free public services (prayer times,
weather, exchange rates, lyrics). See [PRIVACY.md](PRIVACY.md) for the full list.

## Build from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
cd FocusGlance
dotnet publish -c Release -o ..\publish
```

This produces a single self-contained `publish\Focus-with-a-glance.exe`.

The Store package is built with `packaging\build-store.ps1` (see [packaging/README.md](packaging/README.md)).

### Project layout

```
FocusGlance/
  Program.cs        window, tray, WebView2 bridge, settings window
  Services.cs       settings store, startup, media session, calendar
  Native.cs         Win32 / Core Audio interop
  AppInfo.cs        public links, Store detection
  ui/index.html     the widget
  ui/settings.html  the settings window
  ui/common.js      defaults, translations, prayer and Hijri helpers
packaging/          MSIX manifest, assets and build script for the Store
```

### Developing the UI

Set these environment variables before starting the exe:

| Variable | Effect |
|---|---|
| `FOCUSGLANCE_UI=<path to FocusGlance\ui>` | Load the UI from disk instead of the embedded copy (use *Reload widget* in the tray menu after editing) |
| `FOCUSGLANCE_DEVTOOLS=1` | Enable WebView2 DevTools (F12) |
| `FOCUSGLANCE_LOG=<file>` | Write a debug log, including JavaScript errors |
| `FOCUSGLANCE_ZOOM=2.5` | Render the whole widget larger (1–4), for crisp recordings |
| `FOCUSGLANCE_DATA=<folder>` | Use a separate settings/profile folder instead of `%LocalAppData%\FocusGlance` |

## Contributing

Bug reports, translations and ideas are welcome. Please open an issue first for larger changes.

## License

[MIT](LICENSE)

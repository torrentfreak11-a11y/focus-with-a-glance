# Changelog

## 2.2.1

- Fixed: after an adhan with an iqama countdown, the widget stayed on the prayer card instead of returning to the
  clock (so the weather icon beside the clock never came back)

## 2.2.0

- First run starts with a language choice: **English, العربية or 中文** (full Chinese translation)
  - Arabic sets up prayer times, adhan and iqama alerts, Ramadan mode and the Hijri date automatically
  - English asks for a second calendar (Hijri → then offers prayer times and Ramadan, or Chinese, or none)
  - Chinese uses the Chinese lunar calendar first, with the Gregorian date beside it
- Calendars: main + second calendar — Gregorian, Hijri, Chinese, and Coptic (Pharaonic pack)
- Compact clock shows the weather beside the time, with animated sun, moon, clouds, rain, snow, fog, thunder and wind
- Bluetooth: when headphones or other devices connect, the notch shows the name, battery and volume
- Hides automatically during games, full-screen video and presentations
- New position: **in the taskbar**, as a small pill; cards open upward from it
- Pharaonic pack (Microsoft Store add-on): Coptic calendar, lapis-and-gold theme, hieroglyph details, your name in a cartouche
- Reliability: recovers on its own if its web view crashes; unexpected errors are written to `crash.log`
- Fixed: wind streaks overlapping the clock digits

## 2.1.1

- Fixed: on first launch the welcome text appeared before the glass finished dropping in
- Fixed: the English welcome title was cut off ("Welcome aboard" now)
- Fixed: Ramadan mode set to "always on" could show "Day 31" after Maghrib
- For developers: `FOCUSGLANCE_ZOOM` (render the widget larger) and `FOCUSGLANCE_DATA` (separate profile folder)
- README: screenshots

## 2.1.0

- Settings window (gear on the clock card, or the tray icon), fully in English and Arabic
- First-run name prompt
- Prayer times card with city search, calculation methods, Hanafi Asr and per-prayer tuning
- Adhan alert with chime or custom recording, and iqama countdown
- Ramadan mode: iftar and suhoor countdowns
- Hijri date in four calendars and four styles, with moon-sighting adjustment
- Currency card with ~160 currencies
- Focus (Pomodoro) timer, world clock and countdown cards
- Accent and glass colors, position (top left/center/right) and display choice
- Microsoft Store package with a startup task

## 2.0.0

- Liquid-glass notch, compact clock, full cards with arrows
- Weather, media player with synced lyrics, volume overlay
- Google Meet countdown from iCal links

# Privacy policy

*Focus with a glance — last updated 10 November 2026*

Focus with a glance does not collect, store or sell any personal data. There is no account, no analytics, no
advertising and no telemetry. The developer receives nothing from the app.

## What stays on your PC

Everything you enter: your name, settings, countdown dates, Google Calendar links and any adhan sound you
choose. These are saved in `%LocalAppData%\FocusGlance` (or next to the exe in portable mode). Uninstalling
the app or deleting that folder removes them.

## Online services the app contacts

To show live information, the app connects directly from your PC to these free public services. Each request
contains only what is needed to answer it, and the services' own privacy policies apply.

| Service | Used for | What it receives |
|---|---|---|
| ipwho.is, ipapi.co | Approximate location, only when Location is set to *auto-detect* | Your IP address |
| api.aladhan.com | Prayer times | Coordinates and calculation method |
| api.open-meteo.com, geocoding-api.open-meteo.com | Weather, city search | Coordinates, or the city name you type |
| open.er-api.com | Exchange rates | The base currency |
| lrclib.net | Synced lyrics, only when you tap the lyrics button (or pin lyrics) | Title, artist and length of the song playing |
| site.api.espn.com, a.espncdn.com | Live football scores and team flags (free GitHub version only; turn off in Settings → Sports) | The competitions you follow |
| calendar.google.com | Meet countdown, only if you add iCal links | Your private iCal link |

Like any internet request, these services can see your IP address.

## Media and system access

To show the mini player and volume overlay, the app reads the current media title, artist and artwork, and the
system volume level, from Windows. This information is only shown on screen and, for lyrics, sent to lrclib.net
as described above.

## Screen time

To show today's screen time on the welcome card, the app notes every few seconds which app is in front (only
while you are using the PC). The daily totals per app are kept in `%LocalAppData%\FocusGlance\screentime.json`,
are never sent anywhere, and restart each day. Turn it off in Settings → General → Screen time.

## Bluetooth

When a Bluetooth device connects, the app reads its name, type and battery level from Windows to show them on
screen for a few seconds. This stays on your PC and is never sent anywhere. You can turn it off in
Settings → General → Bluetooth devices.

## Purchases (Microsoft Store version)

The optional Pharaonic pack is sold through the Microsoft Store. Microsoft handles the payment; the app only asks
Windows whether the add-on is owned. The developer receives no payment details.

## Children

The app does not knowingly collect any information from anyone, including children.

## Changes

Any change to this policy will be published in this file in the project repository.

## Contact

Please open an issue in the project's GitHub repository.

# 🕌 Fluent Prayer Times
### Prayer times for Windows

**English** · [العربية](README.md)

A lightweight prayer times app with a native Windows 11 look (WinUI 3). It lives in the notification area and reminds you of the next prayer and the iqama. The interface is available in 11 languages and follows the Windows language automatically.

*brought to you by app.instinct AI* · Developed by Mohamed Elnaggar

## Screenshots

<p align="center">
<strong>Main window in light and dark themes</strong><br><br>
<img src="docs/screenshots/1-main-light-dark.jpg" alt="Main window in light and dark themes: daily prayer times and the countdown to the next prayer." width="700"><br><br>
Daily prayer times and the countdown to the next prayer.
</p>

<table>
<tr>
<td align="center" valign="top" width="50%">
<strong>Azkar reminder</strong><br><br>
<img src="docs/screenshots/2-azkar-reminder.jpg" alt="Azkar reminder: the current dhikr and the reminder interval." width="280"><br><br>
The current dhikr and the reminder interval.
</td>
<td align="center" valign="top" width="50%">
<strong>Date converter</strong><br><br>
<img src="docs/screenshots/3-date-converter.jpg" alt="Date converter: Hijri to Gregorian." width="280"><br><br>
Convert a Hijri date to Gregorian.
</td>
</tr>
<tr>
<td align="center" valign="top" width="50%">
<strong>Hijri calendar</strong><br><br>
<img src="docs/screenshots/4-hijri-calendar.jpg" alt="Hijri calendar: days of the month and Islamic occasions, with month arrows." width="280"><br><br>
Days of the month and Islamic occasions. The month arrows mirror in right-to-left languages.
</td>
<td align="center" valign="top" width="50%">
<strong>Settings</strong><br><br>
<img src="docs/screenshots/5-settings.jpg" alt="Settings: language picker with the Apply changes button, appearance, and accent color picker." width="280"><br><br>
Language and the Apply changes button, appearance, and accent color.
</td>
</tr>
<tr>
<td colspan="2" align="center">
<strong>Tray hover card</strong><br><br>
<img src="docs/screenshots/6-tray-hover.jpg" alt="Tray hover card: time left to the next prayer, with the city and prayer time." width="540"><br><br>
Time left to the next prayer, with the city and prayer time.
</td>
</tr>
<tr>
<td colspan="2" align="center">
<strong>Desktop widget</strong><br><br>
<img src="docs/screenshots/7-desktop-widget.jpg" alt="Desktop widget: the next prayer name, its time, and the countdown." width="230"><br><br>
The next prayer name, its time, and the countdown.
</td>
</tr>
</table>

## ✨ Features

**Times and reminders**
- **Next prayer countdown** in hours:minutes, with the Hijri and Gregorian dates.
- **Adhan and iqama**: when the time comes, "Time for Adhan" shows for one minute, then an iqama countdown, then "Time for Iqama". The iqama delay can be set per prayer.
- **Adhan and iqama sounds**: default, short, or full, from the notification settings.
- **Azkar reminder**: the Azkar page sends a short dhikr notification every interval you choose (1 minute or more), from Hisn al-Muslim and the daily azkar books. It can be turned on or off. Azkar texts stay in Arabic in every language.
- **Any city in the world**: search for a city, detect your location, or enter coordinates, with daylight saving support.

**Calendar and dates**
- **Hijri calendar**: a monthly calendar with month navigation and a jump back to today. The arrows flip direction in right-to-left languages.
- **Islamic occasions**: the "Occasions this month" list shows Islamic occasions and their dates (such as the Prophet's birthday and Isra and Mi'raj).
- **Date converter** between Hijri and Gregorian.

**On your desktop**
- **Tray icon** with a hover card and a right-click menu. Clicking the icon always opens the main page, not the last page you left.
- **Floating desktop widget**: drag it anywhere and it snaps to the nearest position.
- A native Windows 11 window, taller so the content fits.

**Languages and direction**
- **11 languages**: Arabic, English, French, German, Spanish, Turkish, Urdu, Indonesian, Russian, Portuguese, and Persian. Translations other than Arabic and English are machine-generated.
- **Language picker** in Settings. "Auto" follows the Windows display language.
- **Full right-to-left layout** for Arabic, Urdu, and Persian: the sidebar sits on the right, and the title bar mirrors so the title is on the right and the close button on the left.
- An **Apply changes** button at the top of Settings restarts the app to apply a new language or accent color.

**Appearance**
- Mica Alt, Mica, or Acrylic backdrop, and a light, dark, or system theme.
- **App accent color**: preset colors, a custom color from the color picker, and a reset to the default.
- Run at Windows startup. Works offline after the first download of the times.

**Updates and installation**
- **Automatic update check** at launch and every six hours, with a notification that has a direct download button. It can be turned off in Settings. A manual check is available in Settings or from the tray menu.
- **Small update download**: the update uses the small installer (no .NET bundled) instead of the full one.
- **Modern installer**: an install button, a green progress bar, a done screen, automatic launch after install, and shortcuts on the desktop and in the Start menu.
- The **About** page shows the actual app version, the app icon, and a **GitHub Repo Link** to the project repository.

## 📦 Installation
1. Download `FluentPrayerTimes-vX.Y.Z-installer-win-x64.exe` from the [Releases](../../releases/latest) page and run it.
2. If SmartScreen appears, click **More info** then **Run anyway** (the app is not code-signed).

A portable build (zip) and a small installer that needs the .NET 8 Desktop Runtime are also on the same page.

**Requirements:** Windows 10 (1809 or later) / Windows 11, x64, and [Windows App Runtime 1.6](https://aka.ms/windowsappsdk/1.6/latest/windowsappruntimeinstall-x64.exe) (installed once; the installer handles it when needed).

## 🕋 Prayer times source
Times come from the [Aladhan API](https://aladhan.com/prayer-times-api), using the Egyptian General Authority of Survey method (method 5) by default.

## 📄 License
[MIT](LICENSE) © Mohamed Elnaggar · brought to you by app.instinct AI

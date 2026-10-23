## 1.3.4

### Changes
- Prayer times are now calculated inside the app, with seconds, for angle-based methods (Egyptian General Authority of Survey, Muslim World League, Karachi, ISNA, Kuwait and custom angles). Fajr, Dhuhr, Asr, Maghrib and Isha are rounded to the nearest minute and sunrise is rounded down. Your existing method, Asr school, high-latitude and time zone settings are respected. Other methods, polar places where the sun does not rise or set, the Hijri date and the time zone still come from the Aladhan API.
- Added a "No sound" choice to the adhan and iqama sound lists. Notifications are silent only when you choose it; your existing sound choices are unchanged.
- The data sources text in About and the README now describe how times are calculated.

### Fixes
- The daylight saving time section is shown only for Egypt locations, so users in other countries no longer see it. Thanks to Aadil Islam for this contribution.

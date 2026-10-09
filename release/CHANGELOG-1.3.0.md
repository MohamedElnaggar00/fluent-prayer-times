## Fluent Prayer Times 1.3.0

### Added
- Location-based prayer calculation defaults with independent manual overrides.
- Calculation methods for Egypt, North America, Muslim World League, Umm al-Qura, Karachi, France and other regional authorities.
- Asr school selection, custom Fajr/Isha angles and minute offsets, and high-latitude adjustments.
- Optional time-zone override.

### Fixed
- Worldwide location selection no longer assumes Egyptian cities when requesting prayer times.
- Calculation settings are included in the offline cache key to prevent stale timings after an option changes.
- Improved alignment of the desktop widget's prayer name, time and heading.
- Iqama countdowns show whole minutes only, rounded up, without seconds.
- Installers check the x64 .NET 8 runtime folder, install the correct Microsoft.NETCore.App runtime and verify Microsoft download signatures.
- The small runtime-dependent installer uses a native front end that can start on a PC without .NET installed.
- Windows App Runtime detection includes all users.

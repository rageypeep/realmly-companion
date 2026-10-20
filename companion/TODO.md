# Companion roadmap

Uploads stay manual by default. Never execute SavedVariables as Lua.

## First milestone: confident uploads

- [x] Detect records not previously uploaded through this Companion; identify their characters (0.1.19).
- [x] Preview characters, adventure dates and record counts before sending (0.1.19).
- [x] Keep a bounded local upload history, distinguishing success, duplicates and failures (0.1.19).
- [x] Refresh the preview when the selected SavedVariables file changes (0.1.19).
- [x] Explain that local detection cannot account for uploads from the website or another PC (0.1.19).

## Next milestones

- [x] Detect running WoW clients; block addon replacement while running/unknown and explain unsaved records (0.1.21).
- [x] Optional system-tray mode and update notifications; Open/Check updates/Exit menu, no surprise uploads (0.1.21).
- [x] Copy a privacy-safe diagnostic report: versions and categorized errors, no tokens or personal paths (0.1.22).
- [x] Remember named installations, independent Retail / Forever paths, rename/forget shortcuts and legacy migration (0.1.22).
- [x] Optional automatic uploads per selected setup, default off with explicit consent, stable-file checks, retry backoff and pause controls (0.1.23).

## Verification / release

- [x] Test parsing, malformed data, duplicate detection, history persistence and failed uploads.
- [x] Inspect the existing themed UI at minimum and normal window sizes (offscreen fixtures, 144 DPI).
- [x] Package and publish 0.1.19; verify public release metadata and download SHA256.
- [x] Verify 0.1.21 process/tray tests, options rendering, public download and updater SHA256.
- [x] Verify 0.1.22 setup migration/switching, diagnostics privacy and clipboard stub, UI renders and release ZIP.
- [x] Verify 0.1.23 automatic-upload consent/defaults, stable saves, retry/auth/cancellation behavior, closing-window race and public ZIP SHA256.
- [x] Verify 0.1.24 disabled notification label contrast, dependency hint, enabled behavior and public ZIP SHA256.

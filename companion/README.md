# Realmly Companion for Windows

## 0.1.34 / Store 0.2.8: Screenshot manager

A new Screenshot manager button opens a setup-specific local library. Retail and
Forever use separate Screenshots folders; a folder picker handles custom layouts.
JPG, PNG and BMP are supported. Discovery is non-recursive and bounded to 2,000
images. Only the selected preview is decoded; decode work is serialized.

The data-only Lua reader matches successful recorded captures to the original
WoWScrnShot filename within five seconds of the recorded local wall clock. Its
adventure day comes from the event's UTC timestamp, including around midnight.
Ambiguous captures need a choice; renamed/plain images need an explicit character
and recorded day. A mismatched-client Lua file never supplies automatic matches.

Connect and upload Realmly.lua first. Check up to six images, review their exact
destinations, then upload privately. The paired token is accepted only by the
website's screenshot operations. Images are resized to 2,048px, metadata stripped
by re-encoding, and sent through existing moderation/WebP storage/quotas. Original
local files are never changed or deleted. Cancel operation stops active work.

Manage on Realmly opens the web manager for final badge previews, captions,
deletions and explicit social publication. The app does not publish images or
chronicles. Duplicate uploads are blocked during the manager visit; this is not
a persistent synchronization service. Refresh does not watch or upload files.

## 0.1.33 / Store 0.2.7: Growing SavedVariables histories

Valid histories with more than 60,000 nested table fields now preview normally.
The data-only reader allows up to 250,000 fields while retaining the 2 MB byte
limit, nesting bound and executable-Lua rejection. Exceeding the complexity
limit now explains the actual problem instead of claiming the file is addon source.
Existing records, settings and local upload history are preserved.

## 0.1.32 / Store 0.2.6: Retail branding

The game-client selector now says WoW Retail. Retail addon checks use the current
RealmlyRetail-addon.zip download; legacy addon version parsing remains supported.
Diagnostic reports show the numeric addon version without the old test suffix.
Existing named setups, game paths, sign-in and upload preferences are preserved.

## 0.1.31 / Store 0.2.5: addon checks return to the top

The addon Check for updates action scrolls to the top when starting and after
success or failure, including when focus is restored after the check. Other
operations retain their existing scroll behaviour.

## 0.1.30 / Store 0.2.4: stable setup switching and clearer previews

Changing game client or saved setup returns the view to the top, including after
its automatic addon check. Busy operations suppress focus-driven scroll jumps
while disabling/re-enabling controls and restore the original control's focus.
Upload status text uses the panel's available width. Missing SavedVariables now
shows concise logout/reload guidance instead of repeating a long file path.

## 0.1.29 / Store 0.2.3: client-aware Realmly.lua paths

Switching clients retargets standard SavedVariables paths under the selected WoW
root to `_forever_` or `_retail_`, preserving the account folder. Old Classic
beta paths are corrected too, even if that old file still exists. A target need
not exist yet: log out or /reload in the selected client to write it.
Explicit custom/backup paths and each client's remembered account stay separate.
Automatic uploads are disabled when repairing a setup's selected file.

## 0.1.28 / Store 0.2.2: visible addon-install feedback

Blocked addon installs and installation failures now display a warning popup
and an explanation beside the addon buttons, not only the lower status area.
Running/unknown WoW state, missing folders, unreadable addon versions and
download/install errors are covered. WoW must be fully closed for addon updates;
`/reload` only flushes records and does not release the running client.

## 0.1.27 / Store 0.2.1: correct WoW Forever folder

WoW Forever now uses `_forever_` for addon installation, installed-version checks,
SavedVariables discovery and running-client detection. Retail still uses
`_retail_`. Classic beta folders are no longer treated as Forever.
Existing game files and saved records are not moved or deleted.
If your saved setup points to a Classic beta Realmly.lua, use Browse to select
the file under `_forever_/WTF/Account/<account>/SavedVariables/Realmly.lua`.

The Companion is a small WinForms application for Windows 10/11. It can:

- install or update the correct Realmly Add-on ZIP into a selected WoW client;
- find or let the player choose `Realmly.lua`;
- pair to Realmly through a one-time browser approval; and
- upload the selected SavedVariables file directly to the player&rsquo;s Realmly account.
- check for newer Companion releases when it opens, without preventing normal use.

## 0.1.26: reliable automatic-upload consent

The automatic-upload checkbox now commits the explicitly confirmed preference
after the consent popup, instead of reading a transient checkbox value. It stays
in sync with the saved setup. Missing account/file requirements and the resulting
on/off state are shown inside App options. Watching begins after options closes.
Tests cover nested modal OK/Cancel, disabling, persistence and missing requirements.

## 0.1.25: consistent dark controls

Review & upload, Upload history and Diagnostics now use bronze/gold scrollbars,
matching the main window. Text remains selectable, read-only and keyboard/wheel
scrollable. App options uses dark bronze checkboxes with gold ticks, hover/focus
feedback and readable disabled labels. Existing consent and tray dependencies
are unchanged. Render tests cover narrow dialogs, scrolling and checkbox states.

## 0.1.24: readable disabled notification option

The disabled update-notification checkbox now paints its label in muted gold,
with a second-line Requires tray mode hint. Enabling tray mode removes the hint
and restores normal native interaction. Tests render both states, check muted
gold pixels and verify that the dependency still prevents disabled interaction.

## 0.1.23: explicit opt-in automatic uploads

- App options / Auto-upload this setup's saved records is off by default for all
  existing/new setups. Enabling asks for confirmation covering every character
  in the selected file, including existing records on the first eligible check.
  Account connection and an existing selected Lua file are required to enable.
- Only the currently selected setup is watched while Companion is running,
  including tray mode. It does not register Windows startup or run after Exit.
  No posts are generated/published and no addon/app updates are installed.
- Checks use the existing 10-second polling timer. A file's size/mtime must stay
  unchanged for at least 10 seconds; metadata is checked again after safe parsing.
  Only snapshots with locally new record IDs are sent. Unchanged/handled files
  are not uploaded repeatedly; server-side duplicate handling remains in place.
- Automatic requests use the same snapshot endpoint, size limit and protected
  account token as manual uploads. They are single-flight within the app and do
  not overlap other operations or run while a modal window is open.
- Connection/server/rate-limit failures back off from 30 seconds to 15 minutes.
  401/403 pauses pending sign-in. Invalid files/permanent request failures pause
  that saved version until a new file save or an explicit disable/re-enable.
- Turning the option off cancels any pending request; data already accepted by
  Realmly cannot be recalled. Changing the folder/file disables that setup's
  permission. Reconnecting an account disables automatic uploads for all setups
  so a different account never inherits consent. Save as new defaults back off.
- Main upload status shows off/watching/up-to-date/paused/retrying. History marks
  Automatic versus Manual attempts. Diagnostics include only safe state flags.
- Background previews marshal UI changes back to the window thread and cancel
  when the window exits, preventing late reads from recreating closed controls.
- Tests use temporary Lua fixtures and an HTTP stub, never real user uploads.
  Coverage includes stable-save delay, exact snapshots, retry/auth/file failure,
  cancellation, duplicate suppression, setup selection and renewed consent.

Manual Review & upload continues to work when automatic uploads are disabled or paused.

## 0.1.22: saved setups and privacy-safe diagnostics

- Saved setup selects a remembered client/root/SavedVariables combination.
  Switching Game client returns to its last selected setup; multiple setups for
  the same client are supported. Setups offers Save as new, Rename and Forget.
  Save as new copies the current paths; browse to a different root afterwards.
  Forget removes only a Companion shortcut, never game data or website profiles.
- Existing settings migrate automatically to one named setup, keeping the old
  folder/file/client selections and account connection. Legacy top-level fields
  continue to mirror the active setup. Settings and upload tokens stay local.
- App options / Diagnostics previews a report. Only Copy diagnostics writes it
  to the clipboard; nothing is uploaded or copied automatically. It includes app/
  addon/runtime versions, presence/state flags, counts and up to eight safe error
  categories from the current app session (network, permissions, sign-in, etc.).
- The report is an allowlist, not redacted raw logs: no paths, names, account IDs,
  tokens, file contents, exception messages/stack traces or arbitrary version
  suffixes. Recent repeated errors are deduplicated. Normal visible error messages
  can still explain local problems, but are never inserted into the report.
- Tests cover migration, independent client/multiple-install switching, JSON
  round-trip persistence, duplicate-name validation, forgetting shortcuts and
  diagnostics containing seeded private markers. Clipboard copying uses an
  injected stub in tests; the user's clipboard is not touched.

At 0.1.22, automatic uploads were still a future opt-in decision.

## 0.1.21: game-aware updates and optional tray mode

- Game status matches known WoW executable names to the selected client folder,
  not an unrelated Retail/Forever installation. An inaccessible WoW process is
  treated as unknown, never assumed safe to replace addon files.
- Addon installation checks before confirmation and again immediately before
  replacement, after downloading/extracting. Running/unknown clients block the
  swap and preserve the old addon. The app never closes WoW for you.
- Upload review warns when WoW is running/unknown: only saved on-disk records
  are included. `/reload` or logout is needed for recent in-game changes. Uploads
  are still allowed after the explicit review/confirmation.
- App options has independent opt-ins for close/minimize to tray and update
  notifications (the latter requires tray mode). Both default off; no Windows
  startup registration is added. The icon may be in Windows' hidden-icon area.
- Tray menu: Open, Check for updates, Exit; double-click or an update balloon
  opens the app. Exit waits for active operations. Disabling tray mode restores
  a hidden app. Self-update restart and Windows shutdown bypass close-to-tray.
- Normal startup/manual update checks continue. With both options enabled,
  checks also run every six hours while the app stays open. Notifications are
  once per new version/client per app session; Windows may suppress balloons.
  Clicking a notice only opens the app: downloads, installs and uploads still
  require approval. Notification failures do not make an addon check fail.

Tests simulate process paths, inaccessible clients and a game starting before
the file swap. Offscreen forms verify tray hide/restore/Exit, preferences,
notification deduplication, normal close and updater/Windows-shutdown exits.
Tests do not insert a real tray icon, send OS notifications or control WoW.

## 0.1.20: compact upload section

The main upload section now shows one summary line and the last upload time.
Character/date details remain in Review & upload; file-selection and local-count
explanations are hover hints rather than permanent paragraphs.

## 0.1.19: confident manual uploads

- Local SavedVariables preview lists characters, record counts and UTC adventure dates.
- New-record counts use stable adventurer/record ID hashes from successful uploads
  through this Companion. Website / other-PC uploads are not known locally; the
  first upload may show existing website records as new. The server deduplicates.
- Preview refreshes on activation and checks for file changes every 10 seconds.
  Refresh preview also forces a rescan. Scanning does not upload or execute Lua.
- Review & upload opens a confirmation window and sends exactly that reviewed
  in-memory snapshot, not a later on-disk revision. Cancel sends nothing.
- Upload history retains the last 20 attempts, distinguishes accepted/duplicate/
  failed results, and shows the most recent successful upload after a failure.
- `%LocalAppData%/Realmly/upload-history.json` contains record ID hashes and count/
  time/outcome metadata, not raw notes, account tokens or personal file paths.
  A failed upload never marks records as sent. Local history save failures do not
  turn a confirmed server upload into a failed upload.

Tests use data-only fixtures and an HTTP stub (no real uploads), plus offscreen
normal/narrow WinForms rendering. The roadmap is tracked in [TODO.md](TODO.md).

## 0.1.18: border/repaint correction

FormBorderStyle.None was already set, but native resize styles still allowed
Windows to render a grey frame. The custom window now disables DWM non-client
rendering and native frame repainting while retaining the resize hit targets.
Activation is processed without native repainting. Full client invalidation and
ResizeRedraw clear stale border pixels during growing/shrinking. The client-size
override also removes a measured 20px mismatch caused by WinForms counting the
native sizing frame that our NCCALCSIZE handling had already removed.

Tests check actual native/managed client geometry, DWM rendering state, native
paint suppression, repeated resize invalidations and both outer border corners.
Please also verify slow interactive resizing on the player's desktop compositor.

References: [non-client rendering policy](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmncrenderingpolicy)
and [activation without repainting](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-ncactivate).

## 0.1.17: custom launcher frame

The native pale title bar and scrollbar are replaced by a dark bronze title bar,
gold compass/caption controls and a slim bronze scrollbar. The content still uses
the same account, addon and upload flows. No new runtime dependencies were added.

RealmlyWindow retains native sizing/system-menu styles, signed multi-monitor
hit testing and work-area maximize bounds. The title bar supports drag and
double-click maximize/restore, a themed Alt+Space/right-click window menu and
explicit Alt+F4 closing. Buttons have accessible names. The custom scroll
control supports wheel, thumb dragging, track paging, focus visibility and
Up/Down/Page Up/Page Down/Home/End keyboard scrolling with an accessible value.

Regression tests cover native resize/minmax messages, scroll geometry, keyboard
scrolling, no initial focus jump, narrow/high-DPI renders and all updater tests.
After installing, also check dragging, minimizing/restoring, edge resizing,
Alt+Space/Alt+F4 and maximize on each physical monitor at your Windows scaling.

API references: [WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest)
and [MINMAXINFO](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-minmaxinfo).

## 0.1.16: Realmly compass icon

The EXE, window and taskbar use the new transparent gold/teal compass icon,
with native sizes from 16 to 256 pixels. Asset and generation notes are in
Assets/README.md; rebuild the ICO using tools/BuildIcon.ps1.

## 0.1.15: in-app self-updates

The Companion checks for new versions at startup and through Check Companion
updates. Confirm Update Companion to download, verify, replace and reopen the
app without a browser or manual ZIP extraction. No update is installed silently.

The HTTPS release manifest supplies the ZIP SHA256. The updater also checks the
embedded EXE version and limits download/extraction sizes. A bundled hidden
Windows PowerShell helper waits for the exact old process to exit, performs a
same-volume replacement and restarts the app. A failed replacement/launch leaves
or restores the previous EXE; failures show a message and retain a local log in
%LocalAppData%/Realmly/Updates. No administrator elevation is requested. The EXE
must be in a writable folder; settings, DPAPI tokens and WoW data are untouched.
Downloads remain unsigned during testing; hash verification is not code signing.

Users of 0.1.14 or older must download 0.1.15 once to gain the self-updater.
All subsequent releases must publish an immutable versioned ZIP and update both
Companion JSON manifests with its exact SHA256 after packaging.

Tests include a real hidden helper handover using disposable probe executables,
paths containing spaces/quotes, bad hashes, mismatched versions, and forced
restart failure with rollback. They do not update the developer's running app.

## Layout and addon checking (0.1.14)

- Three clearly labelled sections: game/addon, account and upload. Scrollable,
  DPI-aware layout, readable buttons and separate operational/update status.
- Checks the selected client's installed Realmly.toc against the actual latest
  ZIP at startup, on client selection and on demand. Retail suffixes are supported.
  No separately maintained addon release manifest can drift out of sync.
- Shows missing, unreadable, outdated, current and newer-than-published installs.
  Updates are explicit, with confirmation, never silent. Close WoW first.
- Extracts and validates updates in a staging directory before replacing Realmly;
  restores the old installation if the final move fails. WTF is never modified.
- Offline checks fail gracefully; normal pairing and uploads remain available.
- Only the DPAPI-encrypted token is serialized; the decrypted accessor is excluded.

Regression tests and offscreen WinForms render checks (Windows required):

```powershell
dotnet run --project ../companion.tests/CompanionTests.csproj -c Release -- ../output/companion-review --live
```

These tests use a temporary WoW directory and injected account settings, never
the player's actual installation or credentials. --live only reads published ZIPs.

The application is deliberately not an OAuth client. Battle.net sign-in remains
on `realmly.me`; the Companion receives a 90-day upload token which is encrypted
with Windows DPAPI for the current Windows user.

## Build a portable Windows release

```powershell
dotnet publish .\RealmlyCompanion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64
```

The resulting `RealmlyCompanion.exe` is unsigned during testing, so Windows may
show its normal unknown-publisher warning until code-signing is set up.

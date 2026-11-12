# Realmly Companion

[Documentation index, roadmaps and feature ideas](docs/README.md).

**Bring your adventures home.**

Realmly Companion is a Windows desktop app that connects your World of Warcraft
field notes to [Realmly](https://realmly.me), a social home for your WoW characters.
Keep the Realmly addon current, review your saved adventures and upload them to
your account, ready to turn into chronicles on the website.

[Download the Companion](https://realmly.me/download) ·
[Get the addon](https://realmly.me/addon) ·
[Report an issue](https://github.com/rageypeep/realmly-companion/issues)

[Microsoft Store patch notes](docs/store-changelog.md) include individual build
changes and combined text for Store submissions.

> **Release status:** This repository contains the Companion's 0.1.45 source and
> regression tests, plus a separate 0.5.1 Microsoft Store packaging configuration.
> Current website downloads are unsigned portable builds; a signed
> customer release is not available yet.

## What it does

- **Install and update the addon** for WoW Forever or Retail.
  The app checks installed versions and blocks addon replacement while the
  selected game client is running or its status cannot be verified.
- **Find your field notes** by locating the addon's `Realmly.lua` SavedVariables file.
- **Connect securely** through approval in your browser. Battle.net sign-in stays
  on the website; you do not enter your Battle.net password in the Companion.
- **Review before uploading** with character names, adventure dates and record counts.
- **Translate pasted chat** in its own themed tab: see an English translation and a suggested reply in the source language plus English. Optional reply guidance and copy buttons keep you in control. Requires a connected account; sends only the pasted text and guidance to OpenAI when you click Translate, independently of characters, uploads and chronicles.
- **Manage local screenshots** for each setup, preview images, match recorded milestones, and review private uploads. Originals stay untouched; captions, badges and social sharing are managed on Realmly.
- **See upload history** and distinguish accepted uploads, duplicates and failures.
- **Remember named setups** for different game installations and clients.
- **Use optional tray mode** and update notifications.
- **Enable optional automatic uploads** for the selected setup, with explicit consent.
- **Update the Companion in-app** after confirmation, with download checks and
  rollback handling if replacement or restart fails.
- **Copy a privacy-conscious diagnostic report** when you need help.

The Companion uploads recorded adventures and offers a separate manual translator. It does **not** write or publish
social posts automatically; creating and publishing chronicles happens on Realmly.

## Getting started

1. Download the Windows x64 ZIP from the
   [official Companion page](https://realmly.me/download).
2. Extract it into a folder you can write to, then run `RealmlyCompanion.exe`.
   Keep the app in a writable location so its self-updater can replace it.
3. Choose your game client and World of Warcraft folder.
4. Close WoW before installing or updating the addon.
5. Connect your Realmly account using the browser approval flow.
6. Play with the addon enabled. Log out or type `/reload` before uploading so
   WoW writes the latest records to disk.
7. Check the selected SavedVariables file, choose **Review & upload**, and confirm
   the snapshot you want to send.

Windows 10/11 x64 is the target platform. Official download builds are
self-contained, so a separate .NET installation is not required.

### Choosing the right Lua file

Select the account-level SavedVariables file, typically:

```text
World of Warcraft/<client>/WTF/Account/<account>/SavedVariables/Realmly.lua
```

Do not select the addon program file under `Interface/AddOns/Realmly/`.
The selected SavedVariables file can contain records for multiple characters;
the upload review lists the characters found in that snapshot.

### Automatic uploads

Automatic uploads are **off by default**. To enable them, select an existing
SavedVariables file, connect your account, open **App options**, enable the
option and accept the confirmation. Watching begins after App options closes.

- Only the currently selected setup is watched while the Companion is running,
  including when it is running in the tray.
- The app waits for a saved file to become stable before reading it.
- A snapshot can include existing records for every character in the file.
  Realmly handles duplicates.
- Changing the game folder or Lua file disables that setup's automatic-upload
  permission. Reconnecting an account disables automatic uploads for all setups.
- Automatic uploads do not publish posts or install app/addon updates.
- You can turn the option off at any time. Data already accepted by Realmly
  cannot be recalled by disabling the option.

## Privacy and safety

- Uploads send the selected file's saved adventure data to `https://realmly.me`
  through your connected account. They are manual unless you enable automatic uploads.
- The upload token is stored locally using Windows DPAPI encryption for your
  Windows user account. Treat your local settings and tokens as private.
- Preferences and local upload history are stored under `%LocalAppData%/Realmly`.
- Lua SavedVariables are parsed as data, not executed as scripts.
- "New on this PC" counts use this Companion's local upload history. They cannot
  account for uploads made on the website or another PC.
- The diagnostic report excludes tokens, personal file paths, character/setup
  names, file contents and raw error messages. It is not sent automatically.
- Update packages are checked against their advertised SHA-256 and executable
  version. These checks are **not** a substitute for publisher code signing.

This repository is for the desktop Companion only. The Realmly website backend,
database, deployment configuration and service credentials are not included.

## Building from source

Use Windows with the **.NET 8 SDK** installed. Run these commands from the
repository root:

```powershell
dotnet build companion/RealmlyCompanion.csproj -c Release
dotnet run --project companion/RealmlyCompanion.csproj -c Release
```

The application connects to the live Realmly service. Running a locally built
Companion does not require access to the private website repository, but account
pairing and uploads require a Realmly account. Do not approve uploads of personal
data unless you intend to send it to Realmly.

### Run the tests

```powershell
dotnet run --project companion.tests/CompanionTests.csproj -c Release -- ./output/companion-review
```

The default suite uses synthetic character data, temporary directories, injected
settings and stubbed upload requests. It does not upload real adventures, replace
your installed Companion or modify your WoW installation. UI checks create their
own offscreen windows; the updater tests launch disposable probe executables.
Screenshots are written to the selected output directory.

Add `--live` to also check the versions of published addon packages. This makes
read-only requests to Realmly; it does not install the downloaded addons.

### Build a portable Windows x64 executable

```powershell
dotnet publish companion/RealmlyCompanion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/win-x64
```

The resulting `artifacts/win-x64/RealmlyCompanion.exe` is **unsigned**. Include
this repository's `LICENSE` when redistributing your build. Building it does not
create an official release, change the website download or enable a signing service.

For an existing release ZIP, you can additionally verify its advertised SHA-256
and executable version. Place the release manifest and its matching ZIP in the
same directory, then run:

```powershell
dotnet run --project companion.tests/CompanionTests.csproj -c Release -- ./output/companion-review --release ./artifacts/RealmlyCompanion-release.json
```

### Repository layout

```text
companion/        Windows application, updater helper, icon and icon-build tool
companion.tests/  Regression tests and disposable updater probe
LICENSE           MIT licence
```

The project file embeds the updater helper and application icon; it does not
reference website source files. Build output, credentials, signing keys and
personal SavedVariables files are excluded by `.gitignore`.

## Microsoft Store build

The Store flavour is packaged separately as MSIX and uses Microsoft Store for
Companion updates. It does not check the portable release feed and excludes the
portable EXE-replacement helper. Addon installation and uploads remain available.
The portable build and website download are unchanged.

See [Store packaging instructions](packaging/README.md) for the registered package
identity, build command, tests and pre-submission checklist. Building an unsigned
submission MSIX does not mean it is approved or signed for customer installation.

## Code signing policy

Standalone code signing is **not configured yet**. Current website downloads are unsigned and Windows
may display an unknown-publisher or SmartScreen warning.

Unsigned development builds keep the existing in-app updater. The separate 0.2.7
Store flavour relies on Microsoft Store for updates and signing after acceptance.
It is not a currently published signed customer release.

For standalone downloads, we may also investigate SignPath Foundation's open-source signing programme.
No application approval or signing endorsement is claimed. This section will be
updated with the actual signing identity and release approval process if accepted.

## Reporting problems

Open an [issue](https://github.com/rageypeep/realmly-companion/issues) with:

- Your Companion version, Windows version and selected game client.
- What you expected, what happened and steps to reproduce it.
- The diagnostic report from **App options → Diagnostics**, if useful.

Check screenshots for personal information before attaching them. Do not post
account tokens, local settings files or raw SavedVariables files publicly.

## Licence

The Companion is released under the [MIT License](LICENSE).
Third-party dependencies and assets remain subject to their respective licences.
Licensing this Companion does not license the private Realmly website code.

Realmly is an independent fan project, not affiliated with or endorsed by
Blizzard Entertainment.

## Translate tab (0.1.45 / Store 0.5.1)

Connect Realmly once in the Realmly tab, then open **Translate**. Paste up to
3,000 characters and optionally describe your intended reply (up to 500
characters). Click **Translate & reply**. Review the detected language,
English translation and both versions of the suggested reply, then use the copy
buttons or select text with Ctrl+C. The app never sends a reply into WoW.

Only that message and guidance go through the separate authenticated
`POST /api/companion/translate` endpoint. No character/setup data, Lua files or
screenshots are included. No clipboard monitoring or chat collection occurs.
Inputs/results stay in app memory, are not added to local history/settings or
chronicles, and can be removed with **Clear** or by exiting the app (tray mode
keeps the app running). Clear/cancel suppresses late results.

The paired account token authorises this optional feature too. Server-side
limits are ten attempts per minute, 100 per UTC day and one in-flight request
per account. The OpenAI key remains on the server; the public client needs no
API key. AI may misunderstand slang; check the reply before pasting it.

In Translate, turn **Suggest a reply** off for **Translate** only, or on for
**Translate & reply**. The large action button changes its label. Translate-only
hides reply controls and sends no reply guidance; the AI is asked only for the
translation and language, without generating suggested replies. The mode choice
stays in memory for this app session. Existing versions still default to replies.

## Optional Add-ons (0.1.45 / Store 0.5.1)

Next to Translate, **Add-ons** offers **Chat Copy** and **Sparkles** for WoW
Forever. Select the WoW folder in the Realmly tab. Each card shows installed and
latest versions and an Install/Update button. The target is always that folder's
`_forever_/Interface/AddOns`, even when the main recorder is set to Retail.

Opening Add-ons checks a small official release catalog. The existing tray
update-check cycle also checks these add-ons and uses the existing notification
preference. Checks never install silently. Click Install or Update with Forever
closed; the app downloads a versioned ZIP, verifies SHA-256 and the TOC, stages
it and replaces only that add-on folder. A failed replacement restores the old
copy. The app rechecks game state and selected setup before replacement, rejects
unsafe archives and downgrades, and preserves SavedVariables and other add-ons.
No account connection is required. The app handles all add-on downloads and installation.

In game, the brands are **Realmly Chat Copy** and **Realmly Sparkles**. Sparkles
keeps its `RealmlyGathering` folder and SavedVariables identity, preserving
preferences and saved herb locations. `/rls` and `/rlg` both open it.

## Uninstall and Discord (0.1.45 / Store 0.5.1)

Add-ons includes an Uninstall button for each optional tool. Confirmation shows
the exact Forever folder; game-state, path and setup checks also apply to removal.
Only that add-on folder is removed. SavedVariables, herb history, Retail and the
main recorder stay untouched. For Sparkles, use /rls > Turn off & restore in-game
before closing Forever and uninstalling it. Reinstall remains available later.

The separate Discord tab has a large Join the Discord button, a selectable invite
link and community/support information. It opens https://discord.gg/Js2mcA7w9
only when clicked. Discord handles joining/sign-in; the app never joins for you.

The Sparkles card embeds the dedicated Realmly Sparkles artwork, also used
in-game. Its source PNG is `companion/Assets/sparkles.png`. Current optional
releases are Chat Copy 0.1.4 and Sparkles 0.2.2, both with copy-ready Discord
invites. The main Retail/Forever recorder 0.8.22 also includes community links.

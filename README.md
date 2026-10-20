# Realmly Companion

**Bring your adventures home.**

Realmly Companion is a Windows desktop app that connects your World of Warcraft
field notes to [Realmly](https://realmly.me), a social home for your WoW characters.
Keep the Realmly addon current, review your saved adventures and upload them to
your account, ready to turn into chronicles on the website.

[Download the Companion](https://realmly.me/companion) ·
[Get the addon](https://realmly.me/addon) ·
[Report an issue](https://github.com/rageypeep/realmly-companion/issues)

> **Repository status:** The public Companion repository is being prepared.
> Source code and build instructions have not been imported yet. Current downloads
> are unsigned testing builds; a signed customer release is not available yet.

## What it does

- **Install and update the addon** for WoW Forever or the Retail testing client.
  The app checks installed versions and blocks addon replacement while the
  selected game client is running or its status cannot be verified.
- **Find your field notes** by locating the addon's `Realmly.lua` SavedVariables file.
- **Connect securely** through approval in your browser. Battle.net sign-in stays
  on the website; you do not enter your Battle.net password in the Companion.
- **Review before uploading** with character names, adventure dates and record counts.
- **See upload history** and distinguish accepted uploads, duplicates and failures.
- **Remember named setups** for different game installations and clients.
- **Use optional tray mode** and update notifications.
- **Enable optional automatic uploads** for the selected setup, with explicit consent.
- **Update the Companion in-app** after confirmation, with download checks and
  rollback handling if replacement or restart fails.
- **Copy a privacy-conscious diagnostic report** when you need help.

The Companion uploads recorded adventures. It does **not** write or publish
social posts automatically; creating and publishing chronicles happens on Realmly.

## Getting started

1. Download the Windows x64 ZIP from the
   [official Companion page](https://realmly.me/companion).
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

## Code signing policy

Code signing is **not configured yet**. Current downloads are unsigned and Windows
may display an unknown-publisher or SmartScreen warning.

The intended release process separates unsigned development builds from signed
customer releases. Version 0.2.0 is the planned customer release, not a currently
published signed build. Separate update channels still need to be implemented.

We intend to investigate SignPath Foundation's open-source signing programme.
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

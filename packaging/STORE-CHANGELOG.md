# Microsoft Store patch notes

Store submissions can combine several Companion builds. The version headings
below identify which build contains each change; they do not mean that every
build was submitted to or published by Microsoft Store.

The current Store package configuration is **0.5.1** (portable equivalent: **0.1.45**).
Submission dates and the last published Store version are not tracked yet.

## Ready to paste: 0.5.1 - Translate, Add-on Manager and Discord

This combined Store release brings together the two main additions, translation
and add-on management. The final `.1` marks the Discord tab addition. Earlier
entries below remain the development history, not a record of Store submissions.

```text
Translate
- Paste chat for an English translation, with optional suggested replies in the original language and English.
- Choose Translate only or Translate & reply using a large button and themed switch.
- Text is sent to OpenAI only when you click Translate. It stays separate from character profiles and chronicles, with no local translation history.

Add-on Manager
- Install, check for updates and uninstall Chat Copy or Sparkles directly in the Companion.
- See installed and latest versions. The app handles downloading, verification and installation into your selected WoW folder's _forever_/Interface/AddOns directory.
- Forever must be closed for installation or removal. Saved settings and herb history are preserved. Sparkles includes dedicated original artwork.

Discord
- New Discord tab with a large Join the Discord button for the Realmly community, support, bug reports and suggestions.

Existing uploads, screenshots, account connections and setup preferences are preserved.
```

## Ready to paste: 0.3.8 - Sparkles artwork and layout

```text
- Dedicated original Sparkles icon in Add-ons, matching its new in-game artwork.
- Fixes blank space at the bottom of the Add-ons scroll area at some window sizes.
- Updated Realmly add-ons include copyable Discord community invites.
- Existing installation, update, uninstall and saved-setting behaviour is preserved.
```

## Ready to paste: 0.3.7 - Sparkles artwork

```text
- Sparkles now has dedicated original artwork in Add-ons: a golden sparkle, herb sprig and ore crystal.
- Matches the new Realmly Sparkles in-game icon. Updated add-ons include copyable Discord community invites.
- Install, update, uninstall and saved-setting behaviour are unchanged.
```

## Ready to paste: 0.3.6 - Uninstall and community

```text
- Uninstall Chat Copy or Sparkles directly from Add-ons, with confirmation showing the target folder.
- Removal requires Forever to be closed and preserves saved settings and herb history. Sparkles includes a reminder to use Turn off & restore first.
- New Discord tab with a large Join the Discord button, selectable invite and friendly community/support information.
- Discord opens only when requested; joining and sign-in remain in Discord.
```

## Ready to paste: 0.3.5 - Simpler Add-ons controls

```text
- Removed the manual ZIP download buttons from Add-ons.
- Install and Update handle downloads, verification and installation directly in the app.
- Installed/latest version checks and existing safeguards are preserved.
```

## Ready to paste: 0.3.4 - Install and update optional add-ons

```text
- Install Chat Copy and Sparkles directly from Add-ons into your selected WoW folder's _forever_/Interface/AddOns directory.
- See installed and latest versions, with Install, Update and Up to date states.
- Version checks run when opening Add-ons and through the existing update-check cycle. Updates remain an explicit action while Forever is closed.
- Downloads are checked against the official release catalog, SHA-256 and Forever TOC. Staged replacement and rollback protect existing installations.
- SavedVariables, herb history, Retail files and the main recorder are preserved. No account connection is needed.
```

## Ready to paste: 0.3.3 - Optional Add-ons

```text
- New Add-ons tab beside Translate, with Chat Copy and Sparkles for WoW Forever.
- Browse features, in-game commands and installation instructions without connecting an account.
- Download buttons open official add-on ZIPs in your browser; installation stays manual.
- Gathering is now called Realmly Sparkles in-game. Existing settings, saved herb locations and the /rlg command are preserved; /rls also opens it.
- Character uploads, screenshots, translation and existing setup preferences are unchanged.
```

## Ready to paste: 0.3.2 - Translate your way

```text
- Choose Translate only or Translate & reply using the themed Suggest a reply switch.
- A large action button changes its label to match your choice.
- Translate-only hides reply guidance and suggestions, and asks the AI only for the English translation.
- Switching modes clears old results; reply guidance is never sent in translate-only mode.
- Existing uploads, screenshots, account connection and character tools are unchanged.
```

## Ready to paste: 0.3.1 - Translate messages

```text
- New independent Translate tab, styled in Realmly's dark, bronze and gold colours.
- Paste a message for an English translation and a suggested reply in its original language, with an English version alongside it.
- Guide the reply with optional wording, review it, and copy either version yourself.
- Translation requires your connected Realmly account and sends only the pasted message and reply guidance to OpenAI when you click Translate.
- Messages stay separate from character profiles, uploads, screenshots and chronicles. No local translation history or automatic chat sending.
- Includes cancellation, Clear, input limits and protection against stale replies.

Existing adventures, uploads, screenshots, setups and sharing choices are preserved.
```

## Ready to paste: 0.3.0 - Screenshot Manager and reliability fixes

```text
- New Screenshot Manager: browse and preview your Retail and Forever screenshots in separate libraries.
- Match screenshots to recorded achievements and milestones, review their character/day destinations, and upload selected images privately to Realmly.
- Original images stay untouched. Manage captions, badges and sharing on the website.
- Fixed screenshot destinations after addon character-ID migrations.
- Added matching dark dropdowns, gold checkboxes and bronze/gold scrolling.
- Fixed clipped dropdowns at larger Windows display scales and improved long-label readability.

Your existing adventures, screenshots, setups and sharing choices are preserved.
```

## Ready to paste: 0.2.11 update from 0.2.10

```text
- Fixed dropdown boxes being clipped at larger Windows display scales.
- Expanded opened menus for long labels and added full-text selection tooltips.
- Preserved existing keyboard controls, screenshot matching and upload behavior.
```

## Ready to paste: 0.2.10 update from 0.2.9

```text
- Fixed screenshot uploads incorrectly reporting a missing destination after an addon character-ID migration.
- Improved destination errors to identify the affected image and any missing character or adventure day.
- Existing character data, screenshots and sharing choices are preserved.
```

## Ready to paste: 0.2.9 update from 0.2.8

```text
- Updated screenshot checkboxes and scrollbars to Realmly's gold and bronze style.
- Added matching dark dropdown menus throughout the Companion and Screenshot Manager.
- Preserved keyboard navigation, native selection and accessible checkbox behavior.
```

## Ready to paste: 0.2.8 update from 0.2.7

```text
- Added a Screenshot Manager for separate Retail and Forever setups.
- Preview local images and match recorded achievements or milestones by filename time.
- Review character/day destinations and upload selected screenshots privately with automated moderation.
- Original images stay untouched. Open Realmly to edit captions, preview badges and choose what to share.
```

## Ready to paste: 0.2.7 update from 0.2.5

Use this combined text if 0.2.5 was the version in your previous Store submission.
It includes the changes from 0.2.6 and 0.2.7:

```text
- Fixed an issue that could reject a valid Realmly.lua file as your adventure history grew.
- Improved error messages when a SavedVariables file exceeds supported limits.
- Updated Retail labels to WoW Retail and improved Retail addon download handling.

Your saved adventures, setups, sign-in and upload history are preserved.
```

If the previous Store submission was older, also include the relevant changes
from the build history below. Leave out changes already included in that submission.

## Build history

### 0.3.0 - Screenshot-feature Store release

Combines the Screenshot Manager and fixes from 0.2.8 through 0.2.11.
Portable equivalent: 0.1.37. No new portable release is created by this Store version bump.

### 0.2.11 - Dropdown clipping

Portable equivalent: 0.1.37.

- Correct dropdown layout height and native frame padding at scaled displays.
- Wider opened lists and full-selection tooltips.
- Full/narrow layout checks at 96 and 144 DPI.


### 0.2.10 - Screenshot destination recovery

Portable equivalent: 0.1.36.

- Reconnects migrated local capture IDs to the same owned character, realm and game version.
- Rejects ambiguous identities and retains server-side capture checks.
- Shows image-specific character/day guidance instead of a generic missing destination.


### 0.2.9 - Themed controls

Portable equivalent: 0.1.35.

- Gold screenshot ticks and custom bronze/gold library scrolling.
- Dark setup, client, capture, character and day dropdowns with bronze highlights.
- Preserved keyboard and accessibility behavior.


### 0.2.8 - Screenshot manager

Portable equivalent: 0.1.34.

- Local screenshot browsing, safe previews and recorded milestone matching.
- Explicit, cancellable private uploads using the paired Realmly account.
- Separate game setups, destination review and preserved original files.
- Website controls remain responsible for captions, badges and social sharing.


### 0.2.7 - Growing adventure histories

Portable equivalent: 0.1.33.

- Fixed valid Realmly.lua files being rejected as adventure histories grew.
- Improved error messages when SavedVariables files exceed supported limits.

### 0.2.6 - Retail naming

Portable equivalent: 0.1.32.

- Updated the game-client label to WoW Retail.
- Updated Retail addon downloads and cleaned up old tester wording in diagnostics.

### 0.2.5 - Easier addon checks

Portable equivalent: 0.1.31.

- Addon update checks now return the view to the top so the result is easier to see.

### 0.2.4 - Smoother setup switching

Portable equivalent: 0.1.30.

- Improved scrolling and focus when switching game clients or saved setups.
- Made upload status messages easier to read.
- Added clearer guidance when a SavedVariables file has not been created yet.

### 0.2.3 - Correct files for each game

Portable equivalent: 0.1.29.

- Improved SavedVariables file selection when switching between Forever and Retail.
- Kept each client's remembered account separate and preserved custom file locations.
- Turned off automatic uploads when repairing a setup's file selection so it can be reviewed.

### 0.2.2 - Clearer addon installation feedback

Portable equivalent: 0.1.28.

- Added visible explanations when an addon install is blocked or fails.
- Made it clearer that WoW must be fully closed before updating the addon.

### 0.2.1 - WoW Forever folder support

Portable equivalent: 0.1.27.

- Corrected addon installation, file discovery and running-game detection for the WoW Forever folder.
- Kept existing game files and saved adventures in place.

### 0.2.0 - Initial Store package

Portable equivalent: 0.1.26.

- Added Microsoft Store packaging for Realmly Companion.
- Store builds receive Companion updates through Microsoft Store.
- Included addon management, saved setups, upload previews and history, optional automatic uploads, tray mode and diagnostics.

## Keeping notes between submissions

- Add a short player-facing entry here whenever a change affects the Store build.
- Keep the individual build entries even when several builds are skipped for submission.
- Before submitting, combine the entries newer than the last version you submitted into the ready-to-paste section.
- Record the submitted version and date when it is actually submitted; record publication separately when known.
- Building an MSIX or publishing a portable update does not mark a Store release as submitted or published.

For technical implementation details, see [the Companion development notes](../companion/README.md).
For package creation, see [the Store packaging guide](README.md).

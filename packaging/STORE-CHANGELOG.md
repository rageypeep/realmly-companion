# Microsoft Store patch notes

Store submissions can combine several Companion builds. The version headings
below identify which build contains each change; they do not mean that every
build was submitted to or published by Microsoft Store.

The current Store package configuration is **0.2.9** (portable equivalent: **0.1.35**).
Submission dates and the last published Store version are not tracked yet.

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

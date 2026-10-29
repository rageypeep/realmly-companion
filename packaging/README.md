# Microsoft Store package

[Store patch notes and combined submission text](STORE-CHANGELOG.md) are kept separately
from portable releases. Use them to combine changes when skipping Store builds.

This creates a self-contained Windows x64 MSIX of the existing WinForms app.
The Store flavour is **0.2.11**, requires Windows 10 build 19041 or later, and uses
Microsoft Store for Companion updates. Portable development builds remain 0.1.37
with their existing updater. Addon checking, installation, uploads and tray mode
remain available in both flavours.

The Store build excludes the EXE-replacement implementation and its PowerShell
helper. The packaging script does not sign, install, upload or submit anything,
does not modify certificate stores and does not change the website download.

## Required Partner Center values

The registered identity supplied by the maintainer is recorded in
`StoreIdentity.json`. It contains public metadata, not credentials.
To build for a different app, create/reserve its name and open **Product management
→ Product identity** and copy these public values exactly:

- `Package/Identity/Name` → `-IdentityName`
- `Package/Identity/Publisher` → `-Publisher` (including `CN=` and the exact value)
- `Package/Properties/PublisherDisplayName` → `-PublisherDisplayName`

If the reserved app name differs from Realmly Companion, also supply `-DisplayName`.
These are package identity values, not account passwords or signing credentials.
Do not guess them: a mismatched identity cannot be submitted as your app.

## Build the submission package

Install the .NET 8 SDK and Windows 10/11 SDK on Windows. From the repository root:

```powershell
./packaging/Build-Msix.ps1
# Or explicitly override all three identity fields:
./packaging/Build-Msix.ps1 -IdentityName 'EXACT_PARTNER_CENTER_NAME' -Publisher 'CN=EXACT_PARTNER_CENTER_PUBLISHER' -PublisherDisplayName 'YOUR_PUBLISHER_DISPLAY_NAME'
```

The script prints the MSIX path and SHA-256. Every build has a fresh directory
under `artifacts/msix/`; generated packages and logos are not committed.
Package version 0.2.11.0 and display version 0.2.11 match. Use `-Version` with a
new four-part version ending in `.0` when submitting a later Store release.

The MSIX is **unsigned for submission**. It is not a directly installable,
trusted customer download. Store signing happens after Microsoft's submission
process; simply creating this package does not mean the app is approved.

## Validate before identity is available

```powershell
./packaging/Build-Msix.ps1 -ValidationOnly
```

This deliberately uses a fake identity and names the output `*-validation-only.msix`.
Do not submit, distribute or install that package. Rebuild with the real identity.
MakeAppx performs package validation; that is not a substitute for Store
certification or an installed-package smoke test.

## Regression tests

Run both flavours on Windows:

```powershell
dotnet run --project companion.tests/CompanionTests.csproj -c Release -- ./output/portable
dotnet run --project companion.tests/CompanionTests.csproj -c Release -p:StoreDistribution=true -- ./output/store
```

Tests verify that Store builds neither consult the portable update feed nor
invoke the replacement path, while portable builds retain update checking.
You can also inspect the actual published payload without running the app:

```powershell
dotnet run --project companion.tests/CompanionTests.csproj -c Release -- --store-payload ./artifacts/msix/BUILD_ID/payload
```

The packaged runtime's licence and third-party notices are included under
`ThirdPartyLicenses/`, in addition to the Companion's `LICENSE.txt`.

## Before submission

- Test the installed package through an appropriate signed test build or Store
  private flight, especially game folders on different drives, addon replacement,
  SavedVariables reads, browser pairing, tray mode and optional auto-uploads.
- Test permissions/virtualization for game installations under Program Files.
- Verify settings/token migration from the portable app and avoid simultaneously
  running Store and portable instances with auto-uploads enabled.
- Supply an accurate privacy policy, screenshots and support information.
- Explain `runFullTrust` in certification notes: this is a normal, non-elevated
  desktop app which needs user-selected game-file access and native tray UI.
- Provide reviewers a supported way to test account-dependent features and
  disclose that Realmly and the addon are required. Do not publish reviewer
  credentials in this repository.
- Keep third-party names/artwork permissions and independent-fan-project wording
  clear. Store acceptance has not been established.

See Microsoft's [manual packaging guide](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-manual-conversion)
and [Product identity instructions](https://learn.microsoft.com/en-us/windows/apps/publish/view-app-identity-details).

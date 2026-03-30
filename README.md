# Focus

Focus is a .NET 8 console application for building and maintaining text-first mind maps. It stores maps as JSON files, presents them in a tree-like terminal UI, and lets you navigate, expand, split, and link ideas without leaving the keyboard.

## What the project does

- Creates and edits mind maps as nested nodes.
- Stores user data in JSON files under a configurable data folder.
- Uses a page-based terminal interface with command history and autocomplete.
- Supports idea tags, cross-node links, collapsing branches, and slicing parts of a map into a new file.
- Can synchronize saved maps through Git when a repository is configured on Windows.
- Can check for and apply packaged app updates through Velopack when the app is installed.

## How it works

When the app starts, it loads or creates a user configuration file at `~/focus-config.json`. If no valid configuration exists, it asks the user where data should be stored and defaults to the system Documents folder.

Mind maps are saved as `.json` files inside:

`<DataFolder>/FocusMaps`

The home screen lists the most recently accessed map files and lets the user:

- open a map by number, shortcut key, or file name
- create a new map
- rename or delete an existing map
- refresh the list
- trigger an application update when one is available
- exit the app

Inside the editor, the current node subtree is rendered in the console with shortcuts for child selection. The main commands include:

- `add` to add a note
- `idea` to add an idea-tag child
- `clearideas` to remove idea tags
- `cd`, `up`, and `ls` to move through the tree
- `edit` and `del` to modify or remove nodes
- `min` and `max` to collapse or expand nodes
- `slice` to attach or detach map content
- `linkfrom` and `linkto` to create cross-map or cross-node links
- `exit` to leave the editor

Every successful edit saves the active map immediately. After saving, the app also runs the configured synchronization handler.

## Solution structure

The solution is split into two projects:

- `Focus/Focus`
  The main application. It contains the domain model (`MindMap`, `Node`, `MapsStorage`), page flow, dialogs, update handling, and map file operations.

- `Focus/Systems.Sanity.Focus.Inrastructure`
  Shared infrastructure for console rendering, command input, autocomplete/history, OS helpers, and file synchronization support.

Notable implementation details:

- The console input system is custom. It supports history navigation, inline editing, autocomplete, and password input.
- Map rendering is text-based and color-aware through `ColorfulConsole`.
- Git synchronization is enabled only when a Git repository is configured and the app is running on Windows.
- Packaging supports `win-x64`, `linux-x64`, and `osx-arm64` publishing through `Velopack`.

## Build and run

From the repository root:

```bash
dotnet build Focus/Focus.sln
dotnet run --project Focus/Focus/Systems.Sanity.Focus.csproj
```

For local release deployment of build artifacts, the repository includes:

- `Focus/Focus/ReleaseAndUpload.Shared.ps1`
  Shared helper functions for release versioning, artifact validation, and platform-scoped sync planning across Windows, Linux, and Mac publishing.

- `Focus/Focus/ReleaseAndUpload.ps1`
  Calculates the next shared release version automatically, builds and packages the Windows app, prompts for upload credentials when needed, stores them as a DPAPI-encrypted `Export-Clixml` file under `%APPDATA%\Focus\Secrets\focus-ftps.clixml`, and mirrors only Windows-owned files from `Focus/Focus/Releases` to an FTP, FTPS, or SFTP target directory using WinSCP.

- `Focus/Focus/ReleaseAndUploadLauncher.ps1`
  Convenience wrapper around `ReleaseAndUpload.ps1` that targets the project's current SFTP release endpoint.

- `Focus/Focus/ReleaseAndUploadLinux.ps1`
  Calculates the next shared release version automatically, builds and packages the Linux app for `linux-x64`, and mirrors only Linux-owned files from `Focus/Focus/Releases` to an SFTP target directory using `ssh` and `scp` with the current SSH key or agent configuration.

- `Focus/Focus/ReleaseAndUploadLinuxLauncher.ps1`
  Convenience wrapper around `ReleaseAndUploadLinux.ps1` that targets the project's current SFTP release endpoint.

- `Focus/Focus/ReleaseAndUploadMac.ps1`
  Calculates the next shared release version automatically, builds and packages the macOS app for `osx-arm64`, requires Apple signing and notarization inputs for Velopack packaging, and mirrors only Mac-owned files from `Focus/Focus/Releases` to an SFTP target directory using `ssh` and `scp` with the current SSH key or agent configuration.

- `Focus/Focus/ReleaseAndUploadMacLauncher.ps1`
  Convenience wrapper around `ReleaseAndUploadMac.ps1` that targets the project's current SFTP release endpoint.

Example workflow:

```powershell
pwsh -File Focus/Focus/ReleaseAndUpload.ps1 -RemoteBaseUrl sftp://example.com/var/www/html/Releases -UpdateCredential -DryRun
pwsh -File Focus/Focus/ReleaseAndUpload.ps1 -RemoteBaseUrl sftp://example.com/var/www/html/Releases
pwsh -File Focus/Focus/ReleaseAndUpload.ps1 -RemoteBaseUrl sftp://example.com/var/www/html/Releases -Version Auto
pwsh -File Focus/Focus/ReleaseAndUploadLauncher.ps1 -DryRun
pwsh -File Focus/Focus/ReleaseAndUploadLauncher.ps1 -Version Auto
pwsh -File Focus/Focus/ReleaseAndUploadLauncher.ps1
pwsh -File Focus/Focus/ReleaseAndUploadLinux.ps1 -RemoteBaseUrl sftp://example.com/var/www/html/Releases -RemoteUser deploy -DryRun
pwsh -File Focus/Focus/ReleaseAndUploadLinux.ps1 -RemoteBaseUrl sftp://example.com/var/www/html/Releases -RemoteUser deploy
pwsh -File Focus/Focus/ReleaseAndUploadLinux.ps1 -RemoteBaseUrl sftp://example.com/var/www/html/Releases -RemoteUser deploy -Version Auto
pwsh -File Focus/Focus/ReleaseAndUploadLinuxLauncher.ps1 -RemoteUser deploy -DryRun
pwsh -File Focus/Focus/ReleaseAndUploadLinuxLauncher.ps1 -RemoteUser deploy -Version Auto
pwsh -File Focus/Focus/ReleaseAndUploadLinuxLauncher.ps1 -RemoteUser deploy
pwsh -File Focus/Focus/ReleaseAndUploadMac.ps1 -RemoteBaseUrl sftp://example.com/var/www/html/Releases -RemoteUser deploy -BundleId com.example.focus -SignAppIdentity "Developer ID Application: Example" -SignInstallIdentity "Developer ID Installer: Example" -NotaryProfile focus-notary -DryRun
pwsh -File Focus/Focus/ReleaseAndUploadMac.ps1 -RemoteBaseUrl sftp://example.com/var/www/html/Releases -RemoteUser deploy -BundleId com.example.focus -SignAppIdentity "Developer ID Application: Example" -SignInstallIdentity "Developer ID Installer: Example" -NotaryProfile focus-notary
pwsh -File Focus/Focus/ReleaseAndUploadMac.ps1 -RemoteBaseUrl sftp://example.com/var/www/html/Releases -RemoteUser deploy -Version Auto -BundleId com.example.focus -SignAppIdentity "Developer ID Application: Example" -SignInstallIdentity "Developer ID Installer: Example" -NotaryProfile focus-notary
pwsh -File Focus/Focus/ReleaseAndUploadMacLauncher.ps1 -RemoteUser deploy -BundleId com.example.focus -SignAppIdentity "Developer ID Application: Example" -SignInstallIdentity "Developer ID Installer: Example" -NotaryProfile focus-notary -DryRun
pwsh -File Focus/Focus/ReleaseAndUploadMacLauncher.ps1 -RemoteUser deploy -Version Auto -BundleId com.example.focus -SignAppIdentity "Developer ID Application: Example" -SignInstallIdentity "Developer ID Installer: Example" -NotaryProfile focus-notary
pwsh -File Focus/Focus/ReleaseAndUploadMacLauncher.ps1 -RemoteUser deploy -BundleId com.example.focus -SignAppIdentity "Developer ID Application: Example" -SignInstallIdentity "Developer ID Installer: Example" -NotaryProfile focus-notary
```

Notes:

- Windows publishing requires WinSCP. The release script uses `WinSCP.com`, so the standard WinSCP install works in `pwsh`.
- Linux publishing requires PowerShell 7, the .NET SDK, `vpk`, and the OpenSSH client tools (`ssh` and `scp`) to be available on the machine running the script.
- macOS publishing requires PowerShell 7, the .NET SDK, `vpk`, the OpenSSH client tools (`ssh` and `scp`), and a macOS host with Apple Developer signing identities plus a configured `notarytool` profile.
- Use `-UpdateCredential` when you want to overwrite the saved Windows upload credential before running a Windows release.
- Linux publishing does not store credentials locally. It uses the active SSH key, agent, and `known_hosts` configuration.
- macOS publishing also uses the active SSH key, agent, and `known_hosts` configuration for uploads. It additionally requires a checked-in `.icns` file at `Focus/Focus/Packaging/mac/Focus.icns` unless you override `-IconPath`.
- `-SkipBuild` uploads an already prepared local `Releases` folder without rerunning packaging. Version resolution stays platform-specific for `-SkipBuild`.
- `-Version Auto` resolves the next shared release version from the current remote `/Releases` feed and always bumps the latest managed version by one patch/build.
- `-Version Auto` requires remote connectivity and an already populated remote release directory, ignores `-Increment`, and cannot be combined with `-SkipBuild`.
- `-DryRun` prints the planned upload and delete actions without changing the remote directory.
- `-RemoteBaseUrl` accepts `ftp://...`, `ftps://...`, `ftpes://...`, and `sftp://...`. `-FtpsBaseUrl` still works as an alias for backwards compatibility.
- `ReleaseAndUploadLinux.ps1` accepts only `sftp://...` endpoints and an optional `-RemoteUser`.
- `ReleaseAndUploadMac.ps1` accepts only `sftp://...` endpoints, requires `-BundleId`, `-SignAppIdentity`, `-SignInstallIdentity`, and `-NotaryProfile`, and optionally accepts `-Keychain` plus `-SignEntitlements`.
- `ftps://...` uses the selected `-FtpsMode` and defaults to explicit TLS. `ftpes://...` always uses explicit TLS.
- If you use SFTP and do not provide `-SshHostKeyFingerprint`, WinSCP will accept the server host key on first connection.
- Windows, Linux, and Mac scripts share one release version sequence and one remote `/Releases` feed, but each script uploads and deletes only its own platform artifacts.
- The remote target directory should correspond to the HTTP-served `/Releases` endpoint used by `AutoUpdateManager`.

## Data and configuration

- User config file: `~/focus-config.json`
- Map storage folder: `<DataFolder>/FocusMaps`
- File format: JSON
- Default map file extension: `.json`

The user config currently supports:

- `dataFolder`
- `gitRepository`
- `translations`

A ready-to-copy sample config lives at [docs/focus-config.sample.json](docs/focus-config.sample.json).

`translations` lets Focus convert localized keyboard input back into the built-in command alphabet. The sample file includes a full Georgian `ka-GE` character dictionary for command entry.

## Development notes

- The solution currently builds successfully with dotnet build Focus/Focus.sln.
- The build emits warnings, including package compatibility warnings around Microsoft.Alm.Authentication and several nullable/platform-specific warnings.
- There is an existing note in Focus/README.txt about console font support: install merged-unifont-mono.ttf if you want better Unicode rendering in the terminal.

## Summary

Focus is a keyboard-driven personal knowledge tool for managing mind maps in plain JSON files. The codebase centers on a console-first editing experience, lightweight persistence, and optional cross-platform distribution with Windows-oriented synchronization features.

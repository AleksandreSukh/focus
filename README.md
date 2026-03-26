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
- Packaging is currently oriented toward `win-x64` publishing through `Velopack`.

## Build and run

From the repository root:

```bash
dotnet build Focus/Focus.sln
dotnet run --project Focus/Focus/Systems.Sanity.Focus.csproj
```

For local release deployment of build artifacts, the repository includes:

- `Focus/Focus/ReleaseAndUpload.ps1`
  Calculates the next release version automatically, builds and packages the app, prompts for upload credentials when needed, stores them as a DPAPI-encrypted `Export-Clixml` file under `%APPDATA%\Focus\Secrets\focus-ftps.clixml`, and mirrors the generated `Focus/Focus/Releases` directory to an FTP, FTPS, or SFTP target directory using WinSCP.

- `Focus/Focus/ReleaseAndUploadLauncher.ps1`
  Convenience wrapper around `ReleaseAndUpload.ps1` that targets the project's current SFTP release endpoint.

Example workflow:

```powershell
pwsh -File Focus/Focus/ReleaseAndUpload.ps1 -RemoteBaseUrl sftp://example.com/var/www/html/Releases -UpdateCredential -DryRun
pwsh -File Focus/Focus/ReleaseAndUpload.ps1 -RemoteBaseUrl sftp://example.com/var/www/html/Releases
pwsh -File Focus/Focus/ReleaseAndUploadLauncher.ps1 -DryRun
pwsh -File Focus/Focus/ReleaseAndUploadLauncher.ps1
```

Notes:

- WinSCP must be installed locally. The release script uses `WinSCP.com`, so the standard WinSCP install works in `pwsh`.
- Use `-UpdateCredential` when you want to overwrite the saved upload credential before running a release.
- `-SkipBuild` uploads an already prepared local `Releases` folder without rerunning packaging.
- `-DryRun` prints the planned upload and delete actions without changing the remote directory.
- `-RemoteBaseUrl` accepts `ftp://...`, `ftps://...`, `ftpes://...`, and `sftp://...`. `-FtpsBaseUrl` still works as an alias for backwards compatibility.
- `ftps://...` uses the selected `-FtpsMode` and defaults to explicit TLS. `ftpes://...` always uses explicit TLS.
- If you use SFTP and do not provide `-SshHostKeyFingerprint`, WinSCP will accept the server host key on first connection.
- The remote target directory should correspond to the HTTP-served `/Releases` endpoint used by `AutoUpdateManager`.

## Data and configuration

- User config file: `~/focus-config.json`
- Map storage folder: `<DataFolder>/FocusMaps`
- File format: JSON
- Default map file extension: `.json`

The user config currently supports:

- `DataFolder`
- GitRepository

## Development notes

- The solution currently builds successfully with dotnet build Focus/Focus.sln.
- The build emits warnings, including package compatibility warnings around Microsoft.Alm.Authentication and several nullable/platform-specific warnings.
- There is an existing note in Focus/README.txt about console font support: install merged-unifont-mono.ttf if you want better Unicode rendering in the terminal.

## Summary

Focus is a keyboard-driven personal knowledge tool for managing mind maps in plain JSON files. The codebase centers on a console-first editing experience, lightweight persistence, and optional Windows-oriented distribution and synchronization features.

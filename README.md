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

The repository also includes a packaging script:

`Focus/Focus/BuildAndPublish.ps1`

That script publishes a self-contained Windows build and creates a Velopack package.

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

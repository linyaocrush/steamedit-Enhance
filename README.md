<p align="center">
  <img src="app.ico" alt="SteamEdit Logo" width="128" />
</p>

<h1 align="center">SteamEdit</h1>

<p align="center">
  <strong>Steam Game Info Editor</strong><br/>
  Rename games, sort, manage launch configs, batch-set controller & VR support
</p>

<p align="center">
  <a href="README-CN.md">中文版</a>
</p>

A Windows desktop application for editing Steam's local `appinfo.vdf` file. Supports renaming games, sorting, hiding, completion markers, batch editing controller/VR settings, and translating names via AI.

> Based on [steamedit](https://steamedit.tg-software.com/) by Tim Green.

## Features

### Edit Game Information

- Rename games, modify sort names
- Toggle hidden/completed status
- Change game type (Game, DLC, Demo, etc.)
- Edit launch configurations (EXE path, launch parameters, working directory)
- Single/batch edit controller support (None/Partial/Full)
- Single/batch edit VR category (Default/Removed)

### AI Translation

- Translate game names to any language via OpenAI-compatible API
- Context-aware: sends developer/publisher/type to improve translation quality
- Custom translation prompts with variables
- Batch translate all filtered games with progress, pause/resume, cancel
- Auto-cached glossary (`glossary.json`): skip API calls for known translations
- AI learns translation rules by analyzing existing glossary patterns

### Search & Find/Replace

- Multi-word fuzzy search (fuzzy search, search terms separated by spaces)
- Web search for game names (supports Google/Bing/DuckDuckGo)
- Batch find/replace with regex and case-sensitivity support

### Custom Sort

- Sort by Name or App ID, ascending/descending

### Other

- Multi-language UI: English / Chinese, switch instantly
- Save / Apply & Restart Steam (single click)
- Auto-start with Windows
- Command-line mode: `-autofix` (headless apply), `-forcestart` (apply & restart Steam)
- Single-file distribution (~600KB, requires .NET 10 Runtime)

## Usage

### 1. Download & Install

Install .NET 10 Runtime (if not using self-contained build):

[Download .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)

### 2. Run

```bash
dotnet run --project SteamEdit.csproj
```

### 3. Build

```bash
dotnet build SteamEdit.csproj
```

### 4. Publish Single Executable

Self-contained (no runtime required, ~110MB):

```bash
dotnet publish SteamEdit.csproj -c Release
```

Framework-dependent (requires .NET 10 Runtime, ~600KB):

```bash
dotnet publish SteamEdit.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=true
```

Output: `bin\Release\net10.0-windows\win-x64\publish\SteamEdit.exe`

### 5. Usage Steps

1. Start the application — it auto-detects Steam path
2. Modify game names, toggle hidden/completed, batch edit controller/VR
3. Click **Save** (saves to `mods.dat`) or **Save & Apply** (writes to `appinfo.vdf` and restarts Steam)

## Technical Stack

| Item | Description |
|------|-------------|
| Framework | .NET 10 |
| UI | Windows Forms (WinForms) |
| Build Tool | dotnet CLI |
| Language | C# 13+ |
| Dependencies | None |

## Project Structure

```
SteamEdit.csproj                  # Single project file

SteamEdit/                        # UI Layer
  MainWindow.cs                   # Main form (game list + edit panel)
  LaunchEditor.cs                 # Launch config editor
  SettingsDlg.cs                  # Settings dialog
  BatchTranslateDialog.cs         # Batch AI translation dialog
  BatchReplaceDialog.cs           # Batch find/replace dialog
  ClearableTextBox.cs             # Custom TextBox with clear button
  I18n.cs                         # Language manager (event-based)
  Resources/
    Strings.resx                  # English strings (default)
    Strings.zh-CN.resx            # Chinese translations

SteamEdit.Data/                   # Core Data Model & I/O
  SteamData.cs                    # Static singleton orchestrator
  AppInfoFactory.cs               # Version-dispatching factory
  PropertyTable.cs                # Hierarchical property store (Dictionary-optimized)
  Mods.cs                         # mods.dat binary format reader/writer
  ModifiedEntry.cs                # Modification overlay (diff tracker)

SteamEdit.Data.Impl/              # Version-specific parsers
  AppInfoEntryBase.cs             # Shared base class
  AppInfoEntry_0x07564427.cs      # Old format
  AppInfoEntry_0x07564428.cs      # Mid format
  AppInfoEntry_0x07564429.cs      # New format

Common/                           # Shared Utilities
  Settings.cs                     # User preferences (custom XML serialization)
  AiTranslator.cs                 # OpenAI-compatible API client
  TranslationGlossary.cs          # Glossary cache
  StringTitleComparer.cs          # Natural sort
  UndoManager.cs                  # Undo stack
```

## Architecture Highlights

| Design | Description |
|--------|-------------|
| Version polymorphism | `AppInfoFactory` + `AppInfoEntryBase` + version-specific subclasses |
| Property path addressing | `["appinfo", "common", "name"]` mirrors VDF hierarchy |
| Modification overlay | Original data preserved, changes tracked as property diffs |
| PropertyTable optimization | Parallel `Dictionary` for O(1) lookups |
| Display name cache | `Dictionary<int, string>` for ListBox rendering |
| Incremental search | Three-way narrowing/widening/replacement filter |

## Steam Path Detection

Reads `HKCU\Software\Valve\Steam\SteamPath` from the registry, then constructs `appcache/appinfo.vdf`. Falls back to `%ProgramFiles(x86)%\Steam`.

## Command-Line Mode

```bash
# Auto-apply pending changes (headless)
SteamEdit.exe -autofix

# Auto-apply and restart Steam
SteamEdit.exe -autofix -forcestart
```

Used for scheduled tasks or unattended operation.

## License

Internal tool, no license.

## Acknowledgments

- [steamedit](https://steamedit.tg-software.com/) by Tim Green

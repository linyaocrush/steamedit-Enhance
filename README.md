<p align="center">
  <img src="app.ico" alt="steamedit-Enhance Logo" width="128" />
</p>

<h1 align="center">steamedit-Enhance</h1>

<p align="center">
  <strong>Local Steam Library Editing Tool</strong><br/>
  Rename games, modify sorting, manage launch configurations, AI translate game names
</p>

<p align="center">
  <a href="README-CN.md">中文</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2F11-blue?style=flat-square" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/UI%20Languages-English%20%7C%20简体中文%20%7C%20繁體中文-orange?style=flat-square" alt="Languages" />
  <img src="https://img.shields.io/badge/License-Proprietary-red?style=flat-square" alt="License" />
</p>

---

## About

SteamEdit is a Windows desktop application that edits Steam's local `appinfo.vdf` file. It lets you customize display names, sorting order, hidden/completed status, launch parameters, controller support, VR settings, and more — all from your local Steam library.

> **Original software:** [https://steamedit.tg-software.com/](https://steamedit.tg-software.com/)

> **Disclaimer:** This project is a migration and localization of the original SteamEdit. If you believe your rights are being infringed, please [submit an Issue](../../issues) and I will address it promptly.

---

## New Features (Compared to the Original)

| Feature | Description |
|:---|:---|
| **AI Translation** | Integrated OpenAI-compatible API for one-click game name translation. Customizable API endpoint, key, and model selection; supports fetching model lists. Automatically uses existing modifications as a glossary to maintain translation consistency. |
| **Glossary Cache** | Modified game names are automatically cached in `glossary.json`, incrementally synced at startup, and read directly during translation — no rebuilding needed. |
| **Web Search** | One-click browser search for game name + translated name. Supports Google, Bing, Baidu, and DuckDuckGo. |
| **Multilingual UI** | English, Simplified Chinese, and Traditional Chinese. Switch languages in Settings without restarting. |
| **Start with Windows** | Option to launch SteamEdit automatically on system startup. Supports `-autofix` (auto-apply changes) and `-forcestart` (restart Steam) arguments. |
| **DPI Awareness** | PerMonitorV2 high-DPI support — no blurriness on 4K or high-resolution displays. |
| **Asynchronous Loading** | `appinfo.vdf` loading runs asynchronously with a wait dialog, cancel option, and 30-second timeout. |
| **Filter Debounce** | 500ms debounce on search input to avoid excessive list refreshes. |
| **Custom Sorting** | Sort games by Name or App ID, with ascending/descending toggle. Sort preference is persisted. |
| **Batch Find & Replace** | Supports plain text and regular expressions with case-sensitivity toggle. Shows live match count. |
| **Translation Rules** | AI analyzes the glossary to automatically derive translation rules, which are then applied to future translations for consistent style. |
| **Crash Logging** | Unhandled exceptions generate a timestamped `crash-*.log` file with system info and full stack trace. Dialog shows the file path with "Close" (continue) or "Exit" options. |

---

## Improvements Over the Original

| Improvement | Description |
|:---|:---|
| **Property Lookup Speed** | PropertyTable now uses a parallel Dictionary, reducing property lookup from O(n) to O(1). |
| **Buffered Log Writing** | Replaced `File.AppendAllLines` with `StreamWriter` (auto-flush every 20 lines) to cut disk I/O. Old logs are cleared on startup; only the current session's log is kept. |
| **Code Refactoring** | Extracted `AppInfoBase<TEntry>` generic base class for three `AppInfoEntry` versions; `PropertyTableIO` delegate injection to unify six duplicated type switches; `Mods` reader consolidation (V4/V5/V6 → single method); `LocalizableForm` base class for i18n boilerplate — eliminating ~1000+ lines of duplicated code total. |
| **Unified Path Resolution** | Consolidated startup path logic into a single method, removing three separate implementations. |
| **Sort Precomputation** | Normalized sort keys are precomputed, avoiding repeated string processing during O(n log n) sorting. |
| **Resource Management** | `IDisposable` resources (e.g., SHA1) are properly released, eliminating memory leaks. |
| **Null-Reference Guards** | Added null checks in 9+ event handlers to prevent crashes when the list is empty. |
| **UI Layout Fix** | Autostart area now uses `FlowLayoutPanel` so Chinese text no longer overlaps adjacent controls. |

---

## Features

| Feature | Description |
|:---|:---|
| **Game Renaming** | Customize the display name of games in your Steam library |
| **Sorting Modification** | Set a custom sort name to change the game’s position in the list |
| **Hide / Completed** | Mark games as hidden or completed |
| **Launch Configuration** | Edit launch executable, arguments, and working directory |
| **Entry Type** | Change the game type (Game, DLC, Demo, etc.) |
| **Controller Support** | Set controller support level (None / Partial / Full) |
| **VR Category** | Set VR category (Default / Removed) |
| **Custom Sorting** | Sort by Name or App ID, with ascending/descending toggle |
| **Batch Find & Replace** | Replace game names in filtered results using plain text or regex, with case-sensitivity option |
| **AI Translation** | Translate game names individually or in batch, with customizable prompt and translation rules |

---

## Requirements

- Windows 10 / 11 (x64)
- Steam client installed

> **Self-contained build** requires no .NET runtime. **Framework-dependent build** needs [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

---

## Building and Running

```bash
# Clone the repository
git clone https://github.com/<your-username>/steamedit.git
cd steamedit

# Development build
dotnet build SteamEdit.csproj

# Run tests
dotnet test SteamEditTests/SteamEditTests.csproj

# Publish self-contained single-file EXE (~110 MB, no runtime required)
dotnet publish SteamEdit.csproj -c Release

# Publish framework-dependent (~627 KB, requires .NET 10 Runtime)
dotnet publish SteamEdit.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=true

# Run
dotnet run --project SteamEdit.csproj
```

Published output is located under `bin/Release/net10.0-windows/win-x64/publish/`.

---

## Usage

### First Launch

1. Run `SteamEdit.exe`
2. The program will automatically locate the Steam installation via the registry
3. If not found, a file dialog will appear — manually locate `appinfo.vdf`  
   > Default path: `C:\Program Files (x86)\Steam\appcache\appinfo.vdf`

### Editing Workflow

1. Select a game from the list on the left
2. Modify the name, sort key, controller support, and other fields on the right
3. Click **Save** → **Apply** → **Restart Steam** for changes to take effect

### AI Translation

1. Open **Settings**, enter your AI API endpoint (e.g., `https://api.openai.com/v1`) and API Key
2. Click **Fetch Model List** to select a model, or enter the model name manually
3. Choose the target translation language (Simplified Chinese / English)
4. Select a game in the main window and click the **AI Translate** button — the translated name will be automatically filled in
5. The glossary is cached to `glossary.json`; translation style will reference your previous modifications

Any service compatible with the OpenAI Chat Completions API is supported (OpenAI, Claude API proxies, local Ollama, etc.).

### Web Search

Select a game and click **Web Search** to open a browser searching for `game name + target language`. The search engine can be changed in Settings (Google / Bing / Baidu / DuckDuckGo).

### Command-Line Mode

```bash
# Auto-apply pending changes (no GUI)
SteamEdit.exe -autofix

# Auto-apply and restart Steam
SteamEdit.exe -autofix -forcestart
```

Useful for periodic maintenance via scheduled tasks.

---

## Internationalization

SteamEdit includes English, Simplified Chinese, and Traditional Chinese, managed through `.resx` resource files.

### Switching Languages

1. Open **Settings** (gear icon)
2. Choose `English`, `简体中文`, or `繁體中文` from the **Language** dropdown
3. The UI switches instantly — no restart required

### Adding a New Language

1. Copy `SteamEdit/Resources/Strings.resx` to `Strings.{culture}.resx` (e.g., `Strings.ja.resx`)
2. Translate all text inside `<value>` tags
3. Add the corresponding option in `SettingsDlg.cs` → `comboLanguage.Items`

---

## Project Structure

```
steamedit/
├── SteamEdit/                    # WinForms UI layer
│   ├── MainWindow.cs             # Main window (layout + event routing)
│   ├── GameListManager.cs        # Game list: search/filter/sort/ListBox
│   ├── EditController.cs         # Property editing/undo/revert
│   ├── GameDataContext.cs        # Shared data container
│   ├── LaunchEditor.cs           # Launch configuration editor
│   ├── SettingsDlg.cs            # Settings dialog
│   ├── I18n.cs                   # Language manager
│   └── Resources/
│       ├── Strings.resx          # English resources (default)
│       ├── Strings.zh-CN.resx   # Simplified Chinese translation
│       └── Strings.zh-TW.resx   # Traditional Chinese translation
├── SteamEdit.Data/               # Core data model & I/O
│   ├── SteamData.cs              # Data loading / saving / applying
│   ├── AppInfoFactory.cs         # Version dispatch factory
│   ├── PropertyTable.cs          # Property table (Dictionary-optimized)
│   └── Mods.cs                   # mods.dat read/write
├── SteamEdit.Data.Impl/          # appinfo.vdf version implementations
│   ├── AppInfoEntryBase.cs       # Entry base class (shared logic)
│   ├── AppInfo_0x07564427.cs     # Legacy format
│   ├── AppInfo_0x07564428.cs     # Intermediate format
│   └── AppInfo_0x07564429.cs     # Latest format
├── Common/                       # Shared utilities
│   ├── Settings.cs               # User settings (JSON serialization + dirty flag)
│   ├── Log.cs                    # Buffered logger
│   ├── AiTranslator.cs           # AI translator (OpenAI-compatible API)
│   ├── TranslationGlossary.cs    # Glossary cache (glossary.json)
│   └── AppHelper.cs              # Path & argument helpers
├── SteamEditTests/               # MSTest unit tests
│   ├── PropertyTableTests.cs     # PropertyTable: 15 tests
│   ├── ModsTests.cs              # Mods binary format: 5 tests
│   └── SettingsTests.cs          # Settings JSON serialization: 4 tests
└── SteamEdit.csproj              # Project file
```

---

## Tech Stack

| Technology | Version |
|:---|:---|
| .NET | 10.0 |
| Language | C# 13+ |
| UI Framework | Windows Forms |
| Target Platform | win-x64 |
| Publishing | Single-file EXE (self-contained or framework-dependent) |

---

## License & Disclaimer

- Original software copyright belongs to [Tim Green](mailto:tim@tg-software.com)
- Original download: [https://steamedit.tg-software.com/](https://steamedit.tg-software.com/)
- This project only involves a technical migration (.NET Framework → .NET 10) and UI localization
- **If there is any infringement, please [submit an Issue](../../issues) to request removal**

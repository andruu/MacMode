<p align="center">
  <img src="assets/logo.png" alt="MacMode" width="128" height="128">
</p>

<h1 align="center">MacMode</h1>

<p align="center">
  <b>Your Mac muscle memory, on Windows.</b>
</p>

Maybe you code on a Mac all day but game on a Windows PC at night. Maybe you forgot your MacBook at home and you're stuck with your Windows laptop. Or maybe you just made the switch and your fingers keep pressing the wrong shortcuts. Whatever the reason, MacMode has your back.

MacMode is a lightweight Windows system-tray app that makes your **Left Alt key behave like the Mac Command key**. Press **Alt+C** to copy, **Alt+V** to paste, **Alt+T** for a new tab, **Alt+Click** to open a link in a new tab. All the shortcuts you already know just work. It runs quietly in the background, translating your macOS muscle memory into the correct Windows equivalents. No drivers, no remapping tools, no relearning.

This project was vibecoded by a developer with 20+ years on Mac who got tired of relearning every shortcut after picking up a gaming laptop. If you're in the same boat, give it a spin.

---

## Table of Contents

- [How It Works](#how-it-works)
- [Installation](#installation)
- [Usage](#usage)
- [Shortcut Reference](#shortcut-reference)
  - [Global (All Apps)](#global-all-apps)
  - [Chrome / Edge / Brave / Firefox](#chrome--edge--brave--firefox)
  - [VS Code / Cursor](#vs-code--cursor)
  - [Warp Terminal](#warp-terminal)
  - [Windows Terminal / PowerShell](#windows-terminal--powershell)
  - [Windows Explorer](#windows-explorer)
- [Adding Custom Shortcuts](#adding-custom-shortcuts)
  - [Using the Profile Editor](#using-the-profile-editor)
  - [Editing JSON Manually](#editing-json-manually)
- [Creating a New App Profile](#creating-a-new-app-profile)
- [Architecture](#architecture)
- [Settings](#settings)
- [Logs](#logs)
- [Limitations](#limitations)
- [Building from Source](#building-from-source)
- [License](#license)

---

## How It Works

MacMode installs a low-level keyboard hook that intercepts keystrokes system-wide. When **Left Alt** is pressed with another key, the app checks the active foreground process, looks up the matching profile, and translates the shortcut into the correct Windows equivalent.

**The key mapping:** On a Mac, the **Cmd** key sits right next to the spacebar. On a Windows keyboard, **Left Alt** is in that same position. This app treats Left Alt as Cmd, so your muscle memory just works.

When you press a shortcut like **Alt+C** (which your brain thinks of as **Cmd+C**), the app intercepts it and sends **Ctrl+C** to Windows behind the scenes. You never need to think about what Windows shortcut to use. Just press what you'd press on a Mac.

**What stays untouched:**

| Behavior | Detail |
|---|---|
| **Right Alt (AltGr)** | Completely untouched. Works normally for international characters |
| **Win key** | Unchanged |
| **Ctrl key** | Unchanged |
| **Alt alone** | Preserved. Tapping Alt still opens menu bars |
| **Alt+Tab** | Never translated. Works natively |
| **Alt+F4** | Never translated. Works natively |
| **Alt+Space** | Never translated. Window menu works normally |
| **Alt+`** | Cycles between windows of the same app (like Cmd+` on Mac) |

The app uses per-process profiles. When you're in Chrome, browser-specific shortcuts are active. When you switch to VS Code, editor shortcuts take over. Unrecognized apps fall back to the default profile.

---

## Installation

### Option 1: Download Release

Download the latest release from the [Releases](../../releases) page. Extract the zip and run `MacMode.App.exe`.

### Option 2: Build from Source

```powershell
git clone https://github.com/andruu/MacMode.git
cd MacMode
dotnet build
dotnet run --project src/MacMode.App
```

### Option 3: Publish Self-Contained Exe

```powershell
.\publish.ps1
# Output: ./dist/MacMode.App.exe (self-contained, no .NET install required)
```

---

## Usage

1. Run `MacMode.App.exe`
2. A tray icon appears in the system tray (bottom-right near the clock)
   - **Blue Finder face** = Mac Mode ON
   - **Gold Finder face** = Mac Mode ON, running as Admin
   - **Gray Finder face** = Mac Mode OFF
3. **Left-click** the tray icon to quickly toggle Mac Mode ON/OFF
4. **Right-click** the tray icon for options:
   - **Mac Mode: ON/OFF** -- toggle remapping
   - **Suspend (10 min)** -- temporarily disable, auto-re-enables
   - **Start on Login** -- launch at Windows startup
   - **Edit Profiles...** -- open the built-in profile editor GUI
   - **Restart as Admin** -- relaunch with elevated privileges (needed for remapping inside admin apps like Task Manager)
   - **Exit** -- quit the app

### Panic Key

Press **Ctrl+Alt+Backspace** at any time to immediately disable Mac Mode. A notification confirms the action.

> **Tip:** If the tray icon is hidden, click the `^` overflow arrow in the taskbar to find it, then drag it out to pin it permanently.

---

## Shortcut Reference

Below is every shortcut mapped in the default profiles.

- **Mac Shortcut** -- the shortcut you know from macOS
- **You Press** -- the physical keys you hit on your Windows keyboard (Left Alt = Cmd)
- **Windows Receives** -- what the app actually sends to Windows behind the scenes

### Global (All Apps)

These are the default fallback mappings. They apply to any app that doesn't have its own profile.

#### Essentials

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+C | Alt+C | Ctrl+C | Copy |
| Cmd+V | Alt+V | Ctrl+V | Paste |
| Cmd+X | Alt+X | Ctrl+X | Cut |
| Cmd+A | Alt+A | Ctrl+A | Select all |
| Cmd+Z | Alt+Z | Ctrl+Z | Undo |
| Cmd+Shift+Z | Alt+Shift+Z | Ctrl+Y | Redo |
| Cmd+S | Alt+S | Ctrl+S | Save |
| Cmd+Shift+S | Alt+Shift+S | Ctrl+Shift+S | Save as |
| Cmd+Shift+V | Alt+Shift+V | Win+V | Clipboard history |
| Cmd+Shift+W | Alt+Shift+W | Ctrl+Shift+W | Close all tabs / close window |
| Cmd+Q | Alt+Q | (built-in) | Quit app |
| Cmd+Ctrl+Space | Alt+Ctrl+Space | Win+. | Emoji picker |

#### Text Navigation

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+Left | Alt+Left | Home | Beginning of line |
| Cmd+Right | Alt+Right | End | End of line |
| Cmd+Up | Alt+Up | Ctrl+Home | Beginning of document |
| Cmd+Down | Alt+Down | Ctrl+End | End of document |
| Cmd+Shift+Left | Alt+Shift+Left | Shift+Home | Select to beginning of line |
| Cmd+Shift+Right | Alt+Shift+Right | Shift+End | Select to end of line |
| Cmd+Shift+Up | Alt+Shift+Up | Ctrl+Shift+Home | Select to beginning of document |
| Cmd+Shift+Down | Alt+Shift+Down | Ctrl+Shift+End | Select to end of document |
| Cmd+Backspace | Alt+Backspace | Ctrl+Backspace | Delete word left |

#### Window Management

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+` | Alt+` | (built-in) | Cycle windows of the same app |
| Cmd+M | Alt+M | Win+Down | Minimize window |
| Cmd+H | Alt+H | Win+Down | Hide/minimize window |
| Cmd+Click | Alt+Click | Ctrl+Click | Open link in new tab |

> **Note:** Cmd+`, Cmd+Q, and Cmd+Click are built-in engine features, not simple key remaps. Cmd+` enumerates all visible windows belonging to the current app and switches to the next one. Cmd+Q sends a close message directly to the window (equivalent to clicking the X button). Cmd+Click intercepts the mouse event and translates it into Ctrl+Click so links open in a new tab instead of downloading. All three work just like macOS. Cmd+Q may not work on elevated/admin apps like Task Manager.

#### Common App Shortcuts

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+F | Alt+F | Ctrl+F | Find |
| Cmd+N | Alt+N | Ctrl+N | New |
| Cmd+O | Alt+O | Ctrl+O | Open |
| Cmd+W | Alt+W | Ctrl+W | Close tab/window |
| Cmd+T | Alt+T | Ctrl+T | New tab |
| Cmd+P | Alt+P | Ctrl+P | Print / Quick open |
| Cmd+B | Alt+B | Ctrl+B | Bold |
| Cmd+I | Alt+I | Ctrl+I | Italic |
| Cmd+U | Alt+U | Ctrl+U | Underline |
| Cmd+K | Alt+K | Ctrl+K | Insert link |
| Cmd+R | Alt+R | Ctrl+R | Refresh |
| Cmd+L | Alt+L | Ctrl+L | Address bar / select line |
| Cmd+D | Alt+D | Ctrl+D | Bookmark / add selection |
| Cmd+G | Alt+G | Ctrl+G | Find next |
| Cmd+Shift+G | Alt+Shift+G | Ctrl+Shift+G | Find previous |
| Cmd+Shift+F | Alt+Shift+F | Ctrl+Shift+F | Find in files / Search all |
| Cmd+Shift+P | Alt+Shift+P | Ctrl+Shift+P | Command palette |
| Cmd+Shift+R | Alt+Shift+R | Ctrl+Shift+R | Hard refresh (force reload) |
| Cmd+J | Alt+J | Ctrl+J | Downloads / toggle panel |
| Cmd+, | Alt+, | Ctrl+, | Settings / preferences |
| Cmd+Shift+T | Alt+Shift+T | Ctrl+Shift+T | Reopen closed tab |
| Cmd+Shift+N | Alt+Shift+N | Ctrl+Shift+N | New incognito / new folder |
| Cmd+Shift+[ | Alt+Shift+[ | Ctrl+Shift+Tab | Previous tab |
| Cmd+Shift+] | Alt+Shift+] | Ctrl+Tab | Next tab |
| Cmd+= | Alt+= | Ctrl+= | Zoom in |
| Cmd+- | Alt+- | Ctrl+- | Zoom out |
| Cmd+0 | Alt+0 | Ctrl+0 | Reset zoom |

#### Screenshots

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+Shift+3 | Alt+Shift+3 | Win+PrintScreen | Full screen capture (saves to Pictures/Screenshots) |
| Cmd+Shift+4 | Alt+Shift+4 | Win+Shift+S | Area select (opens Snipping Tool) |
| Cmd+Shift+5 | Alt+Shift+5 | Win+Shift+S | Screenshot toolbar (opens Snipping Tool) |

---

### Chrome / Edge / Brave / Firefox

Process names: `chrome`, `msedge`, `brave`, `firefox`, `opera`

Includes all global shortcuts plus:

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+T | Alt+T | Ctrl+T | New tab |
| Cmd+W | Alt+W | Ctrl+W | Close tab |
| Cmd+Shift+T | Alt+Shift+T | Ctrl+Shift+T | Reopen closed tab |
| Cmd+L | Alt+L | Ctrl+L | Focus address bar |
| Cmd+R | Alt+R | Ctrl+R | Reload page |
| Cmd+Shift+[ | Alt+Shift+[ | Ctrl+Shift+Tab | Previous tab |
| Cmd+Shift+] | Alt+Shift+] | Ctrl+Tab | Next tab |
| Cmd+1 through Cmd+9 | Alt+1 through Alt+9 | Ctrl+1 through Ctrl+9 | Switch to tab N |
| Cmd+N | Alt+N | Ctrl+N | New window |
| Cmd+Shift+N | Alt+Shift+N | Ctrl+Shift+N | New incognito/private window |
| Cmd+D | Alt+D | Ctrl+D | Bookmark page |
| Cmd+G | Alt+G | Ctrl+G | Find next |
| Cmd+Shift+G | Alt+Shift+G | Ctrl+Shift+G | Find previous |
| Cmd+[ | Alt+[ | Alt+Left | Browser back |
| Cmd+] | Alt+] | Alt+Right | Browser forward |

> **Note:** Alt+Left/Right in Chrome maps to Home/End (beginning/end of line) for text editing. Use Alt+[ and Alt+] for browser back/forward navigation.

---

### VS Code / Cursor

Process names: `code`, `cursor`

Includes all global shortcuts plus:

#### Editor

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+P | Alt+P | Ctrl+P | Quick open file |
| Cmd+Shift+P | Alt+Shift+P | Ctrl+Shift+P | Command palette |
| Cmd+G | Alt+G | Ctrl+G | Go to line |
| Cmd+D | Alt+D | Ctrl+D | Add selection to next find match |
| Cmd+L | Alt+L | Ctrl+L | Select line |
| Cmd+/ | Alt+/ | Ctrl+/ | Toggle comment |
| Cmd+H | Alt+H | Ctrl+H | Find and replace |
| Cmd+Shift+F | Alt+Shift+F | Ctrl+Shift+F | Search across files |
| Cmd+Shift+K | Alt+Shift+K | Ctrl+Shift+K | Delete line |
| Cmd+, | Alt+, | Ctrl+, | Open settings |
| Cmd+` | Alt+` | Ctrl+` | Toggle integrated terminal |
| Cmd+J | Alt+J | Ctrl+J | Toggle bottom panel |
| Cmd+= / Cmd+- | Alt+= / Alt+- | Ctrl+= / Ctrl+- | Zoom in / out |
| Cmd+Enter | Alt+Enter | Ctrl+Enter | Accept suggestion |

#### Sidebar Panels

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+B | Alt+B | Ctrl+B | Toggle sidebar |
| Cmd+Shift+E | Alt+Shift+E | Ctrl+Shift+E | Explorer panel |
| Cmd+Shift+G | Alt+Shift+G | Ctrl+Shift+G | Source control panel |
| Cmd+Shift+X | Alt+Shift+X | Ctrl+Shift+X | Extensions panel |
| Cmd+Shift+D | Alt+Shift+D | Ctrl+Shift+D | Debug panel |

#### Cursor AI (Cursor-specific)

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+K | Alt+K | Ctrl+K | Inline AI edit |
| Cmd+L | Alt+L | Ctrl+L | Open AI chat panel |
| Cmd+I | Alt+I | Ctrl+I | AI Agent / Composer |
| Cmd+Shift+I | Alt+Shift+I | Ctrl+Shift+I | Fullscreen Composer |

#### Tab Navigation

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+Shift+[ | Alt+Shift+[ | Ctrl+Shift+Tab | Previous editor tab |
| Cmd+Shift+] | Alt+Shift+] | Ctrl+Tab | Next editor tab |
| Cmd+1 / 2 / 3 | Alt+1 / 2 / 3 | Ctrl+1 / 2 / 3 | Focus editor group |

---

### Warp Terminal

Process names: `warp`

A dedicated profile for the [Warp](https://www.warp.dev/) AI terminal. Warp on Windows uses **Ctrl+Shift** for most common operations (copy, paste, find, new tab) because Ctrl+C must remain "interrupt/cancel" in terminal contexts. This profile handles that translation automatically so you just press the same keys you would on a Mac and it works.

#### Fundamentals

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+C | Alt+C | Ctrl+Shift+C | Copy |
| Cmd+V | Alt+V | Ctrl+Shift+V | Paste |
| Cmd+F | Alt+F | Ctrl+Shift+F | Find |
| Cmd+A | Alt+A | Ctrl+A | Select all |
| Cmd+Z | Alt+Z | Ctrl+Z | Undo |
| Cmd+Shift+Z | Alt+Shift+Z | Ctrl+Shift+Z | Redo |
| Cmd+W | Alt+W | Ctrl+Shift+W | Close tab/pane |
| Cmd+Q | Alt+Q | Alt+F4 | Quit |
| Cmd+Shift+C | Alt+Shift+C | Ctrl+Shift+C | Copy command |
| Cmd+Shift+S | Alt+Shift+S | Ctrl+Shift+S | Share block |

#### Tabs & Panes

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+T | Alt+T | Ctrl+Shift+T | New tab |
| Cmd+Shift+T | Alt+Shift+T | Ctrl+Alt+T | Reopen closed tab |
| Cmd+1 through Cmd+9 | Alt+1 through Alt+9 | Ctrl+1 through Ctrl+9 | Switch to tab N |
| Cmd+Shift+[ | Alt+Shift+[ | Ctrl+PageUp | Previous tab |
| Cmd+Shift+] | Alt+Shift+] | Ctrl+PageDown | Next tab |
| Cmd+D | Alt+D | Ctrl+Shift+D | Split pane right |
| Cmd+Shift+D | Alt+Shift+D | Ctrl+Shift+E | Split pane down |
| Cmd+[ | Alt+[ | Ctrl+Shift+[ | Previous pane |
| Cmd+] | Alt+] | Ctrl+Shift+] | Next pane |

#### Warp Features

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+P | Alt+P | Ctrl+Shift+P | Command palette |
| Cmd+L | Alt+L | Ctrl+Shift+L | Focus terminal input |
| Cmd+K | Alt+K | Ctrl+Shift+K | Clear blocks |
| Cmd+B | Alt+B | Ctrl+Shift+B | Bookmark block |
| Cmd+I | Alt+I | Ctrl+Shift+I | Reinput selected commands |
| Cmd+, | Alt+, | Ctrl+, | Open settings |
| Cmd+= / Cmd+- / Cmd+0 | Alt+= / Alt+- / Alt+0 | Ctrl+= / Ctrl+- / Ctrl+0 | Font size controls |

#### Text Navigation

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+Left | Alt+Left | Home | Beginning of line |
| Cmd+Right | Alt+Right | End | End of line |
| Cmd+Shift+Left | Alt+Shift+Left | Shift+Home | Select to beginning of line |
| Cmd+Shift+Right | Alt+Shift+Right | Shift+End | Select to end of line |
| Cmd+Backspace | Alt+Backspace | Ctrl+Backspace | Delete word left |

---

### Windows Terminal / PowerShell

Process names: `windowsterminal`, `powershell`, `cmd`, `wt`, `pwsh`

A conservative profile. **Cmd+C is intentionally NOT mapped** in terminals because Ctrl+C means "interrupt/cancel" in terminal contexts, not "copy." Use Cmd+Shift+C to copy instead.

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+Shift+C | Alt+Shift+C | Ctrl+Shift+C | Copy (terminal-safe) |
| Cmd+Shift+V | Alt+Shift+V | Ctrl+Shift+V | Paste (terminal-safe) |
| Cmd+A | Alt+A | Ctrl+A | Select all |
| Cmd+Z | Alt+Z | Ctrl+Z | Undo (in input) |
| Cmd+F | Alt+F | Ctrl+F | Find |
| Cmd+Q | Alt+Q | Alt+F4 | Quit |
| Cmd+Left | Alt+Left | Home | Beginning of line |
| Cmd+Right | Alt+Right | End | End of line |
| Cmd+Backspace | Alt+Backspace | Ctrl+Backspace | Delete word left |

---

### Windows Explorer

Process names: `explorer`

| Mac Shortcut | You Press | Windows Receives | Description |
|---|---|---|---|
| Cmd+C | Alt+C | Ctrl+C | Copy |
| Cmd+V | Alt+V | Ctrl+V | Paste |
| Cmd+X | Alt+X | Ctrl+X | Cut |
| Cmd+Z | Alt+Z | Ctrl+Z | Undo |
| Cmd+Shift+Z | Alt+Shift+Z | Ctrl+Y | Redo |
| Cmd+A | Alt+A | Ctrl+A | Select all |
| Cmd+S | Alt+S | Ctrl+S | Save |
| Cmd+N | Alt+N | Ctrl+N | New window |
| Cmd+Shift+N | Alt+Shift+N | Ctrl+Shift+N | New folder |
| Cmd+F | Alt+F | Ctrl+F | Search |
| Cmd+W | Alt+W | Ctrl+W | Close window |
| Cmd+Q | Alt+Q | Alt+F4 | Quit |
| Cmd+Backspace | Alt+Backspace | Delete | Move to Recycle Bin |

> **Note:** Alt+Left/Right are intentionally NOT remapped in Explorer, since their native behavior (back/forward navigation) is useful.

---

## Adding Custom Shortcuts

### Using the Profile Editor

The easiest way to customize shortcuts is through the built-in profile editor. Right-click the tray icon and select **Edit Profiles...** to open it.

The editor lets you:

- Browse and select any profile from the list on the left
- Edit the profile name and which process names it applies to
- Add, remove, or modify shortcut mappings in the grid
- **Record hotkeys** by clicking the "Record" button next to any trigger or action field, then pressing the key combination you want. Press Escape to cancel recording.
- Save changes (the app picks them up automatically)
- Reset any built-in profile back to its default state
- Create new custom profiles or delete ones you no longer need

### Editing JSON Manually

You can also edit the profile JSON files directly in `profiles/` (e.g., `profiles/chrome.json`). Add a new entry to the `"mappings"` array:

```json
{ "trigger": "Alt+E", "action": "Ctrl+E" }
```

Save the file. **Changes are picked up automatically**, no restart needed. The app watches for file changes. If there's a syntax error in your JSON, a tray notification will pop up telling you what went wrong.

### Shortcut String Format

Shortcuts are written as modifier keys joined with `+`, ending with the main key:

```
[Alt+][Shift+][Ctrl+]<Key>
```

**Triggers** (the `"trigger"` field) always start with `Alt+` because Left Alt is your Cmd key. **Actions** (the `"action"` field) are what the app sends to Windows behind the scenes.

#### Available Modifier Names

| Name | In Triggers | In Actions |
|---|---|---|
| `Alt` | Left Alt (your "Cmd" key) | Keeps physical Alt held |
| `Shift` | Shift | Shift |
| `Ctrl` | Control | Control |
| `Win` | (not used in triggers) | Windows key |

#### Available Key Names

| Name | Key | Name | Key |
|---|---|---|---|
| `A`-`Z` | Letter keys | `Left` | Left arrow |
| `0`-`9` | Number keys | `Right` | Right arrow |
| `F1`-`F24` | Function keys | `Up` | Up arrow |
| `Tab` | Tab | `Down` | Down arrow |
| `Space` | Spacebar | `Home` | Home |
| `Backspace` | Backspace | `End` | End |
| `Enter` | Enter | `Delete` | Delete |
| `Escape` | Escape | `PageUp` | Page Up |
| `[` | Open bracket | `PageDown` | Page Down |
| `]` | Close bracket | `/` | Forward slash |
| `-` | Minus | `=` | Equals |
| `,` | Comma | `.` | Period |
| `;` | Semicolon | `'` | Quote |
| `` ` `` | Backtick | `\` | Backslash |
| `PrintScreen` | Print Screen | | |

### Examples

```json
// Alt+E sends Ctrl+E to Windows
{ "trigger": "Alt+E", "action": "Ctrl+E" }

// Alt+Shift+P sends Ctrl+Shift+P to Windows
{ "trigger": "Alt+Shift+P", "action": "Ctrl+Shift+P" }

// Alt+Left sends Home to Windows (beginning of line)
{ "trigger": "Alt+Left", "action": "Home" }

// Alt+Q sends Alt+F4 to Windows (quit app)
{ "trigger": "Alt+Q", "action": "Alt+F4" }

// Alt+Shift+4 sends Win+Shift+S to Windows (screenshot)
{ "trigger": "Alt+Shift+4", "action": "Win+Shift+S" }
```

---

## Creating a New App Profile

1. Create a new JSON file in `profiles/`, e.g., `profiles/slack.json`
2. Use this template:

```json
{
  "name": "Slack",
  "processNames": ["slack"],
  "mappings": [
    { "trigger": "Alt+K", "action": "Ctrl+K" },
    { "trigger": "Alt+Shift+A", "action": "Ctrl+Shift+A" }
  ]
}
```

3. Save the file. The app picks it up automatically.

### Finding the Process Name

To find the process name of any app:

1. Open the app
2. Open Task Manager (Ctrl+Shift+Esc)
3. Find the app in the **Processes** tab
4. Right-click and select **Go to details**
5. The **Name** column shows the `.exe` name. Use the name without `.exe`, lowercase

Or run in PowerShell:

```powershell
Get-Process | Where-Object { $_.MainWindowTitle -ne "" } | Select-Object ProcessName, MainWindowTitle
```

### Profile Priority

- If a process matches a specific profile, that profile's mappings are checked first.
- If the specific shortcut isn't found in the app profile, it falls back to `default.json`.
- If no profile matches the process at all, `default.json` is used entirely.
- This means global shortcuts (like Cmd+C, Cmd+Q, text navigation) work everywhere without being duplicated in every profile.

---

## Architecture

```
MacMode.App (WPF Tray App)
  TrayIcon.cs              System tray, context menu, admin restart, notifications
  ProfileEditorWindow.xaml WPF profile editor with hotkey recording
  DefaultProfiles.cs       Embedded default profiles for reset-to-default
  AppBootstrapper.cs       Composition root, dependency wiring
  App.xaml.cs              Entry point, single-instance mutex

MacMode.Core (Class Library)
  Hook/
    KeyboardHook.cs        WH_KEYBOARD_LL install/uninstall
    MouseHook.cs           WH_MOUSE_LL for Alt+Click to Ctrl+Click (async injection)
    NativeMethods.cs       Win32 P/Invoke (SendInput, SetWindowsHookEx, etc.)
    KeyboardHookEventArgs  Left Alt vs Right Alt detection
  Engine/
    MappingEngine.cs       State machine: Idle -> AltPending -> ChordActive
    KeySender.cs           SendInput wrapper (atomic batches)
    ModifierState.cs       Tracks physical Shift/Ctrl/Alt state
    WindowCycler.cs        Cmd+` same-app window cycling via EnumWindows
    SpecialActionRegistry  Declarative special actions (close-window, cycle-windows)
  Profiles/
    ProfileManager.cs      Loads JSON profiles, O(1) lookup, FileSystemWatcher, error events
    KeyParser.cs           Parses "Alt+Shift+T" -> VK codes
    Profile.cs / KeyMapping.cs   Data models
  ProcessDetection/
    ForegroundProcessDetector.cs   Cached GetForegroundWindow + process name
  Settings/
    SettingsManager.cs     JSON persistence
    StartupManager.cs      Registry Run key for auto-start
  Logging/
    Logger.cs              Rolling daily file logger
```

### State Machine

The core engine uses a three-state machine:

```
                                  Alt+Tab, Alt+F4, Alt+Space
                                  pass through naturally
                                         |
[IDLE] --Left Alt down--> [ALT PENDING] -+
  ^                            |         |
  |                            |         +-- Unmapped key: pass through, -> IDLE
  |                            |
  |                   Mapped chord key detected
  |                   Suppress key, inject correct Windows shortcut
  |                            |
  |                            v
  +--Left Alt released-- [CHORD ACTIVE]
     (suppress Alt up)         |
                               +-- Another mapped key: fire new mapping
                               +-- Unmapped key: suppress
```

**Key design decision:** Left Alt is NOT suppressed when first pressed. It passes through to the system naturally. This means Alt+Tab, Alt+F4, and Alt+Space work without any special handling. When a chord key is detected, the engine cancels the Alt (sends Alt-up) and injects the mapped shortcut in a single atomic `SendInput` batch.

---

## Settings

Settings are stored in `settings.json` in the app directory:

```json
{
  "macModeEnabled": true,
  "startOnLogin": false,
  "debugLogging": false
}
```

| Setting | Default | Description |
|---|---|---|
| `macModeEnabled` | `true` | Whether Mac Mode is active |
| `startOnLogin` | `false` | Auto-start with Windows (uses Registry Run key) |
| `debugLogging` | `false` | Enable verbose logging of every key event |

---

## Logs

Logs are written to:

```
%LocalAppData%\MacMode\logs\macmode-YYYY-MM-DD.log
```

Set `"debugLogging": true` in `settings.json` to see detailed key event traces for troubleshooting.

---

## Limitations

| Limitation | Detail |
|---|---|
| **Elevated apps** | Cannot remap inside admin/elevated processes (Task Manager, PowerToys running as admin, etc.) unless MacMode is also run as admin. Use "Restart as Admin" from the tray menu to fix this. Cmd+Q also cannot close elevated windows |
| **AltGr / International layouts** | Right Alt (AltGr) is explicitly excluded via the extended-key flag. No interference with international characters |
| **Games** | Some fullscreen DirectX/Vulkan games may not respond to SendInput |
| **Hook timeout** | Windows enforces a ~300ms timeout on low-level hook callbacks. Extreme system load could cause the OS to silently remove the hook. MacMode includes a health monitor that automatically detects and reinstalls the hook if this happens |
| **Secure desktop** | The hook does not operate on the UAC secure desktop. This is by design |
| **Menu bar flash** | Since Alt passes through before being cancelled, a very brief menu bar highlight may occasionally appear when using chord shortcuts. This is usually imperceptible |
| **Other Alt hotkey apps** | Apps that register their own global Alt+key hotkeys (e.g., GPU overlays, screenshot tools, clipboard managers) may conflict with MacMode since both hooks race to handle the same keystroke. If you experience inconsistent behavior, check the other app's settings and remap its hotkeys to avoid Alt+letter combos |

---

## Building from Source

### Prerequisites

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build

```powershell
cd MacMode
dotnet build
```

### Run (Development)

```powershell
dotnet run --project src/MacMode.App
```

### Publish Self-Contained Exe

```powershell
.\publish.ps1
```

This produces a single-file self-contained executable in `./dist/` that includes the .NET runtime. No .NET installation required on the target machine.

### Project Structure

```
MacMode/
  MacMode.sln
  src/
    MacMode.App/     WPF tray application
    MacMode.Core/    Core library (hook, engine, profiles)
  profiles/                  Default shortcut profiles (JSON)
  settings.json              App settings
  publish.ps1                Build script
  README.md
```

---

## License

MIT

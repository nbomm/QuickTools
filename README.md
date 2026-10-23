# QuickTools

[![Build](https://github.com/nbomm/QuickTools/actions/workflows/build.yml/badge.svg)](https://github.com/nbomm/QuickTools/actions/workflows/build.yml)

A small Windows tray launcher. Click the tray icon to get a menu of your own shortcuts: programs, folders, documents, URLs, scripts, `shell:` locations. The left-click and right-click menus can be different, or one can mirror the other.

Everything is configured in a built-in editor or a plain JSON file. QuickTools has no installer, no background service and no network access of its own.

```
          ┌──────────────────────┐
          │ 📝 Notepad           │
          │ 📁 Dev Tools       ▶ │
          │ 🌐 Microsoft Edge    │
          │ ──────────────────── │
          │ ⚙ Settings…          │
          │   Exit               │
          └──────────────────────┘◄── the menu's corner sits on the tray icon
                                 [⚡]
```

## Features

- **Separate left-click and right-click menus**, or a single menu that both clicks show (*mirroring*).
- **Folders and submenus** nested as deeply as you like, plus separators.
- **Folder shortcuts are live submenus.** A shortcut that points at a folder lists that folder's contents, with *Open folder* first.
- **Launches anything Windows can open**: `.exe`, `.bat`/`.cmd`/`.ps1`, documents, folders, `.lnk` shortcuts, `http(s)://` links, `shell:` and `ms-settings:` URIs. Targets and arguments can use `%ENVIRONMENT%` variables.
- **Pick from your installed apps**: the same list as Start's *All apps*, including Store apps such as Calculator and Windows Terminal, with a search box.
- **Run as administrator** for any shortcut. Those items show a UAC shield on their icon.
- **The menu always fits on screen.** It's measured before it opens and placed against the tray icon for any taskbar edge, multiple monitors (including negative coordinates), the overflow (^) area, and any display scaling (per-monitor DPI aware).
- **Keyboard support:** Win+B to focus the tray, `&` keyboard keys in menu names, and keyboard shortcuts in the editor.
- **Global hotkey** (Ctrl+Alt+Q by default, configurable or off) that opens the left-click menu right at the mouse pointer, from any program.
- **Start with Windows** (per user, no admin rights needed).
- **Pin to QuickTools** in File Explorer's right-click menu for files and folders (optional, per user, no admin rights needed).
- **Settings that are hard to lose:** saving writes a temporary file and swaps it in, so a crash mid-save can't corrupt them. An invalid file is kept as a backup rather than discarded.

## Requirements

- Windows 10 or 11, x64.
- The [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0), unless you use the self-contained build. Building from source needs the .NET 10 SDK.

## Install

Download an exe from the [Releases](https://github.com/nbomm/QuickTools/releases) page:

- `QuickTools-<version>-win-x64.exe` runs on any Windows 10/11 x64 PC with nothing else installed.
- `QuickTools-<version>-win-x64-framework-dependent.exe` is much smaller but needs the .NET 10 Desktop Runtime.

Each exe has a matching `.sha256` file containing its SHA-256 hash. To check a download, compare the two hashes in PowerShell:

```powershell
(Get-FileHash .\QuickTools-<version>-win-x64.exe -Algorithm SHA256).Hash
Get-Content .\QuickTools-<version>-win-x64.exe.sha256     # the first word must match (case doesn't matter)
```

Or, with both files in the same folder, run `sha256sum -c QuickTools-<version>-win-x64.exe.sha256` in Git Bash, WSL or Linux.

Rename it to `QuickTools.exe` if you like, put it anywhere (for example `%LOCALAPPDATA%\Programs\QuickTools\`), and run it. To start it automatically, turn on **Start QuickTools when I sign in to Windows** in Settings.

## Build from source

```powershell
git clone https://github.com/nbomm/QuickTools.git
cd QuickTools
dotnet build
dotnet test
dotnet run --project src/QuickTools
```

To produce a single `QuickTools.exe`:

| Build | Command | Size | Needs .NET runtime |
|---|---|---|---|
| Framework-dependent | `dotnet publish src/QuickTools -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true` | ~0.2 MB | Yes |
| Self-contained | `dotnet publish src/QuickTools -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true` | ~49 MB | No |

Add `-o <folder>` to choose the output folder. For ARM64 devices, use `-r win-arm64` (not tested).

### Continuous integration and releases

[`.github/workflows/build.yml`](.github/workflows/build.yml) runs on a Windows runner:

- **Every push to `main`:** builds, runs the tests, publishes both exes, and creates a [GitHub Release](https://github.com/nbomm/QuickTools/releases) with both attached, a `.sha256` checksum file for each, and release notes generated from the commits.
- **Pull requests:** build and test only. Their exes can be downloaded from the run's **Artifacts** section on the [Actions](https://github.com/nbomm/QuickTools/actions) page.

Release versions are `Major.Minor.<workflow run number>`, for example `v1.1.42`. The number is stamped into the exe. Major and Minor come from `<VersionPrefix>` in `src/QuickTools/QuickTools.csproj`; change it (for example to `1.2.0` or `2.0.0`) to start a new series. Because pull-request builds use run numbers too, release numbers can skip.

## Command-line options

```
QuickTools.exe [option]

  -h, --help, /?   Show help (including the settings file path) and exit.
  -v, --version    Show the version, e.g. "QuickTools 1.1.5 (995c07a)", and exit.
```

- With no option, QuickTools starts in the tray. Options are case-insensitive.
- They also work while QuickTools is already running.
- All output, including help and version, is written to **stderr**; nothing is written to stdout.
- Exit codes: `0` for success, `2` for an unknown option or too many arguments (the error is printed with the help).

QuickTools is a Windows (GUI) app, so a terminal doesn't wait for it to finish: its output appears after the next prompt. To get it cleanly, redirect stderr:

```powershell
QuickTools.exe --version 2>&1 | Write-Output   # PowerShell
QuickTools.exe --version 2> version.txt        # cmd or PowerShell
```

When there's no terminal at all (for example a desktop shortcut with `--help`), the text is shown in a message box instead.

## Using the menus

| Action | Result |
|---|---|
| Left-click the tray icon | Opens the **left-click menu**. |
| Right-click the tray icon | Opens the **right-click menu** (the same menu when mirroring is on). |
| Win+B, arrow keys to QuickTools, then Enter | Opens the left-click menu from the keyboard. |
| Win+B, arrow keys to QuickTools, then Shift+F10 or the Menu key | Opens the right-click menu from the keyboard. |
| Your hotkey, if you set one | Opens the **left-click menu** at the mouse pointer (see below). |
| Double-click | Not used. |

Every menu ends with two fixed items, **Settings…** and **Exit**. They aren't stored in your settings, so they can't be removed by mistake, whether in the editor or by editing the JSON.

If a launch fails (for example the file is missing), a Windows notification shows the reason. Declining a UAC prompt is not treated as an error.

## Hotkey

Set one in Settings under *Hotkey to open the menu at the mouse pointer*: click the box and press the keys you want (for example Ctrl+Alt+Q). Backspace or **Clear** removes it. The default is **Ctrl+Alt+Q**, including for an existing settings file that doesn't mention a hotkey yet. **An empty hotkey turns the feature off.**

- It needs at least one of Ctrl, Alt or Shift, except for the function keys (F1–F24), which can be used on their own. The Windows key isn't supported.
- The menu opens with its top-left corner on the mouse pointer, flipping to the left and/or above the pointer where it would run off the screen of the monitor the pointer is on.
- It opens the left-click menu. Pressing the hotkey while a menu is already open does nothing.
- If Windows or another program already uses the combination, QuickTools can't take it. It tells you when you save the setting or at startup, and the hotkey stays off until you choose another.

## Folder shortcuts

A shortcut whose target is a folder on a local drive (for example `C:\Users\you\Documents`) becomes a submenu that lists what is in the folder:

- The first item is always **Open folder**, which opens the folder in Explorer. Clicking the submenu item itself only opens the submenu.
- Below it are the subfolders (each its own submenu, with its own *Open folder*), then the files, in Explorer's order (numbers sort as numbers, so `file2` comes before `file10`). Clicking a file opens it with its default program. Hidden and system items are left out, and `.lnk` and `.url` names are shown without their extension.
- The list is read each time the submenu opens, so it is always current. It shows up to 200 items; if there are more, the last item (*N more…*) opens the folder.
- Folders you can't read show *(can't read this folder)*, and empty ones show *(empty)*.
- This applies to every shortcut with a folder target, including ones from drag and drop, *Pin to QuickTools* and *Folder…* in the editor, and to shortcuts saved before this feature existed.

To get a plain item that just opens a folder, set the **Target** to `explorer.exe` and the **Arguments** to the folder's path.

Not turned into submenus: network paths (`\\server\share`, mapped drives), which are never probed because an unreachable server would freeze the menu, and `shell:` locations such as `shell:Downloads`. Those keep opening the folder as before.

## Settings window

Open it with **Settings…** from either menu. It also opens by itself the first time QuickTools runs (when there is no `settings.json` yet), so a new user sees where to start. There is one tab per menu. When *Right-click menu mirrors the left-click menu* is checked, the right-click tab is disabled, but its contents are kept, so turning mirroring off brings them back. **Copy left → right** replaces the right-click menu with a copy of the left one.

| Button / key | What it does |
|---|---|
| Add Shortcut… / Add Folder… / Add Separator | Adds an item after the selected one. If a folder is selected, the item goes inside it. |
| Add App… | Opens the list of installed apps (Start's *All apps*). Type to search, select one or more (Ctrl+click), and click **Add** or press Enter. With nothing selected, Enter adds the top match. Each app is added as a shortcut, in the same place as Add Shortcut…. |
| Edit… / Enter / double-click | Edits the selected shortcut or folder. |
| Remove / Delete | Removes the selection. Removing a folder that has items asks first. |
| Move Up / Move Down / Alt+↑ / Alt+↓ | Reorders the selection within its folder. |
| Drag and drop | Drop onto a folder to move the item into it; drop onto an item to place it before or after; drop on empty space to move it to the end of the top level. |
| Drop files from Explorer | Adds each file or folder as a shortcut, named after the file. |
| Test Launch | Runs the selected shortcut exactly as the menu would. |

Changes are saved and take effect only when you click **OK**. **Cancel** or Esc discards them.

### Shortcut fields

| Field | Notes |
|---|---|
| **Name** | The menu text. Put `&` before a letter to make it the item's keyboard key (`&Notepad` → press N); use `&&` for a literal `&`. |
| **Target** | What to open (a folder becomes a submenu, see [Folder shortcuts](#folder-shortcuts)): a full path, a bare program name (`notepad.exe`, `wt`, `code`, found through `PATH` and the *App Paths* registry key, as in the Run dialog), a folder, a URL, `shell:Downloads`, `ms-settings:display`, and so on. `%ENV%` variables are expanded. **App…** picks an installed app and sets the target to `shell:AppsFolder\<app ID>`, which opens it the way Start does and keeps working when the app updates. |
| **Arguments** | Passed to the target as-is. `%ENV%` variables are expanded. |
| **Start in** | The working directory. When empty, it's the target program's own folder, as with Explorer shortcuts. |
| **Icon** | Optional `.ico` file, or an `.exe` whose icon to use. When empty, the target's own icon is used. |
| **Run as administrator** | Launches with the `runas` verb, which shows a UAC prompt. The menu icon gets a shield badge. |

## Settings file

Everything lives in **`%APPDATA%\QuickTools\settings.json`**. You can edit it by hand while QuickTools is closed. QuickTools reads the file only at startup, and saving from the Settings window overwrites it.

```jsonc
{
  "schemaVersion": 1,
  "mirrorMenus": false,              // true: right-click shows leftMenu
  "hotkey": "Ctrl+Alt+Q",            // opens leftMenu at the mouse pointer (the default); "" turns it off
  "leftMenu": [
    { "kind": "Shortcut", "name": "&Notepad", "target": "notepad.exe" },
    {
      "kind": "Folder",
      "name": "Dev Tools",
      "children": [
        {
          "kind": "Shortcut",
          "name": "Terminal (admin)",
          "target": "wt.exe",
          "arguments": "-d %USERPROFILE%\\source",
          "runAsAdmin": true          // omitted when false
        },
        {
          "kind": "Shortcut",
          "name": "Repo",
          "target": "%USERPROFILE%\\source\\repo",
          "iconPath": "C:\\Icons\\repo.ico"
        }
      ]
    },
    { "kind": "Separator" },
    { "kind": "Shortcut", "name": "Display settings", "target": "ms-settings:display" }
  ],
  "rightMenu": [
    { "kind": "Shortcut", "name": "Downloads", "target": "shell:Downloads", "workingDirectory": "C:\\" }
  ]
}
```

(The comments above are only for explanation. The real file is plain JSON, and comments in it make it invalid.)

- `kind` is `Shortcut`, `Folder` or `Separator`. Properties a kind doesn't use are ignored.
- Saving writes `settings.json.tmp` and then swaps it in, so the old file stays intact if the write fails.
- If the file isn't valid JSON, it's renamed to **`settings.json.bad`**, defaults are loaded, and a notification tells you. Fix the `.bad` file and rename it back.
- If the file can't be read at all (for example another program has it locked), QuickTools uses default menus for that session **without touching the file**.

## How menu placement works

1. QuickTools asks the shell for the tray icon's exact rectangle (`Shell_NotifyIconGetRect`). If that isn't available, it falls back to the anchor point Windows sent with the click, then to the cursor position.
2. It measures the menu before showing it.
3. It places the menu so its **bottom-right corner touches the icon's top-left corner**.
4. If that would go past the left or top edge of the work area (for a taskbar on the left or top), it flips to the other side of the icon.
5. Finally it clamps the menu to the work area (the screen minus the taskbar) of the icon's monitor. The menu never covers the taskbar. A menu taller than the screen gets scroll arrows.

The hotkey path instead puts the menu's top-left corner on the mouse pointer, flips it left and/or up where it would overflow, and clamps it to the pointer's monitor.

This logic is in `src/QuickTools/Menus/MenuPlacement.cs`, two functions with no Windows calls that are unit-tested for every taskbar edge.

## Pin to QuickTools

Turn on *Add "Pin to QuickTools" to File Explorer's right-click menu* in Settings and click OK. Right-click any file or folder and choose **Pin to QuickTools** (on Windows 11, under *Show more options*). QuickTools opens its shortcut editor filled in with that item. Click OK to add it to the end of your left-click menu (and the right-click menu too, when mirroring is off).

- It is a plain shell verb in `HKEY_CURRENT_USER\Software\Classes\*\shell\QuickTools.Pin` and `...\Directory\shell\QuickTools.Pin`, running `QuickTools.exe --pin "<path>"`. It isn't a COM shell extension, so nothing is loaded into Explorer.
- If QuickTools isn't running, choosing the item starts it and then offers the pin.
- Close the Settings window before pinning; it holds its own copy of the menus and would overwrite the pin.
- If you move the exe, the option shows as off. Turn it on again and click OK to register the new location.

## Start with Windows and uninstalling

*Start QuickTools when I sign in to Windows* writes the value `QuickTools` under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`. The value is the quoted path to `QuickTools.exe`. If you move the exe, the option shows as off. Turn it on again in Settings and click OK to register the new location.

To uninstall:
1. Turn off *Start with Windows* and the *Pin to QuickTools* item, or delete those registry entries.
2. Exit QuickTools.
3. Delete the exe and `%APPDATA%\QuickTools`.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| A dead QuickTools icon stays in the tray | The process was killed (Task Manager, debugger), so it couldn't remove its icon. Hovering over the icon makes Windows clear it. Use **Exit** to close QuickTools cleanly. |
| A warning that QuickTools is running as administrator | QuickTools itself was started elevated, so **everything it launches is elevated too, without a prompt**, and Windows blocks dragging files in from Explorer. Start it normally and use *Run as administrator* on the items that need it. |
| Items on network shares show a generic icon | This is intentional. QuickTools never contacts a network path (`\\server\...` or a mapped drive) just to draw an icon, because an unreachable server would freeze the menu, and contacting it sends your Windows credentials to that server. The path is only accessed when you click the item. |
| Starting QuickTools a second time does nothing | Only one instance runs per user session. Use the existing tray icon. |
| The icon disappeared after Explorer crashed | QuickTools re-adds its icon automatically when Explorer restarts. If it doesn't, exit QuickTools and start it again. |

## Project layout

```
src/QuickTools/
  Program.cs              Entry point, single-instance check
  AppIcon.cs              Draws the app icon (tray icon and the source of QuickTools.ico)
  QuickTools.ico          The exe's icon, generated from AppIcon.cs
  TrayAppContext.cs       Connects the tray icon, menus, launching and settings
  Tray/TrayIcon.cs        Shell_NotifyIcon wrapper (NOTIFYICON_VERSION_4, TaskbarCreated, icon rect)
  Menus/MenuPlacement.cs  Placement math (pure, unit-tested)
  Menus/MenuBuilder.cs    Settings → ContextMenuStrip; adds Settings… and Exit
  Model/                  AppSettings, MenuEntry (the JSON model)
  Services/               SettingsStore (JSON load/save), Launcher, PathResolver, IconCache, StartupManager,
                          InstalledApps (Start's All apps list)
  UI/                     SettingsForm, MenuEditor (tree editor), EntryEditorForm, AppPickerForm
tests/QuickTools.Tests/   xUnit tests: placement, settings load/save, path resolution, app search
tools/Generate-AppIcon.ps1  Regenerates QuickTools.ico from AppIcon.cs
```

Contributions are welcome. Run `dotnet build` (it should report no warnings) and `dotnet test` before opening a pull request.

The icon is drawn in code in `AppIcon.cs`, which is used for both the tray icon and the exe's icon. If you change the design, run `pwsh tools/Generate-AppIcon.ps1` to regenerate `src/QuickTools/QuickTools.ico`, and commit it along with the code.

## License

[MIT](LICENSE)

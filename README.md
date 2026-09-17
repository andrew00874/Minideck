# MiniDeck

A circular Windows launcher for a three-key macro pad with a pressable rotary dial.

[한국어](README.ko.md) · **English**

![MiniDeck dial](docs/images/dial-en.png)

Turn to choose an app. Press to open it. Drag the dial anywhere and it remembers its position.

## Download and run

Download the [English ZIP](https://github.com/andrew00874/Minideck/releases/download/v0.1.0-beta.1/MiniDeck-0.1.0-beta.1-win-en.zip) or [Korean ZIP](https://github.com/andrew00874/Minideck/releases/download/v0.1.0-beta.1/MiniDeck-0.1.0-beta.1-win-ko.zip) from the [beta release](https://github.com/andrew00874/Minideck/releases/tag/v0.1.0-beta.1). Extract the entire ZIP and run `MiniDeck.exe`. Keep the DLL files and `fonts` folder beside the executable.

Requires Windows 10/11 and .NET Framework 4.8 or newer. No administrator access is required for normal use. The build is unsigned.

The two language editions use the same settings at `%APPDATA%\MiniDeck\settings.json`. Run one edition at a time. Your custom app names are preserved when changing editions.

## Features

- Circular selection with animated icons and smooth translucent edges.
- Drag to reposition; resize the dial from 280 to 560 px.
- Custom PNG/SVG icons, copied into local storage.
- Wrapped app names without ellipses; scroll exceptionally long names.
- Three direct app shortcuts plus left, right and press dial controls.
- Tray operation and optional startup with Windows.
- Onboard mapping editor for the supported device protocol.
- Optional Chrome extension to switch to an already-open website tab.

![English settings](docs/images/settings-en.png)

## First use

1. Add an executable, shortcut, folder or website URL in Settings. Optional arguments have their own field.
2. Assign apps to Key 1, 2 and 3.
3. Use **Turn** and **Press / open** to test the dial without a keyboard.
4. Configure the following outputs on your macro pad:

| Control | Output |
| --- | --- |
| Key 1 / 2 / 3 | Ctrl + Alt + Shift + F1 / F2 / F3 |
| Dial left / right | Ctrl + Alt + Shift + F4 / F5 |
| Dial press | Ctrl + Alt + Shift + F6 |
| Dismiss dial | Esc, or wait about 4.5 seconds |

The first press opens the dial; a press while it is open launches the selected app. Click an icon or the center to launch with your mouse. Dragging moves the overlay without launching. More than eight apps are shown in groups as you turn.

Closing Settings hides it in the system tray. Use **Quit** in the tray menu to exit. If you move the installation after enabling startup, toggle **Start with Windows** off and on to update its path.

The sample Codex entry uses its Windows package app ID and only works where that package is installed. Replace it if needed. The internal `codex.exe` CLI is not the desktop launcher.

## Existing website tabs — Chrome Browser Link (beta)

Follow [the extension setup guide](browser-extension/README.md) once. MiniDeck then activates the most recently used normal tab with the same origin (scheme, hostname and port). It preserves that tab's current page, chart, form and scroll position. A new tab is created only when there is no match.

Use one Chrome profile. Other profiles, browsers and incognito tabs are not searched. Different subdomains are treated as different sites. If the extension is disconnected, MiniDeck shows a connection message instead of silently opening another tab.

This optional integration uses tab metadata and a local native-messaging connection. It does not read page contents, cookies or passwords, or send data to a remote server. The extension is loaded unpacked; it is not published in the Chrome Web Store. Actual installed-extension end-to-end validation is still pending for this beta.

## Onboard keyboard settings (beta)

**Onboard settings** detects VID `1189`, PID `8890`, vendor interface `MI_01`, with 65-byte output reports and Report ID `3`. Similar-looking keyboards can use a different protocol.

- Select a layer (1–3) and configure all six controls.
- **MiniDeck preset** fills in the shortcuts above.
- **Save draft** writes only to this PC. **Preview reports** does not write to the keyboard.
- **Save to device** sends commands that replace all six mappings in the selected layer.
- Reconnect and test the physical keyboard after saving. The tool cannot read back stored mappings or automatically back up the original configuration.

The form displays a local draft, not current device memory. Custom keys and media controls are not automatically linked to MiniDeck actions. Onboard storage contains key mappings; MiniDeck itself runs on the PC. This is not a firmware replacement or a dedicated native-event input mode.

## Scope and validation

The app currently responds to its global shortcuts from any keyboard; input is not restricted to one HID device. Existing-window reuse for desktop apps is limited to absolute-path executables without arguments. Other app types follow their normal launch behavior.

Automated tests cover settings, key assignments, hotkeys, dial navigation, dragging, size and text layout, icon import, onboard packet encoding, translation coverage and browser tab routing. They do not establish firmware compatibility or successful onboard persistence. Real Chrome-extension use, elevated apps and mixed-DPI behavior need further validation.

## Build

Use Windows PowerShell 5.1+ or PowerShell 7. Node.js is only needed for the JavaScript checks. Build scripts restore pinned NuGet packages and verify their SHA-256 hashes.

```powershell
.\build.ps1 -Language ko
.\build.ps1 -Language en -OutputDirectory .\artifacts\en
.\scripts\package.ps1
```

The packaging script creates both language ZIPs and `SHA256SUMS.txt` in `dist`. It includes only runtime files and documentation; settings, hardware diagnostics and manufacturer software are excluded.

Close MiniDeck before testing, because both language editions share the same hotkeys and single-instance mutex:

```powershell
node scripts/localization.cjs
node browser-extension/test.cjs
New-Item -ItemType Directory -Force artifacts\checks
.\MiniDeck.exe --self-test "$PWD\artifacts\checks"
.\MiniDeck.exe --preview "$PWD\artifacts\checks"
```

Self-tests use isolated settings. Preview mode uses sample entries. Neither sends onboard write commands.

## Third-party notices

See [THIRD-PARTY.md](THIRD-PARTY.md), `licenses/` and the font license files. Original manufacturer executables and decompiled vendor code are not included.

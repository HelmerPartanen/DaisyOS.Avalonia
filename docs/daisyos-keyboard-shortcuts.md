# DaisyOS keyboard shortcuts

This document is the authoritative list of keyboard shortcuts currently implemented by DaisyOS. The system bindings follow the Windows 11 conventions in `docs/keyboard-shortcuts.md` where DaisyOS has the corresponding feature.

`Super` refers to the Windows or Meta key.

## System and shell

| Shortcut | Action |
|---|---|
| `Super` | Open or close Launcher when the key is released by itself |
| `Ctrl+Esc` | Open or close Launcher |
| `Super+S` | Open or close Quick Search |
| `Super+I` | Open Settings |
| `Super+E` | Open Files |
| `Super+N` | Open or close Notification Center |
| `Super+L` | Lock DaisyOS |
| `Alt+Tab` | Cycle through DaisyOS-owned application windows |
| `Shift+Alt+Tab` | Cycle backward through DaisyOS-owned application windows |
| `Alt+F4` | Open the power menu when the desktop shell is active |
| `Ctrl+Alt+Delete` | Open or close the power menu |
| `Escape` | Close the active shell overlay |

These are shell-level bindings. Window switching and window-management shortcuts for third-party applications remain compositor-owned.

## Quick Search and Launcher search

| Shortcut | Action |
|---|---|
| `Down` | Select the next result |
| `Up` | Select the previous result |
| `Enter` | Open or execute the selected result |
| `Escape` | Close Quick Search, or clear Launcher search |

Launcher uses the same indexed search, ranking, and result execution as Quick Search whenever a query is entered.

## Files

| Shortcut | Action |
|---|---|
| `Ctrl+C` | Copy the selected item |
| `Ctrl+X` | Cut the selected item |
| `Ctrl+V` | Paste into the current folder |
| `Delete` | Move the selected item to Trash |
| `Shift+Delete` | Request permanent deletion |
| `F2` | Rename the selected item |
| `Enter` | Open the selected item |
| `F5` or `Ctrl+R` | Refresh the current folder |
| `Ctrl+Shift+N` | Create a folder |
| `Ctrl+L` | Edit the current path |
| `Ctrl+T` | Open a new tab |
| `Ctrl+W` | Close the active tab |
| `Alt+Left` | Go back |
| `Alt+Right` | Go forward |
| `Alt+Up` | Open the parent folder |

## Desktop

| Shortcut | Action |
|---|---|
| `Ctrl+C` | Copy the selected desktop item |
| `Ctrl+V` | Paste onto the desktop |
| `Delete` | Move selected desktop items to Trash |
| `F2` | Rename the selected item |
| `Enter` | Open the selected item |
| `F5` or `Ctrl+R` | Refresh desktop items |
| `Ctrl+Shift+N` | Create a folder on the desktop |

## Calendar and editing controls

The calendar supports arrow-key day navigation, `Page Up`, `Page Down`, and `Home`. Standard Avalonia text controls provide conventional editing bindings such as `Ctrl+C`, `Ctrl+X`, `Ctrl+V`, `Ctrl+A`, `Ctrl+Z`, word navigation, and selection.

## Development-only shortcuts

These bindings are disabled in production mode.

| Shortcut | Action |
|---|---|
| `Alt+R` | Open or close shell resource diagnostics |
| `Ctrl+Q` | Exit the development shell |

## Reserved or not yet implemented

The following Windows-style bindings are intentionally not claimed until DaisyOS has the corresponding complete feature: `Super+A` Quick Settings, `Super+R` Run, `Super+V` clipboard history, `Super+Tab` Task View, virtual-desktop shortcuts, screenshot shortcuts, voice typing, and accessibility-tool toggles.

# DaisyOS Development Login Session

The development session makes DaisyOS selectable from GDM, SDDM, and other
Wayland-aware display managers as **DaisyOS (Development)**. It starts a real
KWin Wayland session, with XWayland compatibility, and then runs the current
DaisyOS checkout using the .NET SDK. DaisyOS remains the shell; KWin remains
the compositor and window manager.

## Install

From the repository root, run:

```bash
sudo scripts/install-development-session.sh
```

The installer adds these system files:

```text
/usr/share/wayland-sessions/daisyos-development.desktop
/usr/local/bin/daisyos-development-session
/etc/daisyos/daisyos-development-session.conf
```

Sign out. In the display manager's session menu, choose **DaisyOS
(Development)** and sign in normally. The launcher reads the checkout path
from the configuration file, so edit and rebuild the source tree in place;
sign out and back in to exercise a new shell build.

To register a checkout in another location, pass it explicitly:

```bash
sudo scripts/install-development-session.sh --repo /path/to/DaisyOS
```

## Requirements and recovery

The login needs `kwin_wayland`, XWayland, and the .NET SDK. If any are missing,
or the shell fails to start, DaisyOS writes details to:

```text
~/.local/state/DaisyOS/development-session.log
```

Return to the display manager and choose your normal Plasma, GNOME, or other
desktop session. The development launcher intentionally does not restart a
crashing shell in a loop, so source-level failures leave a clear recovery path.

## Validation

```bash
scripts/test-development-session-install.sh
```

This stages the display-manager entry in a temporary directory and verifies
that the launcher resolves the configured checkout without changing the host.

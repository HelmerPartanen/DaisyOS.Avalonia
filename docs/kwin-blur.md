# Global KWin blur

DaisyOS delegates all backdrop blur to KWin. The shell does not run blur
shaders, allocate blur framebuffers, or sample the wallpaper for panel blur.

## Global tuning file

[`src/DaisyOS.Shell/Themes/KWinBlurSettings.axaml`](../src/DaisyOS.Shell/Themes/KWinBlurSettings.axaml)
is the single tuning contract for:

- KWin minimum, midpoint, and maximum blur strength;
- KWin saturation and noise strength;
- low/high material tint opacity;
- light and dark material tint colors.

KWin accepts blur strength values from 1 through 15. DaisyOS clamps the three
strength anchors to that native range before applying them, so hand-edited
theme values cannot send an invalid value to the compositor.

`KWinBlurConfiguration` reads these resources. `KWinBlurSettingsService` maps
the Settings slider to KWin's native range, writes the compositor values, and
reloads the already-running blur effect through KWin's effect-specific D-Bus
endpoint. A general KWin configuration reload is not sufficient to update the
active blur kernel on every KWin version.
`AppearanceThemeManager` uses the same file to adjust the tint painted over
KWin blur, so Settings and the compositor cannot drift onto separate curves.

## Surface model

Blur-bearing shell regions live in alpha-capable native windows. Each surface
requests `Blur,Transparent`. On X11/XWayland, `KWinNativeBlurRegion` publishes
rounded, measured `_KDE_NET_WM_BLUR_BEHIND_REGION` rectangles.

`WallpaperBackdropDecorator` is only a material painter. It draws tint,
borders, rounded geometry, or an opaque accessibility fallback. It never reads
the wallpaper or computes blur.

## Validation

Run `scripts/test.sh` and `scripts/publish-shell.sh --fast`, then test the full
strength range in the target KWin session.

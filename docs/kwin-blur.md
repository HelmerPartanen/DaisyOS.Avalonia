# Global KWin blur

DaisyOS delegates all backdrop blur to KWin. The shell does not run blur
shaders, allocate blur framebuffers, or sample the wallpaper for panel blur.

## Change strength from code

KWin exposes the blur region per window, but its kernel strength is global to
the running `blur` effect. Use the public convenience API:

```csharp
var result = await KWinBlur.SetStrengthAsync(8);
if (!result.Succeeded)
{
    // Keep the current material visible and surface result.FailureMessage in diagnostics.
}
```

`KWinBlurSettingsService` clamps values to KWin's supported `1..15` range,
writes `Effect-blur/BlurStrength` to `kwinrc`, then asks KWin to reconfigure
the `blur` effect over D-Bus. Reloading the effect is required for a live
change on KWin versions that keep the previous blur kernel after a general
configuration reload.

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

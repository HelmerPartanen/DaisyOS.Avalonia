using DaisyOS.Core.Models;

namespace DaisyOS.Core.Services;

public interface IThemeService
{
    ThemeMode CurrentTheme { get; }

    AccentColor CurrentAccentColor { get; }

    bool HighContrast { get; }

    bool ReduceMotion { get; }

    void SetTheme(ThemeMode themeMode);

    void SetAccentColor(AccentColor accentColor);

    void SetHighContrast(bool enabled);

    void SetReduceMotion(bool enabled);
}

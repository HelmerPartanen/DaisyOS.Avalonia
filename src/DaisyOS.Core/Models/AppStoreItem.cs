namespace DaisyOS.Core.Models;

public enum AppCategory
{
    Discover,
    Productivity,
    Creative,
    Development,
    Gaming,
    Audio,
    Installed
}

public enum AppInstallState
{
    NotInstalled,
    Installing,
    Installed,
    UpdateAvailable
}

public sealed record AppStoreItem(
    string Id,
    string PackageName,
    string Name,
    string Summary,
    string Description,
    AppCategory Category,
    string IconPath,
    string IconGlyph,
    string Version,
    string Publisher,
    string DownloadSize,
    double Rating,
    bool IsFeatured,
    bool IsEditorsChoice,
    AppInstallState InstallState = AppInstallState.NotInstalled);

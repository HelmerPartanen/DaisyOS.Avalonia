using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using DaisyOS.Core.Models;
using DaisyOS.Shell.Services.Wallpaper.VideoWallpaper;

namespace DaisyOS.Shell.Apps.System.Settings
{
    public partial class SettingsView : UserControl
    {
        private Bitmap? _wallpaperPreview;
        private bool _isRefreshingAppearanceControls;
        private string? _previewWallpaperUri;
        private int _previewRequestVersion;

        public SettingsView()
        {
            InitializeComponent();
            AttachedToVisualTree += (_, _) =>
            {
                if (Application.Current is App app)
                {
                    app.AppearanceChanged -= OnAppearanceChanged;
                    app.AppearanceChanged += OnAppearanceChanged;
                }
                RefreshAppearanceControls();
            };
            DetachedFromVisualTree += (_, _) =>
            {
                if (Application.Current is App app)
                {
                    app.AppearanceChanged -= OnAppearanceChanged;
                }
                _wallpaperPreview?.Dispose();
                _wallpaperPreview = null;
            };
        }

        private void OnNavCategoryClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button clickedBtn)
            {
                var parent = clickedBtn.Parent as StackPanel;
                if (parent != null)
                {
                    foreach (var child in parent.Children)
                    {
                        if (child is Button btn)
                        {
                            btn.Classes.Remove("Active");
                        }
                    }
                }

                clickedBtn.Classes.Add("Active");

                var isAppearance = string.Equals(clickedBtn.Tag as string, "Appearance", StringComparison.Ordinal);
                if (SystemListView != null && SystemDetailView != null && AppearanceView != null)
                {
                    SystemListView.IsVisible = !isAppearance;
                    SystemDetailView.IsVisible = false;
                    AppearanceView.IsVisible = isAppearance;
                    if (isAppearance)
                    {
                        RefreshAppearanceControls();
                    }
                }
            }
        }

        private void OnSystemSettingItemClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string settingKey })
            {
                if (SystemListView != null && SystemDetailView != null && AppearanceView != null)
                {
                    SystemListView.IsVisible = false;
                    SystemDetailView.IsVisible = true;
                    AppearanceView.IsVisible = false;

                    switch (settingKey)
                    {
                        case "Power":
                            DetailTitleText.Text = "Power & Battery";
                            break;
                        case "Multitasking":
                            DetailTitleText.Text = "Multitasking & Workspaces";
                            break;
                        case "Performance":
                            DetailTitleText.Text = "System Performance";
                            break;
                        case "Recovery":
                            DetailTitleText.Text = "Recovery & Maintenance";
                            break;
                        case "Clipboard":
                            DetailTitleText.Text = "Clipboard & History";
                            break;
                    }
                }
            }
        }

        private void OnBackToSystemListClicked(object? sender, RoutedEventArgs e)
        {
            if (SystemListView != null && SystemDetailView != null && AppearanceView != null)
            {
                SystemListView.IsVisible = true;
                SystemDetailView.IsVisible = false;
                AppearanceView.IsVisible = false;
            }
        }

        private async void OnThemeModeSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshingAppearanceControls ||
                sender is not ComboBox { SelectedItem: ComboBoxItem { Tag: string value } } ||
                !Enum.TryParse<ThemeMode>(value, out var mode) ||
                Application.Current is not App app)
            {
                return;
            }

            await app.SetThemeModeAsync(mode);
        }

        private async void OnAccentColorClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string value } ||
                !Enum.TryParse<AccentColor>(value, out var color) ||
                Application.Current is not App app)
            {
                return;
            }

            await app.SetAccentColorAsync(color);
        }

        private async void OnPickWallpaperClicked(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null || Application.Current is not App app)
            {
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose wallpaper",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp", "*.gif" } },
                    new FilePickerFileType("Videos") { Patterns = new[] { "*.mp4", "*.webm", "*.mkv", "*.mov" } }
                }
            });

            if (files.Count == 0 || string.IsNullOrWhiteSpace(files[0].Path.LocalPath))
            {
                return;
            }

            try
            {
                await app.SetWallpaperAsync(files[0].Path.LocalPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Couldn’t use that wallpaper: {ex.Message}");
            }
        }

        private async void OnResetWallpaperClicked(object? sender, RoutedEventArgs e)
        {
            if (Application.Current is not App app) return;
            await app.ResetWallpaperAsync();
        }

        private async void OnHighContrastToggled(object? sender, RoutedEventArgs e)
        {
            if (Application.Current is App app && HighContrastToggle is not null)
            {
                await app.SetHighContrastAsync(HighContrastToggle.IsChecked == true);
            }
        }

        private async void OnReduceMotionToggled(object? sender, RoutedEventArgs e)
        {
            if (Application.Current is App app && ReduceMotionToggle is not null)
            {
                await app.SetReduceMotionAsync(ReduceMotionToggle.IsChecked == true);
            }
        }

        private void OnAppearanceChanged(object? sender, EventArgs e) => RefreshAppearanceControls();

        private void RefreshAppearanceControls()
        {
            if (Application.Current is not App app || AppearanceView is null)
            {
                return;
            }

            _isRefreshingAppearanceControls = true;
            ThemeModePicker.SelectedIndex = app.CurrentThemeMode switch
            {
                ThemeMode.Light => 0,
                ThemeMode.Dark => 1,
                _ => 2
            };
            _isRefreshingAppearanceControls = false;
            AutomaticThemeInfo.IsVisible = app.CurrentThemeMode == ThemeMode.Automatic;
            AutomaticThemeInfo.Text = app.GetAutomaticThemeScheduleDescription();
            HighContrastToggle.IsChecked = app.HighContrastEnabled;
            ReduceMotionToggle.IsChecked = app.ReduceMotionEnabled;
            WallpaperNameText.Text = Path.GetFileName(app.CurrentWallpaperUri) is { Length: > 0 } name ? name : "DaisyOS wallpaper";
            RefreshWallpaperPreview(app.CurrentWallpaperUri);

            SetActive(AccentDefaultButton, app.CurrentAccentColor == AccentColor.Default);
            SetActive(AccentBlueButton, app.CurrentAccentColor == AccentColor.Blue);
            SetActive(AccentPurpleButton, app.CurrentAccentColor == AccentColor.Purple);
            SetActive(AccentPinkButton, app.CurrentAccentColor == AccentColor.Pink);
            SetActive(AccentRedButton, app.CurrentAccentColor == AccentColor.Red);
            SetActive(AccentOrangeButton, app.CurrentAccentColor == AccentColor.Orange);
            SetActive(AccentGreenButton, app.CurrentAccentColor == AccentColor.Green);
            SetActive(AccentGrayButton, app.CurrentAccentColor == AccentColor.Gray);
        }

        private static void SetActive(Button? button, bool isActive)
        {
            if (button is null) return;
            var selectedClass = button.Classes.Contains("AccentSwatch") ? "Selected" : "Active";
            if (isActive) button.Classes.Add(selectedClass);
            else button.Classes.Remove(selectedClass);
        }

        private void RefreshWallpaperPreview(string wallpaperUri)
        {
            if (WallpaperPreviewImage is null || string.Equals(_previewWallpaperUri, wallpaperUri, StringComparison.Ordinal)) return;

            _previewWallpaperUri = wallpaperUri;
            _ = RefreshWallpaperPreviewAsync(wallpaperUri, Interlocked.Increment(ref _previewRequestVersion));
        }

        private async Task RefreshWallpaperPreviewAsync(string wallpaperUri, int requestVersion)
        {
            if (WallpaperPreviewImage is null) return;

            _wallpaperPreview?.Dispose();
            _wallpaperPreview = null;
            WallpaperPreviewImage.Source = null;

            try
            {
                var resolvedPath = ResolveWallpaperPath(wallpaperUri);
                var extension = Path.GetExtension(resolvedPath).ToLowerInvariant();
                Bitmap? preview = null;

                if (File.Exists(resolvedPath) && extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".gif")
                {
                    preview = new Bitmap(resolvedPath);
                }
                else if (IsVideoExtension(extension))
                {
                    preview = await LoadVideoPreviewAsync(resolvedPath);
                }
                else if (wallpaperUri.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                {
                    preview = new Bitmap(AssetLoader.Open(new Uri(wallpaperUri)));
                }

                if (requestVersion != Volatile.Read(ref _previewRequestVersion))
                {
                    preview?.Dispose();
                    return;
                }

                _wallpaperPreview = preview;
                WallpaperPreviewImage.Source = preview;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Couldn’t create wallpaper preview: {ex.Message}");
            }
        }

        private static bool IsVideoExtension(string extension) => extension is ".mp4" or ".webm" or ".mkv" or ".mov";

        private static string ResolveWallpaperPath(string wallpaperUri)
        {
            if (File.Exists(wallpaperUri)) return wallpaperUri;

            var relativePath = wallpaperUri.Replace("avares://DaisyOS.Shell/", "src/DaisyOS.Shell/", StringComparison.Ordinal);
            return File.Exists(relativePath) ? relativePath : wallpaperUri;
        }

        private static async Task<WriteableBitmap?> LoadVideoPreviewAsync(string videoPath)
        {
            if (!File.Exists(videoPath)) return null;

            using var videoWallpaper = new VideoWallpaperService();
            if (!videoWallpaper.Load(videoPath)) return null;

            videoWallpaper.Play();
            for (var attempt = 0; attempt < 30; attempt++)
            {
                await Task.Delay(16);
                if (videoWallpaper.TryGetFrame(out var frame)) return CopyVideoFrame(frame);
            }

            return null;
        }

        private static unsafe WriteableBitmap? CopyVideoFrame(DaisyNativeWallpaper.NativeVideoFrame frame)
        {
            try
            {
                var pointer = (ulong)frame.Stride0 | ((ulong)frame.Stride1 << 32);
                if (frame.Width <= 0 || frame.Height <= 0 || pointer == 0) return null;

                var bitmap = new WriteableBitmap(
                    new PixelSize(frame.Width, frame.Height),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Opaque);
                using (var lockedBuffer = bitmap.Lock())
                {
                    var byteCount = (long)frame.Width * frame.Height * 4;
                    Buffer.MemoryCopy((void*)pointer, (void*)lockedBuffer.Address, byteCount, byteCount);
                }

                return bitmap;
            }
            finally
            {
                var pointer = (ulong)frame.Stride0 | ((ulong)frame.Stride1 << 32);
                if (pointer != 0) NativeMemory.Free((void*)pointer);
                foreach (var fileDescriptor in frame.Fd)
                {
                    if (fileDescriptor >= 0) TryClose(fileDescriptor);
                }
                if (frame.AcquireFence >= 0) TryClose(frame.AcquireFence);
            }
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int close(int fileDescriptor);

        private static void TryClose(int fileDescriptor)
        {
            try { close(fileDescriptor); }
            catch { }
        }
    }
}

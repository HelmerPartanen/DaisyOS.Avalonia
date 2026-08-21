using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using DaisyOS.Core.Models;
using DaisyOS.System.Audio;
using DaisyOS.System.Bluetooth;
using DaisyOS.System.Networking;
using DaisyOS.System.Processes;
using DaisyOS.Shell.Controls;
using DaisyOS.Shell.Services.Wallpaper;
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
            ContentArea.SizeChanged += (_, _) => UpdateContentWidth();
            NavigationSearch.PropertyChanged += (_, args) =>
            {
                if (args.Property == SearchBar.TextProperty)
                {
                    FilterNavigation(args.GetNewValue<string>() ?? string.Empty);
                }
            };
            AttachedToVisualTree += (_, _) =>
            {
                if (Application.Current is App app)
                {
                    app.AppearanceChanged -= OnAppearanceChanged;
                    app.AppearanceChanged += OnAppearanceChanged;
                }
                UpdateContentWidth();
                RefreshAppearanceControls();
                ShowCategory("System");
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

                ShowCategory(clickedBtn.Tag as string ?? "System");
            }
        }

        private void OnSystemSettingItemClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string settingKey })
            {
                ShowCategory("System");
            }
        }

        private void OnBackToSystemListClicked(object? sender, RoutedEventArgs e)
        {
            ShowCategory("System");
        }

        private void FilterNavigation(string query)
        {
            var search = query.Trim();
            foreach (var control in NavigationItems.Children)
            {
                if (control is not Button button)
                {
                    continue;
                }

                var name = button.Tag?.ToString() ?? string.Empty;
                var searchableName = name == "Privacy" ? "Privacy security" : name;
                button.IsVisible = string.IsNullOrWhiteSpace(search) ||
                                   searchableName.Contains(search, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void ShowCategory(string category)
        {
            var isAppearance = string.Equals(category, "Appearance", StringComparison.Ordinal);
            SystemListView.IsVisible = false;
            SystemDetailView.IsVisible = false;
            CategoryView.IsVisible = !isAppearance;
            AppearanceView.IsVisible = isAppearance;

            if (isAppearance)
            {
                RefreshAppearanceControls();
                return;
            }

            BuildCategory(category);
        }

        private void BuildCategory(string category)
        {
            CategoryContent.Children.Clear();
            var (title, subtitle) = category switch
            {
                "Displays" => ("Displays", "Screen, scale, colour and night-time comfort."),
                "Sound" => ("Sound", "Output, microphone and sound feedback."),
                "Network" => ("Network", "Wi-Fi, wired connections and connection privacy."),
                "Bluetooth" => ("Bluetooth", "Connect accessories and manage discoverability."),
                "Notifications" => ("Notifications", "Decide when DaisyOS can interrupt you."),
                "Apps" => ("Apps", "Default behaviour, permissions and background activity."),
                "Storage" => ("Storage", "Storage health, removable drives and cleanup."),
                "Accessibility" => ("Accessibility", "Make DaisyOS easier to see, hear and control."),
                "Privacy" => ("Privacy & security", "Control what apps may access and how DaisyOS protects you."),
                "Accounts" => ("Accounts", "Your profile, sign-in and session options."),
                "Input" => ("Keyboard & mouse", "Typing, pointing and controller preferences."),
                "DateTime" => ("Date & time", "Clock, time zone and calendar display."),
                "Gaming" => ("Gaming", "Performance, controller and full-screen preferences."),
                "About" => ("About DaisyOS", "Device details, software updates and support."),
                _ => ("System", "Power, workspaces, updates and everyday desktop behaviour.")
            };

            var pageTitle = new TextBlock
            {
                Text = title,
                FontSize = 26,
                FontWeight = FontWeight.SemiBold
            };
            pageTitle.Classes.Add("SettingsPageTitle");
            CategoryContent.Children.Add(new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    pageTitle,
                    Secondary(subtitle)
                }
            });

            switch (category)
            {
                case "Displays": BuildDisplays(); break;
                case "Sound": BuildSound(); break;
                case "Network": BuildNetwork(); break;
                case "Bluetooth": BuildBluetooth(); break;
                case "Notifications": BuildNotifications(); break;
                case "Apps": BuildApps(); break;
                case "Storage": BuildStorage(); break;
                case "Accessibility": BuildAccessibility(); break;
                case "Privacy": BuildPrivacy(); break;
                case "Accounts": BuildAccounts(); break;
                case "Input": BuildInput(); break;
                case "DateTime": BuildDateTime(); break;
                case "Gaming": BuildGaming(); break;
                case "About": BuildAbout(); break;
                default: BuildSystem(); break;
            }
        }

        private void BuildSystem()
        {
            AddSection("POWER", "Choose how the desktop balances battery life and speed.",
                Picker("Power mode", "power.mode", "Balanced", "Balanced", "Power saver", "Performance"),
                Picker("Screen turns off", "power.screen-timeout", "10 minutes", "5 minutes", "10 minutes", "15 minutes", "Never"),
                Toggle("Lock when the screen turns off", "power.lock-on-sleep", true));
            AddSection("DESKTOP", null,
                Toggle("Show date in the system bar", "system.show-date", false, value => { if (Application.Current is App app) app.SessionState.ShowDateInSystemBar = value; }),
                Toggle("Restore apps after signing in", "system.restore-apps", true),
                Toggle("Keep clipboard history", "system.clipboard-history", true));
            AddSection("UPDATES", "DaisyOS checks in the background and always asks before installing.",
                Toggle("Check for updates automatically", "system.auto-updates", true),
                Toggle("Download updates on metered connections", "system.metered-updates", false));
        }

        private void BuildDisplays()
        {
            AddSection("DISPLAY", "Changes are saved as your DaisyOS preference; applying resolution and HDR is delegated to the active compositor.",
                Picker("Preferred scale", "display.scale", "100%", "100%", "125%", "150%", "200%"),
                Picker("Refresh rate", "display.refresh-rate", "Automatic", "Automatic", "60 Hz", "120 Hz", "144 Hz"),
                Toggle("Use night light", "display.night-light", false),
                Toggle("Use HDR when available", "display.hdr", false));
            AddSection("MULTIPLE DISPLAYS", null,
                Picker("When a display is connected", "display.external-mode", "Extend desktop", "Extend desktop", "Mirror displays", "Second screen only"),
                Toggle("Remember display arrangement", "display.remember-arrangement", true));
        }

        private void BuildSound()
        {
            AddSection("OUTPUT", "These controls change the active PipeWire device.", VolumeRow("Output volume", false),
                Toggle("Play a sound for alerts", "sound.alerts", true));
            AddSection("INPUT", "Microphone volume is applied to the active PipeWire source.", VolumeRow("Input volume", true),
                Toggle("Mute microphone", "sound.input-muted", false, value => { _ = SetMicrophoneMuted(value); }));
            AddSection("SOUND EFFECTS", null,
                Toggle("Play feedback when changing volume", "sound.volume-feedback", true),
                Toggle("Spatial audio when available", "sound.spatial", false));
        }

        private void BuildNetwork()
        {
            var status = Secondary("Checking Wi-Fi availability…");
            var wifi = Toggle("Wi-Fi", "network.wifi", true, value => { _ = SetWifi(value, status); });
            AddSection("CONNECTIONS", "Wi-Fi changes are applied through NetworkManager.", wifi, status,
                Toggle("Ask before joining public networks", "network.ask-public", true),
                Toggle("Use random hardware address", "network.random-mac", true));
            AddSection("CONNECTION PRIVACY", null,
                Toggle("Connect automatically to known networks", "network.auto-connect", true),
                Toggle("Allow background sync on metered networks", "network.metered-sync", false));
            _ = RefreshWifiAsync(wifi, status);
        }

        private void BuildBluetooth()
        {
            var status = Secondary("Checking Bluetooth availability…");
            var bluetooth = Toggle("Bluetooth", "bluetooth.enabled", false, value => { _ = SetBluetooth(value, status); });
            AddSection("BLUETOOTH", "Bluetooth changes are applied through the system adapter.", bluetooth, status,
                Toggle("Allow nearby devices to discover this computer", "bluetooth.discoverable", false),
                Toggle("Reconnect trusted devices automatically", "bluetooth.auto-connect", true));
            _ = RefreshBluetoothAsync(bluetooth, status);
        }

        private void BuildNotifications()
        {
            AddSection("FOCUS", "Notifications still appear in Notification Centre while focus is on.",
                Toggle("Do not disturb", "notifications.dnd", false, value => { if (Application.Current is App app) app.SetDoNotDisturb(value); }),
                Toggle("Turn on Do Not Disturb while gaming", "notifications.dnd-gaming", true));
            AddSection("NOTIFICATION STYLE", null,
                Toggle("Show notification banners", "notifications.banners", true),
                Toggle("Play notification sounds", "notifications.sounds", true),
                Toggle("Show notifications on the lock screen", "notifications.lock-screen", false));
        }

        private void BuildApps()
        {
            AddSection("DEFAULTS", "DaisyOS will use these choices when an app does not specify a preference.",
                Picker("Web links", "apps.web", "Ask every time", "Ask every time", "DaisyOS Browser", "Default browser"),
                Picker("New files open with", "apps.files", "File Manager", "File Manager", "Ask every time"));
            AddSection("APP BEHAVIOUR", null,
                Toggle("Allow apps to run in the background", "apps.background", true),
                Toggle("Ask before an app opens an external link", "apps.external-links", true),
                Toggle("Show recently used apps in the launcher", "apps.recent", true));
        }

        private void BuildStorage()
        {
            AddSection("STORAGE", "Storage details are read from the connected filesystems.",
                Picker("Empty Trash after", "storage.trash-retention", "30 days", "Never", "30 days", "90 days", "1 year"),
                Toggle("Show a warning when storage is nearly full", "storage.low-space-warning", true));
            AddSection("REMOVABLE DRIVES", null,
                Toggle("Open removable drives when connected", "storage.auto-open", true),
                Toggle("Ask before formatting a drive", "storage.confirm-format", true));
        }

        private void BuildAccessibility()
        {
            AddSection("VISION", "Appearance settings are applied immediately.",
                Toggle("High contrast", "accessibility.high-contrast", false, value => { _ = SetHighContrast(value); }),
                Toggle("Reduce motion", "accessibility.reduce-motion", false, value => { _ = SetReduceMotion(value); }),
                Picker("Text size", "accessibility.text-size", "Default", "Small", "Default", "Large", "Extra large"));
            AddSection("INTERACTION", null,
                Toggle("Show keyboard focus", "accessibility.focus-indicators", true),
                Toggle("Sticky keys", "accessibility.sticky-keys", false),
                Toggle("On-screen keyboard", "accessibility.on-screen-keyboard", false));
        }

        private void BuildPrivacy()
        {
            AddSection("PERMISSIONS", "Permission requests always require a clear app-specific explanation.",
                Toggle("Ask before apps use location", "privacy.location", true),
                Toggle("Ask before apps use camera", "privacy.camera", true),
                Toggle("Ask before apps use microphone", "privacy.microphone", true));
            AddSection("DIAGNOSTICS", null,
                Toggle("Share anonymous diagnostics", "privacy.diagnostics", false),
                Toggle("Clear search history when signing out", "privacy.clear-search", false));
        }

        private void BuildAccounts()
        {
            AddSection("YOUR ACCOUNT", "Your profile picture and account identity stay on this device unless you choose a sync provider.",
                Toggle("Require password after sleep", "accounts.password-after-sleep", true),
                Toggle("Show account name on the lock screen", "accounts.show-name-lock-screen", true));
            AddSection("SESSION", null,
                Toggle("Remember open apps when signing out", "accounts.remember-session", true),
                Toggle("Allow guest sessions", "accounts.guest-session", false));
        }

        private void BuildInput()
        {
            AddSection("KEYBOARD", null,
                Picker("Keyboard repeat speed", "input.repeat-speed", "Normal", "Slow", "Normal", "Fast"),
                Picker("Compose key", "input.compose-key", "Disabled", "Disabled", "Right Alt", "Menu key"),
                Toggle("Use Caps Lock as an additional Ctrl key", "input.caps-as-ctrl", false));
            AddSection("MOUSE & TRACKPAD", null,
                Picker("Pointer speed", "input.pointer-speed", "Normal", "Slow", "Normal", "Fast"),
                Toggle("Natural scrolling", "input.natural-scroll", false),
                Toggle("Tap to click", "input.tap-to-click", true));
        }

        private void BuildDateTime()
        {
            AddSection("TIME", "System time and time zone changes require permission from the active Linux session.",
                Toggle("Set time automatically", "time.automatic", true),
                Picker("Clock format", "time.clock-format", "24-hour", "12-hour", "24-hour"),
                Toggle("Show week numbers in Calendar", "time.week-numbers", false));
            AddSection("REGION", null,
                Picker("First day of week", "time.first-day", "Monday", "Sunday", "Monday", "Saturday"),
                Picker("Temperature", "time.temperature", "Celsius", "Celsius", "Fahrenheit"));
        }

        private void BuildGaming()
        {
            AddSection("PERFORMANCE", "Gaming Mode changes shell behaviour immediately and is also available in Quick Settings.",
                Toggle("Gaming Mode", "gaming.mode", false, value => { if (Application.Current is App app) app.SessionState.GamingMode = value; }),
                Toggle("Keep notifications quiet while gaming", "gaming.quiet-notifications", true),
                Toggle("Prefer performance power mode while gaming", "gaming.performance-power", true));
            AddSection("CONTROLLER", null,
                Toggle("Open console mode with the Guide button", "gaming.guide-console", true),
                Toggle("Show controller button hints", "gaming.button-hints", true));
        }

        private void BuildAbout()
        {
            AddSection("DAISYOS", "A responsive Linux desktop shell built around your existing applications.",
                InfoRow("Version", "DaisyOS development build"),
                InfoRow("Desktop session", Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "Linux desktop session"));
            AddSection("SOFTWARE", null,
                Toggle("Check for updates automatically", "system.auto-updates", true),
                Toggle("Include preview updates", "about.preview-updates", false));
        }

        private void AddSection(string label, string? description, params Control[] rows)
        {
            var section = new StackPanel { Spacing = 12 };
            section.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = TryBrush("ContentTertiaryBrush")
            });
            if (!string.IsNullOrWhiteSpace(description)) section.Children.Add(Secondary(description));

            // Group related preferences into one calm, scannable surface. Individual rows
            // remain borderless; dividers expose relationships without visual noise.
            var rowsHost = new StackPanel { Spacing = 0 };
            for (var index = 0; index < rows.Length; index++)
            {
                rowsHost.Children.Add(rows[index]);
                if (index < rows.Length - 1)
                {
                    rowsHost.Children.Add(new Border
                    {
                        Height = 1,
                        Margin = new Thickness(16, 0),
                        Background = TryBrush("StrokeSubtleBrush")
                    });
                }
            }

            var group = new Border { Child = rowsHost };
            group.Classes.Add("SettingsGroupCard");
            section.Children.Add(group);
            CategoryContent.Children.Add(section);
        }

        private Border Toggle(string title, string key, bool fallback, Action<bool>? apply = null)
        {
            var app = Application.Current as App;
            var value = key switch
            {
                "accessibility.high-contrast" when app is not null => app.HighContrastEnabled,
                "accessibility.reduce-motion" when app is not null => app.ReduceMotionEnabled,
                "notifications.dnd" when app is not null => app.SessionState.DoNotDisturb,
                "gaming.mode" when app is not null => app.SessionState.GamingMode,
                "system.show-date" when app is not null => app.SessionState.ShowDateInSystemBar,
                _ => bool.TryParse(app?.GetPreference(key, fallback.ToString()) ?? fallback.ToString(), out var saved) ? saved : fallback
            };
            var toggle = new ToggleSwitch { IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
            toggle.Click += (_, _) =>
            {
                var enabled = toggle.IsChecked == true;
                app?.SetPreference(key, enabled.ToString());
                apply?.Invoke(enabled);
            };
            return PreferenceRow(title, null, toggle);
        }

        private Border Picker(string title, string key, string fallback, params string[] options)
        {
            var app = Application.Current as App;
            var saved = app?.GetPreference(key, fallback) ?? fallback;
            var picker = new ComboBox
            {
                Width = 156,
                Height = 32,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            // Reuse Appearance's picker contract instead of creating a second dropdown visual language.
            picker.Classes.Add("ThemeModePicker");
            foreach (var option in options)
            {
                var item = new ComboBoxItem { Content = option };
                item.Classes.Add("ThemeModeOption");
                picker.Items.Add(item);
            }
            picker.SelectedIndex = Math.Max(0, Array.IndexOf(options, saved));
            picker.SelectionChanged += (_, _) =>
            {
                if (picker.SelectedItem is ComboBoxItem { Content: string selected }) app?.SetPreference(key, selected);
            };
            return PreferenceRow(title, null, picker);
        }

        private Border InfoRow(string title, string value) => PreferenceRow(title, value, null);

        private Border VolumeRow(string title, bool input)
        {
            var app = Application.Current as App;
            var slider = new QuickSettingsSlider
            {
                Minimum = 0,
                Maximum = 100,
                Width = 200,
                Value = app?.SessionState.LastKnownVolume ?? 72,
                Icon = input ? "mic" : "volume_up",
                ShowThumb = false,
                TrackCornerRadius = new CornerRadius(10),
                VerticalAlignment = VerticalAlignment.Center
            };
            slider.ValueChanged += (_, _) =>
            {
                var volume = Math.Round(slider.Value);
                if (!input && app is not null) app.SessionState.LastKnownVolume = volume;
                var audio = new LinuxAudioService(new SafeCommandRunner());
                _ = input ? audio.SetInputVolumeAsync(volume) : audio.SetVolumeAsync(volume);
            };
            _ = LoadVolumeAsync(slider, input);
            return PreferenceRow(title, input ? "Microphone" : "System output", slider);
        }

        private Border PreferenceRow(string title, string? description, Control? trailing)
        {
            var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
            var copy = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            copy.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeight.SemiBold });
            if (!string.IsNullOrWhiteSpace(description)) copy.Children.Add(Secondary(description));
            content.Children.Add(copy);
            if (trailing is not null)
            {
                Grid.SetColumn(trailing, 1);
                content.Children.Add(trailing);
            }

            var row = new Border
            {
                Background = Brushes.Transparent,
                Child = content
            };
            row.Classes.Add("SettingsGroupRow");
            return row;
        }

        private void UpdateContentWidth()
        {
            // Scrollbars live at the panel edge. Reserve a stable gutter plus readable
            // content insets here, instead of shrinking the ScrollViewer itself.
            const double scrollbarGutter = 16;
            const double contentHorizontalInset = 72;
            var width = Math.Min(900, ContentArea.Bounds.Width - scrollbarGutter - contentHorizontalInset);
            if (width <= 0)
            {
                return;
            }

            CategoryContent.Width = width;
            AppearanceContent.Width = width;
        }

        private static TextBlock Secondary(string text) => new()
        {
            Text = text,
            FontSize = 13,
            Foreground = TryBrush("ContentSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap
        };

        private static IBrush? TryBrush(string resourceKey) =>
            Application.Current?.TryFindResource(resourceKey, Application.Current.ActualThemeVariant, out var value) == true
                ? value as IBrush
                : null;

        private async Task LoadVolumeAsync(QuickSettingsSlider slider, bool input)
        {
            try
            {
                var audio = new LinuxAudioService(new SafeCommandRunner());
                var volume = input ? await audio.GetInputVolumeAsync() : await audio.GetVolumeAsync();
                if (volume is not null) slider.Value = volume.Value;
            }
            catch { /* Keep the last safe slider position if PipeWire is unavailable. */ }
        }

        private async Task SetMicrophoneMuted(bool muted)
        {
            try { await new LinuxAudioService(new SafeCommandRunner()).SetInputMutedAsync(muted); }
            catch { }
        }

        private async Task RefreshWifiAsync(Border row, TextBlock status)
        {
            try
            {
                var radio = await new LinuxWirelessNetworkService(new SafeCommandRunner()).GetRadioStatusAsync();
                status.Text = radio.Detail;
                if (row.Child is Grid { Children: { Count: > 1 } } grid && grid.Children[1] is ToggleSwitch toggle)
                    toggle.IsChecked = radio.IsEnabled;
            }
            catch { status.Text = "Wi-Fi status could not be read."; }
        }

        private async Task SetWifi(bool enabled, TextBlock status)
        {
            try
            {
                var changed = await new LinuxWirelessNetworkService(new SafeCommandRunner()).SetEnabledAsync(enabled);
                status.Text = changed ? (enabled ? "Wi-Fi is on." : "Wi-Fi is off.") : "DaisyOS could not change Wi-Fi.";
            }
            catch { status.Text = "DaisyOS could not change Wi-Fi."; }
        }

        private async Task RefreshBluetoothAsync(Border row, TextBlock status)
        {
            try
            {
                var adapter = await new LinuxBluetoothService(new SafeCommandRunner()).GetAdapterStatusAsync();
                status.Text = adapter.Detail;
                if (row.Child is Grid { Children: { Count: > 1 } } grid && grid.Children[1] is ToggleSwitch toggle)
                    toggle.IsChecked = adapter.IsEnabled;
            }
            catch { status.Text = "Bluetooth status could not be read."; }
        }

        private async Task SetBluetooth(bool enabled, TextBlock status)
        {
            try
            {
                var changed = await new LinuxBluetoothService(new SafeCommandRunner()).SetEnabledAsync(enabled);
                status.Text = changed ? (enabled ? "Bluetooth is on." : "Bluetooth is off.") : "DaisyOS could not change Bluetooth.";
            }
            catch { status.Text = "DaisyOS could not change Bluetooth."; }
        }

        private async Task SetHighContrast(bool enabled)
        {
            if (Application.Current is App app) await app.SetHighContrastAsync(enabled);
        }

        private async Task SetReduceMotion(bool enabled)
        {
            if (Application.Current is App app) await app.SetReduceMotionAsync(enabled);
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
                    // Keep the default filter inclusive: some portal/native pickers expose only
                    // the first filter until the user changes it manually.
                    new FilePickerFileType("Wallpaper files") { Patterns = WallpaperFileTypes.WallpaperPatterns },
                    new FilePickerFileType("Images") { Patterns = WallpaperFileTypes.ImagePatterns },
                    new FilePickerFileType("Videos") { Patterns = WallpaperFileTypes.VideoPatterns },
                    FilePickerFileTypes.All
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
                Bitmap? preview = null;

                if (File.Exists(resolvedPath) && WallpaperFileTypes.IsImage(resolvedPath))
                {
                    preview = new Bitmap(resolvedPath);
                }
                else if (WallpaperFileTypes.IsVideo(resolvedPath))
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

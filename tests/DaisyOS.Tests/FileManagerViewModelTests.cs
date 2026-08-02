using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.Shell.Services;
using DaisyOS.Shell.ViewModels;
using Xunit;

namespace DaisyOS.Tests;

public sealed class FileManagerViewModelTests
{
    [Fact]
    public void SearchAndHiddenFileToggleUpdateVisibleItems()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Path, "notes.txt"), "notes");
        File.WriteAllText(Path.Combine(directory.Path, "photo.jpg"), "image");
        File.WriteAllText(Path.Combine(directory.Path, ".secret"), "hidden");
        using var viewModel = CreateViewModel(directory.Path);

        Assert.Equal(2, viewModel.Items.Count);

        viewModel.SearchQuery = "notes";
        Assert.Equal("notes.txt", Assert.Single(viewModel.Items).Name);
        Assert.Equal("1 file", viewModel.StatusLabel);

        viewModel.SearchQuery = string.Empty;
        viewModel.ShowHiddenFiles = true;
        Assert.Equal(3, viewModel.Items.Count);
        Assert.Contains(viewModel.Items, item => item.Name == ".secret");
    }

    [Fact]
    public void NavigationMaintainsBreadcrumbsAndBackForwardState()
    {
        using var directory = new TemporaryDirectory();
        var child = Directory.CreateDirectory(Path.Combine(directory.Path, "child"));
        using var viewModel = CreateViewModel(directory.Path);

        viewModel.NavigateTo(child.FullName);

        Assert.Equal(child.FullName, viewModel.CurrentPath);
        Assert.Equal("child", viewModel.CurrentFolderName);
        Assert.NotEmpty(viewModel.Breadcrumbs);
        Assert.True(viewModel.NavigateBackCommand.CanExecute(null));

        viewModel.NavigateBackCommand.Execute(null);
        Assert.Equal(directory.Path, viewModel.CurrentPath);
        Assert.True(viewModel.NavigateForwardCommand.CanExecute(null));
    }

    [Fact]
    public void PinAndRemoveLocationPersistAcrossViewModels()
    {
        using var directory = new TemporaryDirectory();
        var settings = new JsonSettingsService(Path.Combine(directory.Path, "settings.json"));
        settings.PinnedFileManagerLocations = [];
        var child = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects"));

        using (var viewModel = CreateViewModel(directory.Path, settings))
        {
            viewModel.NavigateTo(child.FullName);
            viewModel.ToggleCurrentLocationPinCommand.Execute(null);

            var pinned = Assert.Single(viewModel.SidebarPlaces);
            Assert.Equal("Projects", pinned.Name);
            Assert.Equal(child.FullName, pinned.Path);

            viewModel.RemovePinnedLocationCommand.Execute(pinned);
            Assert.Empty(viewModel.SidebarPlaces);
        }

        var reloadedSettings = new JsonSettingsService(Path.Combine(directory.Path, "settings.json"));
        Assert.NotNull(reloadedSettings.PinnedFileManagerLocations);
        Assert.Empty(reloadedSettings.PinnedFileManagerLocations);
    }

    [Fact]
    public void LayoutSizeAndItemPinCommandsUpdatePresentationState()
    {
        using var directory = new TemporaryDirectory();
        var settings = new JsonSettingsService(Path.Combine(directory.Path, "settings.json"));
        settings.PinnedFileManagerLocations = [];
        var folder = Directory.CreateDirectory(Path.Combine(directory.Path, "Pinned folder"));
        using var viewModel = CreateViewModel(directory.Path, settings);
        var item = Assert.Single(viewModel.Items, candidate => candidate.IsDirectory);

        viewModel.SetLayoutCommand.Execute("Grid");
        viewModel.SetItemSizeCommand.Execute("Large");
        viewModel.PinItemLocationCommand.Execute(item);

        Assert.True(viewModel.IsGridView);
        Assert.Equal(FileManagerItemSize.Large, viewModel.ItemSize);
        Assert.Equal(152, viewModel.GridItemWidth);
        Assert.True(item.CanUnpinFromSidebar);
        Assert.Equal(folder.FullName, Assert.Single(viewModel.SidebarPlaces).Path);

        viewModel.UnpinItemLocationCommand.Execute(item);
        Assert.True(item.CanPinToSidebar);
        Assert.Empty(viewModel.SidebarPlaces);
    }

    [Fact]
    public void DetailsPaneIsOptInAndRequiresASelection()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Path, "notes.txt"), "notes");
        using var viewModel = CreateViewModel(directory.Path);

        Assert.False(viewModel.ShowDetailsPane);
        Assert.False(viewModel.IsDetailsPaneVisible);

        viewModel.SelectItemCommand.Execute(Assert.Single(viewModel.Items));
        Assert.False(viewModel.IsDetailsPaneVisible);

        viewModel.ToggleDetailsPaneCommand.Execute(null);
        Assert.True(viewModel.IsDetailsPaneVisible);

        viewModel.ClearSelectionCommand.Execute(null);
        Assert.False(viewModel.IsDetailsPaneVisible);
    }

    [Fact]
    public void DroppedItemsMoveOrCopyIntoTargetFolder()
    {
        using var directory = new TemporaryDirectory();
        var destination = Directory.CreateDirectory(Path.Combine(directory.Path, "destination"));
        var movedSource = Path.Combine(directory.Path, "move.txt");
        var copiedSource = Path.Combine(directory.Path, "copy.txt");
        File.WriteAllText(movedSource, "move");
        File.WriteAllText(copiedSource, "copy");
        using var viewModel = CreateViewModel(directory.Path);

        viewModel.DropPaths([movedSource], destination.FullName, copy: false);
        viewModel.DropPaths([copiedSource], destination.FullName, copy: true);

        Assert.False(File.Exists(movedSource));
        Assert.True(File.Exists(Path.Combine(destination.FullName, "move.txt")));
        Assert.True(File.Exists(copiedSource));
        Assert.True(File.Exists(Path.Combine(destination.FullName, "copy.txt")));
    }

    [Fact]
    public void TabsTrackTheirFolderAndCanBeSwitchedAndClosed()
    {
        using var directory = new TemporaryDirectory();
        var child = Directory.CreateDirectory(Path.Combine(directory.Path, "Photos"));
        using var viewModel = CreateViewModel(directory.Path);
        var firstTab = Assert.Single(viewModel.Tabs);

        viewModel.NavigateTo(child.FullName);
        Assert.Equal(child.FullName, firstTab.Path);
        Assert.Equal("Photos", firstTab.Title);

        var secondTab = new FileManagerTabViewModel(directory.Path, "Files");
        viewModel.Tabs.Add(secondTab);
        viewModel.SelectTabCommand.Execute(secondTab);
        Assert.Same(secondTab, viewModel.ActiveTab);
        Assert.Equal(directory.Path, viewModel.CurrentPath);

        viewModel.CloseTabCommand.Execute(secondTab);
        Assert.Single(viewModel.Tabs);
        Assert.Same(firstTab, viewModel.ActiveTab);
        Assert.Equal(child.FullName, viewModel.CurrentPath);
    }

    [Fact]
    public void FileFamiliesUseDistinctSemanticIconsAndColors()
    {
        using var directory = new TemporaryDirectory();
        var document = new FileItemViewModel(Path.Combine(directory.Path, "report.docx"), "report.docx", false, 10, DateTime.Now);
        var spreadsheet = new FileItemViewModel(Path.Combine(directory.Path, "budget.xlsx"), "budget.xlsx", false, 10, DateTime.Now);
        var archive = new FileItemViewModel(Path.Combine(directory.Path, "backup.zip"), "backup.zip", false, 10, DateTime.Now);
        var installer = new FileItemViewModel(Path.Combine(directory.Path, "discord-1.0.deb"), "discord-1.0.deb", false, 10, DateTime.Now);

        Assert.Equal("article", document.IconGlyph);
        Assert.Equal("table_chart", spreadsheet.IconGlyph);
        Assert.Equal("archive", archive.IconGlyph);
        Assert.Equal("inventory_2", installer.IconGlyph);
        Assert.NotEqual(document.IconForeground.ToString(), spreadsheet.IconForeground.ToString());
        Assert.NotEqual(spreadsheet.IconForeground.ToString(), archive.IconForeground.ToString());

        document.Dispose();
        spreadsheet.Dispose();
        archive.Dispose();
        installer.Dispose();
    }

    [Fact]
    public void SidebarPlacesUseDistinctSemanticColors()
    {
        var downloads = new SidebarPlaceViewModel("Downloads", "/tmp/Downloads", "download");
        var pictures = new SidebarPlaceViewModel("Pictures", "/tmp/Pictures", "image");
        var music = new SidebarPlaceViewModel("Music", "/tmp/Music", "music_note_2");
        var drive = new SidebarPlaceViewModel("System drive", "/", "hard_drive");

        Assert.NotEqual(downloads.IconForeground.ToString(), pictures.IconForeground.ToString());
        Assert.NotEqual(pictures.IconForeground.ToString(), music.IconForeground.ToString());
        Assert.NotEqual(music.IconForeground.ToString(), drive.IconForeground.ToString());
    }

    private static FileManagerViewModel CreateViewModel(
        string initialPath,
        ISettingsService? settings = null)
    {
        var trashRoot = Path.Combine(initialPath, "trash");
        return new FileManagerViewModel(
            new RecordingNotificationService(),
            new StubFileAssociationService(),
            settings,
            initialPath,
            Path.Combine(trashRoot, "files"),
            Path.Combine(trashRoot, "info"));
    }

    private sealed class StubFileAssociationService : IFileAssociationService
    {
        public Task<FileHandlerQueryResult> GetHandlersAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FileHandlerQueryResult(true, "text/plain", [], string.Empty));

        public Task<AppLaunchResult> OpenDefaultAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppLaunchResult(false, "Not used."));

        public Task<AppLaunchResult> OpenWithAsync(FileHandler handler, string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppLaunchResult(false, "Not used."));
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public event EventHandler? NotificationsChanged;

        public IReadOnlyList<NotificationItem> GetNotifications() => [];
        public void Add(NotificationItem notification) => NotificationsChanged?.Invoke(this, EventArgs.Empty);
        public void Dismiss(NotificationItem notification) { }
        public void Clear() { }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = global::System.IO.Path.Combine(
                global::System.IO.Path.GetTempPath(),
                "daisyos-files-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

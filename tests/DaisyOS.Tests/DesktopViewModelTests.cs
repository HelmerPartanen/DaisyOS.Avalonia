using DaisyOS.Shell.Services;
using DaisyOS.Shell.ViewModels;
using Xunit;

namespace DaisyOS.Tests;

public sealed class DesktopViewModelTests
{
    [Fact]
    public void SavedPositionsSurviveAViewModelRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "daisyos-desktop-tests", Guid.NewGuid().ToString("N"));
        var desktopPath = Path.Combine(root, "Desktop");
        var layoutPath = Path.Combine(root, "desktop-layout.json");
        Directory.CreateDirectory(desktopPath);
        File.WriteAllText(Path.Combine(desktopPath, "Alpha.txt"), string.Empty);
        File.WriteAllText(Path.Combine(desktopPath, "Beta.txt"), string.Empty);

        try
        {
            var services = ShellServices.CreateMock();
            var store = new DesktopLayoutStore(layoutPath);

            using (var first = new DesktopViewModel(services, desktopPath, store))
            {
                first.ResizeGrid(4, 4);
                var alpha = Assert.Single(first.DesktopCells, cell => cell.Item?.Name == "Alpha.txt");
                var destination = Assert.Single(first.DesktopCells, cell => cell.Row == 3 && cell.Column == 2);

                first.MoveDesktopItem(alpha, destination);
            }

            using var second = new DesktopViewModel(services, desktopPath, store);
            second.ResizeGrid(4, 4);

            var restored = Assert.Single(second.DesktopCells, cell => cell.Item?.Name == "Alpha.txt");
            Assert.Equal(3, restored.Row);
            Assert.Equal(2, restored.Column);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ResizeKeepsEveryVisibleItemInAUniqueCell()
    {
        var root = Path.Combine(Path.GetTempPath(), "daisyos-desktop-tests", Guid.NewGuid().ToString("N"));
        var desktopPath = Path.Combine(root, "Desktop");
        Directory.CreateDirectory(desktopPath);
        for (var index = 0; index < 8; index++)
        {
            File.WriteAllText(Path.Combine(desktopPath, $"Item {index}.txt"), string.Empty);
        }

        try
        {
            var services = ShellServices.CreateMock();
            using var viewModel = new DesktopViewModel(
                services,
                desktopPath,
                new DesktopLayoutStore(Path.Combine(root, "desktop-layout.json")));

            viewModel.ResizeGrid(2, 3);

            Assert.Equal(6, viewModel.DesktopCells.Count);
            Assert.Equal(6, viewModel.DesktopCells.Count(cell => cell.HasItem));
            Assert.Equal(
                6,
                viewModel.DesktopCells
                    .Where(cell => cell.HasItem)
                    .Select(cell => cell.Item!.TargetPath)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

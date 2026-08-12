using System;
using System.Collections.Generic;
using DaisyOS.Core.Desktop;
using DaisyOS.Shell.ViewModels;
using Xunit;

namespace DaisyOS.Shell.Tests;

public class DesktopViewModelTests
{
    [Fact]
    public void MoveItemHelper_ReordersElementsCorrectly()
    {
        var list = new List<string> { "A", "B", "C", "D" };

        DesktopViewModel.MoveItem(list, 0, 2); // Move A to slot 2: B, C, A, D
        Assert.Equal(new[] { "B", "C", "A", "D" }, list);

        DesktopViewModel.MoveItem(list, 3, 0); // Move D to slot 0: D, B, C, A
        Assert.Equal(new[] { "D", "B", "C", "A" }, list);

        DesktopViewModel.MoveItem(list, 1, 1); // Same index: no change
        Assert.Equal(new[] { "D", "B", "C", "A" }, list);
    }

    [Fact]
    public void AddItem_StoresCommittedCell()
    {
        var vm = new DesktopViewModel();
        vm.ClearItems();
        var itemA = new DesktopItemViewModel(new DesktopItemState("A", "primary", 1, 2));

        vm.AddItem(itemA);
        Assert.Equal(new GridCell(1, 2), vm.GetCommittedCell(itemA.Id));
    }
}

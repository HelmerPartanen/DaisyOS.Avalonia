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

    [Fact]
    public void ReorderItem_SwapsOccupiedCellsCorrectly()
    {
        var vm = new DesktopViewModel();
        vm.ClearItems();

        var metrics = new DesktopGridMetrics("primary", new Rect(0, 0, 800, 600), 96.0, 100, 100);

        var itemA = new DesktopItemViewModel(new DesktopItemState("A", "primary", 0, 0, 0));
        var itemB = new DesktopItemViewModel(new DesktopItemState("B", "primary", 0, 1, 1));

        vm.AddItem(itemA);
        vm.AddItem(itemB);
        vm.CalculateLayout(metrics);

        // Reorder A (cell 0,0) to cell 0,1 (occupied by B)
        bool reordered = vm.ReorderItem(itemA.Id, new GridCell(0, 1), metrics);
        Assert.True(reordered);

        // Verify committed cells swapped
        Assert.Equal(new GridCell(0, 1), vm.GetCommittedCell(itemA.Id));
        Assert.Equal(new GridCell(0, 0), vm.GetCommittedCell(itemB.Id));
    }

    [Fact]
    public void ReorderItem_ToEmptyCell_MovesItemToTargetCell()
    {
        var vm = new DesktopViewModel();
        vm.ClearItems();

        var metrics = new DesktopGridMetrics("primary", new Rect(0, 0, 800, 600), 96.0, 100, 100);

        var itemA = new DesktopItemViewModel(new DesktopItemState("A", "primary", 0, 0, 0));
        vm.AddItem(itemA);
        vm.CalculateLayout(metrics);

        // Reorder A to empty cell (3, 2)
        bool reordered = vm.ReorderItem(itemA.Id, new GridCell(3, 2), metrics);
        Assert.True(reordered);
        Assert.Equal(new GridCell(3, 2), vm.GetCommittedCell(itemA.Id));
    }

    [Fact]
    public void ReorderItem_SameCell_ReturnsFalse_Idempotent()
    {
        var vm = new DesktopViewModel();
        vm.ClearItems();

        var metrics = new DesktopGridMetrics("primary", new Rect(0, 0, 800, 600), 96.0, 100, 100);
        var itemA = new DesktopItemViewModel(new DesktopItemState("A", "primary", 0, 0, 0));
        vm.AddItem(itemA);
        vm.CalculateLayout(metrics);

        bool reordered = vm.ReorderItem(itemA.Id, new GridCell(0, 0), metrics);
        Assert.False(reordered);
    }

    [Fact]
    public void IsEditMode_PropagatesToItems()
    {
        var vm = new DesktopViewModel();
        vm.ClearItems();
        var itemA = new DesktopItemViewModel(new DesktopItemState("A", "primary", 0, 0, 0));
        vm.AddItem(itemA);

        Assert.False(itemA.IsEditMode);
        vm.IsEditMode = true;
        Assert.True(itemA.IsEditMode);
        vm.IsEditMode = false;
        Assert.False(itemA.IsEditMode);
    }
}

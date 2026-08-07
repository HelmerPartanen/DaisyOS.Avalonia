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
    public void MovePreviewItemToCell_DensePacksGrid()
    {
        var vm = new DesktopViewModel();
        vm.ClearItems();
        var itemA = new DesktopItemViewModel(new DesktopItemState("A", "primary", 0, 0));
        var itemB = new DesktopItemViewModel(new DesktopItemState("B", "primary", 0, 1));
        var itemC = new DesktopItemViewModel(new DesktopItemState("C", "primary", 0, 2));
        var metrics = new DesktopGridMetrics("primary", new Rect(0, 0, 1000, 800), 96, 74, 88);

        vm.AddItem(itemA);
        vm.AddItem(itemB);
        vm.AddItem(itemC);

        vm.BeginDrag(itemC.Id);
        Assert.True(itemC.IsHiddenPlaceholder);

        // Move item C (index 2) to cell (0, 0) (index 0)
        // Expected layout: C, A, B
        var topCell = new GridCell(0, 0);
        bool moved = vm.MovePreviewItemToCell(itemC.Id, topCell, metrics);
        Assert.True(moved);
        Assert.Equal(topCell, vm.GetPreviewCell(itemC.Id));
        Assert.Equal(new GridCell(0, 1), vm.GetPreviewCell(itemA.Id));
        Assert.Equal(new GridCell(0, 2), vm.GetPreviewCell(itemB.Id));

        // Commit reorder
        vm.CommitReorder();
        Assert.Equal(topCell, vm.GetCommittedCell(itemC.Id));
        Assert.False(itemC.IsHiddenPlaceholder);
    }

    [Fact]
    public void CancelDrag_RestoresOriginalCommittedOrder()
    {
        var vm = new DesktopViewModel();
        vm.ClearItems();
        var itemA = new DesktopItemViewModel(new DesktopItemState("A", "primary", 0, 0));
        var itemB = new DesktopItemViewModel(new DesktopItemState("B", "primary", 0, 1));
        var metrics = new DesktopGridMetrics("primary", new Rect(0, 0, 1000, 800), 96, 74, 88);

        vm.AddItem(itemA);
        vm.AddItem(itemB);

        var originalCell = vm.GetCommittedCell(itemA.Id);

        vm.BeginDrag(itemA.Id);
        vm.MovePreviewItemToCell(itemA.Id, new GridCell(4, 4), metrics);

        vm.CancelDrag();

        Assert.Equal(originalCell, vm.GetPreviewCell(itemA.Id));
        Assert.False(itemA.IsHiddenPlaceholder);
    }


}

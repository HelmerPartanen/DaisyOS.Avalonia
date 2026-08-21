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
    public void Trash_IsAddedAsTheFirstDesktopItem()
    {
        var vm = new DesktopViewModel();
        var trash = Assert.Single(vm.Items, item => item.Id.Value == DesktopItemViewModel.TrashItemId);
        var metrics = new DesktopGridMetrics("primary", new Rect(0, 0, 800, 600), 96.0, 100, 100);

        vm.CalculateLayout(metrics);

        Assert.Equal("Trash", trash.Label);
        Assert.Equal(new GridCell(0, 0), vm.GetCommittedCell(trash.Id));
    }

    [Fact]
    public void Trash_CanBeReorderedLikeAnyOtherDesktopItem()
    {
        var vm = new DesktopViewModel();
        vm.ClearItems();
        var trash = new DesktopItemViewModel(new DesktopItemState(DesktopItemViewModel.TrashItemId, "primary", 0, 0, 0));
        var app = new DesktopItemViewModel(new DesktopItemState("app", "primary", 0, 1, 1));
        var metrics = new DesktopGridMetrics("primary", new Rect(0, 0, 800, 600), 96.0, 100, 100);

        vm.AddItem(trash);
        vm.AddItem(app);
        vm.CalculateLayout(metrics);

        Assert.True(vm.ReorderItem(trash.Id, new GridCell(0, 1), metrics));
        Assert.Equal(new GridCell(0, 1), vm.GetCommittedCell(trash.Id));
        Assert.Equal(new GridCell(0, 0), vm.GetCommittedCell(app.Id));
    }

    [Fact]
    public void ReorderItem_ReflowsVerticallyWithinAColumn()
    {
        var vm = new DesktopViewModel();
        vm.ClearItems();

        var metrics = new DesktopGridMetrics("primary", new Rect(0, 0, 800, 600), 96.0, 100, 100);

        var itemA = new DesktopItemViewModel(new DesktopItemState("A", "primary", 0, 0, 0));
        var itemB = new DesktopItemViewModel(new DesktopItemState("B", "primary", 0, 1, 1));

        vm.AddItem(itemA);
        vm.AddItem(itemB);
        vm.CalculateLayout(metrics);

        var itemC = new DesktopItemViewModel(new DesktopItemState("C", "primary", 0, 2, 2));
        vm.AddItem(itemC);
        vm.CalculateLayout(metrics);

        // Reorder A from the top to the bottom of its column.
        bool reordered = vm.ReorderItem(itemA.Id, new GridCell(0, 2), metrics);
        Assert.True(reordered);

        // The other items move only up and down within column zero.
        Assert.Equal(new GridCell(0, 2), vm.GetCommittedCell(itemA.Id));
        Assert.Equal(new GridCell(0, 0), vm.GetCommittedCell(itemB.Id));
        Assert.Equal(new GridCell(0, 1), vm.GetCommittedCell(itemC.Id));
    }

    [Fact]
    public void ReorderItem_WrapsToTheNextColumnOnlyAfterFillingTheCurrentOne()
    {
        var vm = new DesktopViewModel();
        vm.ClearItems();

        var metrics = new DesktopGridMetrics("primary", new Rect(0, 0, 200, 300), 96.0, 100, 100);

        var itemA = new DesktopItemViewModel(new DesktopItemState("A", "primary", 0, 0, 0));
        var itemB = new DesktopItemViewModel(new DesktopItemState("B", "primary", 0, 1, 1));
        var itemC = new DesktopItemViewModel(new DesktopItemState("C", "primary", 0, 2, 2));
        var itemD = new DesktopItemViewModel(new DesktopItemState("D", "primary", 1, 0, 3));
        vm.AddItem(itemA);
        vm.AddItem(itemB);
        vm.AddItem(itemC);
        vm.AddItem(itemD);
        vm.CalculateLayout(metrics);

        // Moving D to the first slot pushes C across the full column boundary.
        bool reordered = vm.ReorderItem(itemD.Id, new GridCell(0, 0), metrics);
        Assert.True(reordered);
        Assert.Equal(new GridCell(0, 0), vm.GetCommittedCell(itemD.Id));
        Assert.Equal(new GridCell(0, 1), vm.GetCommittedCell(itemA.Id));
        Assert.Equal(new GridCell(0, 2), vm.GetCommittedCell(itemB.Id));
        Assert.Equal(new GridCell(1, 0), vm.GetCommittedCell(itemC.Id));
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

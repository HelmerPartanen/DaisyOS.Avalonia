using DaisyOS.Shell.Search;
using DaisyOS.Shell.Search.Indexing;
using DaisyOS.Shell.Search.Database;
using Xunit;

namespace DaisyOS.Tests;

public sealed class SearchServiceTests
{
    [Theory]
    [InlineData("Wáll_paper", "wall paper")]
    [InlineData("  Visual   Studio-Code ", "visual studio code")]
    public void NormalizerHandlesDiacriticsAndSeparators(string input, string expected) =>
        Assert.Equal(expected, FuzzyMatcher.Normalize(input));

    [Fact]
    public void FuzzyMatcherSupportsInitialismsAndPrefixes()
    {
        var bluetooth = new SearchItem("setting:bluetooth", SearchItemType.Setting, "Bluetooth", "Network settings", Keywords: "bt devices");
        Assert.True(FuzzyMatcher.Score("blue", bluetooth) > FuzzyMatcher.Score("bt", bluetooth));
        Assert.True(FuzzyMatcher.Score("bt", bluetooth) > 0);
    }

    [Theory]
    [InlineData("2 + 2", 4)]
    [InlineData("(12 + 8) * 3", 60)]
    [InlineData("sqrt(144)", 12)]
    [InlineData("2^10", 1024)]
    public void CalculatorEvaluatesSafeExpressions(string expression, double expected)
    {
        Assert.True(CalculatorParser.TryEvaluate(expression, out var actual));
        Assert.Equal(expected, actual, 8);
    }

    [Fact]
    public async Task ExactMatchRanksBeforeDescriptionMatch()
    {
        var service = new SearchService([
            new SearchItem("exact", SearchItemType.Setting, "Wallpaper", "Appearance"),
            new SearchItem("weak", SearchItemType.Setting, "Desktop", "Appearance", "Change wallpaper")]);
        var results = await service.SearchAsync("wallpaper", 10, CancellationToken.None);
        Assert.Equal("exact", results[0].Item.Id);
    }

    [Fact]
    public async Task SearchHonorsCancellation()
    {
        var service = new SearchService([]);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SearchAsync("test", 10, cancellation.Token));
    }

    [Fact]
    public void FuzzyMatcherReturnsMatchedNameRanges()
    {
        var match = FuzzyMatcher.Match("fox", new SearchItem("firefox", SearchItemType.Application, "Firefox", "Application"));
        Assert.True(match.IsMatch); Assert.Contains(match.HighlightRanges, range => range.Start == 4 && range.Length == 3);
    }

    [Fact]
    public async Task QuerySpecificUsagePromotesRepeatedSelectionWithoutBeatingExactMatch()
    {
        var root = Path.Combine(Path.GetTempPath(), $"daisy-usage-{Guid.NewGuid():N}"); Directory.CreateDirectory(root);
        try
        {
            await using var service = new SearchService([
                new SearchItem("files", SearchItemType.Application, "Files", "Application"),
                new SearchItem("firefox", SearchItemType.Application, "Firefox", "Application"),
                new SearchItem("fi", SearchItemType.Application, "Fi", "Application")],
                new SearchIndexCoordinator(new SearchIndexingOptions { IncludedPaths = [] }, Path.Combine(root, "index.db")));
            var initial = await service.SearchAsync("fi", 10, CancellationToken.None);
            var firefox = initial.Single(result => result.Item.Id == "firefox");
            for (var index = 0; index < 5; index++) await service.RecordSelectionAsync(firefox, CancellationToken.None);

            var learned = await service.SearchAsync("fi", 10, CancellationToken.None);
            Assert.Equal("fi", learned[0].Item.Id);
            Assert.True(learned.IndexOf(result => result.Item.Id == "firefox") < learned.IndexOf(result => result.Item.Id == "files"));
            Assert.NotNull(service.LastDiagnostics); Assert.Equal(3, service.LastDiagnostics!.ReturnedCount);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task EmptyQueryReturnsPinnedAndUsedItemsInsteadOfEntireCatalog()
    {
        await using var service = new SearchService([
            new SearchItem("pinned", SearchItemType.Application, "Pinned", "Application", IsPinned: true),
            new SearchItem("used", SearchItemType.Application, "Used", "Application"),
            new SearchItem("unused", SearchItemType.Application, "Unused", "Application")]);
        var used = (await service.SearchAsync("used", 10, CancellationToken.None)).Single(result => result.Item.Id == "used");
        await service.RecordSelectionAsync(used, CancellationToken.None);

        var suggestions = await service.SearchAsync("", 10, CancellationToken.None);
        Assert.Contains(suggestions, result => result.Item.Id == "pinned");
        Assert.Contains(suggestions, result => result.Item.Id == "used");
        Assert.DoesNotContain(suggestions, result => result.Item.Id == "unused");
    }

    [Theory]
    [InlineData("open Firefox", "firefox")]
    [InlineData("find wifi settings", "wifi")]
    public async Task NaturalLanguageCatalogQueriesIgnoreActionAndGenericWords(string query, string expectedId)
    {
        await using var service = new SearchService([
            new SearchItem("firefox", SearchItemType.Application, "Firefox", "Application"),
            new SearchItem("wifi", SearchItemType.Setting, "Wi-Fi", "Network settings", Keywords: "wifi wireless internet")]);
        var results = await service.SearchAsync(query, 10, CancellationToken.None);
        Assert.Equal(expectedId, results[0].Item.Id);
    }

    [Fact]
    public async Task FilenamePrefixRanksAboveContentOnlyMatch()
    {
        var root = Path.Combine(Path.GetTempPath(), $"daisy-ranking-{Guid.NewGuid():N}"); Directory.CreateDirectory(root);
        try
        {
            var coordinator = new SearchIndexCoordinator(new SearchIndexingOptions { IncludedPaths = [] }, Path.Combine(root, "index.db"));
            await coordinator.Repository.UpsertBatchAsync([
                new IndexedSearchItem(new SearchItem("gear", SearchItemType.File, "gear.png", "PNG file", Path: "/Downloads/gear.png", Keywords: "image png")),
                new IndexedSearchItem(new SearchItem("changelog", SearchItemType.File, "changelog.md", "MD file", "Updated gear rendering", "/Projects/changelog.md", Keywords: "document md"))
            ], CancellationToken.None);
            await using var service = new SearchService([], coordinator);

            var results = await service.SearchAsync("gear", 10, CancellationToken.None);
            Assert.Equal("gear", results[0].Item.Id);
            Assert.True(results[0].Score > results.Single(result => result.Item.Id == "changelog").Score);
        }
        finally { Directory.Delete(root, true); }
    }
}

file static class SearchResultListExtensions
{
    public static int IndexOf(this IReadOnlyList<SearchResult> results, Func<SearchResult, bool> predicate)
    {
        for (var index = 0; index < results.Count; index++) if (predicate(results[index])) return index;
        return -1;
    }
}

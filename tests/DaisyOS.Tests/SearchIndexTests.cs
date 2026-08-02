using DaisyOS.Shell.Search;
using DaisyOS.Shell.Search.Database;
using DaisyOS.Shell.Search.Indexing;
using Xunit;
using DaisyOS.Shell.Search.Extraction;
using SkiaSharp;
using DaisyOS.Shell.Search.Querying;
using DaisyOS.Shell.Search.Utilities;

namespace DaisyOS.Tests;

public sealed class SearchIndexTests
{
    [Fact]
    public async Task FtsIndexFindsAndDeletesMetadata()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            await using var database = new SearchDatabase(Path.Combine(root, "index.db"));
            var repository = new SearchRepository(database);
            var path = Path.Combine(root, "ProjectPlan.pdf");
            var item = new SearchItem($"fs:{path}", SearchItemType.File, "ProjectPlan.pdf", "PDF file", Path: path, Keywords: "pdf document");
            await repository.UpsertBatchAsync([new IndexedSearchItem(item, DateTimeOffset.UtcNow, 42, root)], CancellationToken.None);

            var results = await repository.SearchAsync("project", 10, CancellationToken.None);
            Assert.Single(results); Assert.Equal(item.Id, results[0].Item.Id);

            await repository.DeleteByPathAsync(path, CancellationToken.None);
            Assert.Empty(await repository.SearchAsync("project", 10, CancellationToken.None));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task FileIndexerSkipsExcludedAndHiddenDirectories()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Documents"));
            Directory.CreateDirectory(Path.Combine(root, "node_modules"));
            Directory.CreateDirectory(Path.Combine(root, ".secret"));
            await File.WriteAllTextAsync(Path.Combine(root, "Documents", "visible.txt"), "visible");
            await File.WriteAllTextAsync(Path.Combine(root, "node_modules", "package.js"), "excluded");
            await File.WriteAllTextAsync(Path.Combine(root, ".secret", "key"), "excluded");
            var provider = new FileSystemIndexProvider(new SearchIndexingOptions { IncludedPaths = [root] });
            var items = new List<IndexedSearchItem>();
            await foreach (var item in provider.EnumerateAsync(root, CancellationToken.None)) items.Add(item);

            Assert.Contains(items, item => item.Item.Name == "visible.txt");
            Assert.DoesNotContain(items, item => item.Item.Name is "package.js" or "key");
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("hello world", "\"hello\"* AND \"world\"*")]
    [InlineData("report' OR *", "\"report'\"* AND \"or\"*")]
    public void FtsQueryBuilderProducesLiteralPrefixTerms(string query, string expected) =>
        Assert.Equal(expected, SearchRepository.BuildFtsQuery(query));

    [Fact]
    public async Task OptInContentIndexingMakesPlainTextSearchable()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var document = Path.Combine(root, "notes.txt");
            await File.WriteAllTextAsync(document, "The quarterly aurora roadmap is ready.");
            var provider = new FileSystemIndexProvider(new SearchIndexingOptions { IncludedPaths = [root], IndexFileContents = true });
            var indexed = new List<IndexedSearchItem>();
            await foreach (var item in provider.EnumerateAsync(root, CancellationToken.None)) indexed.Add(item);
            await using var database = new SearchDatabase(Path.Combine(root, "content.db"));
            var repository = new SearchRepository(database); await repository.UpsertBatchAsync(indexed, CancellationToken.None);

            var results = await repository.SearchAsync("aurora", 10, CancellationToken.None);
            Assert.Contains(results, result => result.Item.Path == document);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ContentIndexingRejectsSensitiveAndOversizedText()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, ".env"), "PRIVATE_TOKEN=never-index-this");
            await File.WriteAllBytesAsync(Path.Combine(root, "large.txt"), new byte[1024 * 1024 + 1]);
            var provider = new FileSystemIndexProvider(new SearchIndexingOptions
            { IncludedPaths = [root], IncludeHiddenFiles = true, IndexFileContents = true, MaximumIndexedFileSize = 2 * 1024 * 1024 });
            var indexed = new List<IndexedSearchItem>();
            await foreach (var item in provider.EnumerateAsync(root, CancellationToken.None)) indexed.Add(item);

            Assert.DoesNotContain(indexed, item => item.Item.Description.Contains("never-index-this", StringComparison.Ordinal));
            Assert.DoesNotContain(indexed, item => item.Item.Description.Contains('\0'));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ImageMetadataExtractorReadsDimensionsWithoutDecodingIntoSearchContent()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "wide.png");
            using (var bitmap = new SKBitmap(40, 20))
            using (var image = SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 90))
            using (var stream = File.Create(path)) data.SaveTo(stream);
            var extracted = await new ImageMetadataExtractor().ExtractAsync(new FileInfo(path), CancellationToken.None);
            Assert.NotNull(extracted); Assert.Contains("40 × 20", extracted!.Description); Assert.Contains("landscape", extracted.Keywords);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PdfExtractorFailsClosedForMalformedDocuments()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "broken.pdf"); await File.WriteAllTextAsync(path, "not a pdf");
            Assert.Null(await new PdfTextContentExtractor().ExtractAsync(new FileInfo(path), CancellationToken.None));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("images from downlaods", "downloads")]
    [InlineData("show my photos in Downloads", "downloads")]
    public void NaturalLanguageQueryUnderstandsTypesFillersAndLocationTypos(string query, string location)
    {
        var plan = SearchQueryInterpreter.Interpret(query);
        Assert.Contains("image", plan.StrictFtsQuery); Assert.Contains(location, plan.StrictFtsQuery);
        Assert.DoesNotContain("from", plan.MeaningfulTerms); Assert.DoesNotContain("show", plan.MeaningfulTerms);
    }

    [Fact]
    public async Task NaturalLanguageImageSearchFindsMetadataInDownloads()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var downloads = Directory.CreateDirectory(Path.Combine(root, "Downloads"));
            var imagePath = Path.Combine(downloads.FullName, "summer-holiday.jpg"); await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
            var provider = new FileSystemIndexProvider(new SearchIndexingOptions { IncludedPaths = [root] });
            var indexed = new List<IndexedSearchItem>(); await foreach (var item in provider.EnumerateAsync(root, CancellationToken.None)) indexed.Add(item);
            await using var database = new SearchDatabase(Path.Combine(root, "natural.db")); var repository = new SearchRepository(database);
            await repository.UpsertBatchAsync(indexed, CancellationToken.None);

            var results = await repository.SearchAsync("image form downloads", 10, CancellationToken.None);
            Assert.Contains(results, result => result.Item.Path == imagePath);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void NaturalLanguageQueryUnderstandsModifiedToday()
    {
        var now = new DateTimeOffset(2026, 7, 13, 18, 0, 0, TimeSpan.FromHours(3));
        var plan = SearchQueryInterpreter.Interpret("documents modified today", now);
        Assert.Equal(now.Date, plan.ModifiedAfter); Assert.DoesNotContain("today", plan.MeaningfulTerms);
    }

    [Fact]
    public void NaturalLanguageQueryUnderstandsYesterdayAndLargeFiles()
    {
        var now = new DateTimeOffset(2026, 7, 13, 18, 0, 0, TimeSpan.FromHours(3));
        var yesterday = SearchQueryInterpreter.Interpret("files from yesterday", now);
        Assert.Equal(now.Date.AddDays(-1), yesterday.ModifiedAfter); Assert.Equal(now.Date, yesterday.ModifiedBefore);
        var large = SearchQueryInterpreter.Interpret("large files", now);
        Assert.Equal(100L * 1024 * 1024, large.MinimumSize); Assert.Contains("file", large.StrictFtsQuery);
    }

    [Fact]
    public async Task PrefixRecoveryFindsSingleWordTypingMistakes()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "summer-holiday.jpg"); await File.WriteAllBytesAsync(path, [1]);
            var provider = new FileSystemIndexProvider(new SearchIndexingOptions { IncludedPaths = [root] });
            var indexed = new List<IndexedSearchItem>(); await foreach (var item in provider.EnumerateAsync(root, CancellationToken.None)) indexed.Add(item);
            await using var database = new SearchDatabase(Path.Combine(root, "typo.db")); var repository = new SearchRepository(database);
            await repository.UpsertBatchAsync(indexed, CancellationToken.None);

            Assert.Contains(await repository.SearchAsync("sumemr", 10, CancellationToken.None), result => result.Item.Path == path);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task XdgUserDirectoriesResolveLocalizedDownloadsForIndexing()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var localizedDownloads = Directory.CreateDirectory(Path.Combine(root, "Lataukset"));
            var configuration = Path.Combine(root, "user-dirs.dirs");
            await File.WriteAllTextAsync(configuration, "XDG_DOWNLOAD_DIR=\"$HOME/Lataukset\"\n");
            var directories = XdgUserDirectories.Read(configuration, root);
            var resolved = XdgUserDirectories.Get(directories, "XDG_DOWNLOAD_DIR", Path.Combine(root, "Downloads"));
            Assert.Equal(localizedDownloads.FullName, resolved);

            var gear = Path.Combine(resolved, "gear.png"); await File.WriteAllBytesAsync(gear, [1]);
            var provider = new FileSystemIndexProvider(new SearchIndexingOptions { IncludedPaths = [resolved] });
            var indexed = new List<IndexedSearchItem>(); await foreach (var item in provider.EnumerateAsync(resolved, CancellationToken.None)) indexed.Add(item);
            await using var database = new SearchDatabase(Path.Combine(root, "xdg.db")); var repository = new SearchRepository(database);
            await repository.UpsertBatchAsync(indexed, CancellationToken.None);
            Assert.Contains(await repository.SearchAsync("gear", 10, CancellationToken.None), result => result.Item.Path == gear);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CompletedSourcesPersistUntilBackgroundReconciliation()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var file = Path.Combine(root, "persistent.txt"); await File.WriteAllTextAsync(file, "persistent index");
            await using var database = new SearchDatabase(Path.Combine(root, "persistent.db")); var repository = new SearchRepository(database);
            var options = new SearchIndexingOptions { IncludedPaths = [root], ExcludedPaths = [Path.Combine(root, "persistent.db")] };
            var builder = new InitialIndexBuilder(repository, new FileSystemIndexProvider(options), options);
            await builder.BuildAsync(CancellationToken.None);
            File.Delete(file);

            await builder.BuildAsync(CancellationToken.None);
            Assert.Contains(await repository.SearchAsync("persistent", 10, CancellationToken.None), result => result.Item.Path == file);
            await builder.BuildAsync(CancellationToken.None, forceReconciliation: true);
            Assert.DoesNotContain(await repository.SearchAsync("persistent", 10, CancellationToken.None), result => result.Item.Path == file);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ApplicationWatcherRefreshesCatalogAfterDesktopEntryChanges()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var completion = new TaskCompletionSource<IReadOnlyList<SearchItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
            IReadOnlyList<SearchItem> Reload() => Directory.EnumerateFiles(root, "*.desktop")
                .Select(path => new SearchItem($"app:{path}", SearchItemType.Application, Path.GetFileNameWithoutExtension(path), "Application")).ToArray();
            await using var watcher = new ApplicationCatalogWatcher(Reload, [root]);
            watcher.CatalogChanged += (_, catalog) => completion.TrySetResult(catalog); watcher.Start();
            await File.WriteAllTextAsync(Path.Combine(root, "new-app.desktop"), "[Desktop Entry]");

            var catalog = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains(catalog, item => item.Name == "new-app");
        }
        finally { Directory.Delete(root, true); }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"daisy-search-{Guid.NewGuid():N}"); Directory.CreateDirectory(path); return path;
    }
}

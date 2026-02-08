using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DateCreatedFixer;

public class FixDateCreatedTask : IScheduledTask
{
    private const int Concurrency = 16;
    private static readonly DateTime BadDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<FixDateCreatedTask> _logger;

    public FixDateCreatedTask(
        ILibraryManager libraryManager,
        ILogger<FixDateCreatedTask> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public string Name => "Fix DateCreated Values";

    public string Key => "DateCreatedFixer";

    public string Description => "Fixes items with invalid DateCreated (2000-01-01) by setting them to the file's LastWriteTimeUtc.";

    public string Category => "Library";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("DateCreatedFixer: Batch task starting");

        var query = new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Episode, BaseItemKind.Audio],
            Recursive = true,
        };

        var allItems = _libraryManager.GetItemList(query);

        // Pre-filter to only items with bad dates to avoid iterating the full library
        var itemsToFix = allItems
            .Where(item => item.DateCreated.Year <= 2000 && !string.IsNullOrEmpty(item.Path))
            .ToList();

        _logger.LogInformation(
            "DateCreatedFixer: Found {Total} total items, {ToFix} with bad dates to check",
            allItems.Count,
            itemsToFix.Count);

        int fixedCount = 0;
        int skippedCount = 0;
        int errorCount = 0;

        using var semaphore = new SemaphoreSlim(Concurrency);
        var tasks = new List<Task>();

        for (int i = 0; i < itemsToFix.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = itemsToFix[i];
            var index = i;

            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var fileInfo = new FileInfo(item.Path);
                    if (!fileInfo.Exists)
                    {
                        Interlocked.Increment(ref skippedCount);
                        return;
                    }

                    var lastWrite = fileInfo.LastWriteTimeUtc;
                    if (lastWrite <= BadDate || lastWrite > DateTime.UtcNow)
                    {
                        Interlocked.Increment(ref skippedCount);
                        return;
                    }

                    item.DateCreated = lastWrite;

                    await _libraryManager.UpdateItemAsync(
                        item,
                        item.GetParent(),
                        ItemUpdateType.MetadataEdit,
                        cancellationToken).ConfigureAwait(false);

                    var current = Interlocked.Increment(ref fixedCount);
                    if (current % 500 == 0)
                    {
                        progress.Report((double)index / itemsToFix.Count * 100);
                        _logger.LogInformation("DateCreatedFixer: Fixed {Count} items so far...", current);
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref errorCount);
                    _logger.LogWarning(ex, "DateCreatedFixer: Error fixing {ItemName}", item.Name);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        progress.Report(100);
        _logger.LogInformation(
            "DateCreatedFixer: Batch task completed. Fixed: {Fixed}, Skipped: {Skipped}, Errors: {Errors}",
            fixedCount,
            skippedCount,
            errorCount);
    }
}

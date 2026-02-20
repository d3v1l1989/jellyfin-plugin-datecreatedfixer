using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DateCreatedFixer;

public class DateCreatedFixerService : IHostedService, IDisposable
{
    private static readonly DateTime BadDateThreshold = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<DateCreatedFixerService> _logger;

    // Prevent re-entrant event loops: UpdateItemAsync fires ItemUpdated,
    // which would call this handler again. Track items currently being fixed.
    private readonly ConcurrentDictionary<Guid, byte> _processing = new();

    public DateCreatedFixerService(
        ILibraryManager libraryManager,
        ILogger<DateCreatedFixerService> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DateCreatedFixer: Subscribing to library events");
        _libraryManager.ItemAdded += OnItemChanged;
        _libraryManager.ItemUpdated += OnItemChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DateCreatedFixer: Unsubscribing from library events");
        _libraryManager.ItemAdded -= OnItemChanged;
        _libraryManager.ItemUpdated -= OnItemChanged;
        return Task.CompletedTask;
    }

    private void OnItemChanged(object? sender, ItemChangeEventArgs e)
    {
        var item = e.Item;

        if (item.DateCreated > BadDateThreshold)
        {
            return;
        }

        if (string.IsNullOrEmpty(item.Path))
        {
            return;
        }

        // Skip if this item is already being processed (prevents re-entrant loop)
        if (!_processing.TryAdd(item.Id, 0))
        {
            return;
        }

        try
        {
            var fileInfo = new FileInfo(item.Path);
            if (!fileInfo.Exists)
            {
                _processing.TryRemove(item.Id, out _);
                return;
            }

            var lastWrite = fileInfo.LastWriteTimeUtc;
            if (lastWrite <= BadDateThreshold || lastWrite > DateTime.UtcNow)
            {
                _processing.TryRemove(item.Id, out _);
                return;
            }

            _logger.LogInformation(
                "DateCreatedFixer: Fixing {ItemName} DateCreated from {OldDate} to {NewDate}",
                item.Name,
                item.DateCreated,
                lastWrite);

            item.DateCreated = lastWrite;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _libraryManager.UpdateItemAsync(
                        item,
                        item.GetParent(),
                        ItemUpdateType.MetadataEdit,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "DateCreatedFixer: Failed to save {ItemName}", item.Name);
                }
                finally
                {
                    _processing.TryRemove(item.Id, out _);
                }
            });
        }
        catch (Exception ex)
        {
            _processing.TryRemove(item.Id, out _);
            _logger.LogError(ex, "DateCreatedFixer: Error processing {ItemName}", item.Name);
        }
    }

    public void Dispose()
    {
        _processing.Clear();
        GC.SuppressFinalize(this);
    }
}

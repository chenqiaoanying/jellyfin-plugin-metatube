using System.Text;
using Jellyfin.Plugin.JavTube.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
#if __EMBY__
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Logging;

#else
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;
#endif

namespace Jellyfin.Plugin.JavTube.ScheduledTasks;

public class GenerateTrailersTask : IScheduledTask
{
    // Emby: trailers can be stored in a trailers sub-folder.
    // https://support.emby.media/support/solutions/articles/44001159193-trailers
    private const string TrailersFolder = "trailers";

    // Uniform suffix for all trailer files.
    private const string TrailerFileSuffix = "-Trailer.strm";

    // UTF-8 without BOM encoding.
    private readonly Encoding _utf8WithoutBom = new UTF8Encoding(false);

    private readonly ILibraryManager _libraryManager;
    private readonly ILogger _logger;

#if __EMBY__
    public GenerateTrailersTask(ILogManager logManager, ILibraryManager libraryManager)
    {
        _logger = logManager.CreateLogger<GenerateTrailersTask>();
        _libraryManager = libraryManager;
    }
#else
    public GenerateTrailersTask(ILogger<GenerateTrailersTask> logger, ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
    }
#endif

    public string Key => $"{Plugin.Instance.Name}GenerateTrailers";

    public string Name => "Generate Trailers";

    public string Description => $"Generate video trailers provided by {Plugin.Instance.Name} in library.";

    public string Category => Plugin.Instance.Name;

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfo.TriggerDaily,
            TimeOfDayTicks = TimeSpan.FromHours(1).Ticks
        };
    }

#if __EMBY__
    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
#else
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
#endif
    {
        // Stop the task if disabled.
        if (!Plugin.Instance.Configuration.EnableTrailers)
            return;

        await Task.Yield();

        progress?.Report(0);

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            MediaTypes = new[] { MediaType.Video },
#if __EMBY__
            HasAnyProviderId = new[] { Plugin.Instance.Name },
            IncludeItemTypes = new[] { nameof(Movie) },
#else
            HasAnyProviderId = new Dictionary<string, string> { { Plugin.Instance.Name, string.Empty } },
            IncludeItemTypes = new[] { BaseItemKind.Movie }
#endif
        }).ToList();

        foreach (var (idx, item) in items.WithIndex())
        {
            try
            {
                progress?.Report((double)idx / items.Count * 100);

                var trailersFolderPath = Path.Join(item.ContainingFolderPath, TrailersFolder);

                // Skip if contains .ignore file.
                if (File.Exists(Path.Join(trailersFolderPath, ".ignore")))
                    continue;

                var trailerUrl = item.GetTrailerUrl();

                // Skip if no remote trailers.
                if (string.IsNullOrWhiteSpace(trailerUrl))
                {
                    if (Directory.Exists(trailersFolderPath))
                    {
                        // Delete obsolete trailer files.
                        DeleteFiles(trailersFolderPath, $"*{TrailerFileSuffix}");

                        // Delete directory if empty.
                        DeleteDirectoryIfEmpty(trailersFolderPath);
                    }

                    continue;
                }

                var trailerFilePath = Path.Join(trailersFolderPath,
                    $"{item.Name.Split().First()}{TrailerFileSuffix}");

                // Skip if trailer file already exists.
                if (File.Exists(trailerFilePath))
                    continue;

                // Create trailers folder if not exists.
                if (!Directory.Exists(trailersFolderPath))
                    Directory.CreateDirectory(trailersFolderPath);

                // Delete other trailer files, if any.
                DeleteFiles(trailersFolderPath, $"*{TrailerFileSuffix}");

                _logger.Info("Generate trailer for video: {0}", item.Name);

                // Write .strm trailer file.
                await File.WriteAllTextAsync(trailerFilePath, trailerUrl, _utf8WithoutBom, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.Error("Generate trailer for video {0} error: {1}", item.Name, e.Message);
            }
        }

        progress?.Report(100);
    }

    private static void DeleteFiles(string path, string searchPattern)
    {
        foreach (var file in Directory.GetFiles(path, searchPattern))
            File.Delete(file);
    }

    private static void DeleteDirectoryIfEmpty(string path)
    {
        if (!Directory.GetDirectories(path).Any() && !Directory.GetFiles(path).Any())
            Directory.Delete(path);
    }
}
using System.Runtime.CompilerServices;
using Smartstore.Core.Content.Media;
using Smartstore.Core.Data;

namespace Smartstore.Split3D.Services;

/// <summary>
/// Tracks the picture of an addon (coming soon card) in the catalog album, so an uploaded picture
/// stays permanent and is not removed by the transient media cleanup.
/// </summary>
public class Split3DMediaTrackDetector : IMediaTrackDetector
{
    private readonly SmartDbContext _db;

    public Split3DMediaTrackDetector(SmartDbContext db)
    {
        _db = db;
    }

    public bool MatchAlbum(string albumName)
        => albumName == SystemAlbumProvider.Catalog;

    public void ConfigureTracks(string albumName, TrackedMediaPropertyTable table)
    {
        table.Register<Split3DAddon>(x => x.MediaFileId);
    }

    public async IAsyncEnumerable<MediaTrack> DetectAllTracksAsync(string albumName, [EnumeratorCancellation] CancellationToken cancelToken = default)
    {
        var addons = await _db.Split3DAddons().AsNoTracking()
            .Where(x => x.MediaFileId.HasValue)
            .Select(x => new { x.Id, x.MediaFileId })
            .ToListAsync(cancelToken);

        foreach (var x in addons)
        {
            yield return new MediaTrack
            {
                EntityId = x.Id,
                EntityName = nameof(Split3DAddon),
                MediaFileId = x.MediaFileId.Value,
                Property = nameof(Split3DAddon.MediaFileId)
            };
        }
    }
}

using SyncBridge.Core.Models;

namespace SyncBridge.Core.Interfaces;

/// <summary>
/// Core interface for all sync adapters
/// </summary>
public interface ISyncAdapter
{
    /// <summary>
    /// Gets the name of the adapter system
    /// </summary>
    string SystemName { get; }

    /// <summary>
    /// Gets changes since the specified timestamp
    /// </summary>
    /// <param name="since">Get changes after this timestamp</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of changes detected</returns>
    Task<IEnumerable<SyncChange>> GetChanges(DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific entity by ID
    /// </summary>
    /// <param name="id">Entity identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity if found, null otherwise</returns>
    Task<SyncEntity?> GetById(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates an entity
    /// </summary>
    /// <param name="entity">Entity to upsert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The upserted entity with updated ID and metadata</returns>
    Task<SyncEntity> Upsert(SyncEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the adapter with configuration
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task Initialize(CancellationToken cancellationToken = default);
}

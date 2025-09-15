using Azure.Core;

namespace Sandstorm.Core;

/// <summary>
/// Represents a cloud provider that can create and manage virtual machines for sandboxes
/// </summary>
public interface ICloudProvider
{
    /// <summary>
    /// Creates a new virtual machine instance for a sandbox
    /// </summary>
    /// <param name="config">Configuration for the sandbox VM</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created sandbox instance</returns>
    Task<ISandbox> CreateSandboxAsync(SandboxConfiguration config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an existing sandbox by its identifier
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The sandbox instance</returns>
    Task<ISandbox> GetSandboxAsync(string sandboxId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all active sandboxes
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of active sandboxes</returns>
    Task<IEnumerable<ISandbox>> ListSandboxesAsync(CancellationToken cancellationToken = default);
}
using Microsoft.Extensions.Logging;

namespace Sandstorm.Core;

/// <summary>
/// Main client for interacting with the Sandstorm platform
/// </summary>
public class SandstormClient
{
    private readonly ICloudProvider _cloudProvider;
    private readonly ILogger<SandstormClient>? _logger;

    /// <summary>
    /// Initializes a new instance of the SandstormClient
    /// </summary>
    /// <param name="cloudProvider">The cloud provider to use for creating sandboxes</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public SandstormClient(ICloudProvider cloudProvider, ILogger<SandstormClient>? logger = null)
    {
        _cloudProvider = cloudProvider ?? throw new ArgumentNullException(nameof(cloudProvider));
        _logger = logger;
    }

    /// <summary>
    /// Gets the sandboxes management interface
    /// </summary>
    public ISandboxManager Sandboxes => new SandboxManager(_cloudProvider, _logger);
}

/// <summary>
/// Interface for managing sandboxes
/// </summary>
public interface ISandboxManager
{
    /// <summary>
    /// Creates a new sandbox with default configuration
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created sandbox</returns>
    Task<ISandbox> CreateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new sandbox with the specified configuration
    /// </summary>
    /// <param name="configuration">Configuration for the sandbox</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created sandbox</returns>
    Task<ISandbox> CreateAsync(SandboxConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an existing sandbox by its identifier
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The sandbox instance</returns>
    Task<ISandbox> GetAsync(string sandboxId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all active sandboxes
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of active sandboxes</returns>
    Task<IEnumerable<ISandbox>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of ISandboxManager
/// </summary>
internal class SandboxManager : ISandboxManager
{
    private readonly ICloudProvider _cloudProvider;
    private readonly ILogger? _logger;

    public SandboxManager(ICloudProvider cloudProvider, ILogger? logger)
    {
        _cloudProvider = cloudProvider;
        _logger = logger;
    }

    public async Task<ISandbox> CreateAsync(CancellationToken cancellationToken = default)
    {
        return await CreateAsync(new SandboxConfiguration(), cancellationToken);
    }

    public async Task<ISandbox> CreateAsync(SandboxConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Creating sandbox with name: {SandboxName}", configuration.Name);
        
        var sandbox = await _cloudProvider.CreateSandboxAsync(configuration, cancellationToken);
        
        _logger?.LogInformation("Sandbox created with ID: {SandboxId}", sandbox.SandboxId);
        
        return sandbox;
    }

    public async Task<ISandbox> GetAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Getting sandbox with ID: {SandboxId}", sandboxId);
        
        return await _cloudProvider.GetSandboxAsync(sandboxId, cancellationToken);
    }

    public async Task<IEnumerable<ISandbox>> ListAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Listing all active sandboxes");
        
        return await _cloudProvider.ListSandboxesAsync(cancellationToken);
    }
}
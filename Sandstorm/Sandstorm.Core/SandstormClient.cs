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
    /// <param name="orchestratorEndpoint">The orchestrator endpoint for agent communication</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public SandstormClient(ICloudProvider cloudProvider, string orchestratorEndpoint = "http://localhost:5000", ILogger<SandstormClient>? logger = null)
    {
        _cloudProvider = cloudProvider ?? throw new ArgumentNullException(nameof(cloudProvider));
        OrchestratorEndpoint = orchestratorEndpoint ?? throw new ArgumentNullException(nameof(orchestratorEndpoint));
        _logger = logger;
    }

    /// <summary>
    /// The orchestrator endpoint for agent communication
    /// </summary>
    public string OrchestratorEndpoint { get; }

    /// <summary>
    /// Gets the sandboxes management interface
    /// </summary>
    public SandboxManager Sandboxes => new SandboxManager(_cloudProvider, OrchestratorEndpoint, _logger);
}

/// <summary>
/// Implementation of ISandboxManager
/// </summary>
public class SandboxManager
{
    private readonly ICloudProvider _cloudProvider;
    private readonly string _orchestratorEndpoint;
    private readonly ILogger? _logger;

    internal SandboxManager(ICloudProvider cloudProvider, string orchestratorEndpoint, ILogger? logger)
    {
        _cloudProvider = cloudProvider;
        _orchestratorEndpoint = orchestratorEndpoint;
        _logger = logger;
    }

    public async Task<ISandbox> CreateAsync(CancellationToken cancellationToken = default)
    {
        return await CreateAsync(new SandboxConfiguration(), cancellationToken);
    }

    public async Task<ISandbox> CreateAsync(SandboxConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Creating sandbox with name: {SandboxName}", configuration.Name);
        
        var sandbox = await _cloudProvider.CreateSandboxAsync(configuration, _orchestratorEndpoint, cancellationToken);
        
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
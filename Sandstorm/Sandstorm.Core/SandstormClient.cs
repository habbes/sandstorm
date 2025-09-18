using Microsoft.Extensions.Logging;
using Sandstorm.Core.Providers;
using Sandstorm.Core.Services;

namespace Sandstorm.Core;

/// <summary>
/// Main client for interacting with the Sandstorm platform
/// </summary>
public class SandstormClient
{
    private readonly ICloudProvider? _cloudProvider;
    private readonly SandstormHttpClient? _httpClient;
    private readonly ILogger<SandstormClient>? _logger;

    /// <summary>
    /// Initializes a new instance of the SandstormClient using a cloud provider (legacy mode)
    /// </summary>
    /// <param name="cloudProvider">The cloud provider to use for creating sandboxes</param>
    /// <param name="orchestratorEndpoint">The orchestrator endpoint for agent communication</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    [Obsolete("Use the constructor with apiEndpoint instead. This constructor will be removed in a future version.")]
    public SandstormClient(ICloudProvider cloudProvider, string orchestratorEndpoint = "http://localhost:5000", ILogger<SandstormClient>? logger = null)
    {
        _cloudProvider = cloudProvider ?? throw new ArgumentNullException(nameof(cloudProvider));
        OrchestratorEndpoint = orchestratorEndpoint ?? throw new ArgumentNullException(nameof(orchestratorEndpoint));
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the SandstormClient using the Sandstorm API service
    /// </summary>
    /// <param name="apiEndpoint">The Sandstorm API service endpoint</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public SandstormClient(string apiEndpoint, ILogger<SandstormClient>? logger = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiEndpoint);
        
        ApiEndpoint = apiEndpoint;
        _httpClient = new SandstormHttpClient(apiEndpoint, logger);
        _logger = logger;
        
        _logger?.LogInformation("SandstormClient initialized with API endpoint: {ApiEndpoint}", apiEndpoint);
    }

    /// <summary>
    /// The orchestrator endpoint for agent communication (legacy mode only)
    /// </summary>
    [Obsolete("Use ApiEndpoint instead. This property will be removed in a future version.")]
    public string? OrchestratorEndpoint { get; }

    /// <summary>
    /// The API endpoint for the Sandstorm service
    /// </summary>
    public string? ApiEndpoint { get; }

    /// <summary>
    /// Gets the sandboxes management interface
    /// </summary>
    public SandboxManager Sandboxes
    {
        get
        {
            if (_httpClient != null)
            {
                // New HTTP-based implementation
                var httpManager = new HttpSandboxManager(_httpClient, _logger);
                return new SandboxManager(httpManager, _logger);
            }
            else if (_cloudProvider != null && OrchestratorEndpoint != null)
            {
                // Legacy implementation
                return new SandboxManager(_cloudProvider, OrchestratorEndpoint, _logger);
            }
            else
            {
                throw new InvalidOperationException("SandstormClient not properly initialized");
            }
        }
    }
}

/// <summary>
/// Unified sandbox manager that works with both HTTP API and direct cloud provider
/// </summary>
public class SandboxManager
{
    private readonly ICloudProvider? _cloudProvider;
    private readonly HttpSandboxManager? _httpManager;
    private readonly string? _orchestratorEndpoint;
    private readonly ILogger? _logger;

    // Legacy constructor for cloud provider
    internal SandboxManager(ICloudProvider cloudProvider, string orchestratorEndpoint, ILogger? logger)
    {
        _cloudProvider = cloudProvider;
        _orchestratorEndpoint = orchestratorEndpoint;
        _logger = logger;
    }

    // New constructor for HTTP manager
    internal SandboxManager(HttpSandboxManager httpManager, ILogger? logger)
    {
        _httpManager = httpManager;
        _logger = logger;
    }

    public async Task<ISandbox> CreateAsync(CancellationToken cancellationToken = default)
    {
        return await CreateAsync(new SandboxConfiguration(), cancellationToken);
    }

    public async Task<ISandbox> CreateAsync(SandboxConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (_httpManager != null)
        {
            // Use HTTP API
            return await _httpManager.CreateAsync(configuration, cancellationToken);
        }
        else if (_cloudProvider != null && _orchestratorEndpoint != null)
        {
            // Legacy mode
            _logger?.LogInformation("Creating sandbox with name: {SandboxName}", configuration.Name);
            
            var sandbox = await _cloudProvider.CreateSandboxAsync(configuration, _orchestratorEndpoint, cancellationToken);
            
            _logger?.LogInformation("Sandbox created with ID: {SandboxId}", sandbox.SandboxId);
            
            return sandbox;
        }
        else
        {
            throw new InvalidOperationException("SandboxManager not properly initialized");
        }
    }

    public async Task<ISandbox> GetAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        if (_httpManager != null)
        {
            // Use HTTP API
            return await _httpManager.GetAsync(sandboxId, cancellationToken);
        }
        else if (_cloudProvider != null)
        {
            // Legacy mode
            _logger?.LogInformation("Getting sandbox with ID: {SandboxId}", sandboxId);
            
            return await _cloudProvider.GetSandboxAsync(sandboxId, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("SandboxManager not properly initialized");
        }
    }

    public async Task<IEnumerable<ISandbox>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (_httpManager != null)
        {
            // Use HTTP API
            return await _httpManager.ListAsync(cancellationToken);
        }
        else if (_cloudProvider != null)
        {
            // Legacy mode
            _logger?.LogInformation("Listing all active sandboxes");
            
            return await _cloudProvider.ListSandboxesAsync(cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("SandboxManager not properly initialized");
        }
    }
}
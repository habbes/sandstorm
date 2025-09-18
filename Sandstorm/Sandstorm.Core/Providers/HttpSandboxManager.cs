using Microsoft.Extensions.Logging;
using Sandstorm.Core.Services;

namespace Sandstorm.Core.Providers;

/// <summary>
/// HTTP-based implementation of sandbox management that communicates with the Sandstorm API
/// </summary>
internal class HttpSandboxManager
{
    private readonly SandstormHttpClient _httpClient;
    private readonly ILogger? _logger;

    public HttpSandboxManager(SandstormHttpClient httpClient, ILogger? logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    public async Task<ISandbox> CreateAsync(CancellationToken cancellationToken = default)
    {
        return await CreateAsync(new SandboxConfiguration(), cancellationToken);
    }

    public async Task<ISandbox> CreateAsync(SandboxConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Creating sandbox with configuration via API");
        
        var result = await _httpClient.CreateSandboxAsync(configuration, cancellationToken);
        
        // Get the full sandbox info
        var sandboxInfo = await _httpClient.GetSandboxAsync(result.Id, cancellationToken);
        
        _logger?.LogInformation("Sandbox created successfully via API: {SandboxId}", result.Id);
        
        return new HttpSandbox(_httpClient, sandboxInfo, _logger);
    }

    public async Task<ISandbox> GetAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Getting sandbox via API: {SandboxId}", sandboxId);
        
        var sandboxInfo = await _httpClient.GetSandboxAsync(sandboxId, cancellationToken);
        
        return new HttpSandbox(_httpClient, sandboxInfo, _logger);
    }

    public async Task<IEnumerable<ISandbox>> ListAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Listing sandboxes via API");
        
        var result = await _httpClient.ListSandboxesAsync(cancellationToken);
        
        var sandboxes = new List<ISandbox>();
        foreach (var summary in result.Sandboxes)
        {
            try
            {
                var sandboxInfo = await _httpClient.GetSandboxAsync(summary.Id, cancellationToken);
                sandboxes.Add(new HttpSandbox(_httpClient, sandboxInfo, _logger));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get details for sandbox: {SandboxId}", summary.Id);
                // Continue with other sandboxes
            }
        }
        
        return sandboxes;
    }
}
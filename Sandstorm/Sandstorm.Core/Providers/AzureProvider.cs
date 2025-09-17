using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Sandstorm.Core.Providers;

/// <summary>
/// Azure cloud provider implementation for creating sandboxes
/// </summary>
public class AzureProvider : ICloudProvider
{
    private readonly TokenCredential _credential;
    private readonly string _subscriptionId;
    private readonly string? _tenantId;
    private readonly ArmClient _armClient;
    private readonly ILogger<AzureProvider>? _logger;
    private readonly Dictionary<string, ISandbox> _sandboxes = new();

    /// <summary>
    /// Initializes a new instance of the Azure provider
    /// </summary>
    /// <param name="credential">Azure credentials</param>
    /// <param name="subscriptionId">Azure subscription ID</param>
    /// <param name="tenantId">Azure tenant ID (optional)</param>
    /// <param name="logger">Optional logger</param>
    public AzureProvider(TokenCredential credential, string subscriptionId, string? tenantId = null, ILogger<AzureProvider>? logger = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _subscriptionId = subscriptionId ?? throw new ArgumentNullException(nameof(subscriptionId));
        _tenantId = tenantId;
        _armClient = new ArmClient(_credential, _subscriptionId);
        _logger = logger;
    }

    /// <summary>
    /// Convenience constructor for client secret credentials
    /// </summary>
    /// <param name="tenantId">Azure tenant ID</param>
    /// <param name="clientId">Application client ID</param>
    /// <param name="clientSecret">Application client secret</param>
    /// <param name="subscriptionId">Azure subscription ID</param>
    /// <param name="logger">Optional logger</param>
    public AzureProvider(string tenantId, string clientId, string clientSecret, string subscriptionId, ILogger<AzureProvider>? logger = null)
        : this(new ClientSecretCredential(tenantId, clientId, clientSecret), subscriptionId, tenantId, logger)
    {
    }

    public async Task<ISandbox> CreateSandboxAsync(SandboxConfiguration config, string orchestratorEndpoint, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Creating Azure sandbox: {SandboxName}", config.Name);

        var resourceGroupName = config.ResourceGroupName ?? $"sandstorm-rg-s{config.Name}";
        var sandbox = new AzureSandbox(config, resourceGroupName, orchestratorEndpoint, _armClient, _logger);

        _sandboxes[sandbox.SandboxId] = sandbox;

        try
        {
            await sandbox.CreateAsync(cancellationToken);
            _logger?.LogInformation("Azure sandbox created successfully: {SandboxId}", sandbox.SandboxId);
            return sandbox;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create Azure sandbox: {SandboxName}", config.Name);
            _sandboxes.Remove(sandbox.SandboxId);
            throw;
        }
    }

    public Task<ISandbox> GetSandboxAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        if (_sandboxes.TryGetValue(sandboxId, out var sandbox))
        {
            return Task.FromResult(sandbox);
        }

        // Try to find the sandbox in Azure resources
        // This is a simplified implementation - in production you'd want to store metadata somewhere
        throw new ArgumentException($"Sandbox with ID '{sandboxId}' not found", nameof(sandboxId));
    }

    public Task<IEnumerable<ISandbox>> ListSandboxesAsync(CancellationToken cancellationToken = default)
    {
        // Return cached sandboxes for now
        // In production, you'd query Azure resources to find sandboxes
        return Task.FromResult<IEnumerable<ISandbox>>(_sandboxes.Values.ToList());
    }
}
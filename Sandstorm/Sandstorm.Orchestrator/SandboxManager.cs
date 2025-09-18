using Sandstorm.Core;
using Sandstorm.Orchestrator.Services;

namespace Sandstorm.Orchestrator;

public class SandboxManager
{
    private readonly ICloudProvider _cloudProvider;
    private readonly string _orchestratorEndpoint;
    private readonly ILogger? _logger;
    private readonly OrchestratorState _state;
    private bool _initialized = false;

    internal SandboxManager(OrchestratorState store, ICloudProvider cloudProvider, string orchestratorEndpoint, ILogger? logger)
    {
        _cloudProvider = cloudProvider;
        _orchestratorEndpoint = orchestratorEndpoint;
        _logger = logger;
        _state = store;
    }

    public async Task Initialize()
    {
        _initialized = true;
        _state.DefaultVmImageId = await _cloudProvider.CreateDaultImage(_orchestratorEndpoint);
    }

    public async Task<ISandbox> CreateAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitialized();
        return await CreateAsync(new SandboxConfiguration() { ImageId = _state.DefaultVmImageId }, cancellationToken);
    }

    public async Task<ISandbox> CreateAsync(SandboxConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Creating sandbox with name: {SandboxName}", configuration.Name);

        var sandbox = await _cloudProvider.CreateSandboxAsync(configuration, _orchestratorEndpoint, cancellationToken);
        _state.Sandboxes[sandbox.SandboxId] = sandbox;


        _logger?.LogInformation("Sandbox created with ID: {SandboxId}", sandbox.SandboxId);

        return sandbox;
    }

    public Task<ISandbox> GetAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Getting sandbox with ID: {SandboxId}", sandboxId);

        return Task.FromResult(_state.Sandboxes[sandboxId]);
    }

    public Task<IEnumerable<ISandbox>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_state.Sandboxes.Values.AsEnumerable());
    }

    public async Task EnsureInitialized()
    {
        if (!_initialized)
        {
            await Initialize();
        }
    }
}
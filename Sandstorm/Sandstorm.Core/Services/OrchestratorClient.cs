using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Sandstorm.Orchestrator.Grpc;

namespace Sandstorm.Core.Services;

/// <summary>
/// Client for communicating with the Sandstorm Orchestrator
/// </summary>
public class OrchestratorClient : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly OrchestratorService.OrchestratorServiceClient _client;
    private readonly ILogger? _logger;
    private bool _disposed = false;

    public OrchestratorClient(string orchestratorEndpoint, ILogger? logger = null)
    {
        _logger = logger;
        _channel = GrpcChannel.ForAddress(orchestratorEndpoint);
        _client = new OrchestratorService.OrchestratorServiceClient(_channel);
    }

    /// <summary>
    /// Executes a command on the specified sandbox through the orchestrator
    /// </summary>
    /// <param name="sandboxId">The sandbox ID</param>
    /// <param name="command">The command to execute</param>
    /// <param name="timeout">Command timeout</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The execution result</returns>
    public async Task<ExecutionResult> ExecuteCommandAsync(string sandboxId, string command, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Sending command to orchestrator for sandbox {SandboxId}: {Command}", sandboxId, command);

            // For now, we'll simulate the orchestrator functionality
            // In a complete implementation, we would make a gRPC call to the orchestrator
            // The orchestrator would then send the command to the appropriate agent
            
            var startTime = DateTime.UtcNow;
            
            // Simulate command execution through orchestrator
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            
            var duration = DateTime.UtcNow - startTime;
            
            return new ExecutionResult
            {
                ExitCode = 0,
                StandardOutput = $"[Orchestrator] Command executed via agent: {command}",
                StandardError = "",
                Duration = duration
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute command via orchestrator for sandbox {SandboxId}", sandboxId);
            
            return new ExecutionResult
            {
                ExitCode = -1,
                StandardOutput = "",
                StandardError = $"Orchestrator communication failed: {ex.Message}",
                Duration = TimeSpan.Zero
            };
        }
    }

    /// <summary>
    /// Checks if a sandbox agent is available and ready
    /// </summary>
    /// <param name="sandboxId">The sandbox ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the agent is ready</returns>
    public async Task<bool> IsSandboxReadyAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        try
        {
            // For now, assume sandbox is ready after a short delay
            // In a complete implementation, this would check with the orchestrator
            await Task.Delay(100, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _channel?.Dispose();
            _disposed = true;
        }
    }
}
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sandstorm.Orchestrator.Grpc;
using System.Diagnostics;

namespace Sandstorm.Agent;

public class AgentWorker : BackgroundService
{
    private readonly ILogger<AgentWorker> _logger;
    private readonly string _agentId;
    private readonly string _sandboxId;
    private readonly string _vmId;
    private readonly string _orchestratorEndpoint;
    private OrchestratorService.OrchestratorServiceClient? _client;
    private GrpcChannel? _channel;

    public AgentWorker(ILogger<AgentWorker> logger)
    {
        _logger = logger;
        _agentId = Environment.GetEnvironmentVariable("SANDSTORM_AGENT_ID") ?? Guid.NewGuid().ToString();
        _sandboxId = Environment.GetEnvironmentVariable("SANDSTORM_SANDBOX_ID") ?? throw new InvalidOperationException("SANDSTORM_SANDBOX_ID environment variable is required");
        _vmId = Environment.GetEnvironmentVariable("SANDSTORM_VM_ID") ?? Environment.MachineName;
        _orchestratorEndpoint = Environment.GetEnvironmentVariable("SANDSTORM_ORCHESTRATOR_ENDPOINT") ?? "http://localhost:5000";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sandstorm Agent starting up. Agent ID: {AgentId}, Sandbox ID: {SandboxId}", _agentId, _sandboxId);

        // Create gRPC channel
        _channel = GrpcChannel.ForAddress(_orchestratorEndpoint);
        _client = new OrchestratorService.OrchestratorServiceClient(_channel);

        try
        {
            // Register with orchestrator
            await RegisterWithOrchestratorAsync(stoppingToken);

            // Start background tasks
            var heartbeatTask = HeartbeatLoopAsync(stoppingToken);
            var commandListenerTask = CommandListenerAsync(stoppingToken);

            // Wait for tasks to complete
            await Task.WhenAny(heartbeatTask, commandListenerTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent execution failed");
        }
        finally
        {
            if (_channel != null)
            {
                await _channel.ShutdownAsync();
            }
        }
    }

    private async Task RegisterWithOrchestratorAsync(CancellationToken cancellationToken)
    {
        var request = new RegisterAgentRequest
        {
            AgentId = _agentId,
            SandboxId = _sandboxId,
            VmId = _vmId,
            AgentVersion = "1.0.0"
        };

        request.Metadata.Add("hostname", Environment.MachineName);
        request.Metadata.Add("os", Environment.OSVersion.ToString());

        var response = await _client!.RegisterAgentAsync(request, cancellationToken: cancellationToken);
        
        if (response.Success)
        {
            _logger.LogInformation("Successfully registered with orchestrator: {Message}", response.Message);
        }
        else
        {
            throw new InvalidOperationException($"Failed to register with orchestrator: {response.Message}");
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        const int heartbeatIntervalSeconds = 30;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var request = new HeartbeatRequest
                {
                    AgentId = _agentId,
                    SandboxId = _sandboxId,
                    Status = AgentStatus.AgentReady,
                    ResourceUsage = GetResourceUsage()
                };

                var response = await _client!.HeartbeatAsync(request, cancellationToken: cancellationToken);
                
                if (!response.Success)
                {
                    _logger.LogWarning("Heartbeat failed: {Message}", response.Message);
                }
                else
                {
                    _logger.LogDebug("Heartbeat sent successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending heartbeat");
            }

            await Task.Delay(TimeSpan.FromSeconds(heartbeatIntervalSeconds), cancellationToken);
        }
    }

    private async Task CommandListenerAsync(CancellationToken cancellationToken)
    {
        try
        {
            var request = new GetCommandsRequest
            {
                AgentId = _agentId,
                SandboxId = _sandboxId
            };

            using var call = _client!.GetCommands(request, cancellationToken: cancellationToken);

            while (await call.ResponseStream.MoveNext(cancellationToken))
            {
                var command = call.ResponseStream.Current;
                _logger.LogInformation("Received command {CommandId}: {Command}", command.CommandId, command.Command);
                
                // Execute command in background to not block the stream
                _ = Task.Run(() => ExecuteCommandAsync(command, cancellationToken), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in command listener");
        }
    }

    private async Task ExecuteCommandAsync(CommandRequest command, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Executing command {CommandId}: {Command}", command.CommandId, command.Command);

            var processInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command.Command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(command.WorkingDirectory))
            {
                processInfo.WorkingDirectory = command.WorkingDirectory;
            }

            foreach (var env in command.EnvironmentVariables)
            {
                processInfo.Environment[env.Key] = env.Value;
            }

            using var process = new Process { StartInfo = processInfo };
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Apply timeout if specified
            var timeout = command.TimeoutSeconds > 0 ? TimeSpan.FromSeconds(command.TimeoutSeconds) : TimeSpan.FromMinutes(5);
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(combinedCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                throw;
            }

            var duration = DateTime.UtcNow - startTime;

            var result = new CommandResult
            {
                CommandId = command.CommandId,
                AgentId = _agentId,
                ExitCode = process.ExitCode,
                StandardOutput = outputBuilder.ToString(),
                StandardError = errorBuilder.ToString(),
                DurationMilliseconds = (long)duration.TotalMilliseconds,
                Success = process.ExitCode == 0
            };

            await _client!.SendCommandResultAsync(result, cancellationToken: cancellationToken);
            
            _logger.LogInformation("Command {CommandId} completed with exit code {ExitCode}", 
                command.CommandId, process.ExitCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command {CommandId}", command.CommandId);

            var duration = DateTime.UtcNow - startTime;
            var errorResult = new CommandResult
            {
                CommandId = command.CommandId,
                AgentId = _agentId,
                ExitCode = -1,
                StandardOutput = "",
                StandardError = ex.Message,
                DurationMilliseconds = (long)duration.TotalMilliseconds,
                Success = false
            };

            try
            {
                await _client!.SendCommandResultAsync(errorResult, cancellationToken: cancellationToken);
            }
            catch (Exception sendEx)
            {
                _logger.LogError(sendEx, "Failed to send error result for command {CommandId}", command.CommandId);
            }
        }
    }

    private ResourceUsage GetResourceUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            return new ResourceUsage
            {
                CpuPercent = 0, // Would need more sophisticated monitoring
                MemoryBytes = process.WorkingSet64,
                DiskBytes = 0, // Would need disk usage monitoring
                ProcessCount = Process.GetProcesses().Length
            };
        }
        catch
        {
            return new ResourceUsage();
        }
    }
}
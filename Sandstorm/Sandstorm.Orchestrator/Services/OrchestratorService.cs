using Grpc.Core;
using Sandstorm.Orchestrator.Grpc;
using System.Collections.Concurrent;

namespace Sandstorm.Orchestrator.Services;

public class OrchestratorService : Grpc.OrchestratorService.OrchestratorServiceBase
{
    private readonly ILogger<OrchestratorService> _logger;
    private readonly ConcurrentDictionary<string, AgentConnection> _agents = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<CommandResult>> _pendingCommands = new();

    public OrchestratorService(ILogger<OrchestratorService> logger)
    {
        _logger = logger;
    }

    public override Task<RegisterAgentResponse> RegisterAgent(RegisterAgentRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Agent registration request from {AgentId} for sandbox {SandboxId}", 
            request.AgentId, request.SandboxId);

        var agent = new AgentConnection
        {
            AgentId = request.AgentId,
            SandboxId = request.SandboxId,
            VmId = request.VmId,
            AgentVersion = request.AgentVersion,
            LastHeartbeat = DateTime.UtcNow,
            Status = AgentStatus.AgentReady,
            CommandStream = null,
            Context = context
        };

        _agents.AddOrUpdate(request.AgentId, agent, (key, existing) =>
        {
            existing.LastHeartbeat = DateTime.UtcNow;
            existing.Status = AgentStatus.AgentReady;
            existing.Context = context;
            return existing;
        });

        _logger.LogInformation("Agent {AgentId} registered successfully", request.AgentId);

        return Task.FromResult(new RegisterAgentResponse
        {
            Success = true,
            Message = "Agent registered successfully",
            HeartbeatIntervalSeconds = 30
        });
    }

    public override Task<HeartbeatResponse> Heartbeat(HeartbeatRequest request, ServerCallContext context)
    {
        if (_agents.TryGetValue(request.AgentId, out var agent))
        {
            agent.LastHeartbeat = DateTime.UtcNow;
            agent.Status = request.Status;
            agent.ResourceUsage = request.ResourceUsage;

            _logger.LogDebug("Heartbeat received from agent {AgentId}", request.AgentId);
            
            return Task.FromResult(new HeartbeatResponse
            {
                Success = true,
                Message = "Heartbeat acknowledged"
            });
        }

        _logger.LogWarning("Heartbeat from unknown agent {AgentId}", request.AgentId);
        return Task.FromResult(new HeartbeatResponse
        {
            Success = false,
            Message = "Agent not registered"
        });
    }

    public override async Task GetCommands(GetCommandsRequest request, IServerStreamWriter<CommandRequest> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("Agent {AgentId} connected for command stream", request.AgentId);

        if (!_agents.TryGetValue(request.AgentId, out var agent))
        {
            _logger.LogWarning("Unknown agent {AgentId} attempting to get commands", request.AgentId);
            return;
        }

        agent.CommandStream = responseStream;

        try
        {
            // Keep the stream alive until the agent disconnects
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, context.CancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Command stream for agent {AgentId} cancelled", request.AgentId);
        }
        finally
        {
            if (_agents.TryGetValue(request.AgentId, out var agentToUpdate))
            {
                agentToUpdate.CommandStream = null;
            }
        }
    }

    public override Task<CommandResultResponse> SendCommandResult(CommandResult request, ServerCallContext context)
    {
        _logger.LogInformation("Command result received from agent {AgentId} for command {CommandId}", 
            request.AgentId, request.CommandId);

        if (_pendingCommands.TryRemove(request.CommandId, out var tcs))
        {
            tcs.SetResult(request);
        }

        return Task.FromResult(new CommandResultResponse
        {
            Success = true,
            Message = "Command result processed"
        });
    }

    public override async Task<LogResponse> SendLogs(IAsyncStreamReader<LogMessage> requestStream, ServerCallContext context)
    {
        await foreach (var logMessage in requestStream.ReadAllAsync())
        {
            var logLevel = ConvertLogLevel(logMessage.Level);
            _logger.Log(logLevel, "Agent {AgentId}: {Message}", logMessage.AgentId, logMessage.Message);
        }

        return new LogResponse
        {
            Success = true,
            Message = "Logs processed"
        };
    }

    // Public methods for sandbox operations
    public async Task<CommandResult?> ExecuteCommandAsync(string sandboxId, string command, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var agent = _agents.Values.FirstOrDefault(a => a.SandboxId == sandboxId && a.Status == AgentStatus.AgentReady);
        if (agent == null)
        {
            _logger.LogWarning("No ready agent found for sandbox {SandboxId}", sandboxId);
            return null;
        }

        var commandId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<CommandResult>();
        _pendingCommands[commandId] = tcs;

        var commandRequest = new CommandRequest
        {
            CommandId = commandId,
            Command = command,
            TimeoutSeconds = (int)timeout.TotalSeconds
        };

        try
        {
            if (agent.CommandStream != null)
            {
                await agent.CommandStream.WriteAsync(commandRequest);
                
                using var timeoutCts = new CancellationTokenSource(timeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                
                return await tcs.Task.WaitAsync(combinedCts.Token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command to agent {AgentId}", agent.AgentId);
            _pendingCommands.TryRemove(commandId, out _);
        }

        return null;
    }

    public IEnumerable<AgentConnection> GetActiveAgents()
    {
        return _agents.Values.Where(a => DateTime.UtcNow - a.LastHeartbeat < TimeSpan.FromMinutes(2));
    }

    private static Microsoft.Extensions.Logging.LogLevel ConvertLogLevel(Grpc.LogLevel grpcLogLevel)
    {
        return grpcLogLevel switch
        {
            Grpc.LogLevel.LogTrace => Microsoft.Extensions.Logging.LogLevel.Trace,
            Grpc.LogLevel.LogDebug => Microsoft.Extensions.Logging.LogLevel.Debug,
            Grpc.LogLevel.LogInfo => Microsoft.Extensions.Logging.LogLevel.Information,
            Grpc.LogLevel.LogWarn => Microsoft.Extensions.Logging.LogLevel.Warning,
            Grpc.LogLevel.LogError => Microsoft.Extensions.Logging.LogLevel.Error,
            Grpc.LogLevel.LogFatal => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };
    }
}

public class AgentConnection
{
    public string AgentId { get; set; } = string.Empty;
    public string SandboxId { get; set; } = string.Empty;
    public string VmId { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public DateTime LastHeartbeat { get; set; }
    public AgentStatus Status { get; set; }
    public ResourceUsage? ResourceUsage { get; set; }
    public IServerStreamWriter<CommandRequest>? CommandStream { get; set; }
    public ServerCallContext? Context { get; set; }
}
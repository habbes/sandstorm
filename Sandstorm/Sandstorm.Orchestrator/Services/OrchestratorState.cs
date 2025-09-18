using Sandstorm.Core;
using Sandstorm.Orchestrator.Grpc;
using System.Collections.Concurrent;

namespace Sandstorm.Orchestrator.Services;

public class OrchestratorState
{
    private readonly ConcurrentDictionary<string, AgentConnection> _agents = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<CommandResult>> _pendingCommands = new();
    

    public ConcurrentDictionary<string, AgentConnection> Agents => _agents;
    public ConcurrentDictionary<string, TaskCompletionSource<CommandResult>> PendingCommands => _pendingCommands;
    public ConcurrentDictionary<string, ISandbox> Sandboxes { get; } = new();

    public string DefaultVmImageId { get; set; }
}

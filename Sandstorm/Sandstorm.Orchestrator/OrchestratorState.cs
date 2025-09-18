using Sandstorm.Core;
using Sandstorm.Orchestrator.Grpc;
using Sandstorm.Orchestrator.Services;
using System.Collections.Concurrent;

namespace Sandstorm.Orchestrator;

public class OrchestratorState
{
    private readonly ConcurrentDictionary<string, AgentConnection> _agents = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<CommandResult>> _pendingCommands = new();
    

    public ConcurrentDictionary<string, AgentConnection> Agents => _agents;
    public ConcurrentDictionary<string, TaskCompletionSource<CommandResult>> PendingCommands => _pendingCommands;
    public ConcurrentDictionary<string, ISandbox> Sandboxes { get; } = new();
    public ConcurrentDictionary<(string SandboxId, string ProcessId), IProcess> Processes { get; } = new();

    public string DefaultVmImageId { get; set; }
}

using Grpc.Core;
using Sandstorm.Orchestrator.Grpc;

namespace Sandstorm.Orchestrator.Models;

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
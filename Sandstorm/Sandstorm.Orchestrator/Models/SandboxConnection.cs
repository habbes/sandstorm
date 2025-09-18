using Sandstorm.Core;

namespace Sandstorm.Orchestrator.Models;

public record class SandboxConnection(ISandbox Sandbox, AgentConnection Agent);
using Sandstorm.Core;

namespace Sandstorm.Orchestrator.Services.SandboxManagement;

record class CreateSandboxResult(string Id, SandboxStatus Status);

record class SendCommandRequest(string SandboxId, string Command);

record class SendCommandResult(string Pid, string Command);

record class GetCommandStatusRequest(string SandboxId, string Pid);
record class GetCommandStatusResult(string Pid, string Result, int ExitCode);
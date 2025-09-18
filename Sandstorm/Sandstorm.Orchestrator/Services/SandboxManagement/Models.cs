using Sandstorm.Core;

namespace Sandstorm.Orchestrator.Services.SandboxManagement;

// Sandbox management DTOs
public record CreateSandboxRequest(SandboxConfiguration? Configuration = null);
public record CreateSandboxResult(string Id, SandboxStatus Status);

public record GetSandboxResult(string Id, SandboxStatus Status, string? PublicIpAddress, SandboxConfiguration Configuration);

public record ListSandboxesResult(IEnumerable<SandboxSummary> Sandboxes);
public record SandboxSummary(string Id, SandboxStatus Status, string? PublicIpAddress, DateTimeOffset CreatedAt);

public record DeleteSandboxRequest(string SandboxId);

// Command management DTOs
public record SendCommandRequest(string SandboxId, string Command);
public record SendCommandResult(string ProcessId, string Command, bool IsRunning);

public record GetCommandStatusRequest(string SandboxId, string ProcessId);
public record GetCommandStatusResult(string ProcessId, bool IsRunning, ExecutionResult? Result);

public record GetCommandLogsRequest(string SandboxId, string ProcessId);
public record GetCommandLogsResult(IEnumerable<string> LogLines);

// Process management DTOs
public record TerminateProcessRequest(string SandboxId, string ProcessId);
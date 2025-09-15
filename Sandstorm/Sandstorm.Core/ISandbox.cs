namespace Sandstorm.Core;

/// <summary>
/// Represents a sandbox environment for executing code and commands
/// </summary>
public interface ISandbox
{
    /// <summary>
    /// The unique identifier for this sandbox
    /// </summary>
    string SandboxId { get; }

    /// <summary>
    /// The current status of the sandbox
    /// </summary>
    SandboxStatus Status { get; }

    /// <summary>
    /// The configuration used to create this sandbox
    /// </summary>
    SandboxConfiguration Configuration { get; }

    /// <summary>
    /// The public IP address of the sandbox (if available)
    /// </summary>
    string? PublicIpAddress { get; }

    /// <summary>
    /// Waits for the sandbox to be ready for use
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when sandbox is ready</returns>
    Task WaitForReadyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes C# code in the sandbox
    /// </summary>
    /// <param name="code">The C# code to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A process representing the running code</returns>
    Task<IProcess> RunCodeAsync(CSharpCode code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes Python code in the sandbox
    /// </summary>
    /// <param name="code">The Python code to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A process representing the running code</returns>
    Task<IProcess> RunCodeAsync(PythonCode code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes JavaScript code in the sandbox
    /// </summary>
    /// <param name="code">The JavaScript code to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A process representing the running code</returns>
    Task<IProcess> RunCodeAsync(JavaScriptCode code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a shell command in the sandbox
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A process representing the running command</returns>
    Task<IProcess> RunCommandAsync(ShellCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience method to execute a shell command with just a command string
    /// </summary>
    /// <param name="command">The command string to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A process representing the running command</returns>
    Task<IProcess> RunCommandAsync(string command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs an AI agent task in the sandbox
    /// </summary>
    /// <param name="task">The AI agent task configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A process representing the running agent</returns>
    Task<IProcess> RunAgentAsync(OpenAiAgentTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the sandbox and all associated resources
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about the sandbox
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sandbox information</returns>
    Task<SandboxInfo> GetInfoAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Status of a sandbox
/// </summary>
public enum SandboxStatus
{
    Creating,
    Starting,
    Ready,
    Stopping,
    Stopped,
    Deleted,
    Error
}

/// <summary>
/// Information about a sandbox
/// </summary>
public class SandboxInfo
{
    /// <summary>
    /// The sandbox identifier
    /// </summary>
    public required string SandboxId { get; set; }

    /// <summary>
    /// Current status
    /// </summary>
    public SandboxStatus Status { get; set; }

    /// <summary>
    /// When the sandbox was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Public IP address (if available)
    /// </summary>
    public string? PublicIpAddress { get; set; }

    /// <summary>
    /// Private IP address
    /// </summary>
    public string? PrivateIpAddress { get; set; }

    /// <summary>
    /// Resource usage information
    /// </summary>
    public ResourceUsage? ResourceUsage { get; set; }
}

/// <summary>
/// Resource usage information for a sandbox
/// </summary>
public class ResourceUsage
{
    /// <summary>
    /// CPU usage percentage
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Memory usage in MB
    /// </summary>
    public long MemoryUsageMB { get; set; }

    /// <summary>
    /// Disk usage in MB
    /// </summary>
    public long DiskUsageMB { get; set; }

    /// <summary>
    /// Network bytes sent
    /// </summary>
    public long NetworkBytesSent { get; set; }

    /// <summary>
    /// Network bytes received
    /// </summary>
    public long NetworkBytesReceived { get; set; }
}
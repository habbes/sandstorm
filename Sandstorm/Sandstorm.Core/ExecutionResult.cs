namespace Sandstorm.Core;

/// <summary>
/// Represents the result of a code or command execution
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// The exit code of the execution
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Standard output from the execution
    /// </summary>
    public string StandardOutput { get; set; } = string.Empty;

    /// <summary>
    /// Standard error from the execution
    /// </summary>
    public string StandardError { get; set; } = string.Empty;

    /// <summary>
    /// The duration of the execution
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Whether the execution was successful (exit code 0)
    /// </summary>
    public bool IsSuccess => ExitCode == 0;

    /// <summary>
    /// Any exception that occurred during execution
    /// </summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// Represents a running process in a sandbox
/// </summary>
public interface IProcess
{
    /// <summary>
    /// The unique identifier for this process
    /// </summary>
    string ProcessId { get; }

    /// <summary>
    /// Whether the process is still running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets a stream of log output from the process
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of log lines</returns>
    IAsyncEnumerable<string> GetLogStreamAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for the process to complete and returns the result
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The execution result</returns>
    Task<ExecutionResult> WaitForCompletionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates the process
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task TerminateAsync(CancellationToken cancellationToken = default);
}
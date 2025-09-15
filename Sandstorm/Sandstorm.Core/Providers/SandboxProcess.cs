using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Sandstorm.Core.Providers;

/// <summary>
/// Implementation of IProcess for Azure sandbox processes
/// </summary>
internal class SandboxProcess : IProcess
{
    private readonly string _processId;
    private readonly string _command;
    private readonly AzureSandbox _sandbox;
    private readonly ILogger? _logger;
    private ExecutionResult? _result;
    private bool _isRunning = true;
    private readonly List<string> _logLines = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public SandboxProcess(string processId, string command, AzureSandbox sandbox, ILogger? logger)
    {
        _processId = processId ?? throw new ArgumentNullException(nameof(processId));
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _sandbox = sandbox ?? throw new ArgumentNullException(nameof(sandbox));
        _logger = logger;
        
        // Start execution in background
        _ = Task.Run(ExecuteAsync);
    }

    public string ProcessId => _processId;
    public bool IsRunning => _isRunning;

    public async IAsyncEnumerable<string> GetLogStreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var lastIndex = 0;
        
        while (_isRunning || lastIndex < _logLines.Count)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            lock (_logLines)
            {
                while (lastIndex < _logLines.Count)
                {
                    yield return _logLines[lastIndex];
                    lastIndex++;
                }
            }

            if (_isRunning)
            {
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    public async Task<ExecutionResult> WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        while (_isRunning)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            await Task.Delay(100, cancellationToken);
        }

        return _result ?? new ExecutionResult
        {
            ExitCode = -1,
            StandardError = "Process did not complete properly",
            Duration = TimeSpan.Zero
        };
    }

    public async Task TerminateAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Terminating process: {ProcessId}", _processId);
        _cancellationTokenSource.Cancel();
        _isRunning = false;
        
        // In a real implementation, this would send a termination signal to the remote process
        await Task.CompletedTask;
    }

    private async Task ExecuteAsync()
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger?.LogInformation("Starting process execution: {ProcessId}", _processId);
            
            AddLogLine($"Starting execution of process {_processId}");
            AddLogLine($"Command: {_command}");
            
            // Execute the command on the sandbox
            _result = await _sandbox.ExecuteCommandAsync(_command, _cancellationTokenSource.Token);
            
            AddLogLine($"Process completed with exit code: {_result.ExitCode}");
            
            if (!string.IsNullOrEmpty(_result.StandardOutput))
            {
                foreach (var line in _result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    AddLogLine($"[STDOUT] {line}");
                }
            }
            
            if (!string.IsNullOrEmpty(_result.StandardError))
            {
                foreach (var line in _result.StandardError.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    AddLogLine($"[STDERR] {line}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _result = new ExecutionResult
            {
                ExitCode = -1,
                StandardError = "Process was cancelled",
                Duration = DateTime.UtcNow - startTime
            };
            AddLogLine("Process was cancelled");
        }
        catch (Exception ex)
        {
            _result = new ExecutionResult
            {
                ExitCode = -1,
                StandardError = ex.Message,
                Duration = DateTime.UtcNow - startTime,
                Exception = ex
            };
            AddLogLine($"Process failed with error: {ex.Message}");
            _logger?.LogError(ex, "Process execution failed: {ProcessId}", _processId);
        }
        finally
        {
            _isRunning = false;
            _logger?.LogInformation("Process execution completed: {ProcessId}", _processId);
        }
    }

    private void AddLogLine(string line)
    {
        lock (_logLines)
        {
            _logLines.Add($"[{DateTime.UtcNow:HH:mm:ss}] {line}");
        }
    }
}
using Microsoft.Extensions.Logging;
using Sandstorm.Core.Services;
using System.Runtime.CompilerServices;

namespace Sandstorm.Core.Providers;

/// <summary>
/// HTTP-based implementation of ISandbox that communicates with the Sandstorm API
/// </summary>
internal class HttpSandbox : ISandbox
{
    private readonly SandstormHttpClient _httpClient;
    private readonly ILogger? _logger;
    private GetSandboxResult _sandboxInfo;

    public HttpSandbox(SandstormHttpClient httpClient, GetSandboxResult sandboxInfo, ILogger? logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _sandboxInfo = sandboxInfo ?? throw new ArgumentNullException(nameof(sandboxInfo));
        _logger = logger;
    }

    public string SandboxId => _sandboxInfo.Id;
    public SandboxStatus Status => _sandboxInfo.Status;
    public string? PublicIpAddress => _sandboxInfo.PublicIpAddress;
    public SandboxConfiguration Configuration => _sandboxInfo.Configuration;

    public async Task WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Waiting for sandbox to be ready: {SandboxId}", SandboxId);
        
        while (Status != SandboxStatus.Ready && Status != SandboxStatus.Error && Status != SandboxStatus.Deleted)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            await Task.Delay(2000, cancellationToken);
            
            // Refresh sandbox status
            _sandboxInfo = await _httpClient.GetSandboxAsync(SandboxId, cancellationToken);
        }

        if (Status == SandboxStatus.Error)
        {
            throw new InvalidOperationException($"Sandbox {SandboxId} is in error state");
        }

        if (Status == SandboxStatus.Deleted)
        {
            throw new InvalidOperationException($"Sandbox {SandboxId} has been deleted");
        }

        _logger?.LogInformation("Sandbox is ready: {SandboxId}", SandboxId);
    }

    public async Task<IProcess> RunCodeAsync(CSharpCode code, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Running C# code in sandbox: {SandboxId}", SandboxId);
        
        // Convert C# code to shell command (simplified implementation)
        var command = $"echo '{code.Code}' > temp.cs && csc temp.cs && mono temp.exe";
        return await RunCommandAsync(command, cancellationToken);
    }

    public async Task<IProcess> RunCodeAsync(PythonCode code, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Running Python code in sandbox: {SandboxId}", SandboxId);
        
        // Convert Python code to shell command
        var command = $"python3 -c \"{code.Code.Replace("\"", "\\\"")}\"";
        return await RunCommandAsync(command, cancellationToken);
    }

    public async Task<IProcess> RunCodeAsync(JavaScriptCode code, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Running JavaScript code in sandbox: {SandboxId}", SandboxId);
        
        // Convert JavaScript code to shell command
        var command = $"node -e \"{code.Code.Replace("\"", "\\\"")}\"";
        return await RunCommandAsync(command, cancellationToken);
    }

    public async Task<IProcess> RunCommandAsync(ShellCommand command, CancellationToken cancellationToken = default)
    {
        return await RunCommandAsync(command.Command, cancellationToken);
    }

    public async Task<IProcess> RunCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Running command in sandbox {SandboxId}: {Command}", SandboxId, command);
        
        var result = await _httpClient.SendCommandAsync(SandboxId, command, cancellationToken);
        
        return new HttpProcess(result.ProcessId, command, SandboxId, _httpClient, _logger);
    }

    public async Task<IProcess> RunAgentAsync(OpenAiAgentTask task, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Running AI agent task in sandbox: {SandboxId}", SandboxId);
        
        // Convert agent task to shell command (simplified implementation)
        var command = $"echo 'AI Agent Task: {task.Prompt}'";
        return await RunCommandAsync(command, cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Deleting sandbox: {SandboxId}", SandboxId);
        
        await _httpClient.DeleteSandboxAsync(SandboxId, cancellationToken);
        
        _logger?.LogInformation("Sandbox deletion initiated: {SandboxId}", SandboxId);
    }

    public async Task<SandboxInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Getting sandbox info: {SandboxId}", SandboxId);
        
        // Refresh sandbox info
        _sandboxInfo = await _httpClient.GetSandboxAsync(SandboxId, cancellationToken);
        
        return new SandboxInfo
        {
            SandboxId = _sandboxInfo.Id,
            Status = _sandboxInfo.Status,
            CreatedAt = DateTimeOffset.UtcNow, // Note: We'd need to add this to the API response
            PublicIpAddress = _sandboxInfo.PublicIpAddress,
            PrivateIpAddress = null, // Not available from current API
            ResourceUsage = null // Not available from current API
        };
    }

    public async ValueTask DisposeAsync()
    {
        _logger?.LogInformation("Disposing sandbox: {SandboxId}", SandboxId);
        
        try
        {
            await DeleteAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete sandbox during disposal: {SandboxId}", SandboxId);
        }
    }
}

/// <summary>
/// HTTP-based implementation of IProcess
/// </summary>
internal class HttpProcess : IProcess
{
    private readonly SandstormHttpClient _httpClient;
    private readonly ILogger? _logger;

    public HttpProcess(string processId, string command, string sandboxId, SandstormHttpClient httpClient, ILogger? logger)
    {
        ProcessId = processId ?? throw new ArgumentNullException(nameof(processId));
        Command = command ?? throw new ArgumentNullException(nameof(command));
        SandboxId = sandboxId ?? throw new ArgumentNullException(nameof(sandboxId));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    public string ProcessId { get; }
    public string Command { get; }
    public string SandboxId { get; }

    public bool IsRunning => GetStatusAsync().GetAwaiter().GetResult().IsRunning;

    public async IAsyncEnumerable<string> GetLogStreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Streaming logs for process: {ProcessId}", ProcessId);
        
        var seenLogs = new HashSet<string>();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var (logs, status, error) = await TryGetLogsAndStatus(cancellationToken);

            if (error != null)
            {
                _logger?.LogError(error, "Error streaming logs for process: {ProcessId}", ProcessId);
                yield return $"[ERROR] Failed to get logs: {error.Message}";
                yield break;
            }

            if (logs != null)
            {
                foreach (var log in logs.LogLines)
                {
                    if (seenLogs.Add(log))
                    {
                        yield return log;
                    }
                }
            }

            if (status?.IsRunning == false)
            {
                break;
            }

            await Task.Delay(1000, cancellationToken);
        }
    }

    private async Task<(GetCommandLogsResult? logs, GetCommandStatusResult? status, Exception? error)> TryGetLogsAndStatus(CancellationToken cancellationToken)
    {
        try
        {
            var logs = await _httpClient.GetCommandLogsAsync(SandboxId, ProcessId, cancellationToken);
            var status = await _httpClient.GetCommandStatusAsync(SandboxId, ProcessId, cancellationToken);
            return (logs, status, null);
        }
        catch (OperationCanceledException)
        {
            return (null, null, null);
        }
        catch (Exception ex)
        {
            return (null, null, ex);
        }
    }

    public async Task<ExecutionResult> WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Waiting for process completion: {ProcessId}", ProcessId);
        
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            var status = await _httpClient.GetCommandStatusAsync(SandboxId, ProcessId, cancellationToken);
            
            if (!status.IsRunning && status.Result != null)
            {
                _logger?.LogInformation("Process completed: {ProcessId}", ProcessId);
                return status.Result;
            }

            await Task.Delay(1000, cancellationToken);
        }
    }

    public async Task TerminateAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Terminating process: {ProcessId}", ProcessId);
        
        await _httpClient.TerminateProcessAsync(SandboxId, ProcessId, cancellationToken);
        
        _logger?.LogInformation("Process terminated: {ProcessId}", ProcessId);
    }

    private async Task<GetCommandStatusResult> GetStatusAsync()
    {
        return await _httpClient.GetCommandStatusAsync(SandboxId, ProcessId);
    }
}
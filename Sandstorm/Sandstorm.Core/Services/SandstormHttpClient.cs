using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

namespace Sandstorm.Core.Services;

/// <summary>
/// HTTP client for communicating with the Sandstorm API service
/// </summary>
public class SandstormHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed = false;

    public SandstormHttpClient(string baseUrl, ILogger? logger = null)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Creates a new sandbox
    /// </summary>
    public async Task<CreateSandboxResult> CreateSandboxAsync(SandboxConfiguration? configuration = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Creating sandbox via API");
            
            var request = new CreateSandboxRequest(configuration);
            var response = await PostAsync<CreateSandboxRequest, CreateSandboxResult>("api/sandboxes", request, cancellationToken);
            
            _logger?.LogInformation("Sandbox created successfully: {SandboxId}", response.Id);
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create sandbox via API");
            throw;
        }
    }

    /// <summary>
    /// Gets sandbox information
    /// </summary>
    public async Task<GetSandboxResult> GetSandboxAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Getting sandbox via API: {SandboxId}", sandboxId);
            
            var response = await GetAsync<GetSandboxResult>($"api/sandboxes/{sandboxId}", cancellationToken);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get sandbox via API: {SandboxId}", sandboxId);
            throw;
        }
    }

    /// <summary>
    /// Lists all sandboxes
    /// </summary>
    public async Task<ListSandboxesResult> ListSandboxesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Listing sandboxes via API");
            
            var response = await GetAsync<ListSandboxesResult>("api/sandboxes", cancellationToken);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list sandboxes via API");
            throw;
        }
    }

    /// <summary>
    /// Deletes a sandbox
    /// </summary>
    public async Task DeleteSandboxAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Deleting sandbox via API: {SandboxId}", sandboxId);
            
            await DeleteAsync($"api/sandboxes/{sandboxId}", cancellationToken);
            
            _logger?.LogInformation("Sandbox deletion initiated: {SandboxId}", sandboxId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete sandbox via API: {SandboxId}", sandboxId);
            throw;
        }
    }

    /// <summary>
    /// Sends a command to a sandbox
    /// </summary>
    public async Task<SendCommandResult> SendCommandAsync(string sandboxId, string command, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Sending command to sandbox via API: {SandboxId}, {Command}", sandboxId, command);
            
            var request = new SendCommandRequest(sandboxId, command);
            var response = await PostAsync<SendCommandRequest, SendCommandResult>($"api/sandboxes/{sandboxId}/commands", request, cancellationToken);
            
            _logger?.LogInformation("Command sent successfully. ProcessId: {ProcessId}", response.ProcessId);
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send command to sandbox via API: {SandboxId}, {Command}", sandboxId, command);
            throw;
        }
    }

    /// <summary>
    /// Gets command status
    /// </summary>
    public async Task<GetCommandStatusResult> GetCommandStatusAsync(string sandboxId, string processId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Getting command status via API: {SandboxId}, {ProcessId}", sandboxId, processId);
            
            var response = await GetAsync<GetCommandStatusResult>($"api/sandboxes/{sandboxId}/commands/{processId}/status", cancellationToken);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get command status via API: {SandboxId}, {ProcessId}", sandboxId, processId);
            throw;
        }
    }

    /// <summary>
    /// Gets command logs
    /// </summary>
    public async Task<GetCommandLogsResult> GetCommandLogsAsync(string sandboxId, string processId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Getting command logs via API: {SandboxId}, {ProcessId}", sandboxId, processId);
            
            var response = await GetAsync<GetCommandLogsResult>($"api/sandboxes/{sandboxId}/commands/{processId}/logs", cancellationToken);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get command logs via API: {SandboxId}, {ProcessId}", sandboxId, processId);
            throw;
        }
    }

    /// <summary>
    /// Terminates a process
    /// </summary>
    public async Task TerminateProcessAsync(string sandboxId, string processId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Terminating process via API: {SandboxId}, {ProcessId}", sandboxId, processId);
            
            await DeleteAsync($"api/sandboxes/{sandboxId}/commands/{processId}", cancellationToken);
            
            _logger?.LogInformation("Process terminated: {ProcessId}", processId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to terminate process via API: {SandboxId}, {ProcessId}", sandboxId, processId);
            throw;
        }
    }

    private async Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        await EnsureSuccessStatusCode(response);
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions) 
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        await EnsureSuccessStatusCode(response);
        
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(responseJson, _jsonOptions) 
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    private async Task DeleteAsync(string endpoint, CancellationToken cancellationToken)
    {
        var response = await _httpClient.DeleteAsync(endpoint, cancellationToken);
        await EnsureSuccessStatusCode(response);
    }

    private static async Task EnsureSuccessStatusCode(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API request failed with status {response.StatusCode}: {content}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}

// DTOs for API communication - these should match the server-side DTOs
public record CreateSandboxRequest(SandboxConfiguration? Configuration = null);
public record CreateSandboxResult(string Id, SandboxStatus Status);

public record GetSandboxResult(string Id, SandboxStatus Status, string? PublicIpAddress, SandboxConfiguration Configuration);

public record ListSandboxesResult(IEnumerable<SandboxSummary> Sandboxes);
public record SandboxSummary(string Id, SandboxStatus Status, string? PublicIpAddress, DateTimeOffset CreatedAt);

public record SendCommandRequest(string SandboxId, string Command);
public record SendCommandResult(string ProcessId, string Command, bool IsRunning);

public record GetCommandStatusResult(string ProcessId, bool IsRunning, ExecutionResult? Result);
public record GetCommandLogsResult(IEnumerable<string> LogLines);
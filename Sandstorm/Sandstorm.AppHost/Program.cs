using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add the orchestration service
builder.Services.AddHostedService<LocalOrchestrationService>();

var host = builder.Build();

Console.WriteLine("=== Sandstorm Local Development Environment ===");
Console.WriteLine("");
Console.WriteLine("Starting services...");

await host.RunAsync();

/// <summary>
/// Service that orchestrates local development services similar to Aspire
/// </summary>
public class LocalOrchestrationService : BackgroundService
{
    private readonly ILogger<LocalOrchestrationService> _logger;
    private readonly List<Process> _processes = new();

    public LocalOrchestrationService(ILogger<LocalOrchestrationService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Start orchestrator service
            _logger.LogInformation("🚀 Starting orchestrator service...");
            var orchestratorProcess = StartProject("Sandstorm.Orchestrator", 
                new[] { "--urls", "http://localhost:5000" });
            _processes.Add(orchestratorProcess);

            // Wait for orchestrator to be ready
            await Task.Delay(3000, stoppingToken);
            
            if (orchestratorProcess.HasExited)
            {
                _logger.LogError("❌ Orchestrator failed to start");
                return;
            }

            _logger.LogInformation("✅ Orchestrator running on http://localhost:5000");

            // Wait a bit more to ensure gRPC is ready and test connectivity
            _logger.LogInformation("⏳ Waiting for orchestrator gRPC service to be ready...");
            await Task.Delay(2000, stoppingToken);
            
            // Test orchestrator connectivity
            await WaitForOrchestratorReady(stoppingToken);

            // Start agent service
            _logger.LogInformation("🤖 Starting agent service...");
            var agentProcess = StartProject("Sandstorm.Agent", 
                Array.Empty<string>(),
                new Dictionary<string, string>
                {
                    ["SANDSTORM_SANDBOX_ID"] = "demo-sandbox",
                    ["SANDSTORM_AGENT_ID"] = "demo-agent-01",
                    ["SANDSTORM_VM_ID"] = "demo-vm-01",
                    ["SANDSTORM_ORCHESTRATOR_ENDPOINT"] = "http://localhost:5000"
                });
            _processes.Add(agentProcess);

            await Task.Delay(2000, stoppingToken);
            _logger.LogInformation("✅ Agent service started");

            // Start sample application
            _logger.LogInformation("🎬 Starting sample application...");
            var sampleProcess = StartProject("Sandstorm.Sample", 
                Array.Empty<string>(),
                new Dictionary<string, string>
                {
                    ["CLIENT_ID"] = "demo-client-id",
                    ["CLIENT_SECRET"] = "demo-client-secret",
                    ["TENANT_ID"] = "demo-tenant-id",
                    ["SUBSCRIPTION_ID"] = "demo-subscription-id"
                });
            _processes.Add(sampleProcess);

            _logger.LogInformation("🎉 All services started successfully!");
            _logger.LogInformation("");
            _logger.LogInformation("Services:");
            _logger.LogInformation("  • Orchestrator: http://localhost:5000");
            _logger.LogInformation("  • Agent: Running and connected");
            _logger.LogInformation("  • Sample: Demonstrating SDK usage");
            _logger.LogInformation("");
            _logger.LogInformation("Press Ctrl+C to stop all services");

            // Wait for cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🧹 Stopping all services...");

        foreach (var process in _processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    await process.WaitForExitAsync(cancellationToken);
                }
                process.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping process");
            }
        }

        _processes.Clear();
        _logger.LogInformation("✅ All services stopped");

        await base.StopAsync(cancellationToken);
    }

    private Process StartProject(string projectName, string[]? args = null, Dictionary<string, string>? envVars = null)
    {
        var projectPath = Path.Combine("..", projectName);
        
        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project {projectPath}" + (args != null ? " -- " + string.Join(" ", args) : ""),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Add environment variables
        if (envVars != null)
        {
            foreach (var kvp in envVars)
            {
                processInfo.Environment[kvp.Key] = kvp.Value;
            }
        }

        var process = new Process { StartInfo = processInfo };
        
        // Log important output from the process, filtering out build warnings
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data) && !e.Data.Contains("warning CS"))
            {
                _logger.LogInformation("[{ProjectName}] {Data}", projectName, e.Data);
            }
        };
        
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data) && !e.Data.Contains("warning CS"))
            {
                _logger.LogError("[{ProjectName}] {Data}", projectName, e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private async Task WaitForOrchestratorReady(CancellationToken cancellationToken)
    {
        const int maxRetries = 10;
        const int delayMs = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                var response = await httpClient.GetAsync("http://localhost:5000", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Orchestrator gRPC service is ready");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Orchestrator not ready yet (attempt {Attempt}/{MaxRetries}): {Error}", 
                    i + 1, maxRetries, ex.Message);
            }

            if (i < maxRetries - 1)
            {
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        _logger.LogWarning("⚠️ Orchestrator may not be fully ready, but proceeding with agent startup");
    }
}

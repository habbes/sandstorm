using Sandstorm.Core;

namespace Sandstorm.Orchestrator.Services.SandboxManagement;

class SandboxManagementService(SandboxManager manager, ILogger<SandboxManagementService> logger)
{
    public async Task<CreateSandboxResult> CreateSandbox(CreateSandboxRequest? request = null)
    {
        try
        {
            logger.LogInformation("Creating sandbox with configuration: {@Configuration}", request?.Configuration);
            
            ISandbox sandbox;
            if (request?.Configuration != null)
            {
                sandbox = await manager.CreateAsync(request.Configuration);
            }
            else
            {
                sandbox = await manager.CreateAsync();
            }
            
            logger.LogInformation("Sandbox created successfully: {SandboxId}", sandbox.SandboxId);
            return new CreateSandboxResult(sandbox.SandboxId, sandbox.Status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create sandbox");
            throw;
        }
    }

    public async Task<GetSandboxResult> GetSandbox(string sandboxId)
    {
        try
        {
            logger.LogInformation("Getting sandbox: {SandboxId}", sandboxId);
            var sandbox = await manager.GetAsync(sandboxId);
            
            return new GetSandboxResult(
                sandbox.SandboxId, 
                sandbox.Status, 
                sandbox.PublicIpAddress, 
                sandbox.Configuration);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get sandbox: {SandboxId}", sandboxId);
            throw;
        }
    }

    public async Task<ListSandboxesResult> ListSandboxes()
    {
        try
        {
            logger.LogInformation("Listing all sandboxes");
            var sandboxes = await manager.ListAsync();
            
            var summaries = sandboxes.Select(s => new SandboxSummary(
                s.SandboxId, 
                s.Status, 
                s.PublicIpAddress, 
                DateTimeOffset.UtcNow // Note: We'd need to add CreatedAt to ISandbox interface for real implementation
            ));
            
            return new ListSandboxesResult(summaries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list sandboxes");
            throw;
        }
    }

    public async Task DeleteSandbox(string sandboxId)
    {
        try
        {
            logger.LogInformation("Deleting sandbox: {SandboxId}", sandboxId);
            var sandbox = await manager.GetAsync(sandboxId);
            
            // Queue deletion - run in background to avoid blocking the API call
            _ = Task.Run(async () =>
            {
                try
                {
                    await sandbox.DeleteAsync();
                    logger.LogInformation("Sandbox deleted successfully: {SandboxId}", sandboxId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to delete sandbox: {SandboxId}", sandboxId);
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initiate sandbox deletion: {SandboxId}", sandboxId);
            throw;
        }
    }

    public async Task<SendCommandResult> SendCommand(SendCommandRequest request)
    {
        try
        {
            logger.LogInformation("Sending command to sandbox {SandboxId}: {Command}", request.SandboxId, request.Command);
            
            var sandbox = await manager.GetAsync(request.SandboxId);
            var process = await sandbox.RunCommandAsync(new ShellCommand
            {
                Command = request.Command
            });

            manager.RegisterProcess(request.SandboxId, process);
            
            logger.LogInformation("Command sent successfully. ProcessId: {ProcessId}", process.ProcessId);
            return new SendCommandResult(process.ProcessId, request.Command, process.IsRunning);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send command to sandbox {SandboxId}: {Command}", request.SandboxId, request.Command);
            throw;
        }
    }

    public async Task<GetCommandStatusResult> GetCommandStatus(GetCommandStatusRequest request)
    {
        try
        {
            logger.LogInformation("Getting command status for sandbox {SandboxId}, process {ProcessId}", request.SandboxId, request.ProcessId);
            
            var process = manager.GetProcess(request.SandboxId, request.ProcessId);
            if (process == null)
            {
                throw new ArgumentException($"Process {request.ProcessId} not found in sandbox {request.SandboxId}");
            }

            ExecutionResult? result = null;
            if (!process.IsRunning)
            {
                // If process is completed, get the result
                result = await process.WaitForCompletionAsync();
            }

            return new GetCommandStatusResult(process.ProcessId, process.IsRunning, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get command status for sandbox {SandboxId}, process {ProcessId}", request.SandboxId, request.ProcessId);
            throw;
        }
    }

    public async Task<GetCommandLogsResult> GetCommandLogs(GetCommandLogsRequest request)
    {
        try
        {
            logger.LogInformation("Getting command logs for sandbox {SandboxId}, process {ProcessId}", request.SandboxId, request.ProcessId);
            
            var process = manager.GetProcess(request.SandboxId, request.ProcessId);
            if (process == null)
            {
                throw new ArgumentException($"Process {request.ProcessId} not found in sandbox {request.SandboxId}");
            }

            var logs = new List<string>();
            await foreach (var line in process.GetLogStreamAsync())
            {
                logs.Add(line);
            }

            return new GetCommandLogsResult(logs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get command logs for sandbox {SandboxId}, process {ProcessId}", request.SandboxId, request.ProcessId);
            throw;
        }
    }

    public async Task TerminateProcess(TerminateProcessRequest request)
    {
        try
        {
            logger.LogInformation("Terminating process {ProcessId} in sandbox {SandboxId}", request.ProcessId, request.SandboxId);
            
            var process = manager.GetProcess(request.SandboxId, request.ProcessId);
            if (process == null)
            {
                throw new ArgumentException($"Process {request.ProcessId} not found in sandbox {request.SandboxId}");
            }

            await process.TerminateAsync();
            logger.LogInformation("Process terminated successfully: {ProcessId}", request.ProcessId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to terminate process {ProcessId} in sandbox {SandboxId}", request.ProcessId, request.SandboxId);
            throw;
        }
    }
}

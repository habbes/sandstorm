using Sandstorm.Core;

namespace Sandstorm.Orchestrator.Services.SandboxManagement;

class SandboxManagementService(SandboxManager manager, ILogger<SandboxManagementService> logger)
{
    public async Task<CreateSandboxResult> CreateSandbox()
    {
        var sandbox = await manager.CreateAsync();
        return new CreateSandboxResult(sandbox.SandboxId, sandbox.Status);
    }

    public async Task<IProcess> SendCommand(SendCommandRequest request)
    {
        var sandbox = await manager.GetAsync(request.SandboxId);
        var result = await sandbox.RunCommandAsync(new ShellCommand
        {
            Command = request.Command
        });

        manager.RegisterProcess(sandbox.SandboxId, result);

        return result;
    }

    public async Task GetCommandStatus(GetCommandStatusRequest request)
    {
        manager.GetProcess(request.SandboxId, request.Pid);
    }

    public async Task DeleteSandbox(string sandboxId)
    {
        var sandbox = await manager.GetAsync(sandboxId);
        // TODO: queue deletion

        _ = Task.Run(() => sandbox.DeleteAsync());
    }
}

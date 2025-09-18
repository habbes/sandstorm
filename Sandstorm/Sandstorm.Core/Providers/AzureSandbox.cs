using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;
using Sandstorm.Core.Services;
using System.Diagnostics;
using System.Text;

namespace Sandstorm.Core.Providers;

/// <summary>
/// Azure implementation of ISandbox
/// </summary>
internal class AzureSandbox : ISandbox
{
    const bool CREATE_PUBLIC_IP = false;
    private readonly AzureSandboxConfiguration _configuration;
    private readonly SandboxConfiguration _baseConfiguration;
    private readonly string _resourceGroupName;
    private readonly string _orchestratorEndpoint;
    private readonly ArmClient _armClient;
    private readonly ILogger? _logger;
    private readonly string _sandboxId;

    private ResourceGroupResource? _resourceGroup;
    private VirtualMachineResource? _virtualMachine;
    private SandboxStatus _status = SandboxStatus.Creating;
    private string? _publicIpAddress;
    private OrchestratorClient? _orchestratorClient;
    private bool _disposed = false;

    public AzureSandbox(SandboxConfiguration configuration, string resourceGroupName, string orchestratorEndpoint, ArmClient armClient, ILogger? logger)
    {
        _baseConfiguration = configuration;
        _configuration = new AzureSandboxConfiguration { Name = configuration.Name, CustomImageId = configuration.ImageId };
        _resourceGroupName = resourceGroupName;
        _orchestratorEndpoint = orchestratorEndpoint;
        _armClient = armClient;
        _logger = logger;
        _sandboxId = configuration.Name;

        // Initialize orchestrator client
        _orchestratorClient = new OrchestratorClient(_orchestratorEndpoint, _logger);
    }

    public string SandboxId => _sandboxId;
    public SandboxStatus Status => _status;
    public SandboxConfiguration Configuration => _baseConfiguration;
    public string? PublicIpAddress => _publicIpAddress;

    public async Task CreateAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger?.LogInformation("Starting Azure VM creation for sandbox: {SandboxId}", _sandboxId);

        try
        {
            _status = SandboxStatus.Creating;

            // Create Resource Group
            _logger?.LogDebug("Creating resource group: {ResourceGroupName}", _resourceGroupName);
            var subscription = _armClient.GetDefaultSubscription();
            var resourceGroupData = new ResourceGroupData(AzureLocation.EastUS);

            resourceGroupData.Tags.Add("SandstormSandboxId", _sandboxId);
            resourceGroupData.Tags.Add("CreatedAt", DateTimeOffset.UtcNow.ToString("O"));

            var resourceGroupOperation = await subscription.GetResourceGroups()
                .CreateOrUpdateAsync(Azure.WaitUntil.Completed, _resourceGroupName, resourceGroupData, cancellationToken);
            _resourceGroup = resourceGroupOperation.Value;

            _logger?.LogDebug("Resource group created in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            // Create VM infrastructure in parallel
            var vmTask = CreateVirtualMachineAsync(cancellationToken);
            await vmTask;

            _status = SandboxStatus.Starting;
            _logger?.LogInformation("Azure VM created for sandbox {SandboxId} in {ElapsedMs}ms", _sandboxId, stopwatch.ElapsedMilliseconds);

            // Wait for VM to be running and accessible
            await WaitForVmReadyAsync(cancellationToken);

            _status = SandboxStatus.Ready;
            _logger?.LogInformation("Sandbox {SandboxId} is ready in {ElapsedMs}ms", _sandboxId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _status = SandboxStatus.Error;
            _logger?.LogError(ex, "Failed to create sandbox {SandboxId} after {ElapsedMs}ms", _sandboxId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task CreateVirtualMachineAsync(CancellationToken cancellationToken)
    {
        if (_resourceGroup == null) throw new InvalidOperationException("Resource group not created");

        var location = AzureLocation.WestUS2;
        var vmName = $"vm-{_configuration.Name}";

        // Create VNet
        var vnetData = new VirtualNetworkData()
        {
            Location = location,
            AddressPrefixes = { "10.0.0.0/16" },
            Subnets = { new SubnetData() { Name = "default", AddressPrefix = "10.0.0.0/24" } }
        };

        var vnetOperation = await _resourceGroup.GetVirtualNetworks()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"vnet-{_configuration.Name}", vnetData, cancellationToken);
        var vnet = vnetOperation.Value;

        PublicIPAddressResource? publicIp = null;
        if (CREATE_PUBLIC_IP)
        {// Create Public IP
            var publicIpData = new PublicIPAddressData()
            {
                Location = location,
                PublicIPAllocationMethod = NetworkIPAllocationMethod.Static,
                Sku = new PublicIPAddressSku() { Name = PublicIPAddressSkuName.Standard }
            };

            var publicIpOperation = await _resourceGroup.GetPublicIPAddresses()
                .CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"pip-{_configuration.Name}", publicIpData, cancellationToken);
            publicIp = publicIpOperation.Value;
        }

        // Create Network Security Group with SSH access
        NetworkSecurityGroupResource? nsg = null;

        if (CREATE_PUBLIC_IP)
        {
            var nsgData = new NetworkSecurityGroupData()
            {
                Location = location,
                SecurityRules =
            {
                new SecurityRuleData()
                {
                    Name = "SSH",
                    Protocol = SecurityRuleProtocol.Tcp,
                    SourcePortRange = "*",
                    DestinationPortRange = "22",
                    SourceAddressPrefix = "*",
                    DestinationAddressPrefix = "*",
                    Access = SecurityRuleAccess.Allow,
                    Priority = 300,
                    Direction = SecurityRuleDirection.Inbound
                }
            }
            };

            var nsgOperation = await _resourceGroup.GetNetworkSecurityGroups()
                .CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"nsg-{_configuration.Name}", nsgData, cancellationToken);
            nsg = nsgOperation.Value;
        }

        // Create Network Interface
        var nicData = new NetworkInterfaceData()
        {
            Location = location,
            IPConfigurations =
            {
                new NetworkInterfaceIPConfigurationData()
                {
                    Name = "ipconfig1",
                    PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                    Primary = true,
                    Subnet = new SubnetData() { Id = vnet.Data.Subnets.First().Id },
                    PublicIPAddress = publicIp != null ? new PublicIPAddressData() { Id = publicIp.Id } : null
                }
            },
            NetworkSecurityGroup = nsg != null ? new NetworkSecurityGroupData() { Id = nsg.Id } : null
        };

        var nicOperation = await _resourceGroup.GetNetworkInterfaces()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"nic-{_configuration.Name}", nicData, cancellationToken);
        var nic = nicOperation.Value;

        // Create VM with optimized settings for fast startup
        var vmData = new VirtualMachineData(location)
        {
            HardwareProfile = new VirtualMachineHardwareProfile()
            {
                VmSize = new VirtualMachineSizeType(_configuration.VmSize)
            },
            OSProfile = new VirtualMachineOSProfile()
            {
                AdminUsername = _configuration.AdminUsername,
                AdminPassword = _configuration.AdminPassword,
                ComputerName = vmName,
                LinuxConfiguration = new LinuxConfiguration()
                {
                    DisablePasswordAuthentication = false,
                    ProvisionVmAgent = true,
                }
            },
            NetworkProfile = new VirtualMachineNetworkProfile()
            {
                NetworkInterfaces =
                {
                    new VirtualMachineNetworkInterfaceReference()
                    {
                        Id = nic.Id,
                        Primary = true,
                    }
                }
            },
            StorageProfile = new VirtualMachineStorageProfile()
            {
                OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                {
                    Name = $"disk-{_configuration.Name}",
                    OSType = SupportedOperatingSystemType.Linux,
                    Caching = CachingType.ReadWrite,
                    ManagedDisk = new VirtualMachineManagedDisk()
                    {
                        StorageAccountType = StorageAccountType.PremiumLrs // Use SSD for faster startup
                    }
                },
                ImageReference = GetImageReference()
            }
        };

        // Install essential tools via cloud-init
        var cloudInitScript = GenerateCloudInitScript();
        vmData.OSProfile.CustomData = Convert.ToBase64String(Encoding.UTF8.GetBytes(cloudInitScript));

        var vmOperation = await _resourceGroup.GetVirtualMachines()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, vmName, vmData, cancellationToken);
        _virtualMachine = vmOperation.Value;

        if (CREATE_PUBLIC_IP)
        {
            var publicIpResource = await publicIp.GetAsync();
            _publicIpAddress = publicIpResource.Value.Data.IPAddress;
        }
    }

    private string GenerateCloudInitScript()
    {
        if (!string.IsNullOrEmpty(_configuration.CustomImageId))
        {
            // For custom images, we assume all dependencies are pre-installed
            // Only need to configure environment and start the agent
            return GenerateMinimalCloudInitScript();
        }
        else
        {
            // For base images, install everything from scratch
            return GenerateFullCloudInitScript();
        }
    }

    private string GenerateMinimalCloudInitScript()
    {
        return $@"#cloud-config
write_files:
  - path: /etc/environment
    content: |
      SANDSTORM_SANDBOX_ID={_sandboxId}
      SANDSTORM_VM_ID={Environment.MachineName}
      SANDSTORM_ORCHESTRATOR_ENDPOINT={_orchestratorEndpoint}
    append: true
runcmd:
  - echo 'Configuring Sandstorm Agent for sandbox {_sandboxId}' >> /var/log/sandbox-agent-install.log
  - systemctl daemon-reload
  - systemctl enable sandstorm-agent
  - systemctl start sandstorm-agent
  - echo 'Sandbox initialization complete' > /var/log/sandbox-ready.log
";
    }

    private string GenerateFullCloudInitScript()
    {
        return $@"#cloud-config
package_update: true
package_upgrade: true
packages:
  - python3
  - python3-pip
  - nodejs
  - npm
  - dotnet-sdk-8.0
  - curl
  - wget
  - git
  - vim
  - htop
  - unzip
write_files:
  - path: /etc/environment
    content: |
      SANDSTORM_SANDBOX_ID={_sandboxId}
      SANDSTORM_VM_ID={Environment.MachineName}
      SANDSTORM_ORCHESTRATOR_ENDPOINT={_orchestratorEndpoint}
    append: true
  - path: /opt/sandstorm/install-agent.sh
    permissions: '0755'
    content: |
      #!/bin/bash
      set -e
      
      echo ""Installing Sandstorm Agent...""
      
      # Create sandstorm user
      useradd -m -s /bin/bash sandstorm || true
      
      export HOME=/home/sandstorm
      export DOTNET_CLI_HOME=/home/sandstorm
      # Create directories
      mkdir -p /opt/sandstorm/agent
      mkdir -p /var/log/sandstorm
      
      # Install required packages
      apt-get update
      apt-get install -y git curl
      
      # Download and install .NET if not already present
      if ! command -v dotnet &> /dev/null; then
          echo 'Installing dotnet'
          curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
          export PATH=""$PATH:$HOME/.dotnet""
          echo 'export PATH=""$PATH:$HOME/.dotnet""' >> /etc/environment
      fi
      
      export DOTNET_CLI_HOME=$HOME
      
      echo ""HOME is $HOME""
      echo ""DOTNET_CLI_HOME is $DOTNET_CLI_HOME""
      echo ""cloning sandstorm""
      # Clone the repository and build the agent
      cd /tmp
      git clone https://github.com/habbes/sandstorm.git
      cd sandstorm/Sandstorm
      
      echo ""Building sandstorm agent""
      # Build the agent as AOT binary for Linux
      dotnet publish Sandstorm.Agent/Sandstorm.Agent.csproj \
          -c Release \
          -o /opt/sandstorm/agent \
          -r linux-x64
      
      echo ""Generating run-agent.sh script""
      # Create wrapper script for the service
      cat > /opt/sandstorm/agent/run-agent.sh << 'AGENT_EOF'
      #!/bin/bash
      source /etc/environment
      exec /opt/sandstorm/agent/Sandstorm.Agent
      AGENT_EOF
      
      chmod +x /opt/sandstorm/agent/run-agent.sh
      chmod +x /opt/sandstorm/agent/Sandstorm.Agent
      
      echo ""Generating sandstorm agent service""
      # Create systemd service
      cat > /etc/systemd/system/sandstorm-agent.service << 'SERVICE_EOF'
      [Unit]
      Description=Sandstorm Agent
      After=network.target
      
      [Service]
      Type=simple
      User=sandstorm
      WorkingDirectory=/opt/sandstorm/agent
      ExecStart=/opt/sandstorm/agent/run-agent.sh
      Restart=always
      RestartSec=5
      StandardOutput=journal
      StandardError=journal
      EnvironmentFile=/etc/environment
      
      [Install]
      WantedBy=multi-user.target
      SERVICE_EOF
      
      # Enable and start the service
      systemctl daemon-reload
      systemctl enable sandstorm-agent
      systemctl start sandstorm-agent
      
      echo ""Sandstorm Agent installation complete""
runcmd:
  - curl -fsSL https://get.docker.com -o get-docker.sh
  - sh get-docker.sh
  - usermod -aG docker ubuntu
  - systemctl enable docker
  - systemctl start docker
  - pip3 install --upgrade pip
  - echo 'Start sandbox agent installation' >> /var/log/sandbox-agent-install.log
  - /opt/sandstorm/install-agent.sh >> /var/log/sandbox-agent-install.log
  - echo 'Sandbox initialization complete' > /var/log/sandbox-ready.log
";
    }

    private async Task WaitForVmReadyAsync(CancellationToken cancellationToken)
    {
        // Wait for VM to be running
        var timeout = TimeSpan.FromMinutes(5);
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            try
            {
                if (_virtualMachine != null)
                {
                    var vmData = await _virtualMachine.GetAsync();
                    var instanceView = await _virtualMachine.InstanceViewAsync();

                    var powerState = instanceView.Value.Statuses?.FirstOrDefault(s => s.Code?.StartsWith("PowerState/") == true);
                    if (powerState?.Code == "PowerState/running")
                    {
                        _logger?.LogDebug("VM is running for sandbox: {SandboxId}", _sandboxId);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error checking VM status for sandbox: {SandboxId}", _sandboxId);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }

        throw new TimeoutException($"VM failed to start within {timeout.TotalMinutes} minutes");
    }

    private ImageReference GetImageReference()
    {
        if (!string.IsNullOrEmpty(_configuration.CustomImageId))
        {
            // Use custom pre-baked image
            _logger?.LogInformation("Using custom VM image: {CustomImageId}", _configuration.CustomImageId);
            return new ImageReference()
            {
                Id = new Azure.Core.ResourceIdentifier(_configuration.CustomImageId)
            };
        }
        else
        {
            // Use default Ubuntu image
            _logger?.LogInformation("Using default Ubuntu image");
            return new ImageReference()
            {
                Publisher = "Canonical",
                Offer = "0001-com-ubuntu-server-jammy",
                Sku = "22_04-lts-gen2",
                Version = "latest",
            };
        }
    }

    public async Task WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        // First wait for VM to be ready
        while (_status != SandboxStatus.Ready && _status != SandboxStatus.Error)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        if (_status == SandboxStatus.Error)
        {
            throw new InvalidOperationException("Sandbox failed to start");
        }

        // Then wait for agent to be connected to orchestrator
        if (_orchestratorClient != null)
        {
            _logger?.LogInformation("Waiting for agent to connect to orchestrator for sandbox {SandboxId}", _sandboxId);

            var timeout = DateTime.UtcNow.AddMinutes(60); // TODO: temporarily set high timeout for debugging
            bool agentReady = false;

            while (!agentReady && DateTime.UtcNow < timeout)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException();

                try
                {
                    agentReady = await _orchestratorClient.IsSandboxReadyAsync(_sandboxId, cancellationToken);
                    if (!agentReady)
                    {
                        _logger?.LogDebug("Agent not yet ready for sandbox {SandboxId}, waiting...", _sandboxId);
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug("Error checking agent readiness for sandbox {SandboxId}: {Error}", _sandboxId, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }

            if (!agentReady)
            {
                _logger?.LogWarning("Agent failed to connect to orchestrator within timeout for sandbox {SandboxId}", _sandboxId);
                throw new TimeoutException($"Agent failed to connect to orchestrator within 5 minutes for sandbox {_sandboxId}");
            }

            _logger?.LogInformation("Agent successfully connected to orchestrator for sandbox {SandboxId}", _sandboxId);
        }
    }

    public async Task<IProcess> RunCodeAsync(CSharpCode code, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        return new SandboxProcess($"csharp-{Guid.NewGuid():N}", GenerateCSharpScript(code), this, _logger);
    }

    public async Task<IProcess> RunCodeAsync(PythonCode code, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        return new SandboxProcess($"python-{Guid.NewGuid():N}", GeneratePythonScript(code), this, _logger);
    }

    public async Task<IProcess> RunCodeAsync(JavaScriptCode code, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        return new SandboxProcess($"javascript-{Guid.NewGuid():N}", GenerateJavaScriptScript(code), this, _logger);
    }

    public async Task<IProcess> RunCommandAsync(ShellCommand command, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        return new SandboxProcess($"command-{Guid.NewGuid():N}", command.Command, this, _logger);
    }

    public async Task<IProcess> RunCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        return await RunCommandAsync(new ShellCommand { Command = command }, cancellationToken);
    }

    public async Task<IProcess> RunAgentAsync(OpenAiAgentTask task, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        // This would implement AI agent execution - simplified for now
        var agentScript = $"echo 'Running AI agent with prompt: {task.Prompt} using model: {task.Model}'";
        return new SandboxProcess($"agent-{Guid.NewGuid():N}", agentScript, this, _logger);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        _status = SandboxStatus.Stopping;

        try
        {
            if (_resourceGroup != null)
            {
                _logger?.LogInformation("Deleting sandbox resources: {SandboxId}", _sandboxId);
                await _resourceGroup.DeleteAsync(Azure.WaitUntil.Completed);
                _status = SandboxStatus.Deleted;
                _logger?.LogInformation("Sandbox deleted: {SandboxId}", _sandboxId);
            }
        }
        catch (Exception ex)
        {
            _status = SandboxStatus.Error;
            _logger?.LogError(ex, "Failed to delete sandbox: {SandboxId}", _sandboxId);
            throw;
        }
    }

    public async Task<SandboxInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        return new SandboxInfo
        {
            SandboxId = _sandboxId,
            Status = _status,
            CreatedAt = DateTimeOffset.UtcNow, // Would track actual creation time
            PublicIpAddress = _publicIpAddress,
            // ResourceUsage would require additional monitoring setup
        };
    }

    private async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (_status != SandboxStatus.Ready)
        {
            await WaitForReadyAsync(cancellationToken);
        }
    }

    private string GenerateCSharpScript(CSharpCode code)
    {
        var script = new StringBuilder();
        script.AppendLine("#!/bin/bash");
        script.AppendLine("cd /tmp");
        script.AppendLine($"cat > code.cs << 'EOF'");
        script.AppendLine(code.Code);
        script.AppendLine("EOF");

        if (code.NuGetPackages.Any())
        {
            script.AppendLine("dotnet new console --force");
            foreach (var package in code.NuGetPackages)
            {
                script.AppendLine($"dotnet add package {package}");
            }
        }

        script.AppendLine("dotnet run code.cs");
        return script.ToString();
    }

    private string GeneratePythonScript(PythonCode code)
    {
        var script = new StringBuilder();
        script.AppendLine("#!/bin/bash");
        script.AppendLine("cd /tmp");

        if (code.PipPackages.Any())
        {
            script.AppendLine($"pip3 install {string.Join(" ", code.PipPackages)}");
        }

        script.AppendLine($"cat > code.py << 'EOF'");
        script.AppendLine(code.Code);
        script.AppendLine("EOF");
        script.AppendLine($"python{code.PythonVersion} code.py");
        return script.ToString();
    }

    private string GenerateJavaScriptScript(JavaScriptCode code)
    {
        var script = new StringBuilder();
        script.AppendLine("#!/bin/bash");
        script.AppendLine("cd /tmp");
        script.AppendLine("mkdir -p jsproject && cd jsproject");

        if (code.NpmPackages.Any())
        {
            script.AppendLine("npm init -y");
            script.AppendLine($"npm install {string.Join(" ", code.NpmPackages)}");
        }

        script.AppendLine($"cat > code.js << 'EOF'");
        script.AppendLine(code.Code);
        script.AppendLine("EOF");
        script.AppendLine("node code.js");
        return script.ToString();
    }

    internal async Task<ExecutionResult> ExecuteCommandAsync(string command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command cannot be null or empty", nameof(command));

        Debug.Assert(_orchestratorClient != null, "Orchestrator client should be initialized");
        return await ExecuteCommandViaOrchestratorAsync(command, cancellationToken);
    }

    private async Task<ExecutionResult> ExecuteCommandViaOrchestratorAsync(string command, CancellationToken cancellationToken)
    {
        if (_orchestratorClient == null)
            throw new InvalidOperationException("Orchestrator client not initialized");

        _logger?.LogDebug("Executing command via orchestrator for sandbox {SandboxId}: {Command}", _sandboxId, command);

        try
        {
            // Wait for agent to be ready
            var isReady = await _orchestratorClient.IsSandboxReadyAsync(_sandboxId, cancellationToken);
            if (!isReady)
            {
                return new ExecutionResult
                {
                    ExitCode = -1,
                    StandardOutput = "",
                    StandardError = "Sandbox agent is not ready",
                    Duration = TimeSpan.Zero
                };
            }

            // Execute command through orchestrator
            return await _orchestratorClient.ExecuteCommandAsync(_sandboxId, command, TimeSpan.FromMinutes(5), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute command via orchestrator for sandbox {SandboxId}: {Command}", _sandboxId, command);

            return new ExecutionResult
            {
                ExitCode = -1,
                StandardOutput = "",
                StandardError = $"Orchestrator execution failed: {ex.Message}",
                Duration = TimeSpan.Zero
            };
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Dispose orchestrator client
        _orchestratorClient?.Dispose();

        await DeleteAsync();
    }
}
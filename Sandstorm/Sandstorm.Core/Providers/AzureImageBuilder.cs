using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Sandstorm.Core.Providers;

/// <summary>
/// Utility for creating custom VM images with pre-installed Sandstorm dependencies
/// </summary>
public class AzureImageBuilder
{
    private readonly ArmClient _armClient;
    private readonly ILogger? _logger;

    public AzureImageBuilder(ArmClient armClient, ILogger? logger = null)
    {
        _armClient = armClient ?? throw new ArgumentNullException(nameof(armClient));
        _logger = logger;
    }

    /// <summary>
    /// Creates a custom VM image with all Sandstorm dependencies pre-installed
    /// </summary>
    /// <param name="resourceGroupName">Resource group for the image</param>
    /// <param name="imageName">Name for the custom image</param>
    /// <param name="region">Azure region</param>
    /// <param name="orchestratorEndpoint">Orchestrator endpoint to bake into the image</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The resource ID of the created image</returns>
    public async Task<string> CreateCustomImageAsync(
        string resourceGroupName,
        string imageName,
        string region = "eastus",
        string? orchestratorEndpoint = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting custom image creation: {ImageName} in {Region}", imageName, region);

        var location = new AzureLocation(region);
        var subscription = _armClient.GetDefaultSubscription();

        // Create resource group if it doesn't exist
        var resourceGroupData = new ResourceGroupData(location);
        var resourceGroupOperation = await subscription.GetResourceGroups()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, resourceGroupName, resourceGroupData, cancellationToken);
        var resourceGroup = resourceGroupOperation.Value;

        // Create a temporary VM to prepare the image
        var tempVmName = $"temp-{imageName}-{Guid.NewGuid():N}";
        _ = await CreateTempVmAsync(resourceGroup, tempVmName, location, orchestratorEndpoint, cancellationToken);

        try
        {
            // Wait for VM and setup to complete
            await WaitForVmSetupComplete(resourceGroup, tempVmName, cancellationToken);

            // Generalize and capture the VM as an image
            var imageId = await CaptureVmAsImageAsync(resourceGroup, tempVmName, imageName, location, cancellationToken);

            _logger?.LogInformation("Custom image created successfully: {ImageId}", imageId);
            return imageId;
        }
        finally
        {
            // Clean up temporary VM
            try
            {
                await CleanupTempVmAsync(resourceGroup, tempVmName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to cleanup temporary VM: {TempVmName}", tempVmName);
            }
        }
    }

    private async Task<string> CreateTempVmAsync(
        ResourceGroupResource resourceGroup,
        string vmName,
        AzureLocation location,
        string? orchestratorEndpoint,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Creating temporary VM for image preparation: {VmName}", vmName);

        // Create VNet
        var vnetData = new VirtualNetworkData()
        {
            Location = location,
            AddressPrefixes = { "10.0.0.0/16" },
            Subnets = { new SubnetData() { Name = "default", AddressPrefix = "10.0.0.0/24" } }
        };

        var vnetOperation = await resourceGroup.GetVirtualNetworks()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"vnet-{vmName}", vnetData, cancellationToken);
        var vnet = vnetOperation.Value;

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
                    Subnet = new SubnetData() { Id = vnet.Data.Subnets.First().Id }
                }
            }
        };

        var nicOperation = await resourceGroup.GetNetworkInterfaces()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"nic-{vmName}", nicData, cancellationToken);
        var nic = nicOperation.Value;

        // Create VM with setup script
        var vmData = new VirtualMachineData(location)
        {
            HardwareProfile = new VirtualMachineHardwareProfile()
            {
                VmSize = new VirtualMachineSizeType("Standard_B2s")
            },
            OSProfile = new VirtualMachineOSProfile()
            {
                AdminUsername = "azureuser",
                AdminPassword = GenerateRandomPassword(),
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
                    Name = $"disk-{vmName}",
                    OSType = SupportedOperatingSystemType.Linux,
                    Caching = CachingType.ReadWrite,
                    ManagedDisk = new VirtualMachineManagedDisk()
                    {
                        StorageAccountType = StorageAccountType.PremiumLrs
                    }
                },
                ImageReference = new ImageReference()
                {
                    Publisher = "Canonical",
                    Offer = "0001-com-ubuntu-server-jammy",
                    Sku = "22_04-lts-gen2",
                    Version = "latest",
                }
            }
        };

        // Add cloud-init script to prepare the image
        var setupScript = GenerateImageSetupScript(orchestratorEndpoint);
        vmData.OSProfile.CustomData = Convert.ToBase64String(Encoding.UTF8.GetBytes(setupScript));

        var vmOperation = await resourceGroup.GetVirtualMachines()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, vmName, vmData, cancellationToken);

        return vmOperation.Value.Id;
    }

    private string GenerateImageSetupScript(string? orchestratorEndpoint)
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
  - path: /opt/sandstorm/setup-image.sh
    permissions: '0755'
    content: |
      #!/bin/bash
      set -e
      
      echo ""Setting up Sandstorm custom image...""
      
      # Create sandstorm user
      useradd -m -s /bin/bash sandstorm || true
      
      export HOME=/home/sandstorm
      export DOTNET_CLI_HOME=/home/sandstorm
      
      # Create directories
      mkdir -p /opt/sandstorm/agent
      mkdir -p /var/log/sandstorm
      
      # Install Docker
      curl -fsSL https://get.docker.com -o get-docker.sh
      sh get-docker.sh
      usermod -aG docker ubuntu
      usermod -aG docker sandstorm
      systemctl enable docker
      systemctl start docker
      
      # Install additional Python packages
      pip3 install --upgrade pip
      
      # Download and install .NET if not already present
      if ! command -v dotnet &> /dev/null; then
          echo 'Installing dotnet'
          curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
          export PATH=""$PATH:$HOME/.dotnet""
          echo 'export PATH=""$PATH:$HOME/.dotnet""' >> /etc/environment
      fi
      
      export DOTNET_CLI_HOME=$HOME
      
      echo ""Cloning and building Sandstorm agent""
      # Clone the repository and build the agent
      cd /tmp
      git clone https://github.com/habbes/sandstorm.git
      cd sandstorm/Sandstorm
      
      # Build the agent as AOT binary for Linux
      dotnet publish Sandstorm.Agent/Sandstorm.Agent.csproj \
          -c Release \
          -o /opt/sandstorm/agent \
          -r linux-x64
      
      # Create wrapper script for the service
      cat > /opt/sandstorm/agent/run-agent.sh << 'AGENT_EOF'
      #!/bin/bash
      source /etc/environment
      exec /opt/sandstorm/agent/Sandstorm.Agent
      AGENT_EOF
      
      chmod +x /opt/sandstorm/agent/run-agent.sh
      chmod +x /opt/sandstorm/agent/Sandstorm.Agent
      
      # Create systemd service template
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
      
      systemctl daemon-reload
      
      # Clean up build artifacts
      rm -rf /tmp/sandstorm
      
      echo ""Image setup complete""
      echo ""Image ready"" > /var/log/image-setup-complete.log
runcmd:
  - /opt/sandstorm/setup-image.sh >> /var/log/image-setup.log 2>&1
";
    }

    private async Task WaitForVmSetupComplete(
        ResourceGroupResource resourceGroup,
        string vmName,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Waiting for VM setup to complete: {VmName}", vmName);

        var timeout = TimeSpan.FromMinutes(15); // Allow more time for image setup
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            try
            {
                var vm = await resourceGroup.GetVirtualMachineAsync(vmName);
                var instanceView = await vm.Value.InstanceViewAsync();

                var powerState = instanceView.Value.Statuses?.FirstOrDefault(s => s.Code?.StartsWith("PowerState/") == true);
                if (powerState?.Code == "PowerState/running")
                {
                    // VM is running, now check for setup completion marker
                    _logger?.LogDebug("VM is running, checking for setup completion...");
                    
                    if (await CheckSetupCompletionAsync(vm.Value, cancellationToken))
                    {
                        _logger?.LogInformation("Setup completed successfully");
                        return;
                    }
                    
                    _logger?.LogDebug("Setup still in progress, continuing to wait...");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error checking VM status: {VmName}", vmName);
            }

            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
        }

        throw new TimeoutException($"VM setup failed to complete within {timeout.TotalMinutes} minutes");
    }

    private async Task<bool> CheckSetupCompletionAsync(VirtualMachineResource vm, CancellationToken cancellationToken)
    {
        try
        {
            // Check if setup completion marker exists using VM Run Command
            var checkCommand = "test -f /var/log/image-setup-complete.log && echo 'READY' || echo 'NOT_READY'";
            
            var runCommandInput = new RunCommandInput("RunShellScript")
            {
                Script = { checkCommand }
            };

            var result = await vm.RunCommandAsync(
                Azure.WaitUntil.Completed,
                runCommandInput,
                cancellationToken);

            // Check the output for completion status
            if (result.HasValue && result.Value.Value != null)
            {
                var outputs = result.Value.Value;
                foreach (var output in outputs)
                {
                    if (output.Message?.Contains("READY") == true)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error checking setup completion status, assuming not ready");
            return false;
        }
    }

    private async Task<string> CaptureVmAsImageAsync(
        ResourceGroupResource resourceGroup,
        string vmName,
        string imageName,
        AzureLocation location,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Capturing VM as image: {VmName} -> {ImageName}", vmName, imageName);

        var vm = await resourceGroup.GetVirtualMachineAsync(vmName);

        // Deallocate and generalize the VM
        await vm.Value.DeallocateAsync(Azure.WaitUntil.Completed);
        await vm.Value.GeneralizeAsync();

        // Create managed image from generalized VM using direct Azure Resource Manager operation
        var subscriptionId = resourceGroup.Id.SubscriptionId;
        var resourceGroupName = resourceGroup.Data.Name;
        
        // Construct the image ID
        var imageResourceId = new ResourceIdentifier($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/images/{imageName}");
        
        // Create an HttpClient with Azure authentication for REST calls
        var credential = new Azure.Identity.DefaultAzureCredential();
        var httpClient = new HttpClient();
        
        // Get access token
        var tokenRequestContext = new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" });
        var accessToken = await credential.GetTokenAsync(tokenRequestContext, cancellationToken);
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
        
        // Prepare the managed image request body
        var imagePayload = new
        {
            location = location.ToString(),
            properties = new
            {
                sourceVirtualMachine = new
                {
                    id = vm.Value.Id.ToString()
                },
                hyperVGeneration = "V2"
            },
            tags = new
            {
                Source = "SandstormImageBuilder",
                CreatedBy = "Sandstorm"
            }
        };

        var jsonPayload = JsonSerializer.Serialize(imagePayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        // Make REST API call to create the managed image
        var apiUrl = $"https://management.azure.com{imageResourceId}?api-version=2023-03-01";
        var response = await httpClient.PutAsync(apiUrl, content, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            _logger?.LogInformation("Managed image creation initiated successfully");
            
            // Wait for the image creation to complete
            await WaitForImageCreationAsync(imageResourceId.ToString(), httpClient, cancellationToken);
            
            _logger?.LogInformation("Image captured with ID: {ImageId}", imageResourceId);
            httpClient.Dispose();
            return imageResourceId.ToString();
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            httpClient.Dispose();
            throw new InvalidOperationException($"Failed to create managed image. Status: {response.StatusCode}, Error: {errorContent}");
        }
    }

    private async Task WaitForImageCreationAsync(string imageResourceId, HttpClient httpClient, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMinutes(20);
        var start = DateTime.UtcNow;
        
        while (DateTime.UtcNow - start < timeout)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();
                
            try
            {
                var apiUrl = $"https://management.azure.com{imageResourceId}?api-version=2023-03-01";
                var response = await httpClient.GetAsync(apiUrl, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    if (responseText.Contains("\"provisioningState\":\"Succeeded\""))
                    {
                        _logger?.LogInformation("Image creation completed successfully");
                        return;
                    }
                    else if (responseText.Contains("\"provisioningState\":\"Failed\""))
                    {
                        throw new InvalidOperationException($"Image creation failed: {responseText}");
                    }
                    
                    _logger?.LogDebug("Image creation in progress...");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error checking image creation status");
            }
            
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }
        
        throw new TimeoutException($"Image creation did not complete within {timeout.TotalMinutes} minutes");
        
        throw new TimeoutException($"Image creation did not complete within {timeout.TotalMinutes} minutes");
    }

    private async Task CleanupTempVmAsync(
        ResourceGroupResource resourceGroup,
        string vmName,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Cleaning up temporary VM: {VmName}", vmName);

        try
        {
            // Delete VM
            var vm = await resourceGroup.GetVirtualMachineAsync(vmName);
            await vm.Value.DeleteAsync(Azure.WaitUntil.Completed);

            // Delete associated resources
            await resourceGroup.GetNetworkInterfaceAsync($"nic-{vmName}").Result.Value.DeleteAsync(Azure.WaitUntil.Completed);
            await resourceGroup.GetVirtualNetworkAsync($"vnet-{vmName}").Result.Value.DeleteAsync(Azure.WaitUntil.Completed);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error during cleanup of temporary VM: {VmName}", vmName);
        }
    }

    private static string GenerateRandomPassword()
    {
        var random = new Random();
        var chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var special = "!@#$%^&*";
        
        var password = new string(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray());
        password += special[random.Next(special.Length)];
        password += random.Next(10).ToString();
        
        return password;
    }
}
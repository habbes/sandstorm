using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Google.Protobuf.Reflection.SourceCodeInfo.Types;

namespace Sandstorm.Core.Providers;

internal class SandboxImageCreator(ArmClient _armClient, string _resourceGroupName, string _location)
{
    public async Task<string> CreateManagedImageAsync(string vmName, string imageName, string sandboxId, string orchestratorEndpoint)
    {
        var subscription = await _armClient.GetDefaultSubscriptionAsync();
        var rg = await subscription.GetResourceGroups().GetAsync(_resourceGroupName);


        var vnetData = new VirtualNetworkData()
        {
            Location = _location,
            AddressPrefixes = { "10.0.0.0/16" },
            Subnets = { new SubnetData() { Name = "default", AddressPrefix = "10.0.0.0/24" } }
        };

        var vnetOperation = await rg.Value.GetVirtualNetworks()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"vnet-sandstorm", vnetData);
        var vnet = vnetOperation.Value;

        var nicData = new NetworkInterfaceData()
        {
            Location = _location,
            IPConfigurations =
            {
                new NetworkInterfaceIPConfigurationData()
                {
                    Name = "ipconfig1",
                    PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                    Primary = true,
                    Subnet = new SubnetData() { Id = vnet.Data.Subnets.First().Id }
                }
            },
        };

        var nicOperation = await rg.Value.GetNetworkInterfaces()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"nic-sandstorm", nicData);
        var nic = nicOperation.Value;

        var vmCollection = rg.Value.GetVirtualMachines();
        var cloudInitScript = GetCloudInitScript(sandboxId, orchestratorEndpoint);

        var vmData = new VirtualMachineData(_location)
        {
            HardwareProfile = new VirtualMachineHardwareProfile
            {
                VmSize = VirtualMachineSizeType.StandardB2S
            },
            OSProfile = new VirtualMachineOSProfile
            {
                AdminUsername = "azureuser",
                AdminPassword = "YourStrongPassword123!",
                ComputerName = vmName,
                LinuxConfiguration = new LinuxConfiguration
                {
                    DisablePasswordAuthentication = false
                },
                CustomData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(cloudInitScript))
            },
            StorageProfile = new VirtualMachineStorageProfile
            {
                ImageReference = new ImageReference
                {
                    Publisher = "Canonical",
                    Offer = "UbuntuServer",
                    Sku = "22_04-lts",
                    Version = "latest"
                },
                OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                {
                    Name = $"{vmName}-osdisk",
                    Caching = CachingType.ReadWrite,
                    ManagedDisk = new VirtualMachineManagedDisk
                    {
                        StorageAccountType = StorageAccountType.StandardLrs
                    }
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
            }
        };

        var vmLro = await vmCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, vmName, vmData);
        var vm = vmLro.Value;

        Console.WriteLine($"Setup VM created: {vm.Id}");

        // 2. Wait for the agent service to signal readiness
        // Optionally, you can implement a polling mechanism checking a file or endpoint inside the VM
        Console.WriteLine("Waiting for sandbox-agent to finish initialization...");
        await Task.Delay(TimeSpan.FromMinutes(5)); // crude, replace with actual check if desired

        // 3. Generalize the VM
        Console.WriteLine("Generalizing VM...");
        await vm.GeneralizeAsync();

        // 4. Create Managed Image from the VM
        Console.WriteLine("Creating Managed Image...");
        var imageData = new DiskImageData(_location)
        {
            StorageProfile = new ImageStorageProfile
            {
                OSDisk = new ImageOSDisk(SupportedOperatingSystemType.Linux, OperatingSystemStateType.Generalized)
                {
                    ManagedDiskId = vm.Data.StorageProfile.OSDisk.ManagedDisk.Id
                }
            }
        };

        var imageCollection = rg.Value.GetDiskImages();
        var imageLro = await imageCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, imageName, imageData);
        var image = imageLro.Value;

        Console.WriteLine($"Managed Image created: {image.Id}");

        // 5. Delete the setup VM to save costs
        Console.WriteLine("Deleting setup VM...");
        await vm.DeleteAsync(Azure.WaitUntil.Completed);

        return image.Id;
    }

    private string GetCloudInitScript(string sandboxId, string orchestratorEndpoint)
    {
        var script = $@"#cloud-config
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
      SANDSTORM_SANDBOX_ID={sandboxId}
      SANDSTORM_VM_ID={Environment.MachineName}
      SANDSTORM_ORCHESTRATOR_ENDPOINT={orchestratorEndpoint}
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
        return script;
    }
}


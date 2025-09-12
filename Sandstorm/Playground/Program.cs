// See https://aka.ms/new-console-template for more information
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using dotenv.net;
using System.Diagnostics;






// experiment 1: create Azure VM and check how long it takes to provision and start.

DotEnv.Load(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 3));

// Based on: https://learn.microsoft.com/en-us/samples/azure-samples/azure-samples-net-management/compute-manage-vm-extension/

var clientId = Environment.GetEnvironmentVariable("CLIENT_ID") ?? throw new Exception("Client ID must be set in env vars");
var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET") ?? throw new Exception("Client secret must be set in env vars");
var tenantId = Environment.GetEnvironmentVariable("TENANT_ID") ?? throw new Exception("Tenant ID must be set in env vars");
var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID") ?? throw new Exception("Subscription ID must be set in env vars");

var AdminUsername = Environment.GetEnvironmentVariable("ADMIN_USERNAME") ?? throw new Exception("Admin username must be set in env vars");
var AdminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? throw new Exception("Admin password must be set in env vars");

ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
ArmClient client = new ArmClient(credential, subscription);


var sw = Stopwatch.StartNew();
// Create Resource Group
Console.WriteLine("--------Start create group--------");
var resourceGroupName = "SandstormTestRG";
String location = AzureLocation.WestUS2;

ResourceGroupResource resourceGroup = (await client.GetDefaultSubscription().GetResourceGroups().CreateOrUpdateAsync(Azure.WaitUntil.Completed, resourceGroupName, new ResourceGroupData(location))).Value;
Console.WriteLine("--------Finish create group--------");
Console.WriteLine($"Finished resource group creation after {sw.ElapsedMilliseconds} ms");

// Create a Virtual Machine
await CreateVmAsync(resourceGroup, resourceGroupName, location, "quickstartvm", AdminUsername, AdminPassword);

Console.WriteLine($"Finished VM creation after {sw.ElapsedMilliseconds} ms");

Console.WriteLine("Prease any key to delete the resource group");
Console.ReadKey();

sw.Restart();
await resourceGroup.DeleteAsync(Azure.WaitUntil.Completed);
Console.WriteLine($"Finished resource group deletion after {sw.ElapsedMilliseconds} ms");


static async Task CreateVmAsync(
            ResourceGroupResource resourcegroup,
            string resourceGroupName,
            string location,
            string vmName,
            string adminUsername,
            string adminPassword)
{
    var collection = resourcegroup.GetVirtualMachines();

    // Create VNet
    Console.WriteLine("--------Start create VNet--------");
    var vnetData = new VirtualNetworkData()
    {
        Location = location,
        AddressPrefixes = { "10.0.0.0/16" },
        Subnets = { new SubnetData() { Name = "SubnetSampleName", AddressPrefix = "10.0.0.0/28" } }
    };

    var vnetCollection = resourcegroup.GetVirtualNetworks();
    var vnet = (await vnetCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, "SampleVNet", vnetData)).Value;
    Console.WriteLine("--------Done create VNet--------");

    // Create Network Interface
    Console.WriteLine("--------Start create Network Interface--------");
    var nicData = new NetworkInterfaceData()
    {
        Location = location,
        IPConfigurations = {
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = "SampleIpConfigName",
                            PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                            Primary = false,
                            Subnet = new SubnetData()
                            {
                                Id = vnet.GetSubnets().ElementAt(0).Id
                            }
                        }
                    }
    };
    var nicCollection = resourcegroup.GetNetworkInterfaces();
    var nic = (await nicCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, "SampleNicName", nicData)).Value;

    // Create VM
    Console.WriteLine("--------Start create VM--------");
    var vmData = new VirtualMachineData(location)
    {
        HardwareProfile = new VirtualMachineHardwareProfile()
        {
            VmSize = VirtualMachineSizeType.StandardF2
        },
        OSProfile = new VirtualMachineOSProfile()
        {
            AdminUsername = adminUsername,
            AdminPassword = adminPassword,
            ComputerName = "linux Compute",
            LinuxConfiguration = new LinuxConfiguration()
            {
                DisablePasswordAuthentication = true,
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
                Name = "Sample",
                OSType = SupportedOperatingSystemType.Windows,
                Caching = CachingType.ReadWrite,
                ManagedDisk = new VirtualMachineManagedDisk()
                {
                    StorageAccountType = StorageAccountType.StandardLrs
                }
            },
            DataDisks =
                        {
                            new VirtualMachineDataDisk(1, DiskCreateOptionType.Empty)
                            {
                                DiskSizeGB = 100,
                                ManagedDisk = new VirtualMachineManagedDisk()
                                {
                                    StorageAccountType = StorageAccountType.StandardLrs
                                }
                            },
                            new VirtualMachineDataDisk(2, DiskCreateOptionType.Empty)
                            {
                                DiskSizeGB = 10,
                                Caching = CachingType.ReadWrite,
                                ManagedDisk = new VirtualMachineManagedDisk()
                                {
                                    StorageAccountType = StorageAccountType.StandardLrs
                                }
                            },
                        },
            ImageReference = new ImageReference()
            {
                Publisher = "MicrosoftWindowsServer",
                Offer = "WindowsServer",
                Sku = "2016-Datacenter",
                Version = "latest",
            }
        }
    };

    var resource = await collection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, vmName, vmData);
    Console.WriteLine("VM ID: " + resource.Id);
    Console.WriteLine("--------Done create VM--------");
}
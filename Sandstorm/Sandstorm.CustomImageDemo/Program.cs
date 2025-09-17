using Azure.Identity;
using Azure.ResourceManager;
using dotenv.net;
using Microsoft.Extensions.Logging;
using Sandstorm.Core;
using Sandstorm.Core.Providers;

// Load environment variables
DotEnv.Load(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 3));

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<Program>();

Console.WriteLine("=== Sandstorm Custom Image Demo ===");
Console.WriteLine();

// Example 1: Creating a custom image (commented out as it requires Azure credentials)
/*
var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? throw new Exception("Azure Client ID required");
var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? throw new Exception("Azure Client Secret required");
var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? throw new Exception("Azure Tenant ID required");
var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID") ?? throw new Exception("Azure Subscription ID required");

var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
var armClient = new ArmClient(credential, subscriptionId);

Console.WriteLine("Creating custom VM image with pre-installed dependencies...");
var imageBuilder = new AzureImageBuilder(armClient, logger);
var imageId = await imageBuilder.CreateCustomImageAsync(
    "sandstorm-images-rg", 
    "sandstorm-optimized-v1", 
    "westus2", 
    "https://your-orchestrator.example.com");
    
Console.WriteLine($"Custom image created: {imageId}");
Console.WriteLine();
*/

// Example 2: Using a custom image for fast sandbox creation
Console.WriteLine("Example: Using custom image for fast sandbox creation");
Console.WriteLine();

// Configuration with custom image
var config = new SandboxConfiguration
{
    Name = "fast-sandbox",
    VmSize = "Standard_B2s",
    Region = "westus2",
    // Use a pre-created custom image - this will start in ~10 seconds instead of ~5 minutes
    CustomImageId = "/subscriptions/your-sub/resourceGroups/sandstorm-images-rg/providers/Microsoft.Compute/images/sandstorm-optimized-v1"
};

Console.WriteLine("Sandbox Configuration:");
Console.WriteLine($"  Name: {config.Name}");
Console.WriteLine($"  VM Size: {config.VmSize}");
Console.WriteLine($"  Region: {config.Region}");
Console.WriteLine($"  Custom Image: {config.CustomImageId}");
Console.WriteLine();

Console.WriteLine("Benefits of using custom images:");
Console.WriteLine("✓ Startup time: ~10 seconds (vs 5+ minutes)");
Console.WriteLine("✓ All dependencies pre-installed (Docker, .NET, Node.js, Python)");
Console.WriteLine("✓ Sandstorm agent pre-built and ready");
Console.WriteLine("✓ Minimal cloud-init scripts");
Console.WriteLine("✓ Consistent environment across all VMs");
Console.WriteLine();

// Configuration without custom image (traditional approach)
var traditionalConfig = new SandboxConfiguration
{
    Name = "traditional-sandbox",
    VmSize = "Standard_B2s",
    Region = "westus2"
    // No CustomImageId - will use base Ubuntu image
};

Console.WriteLine("Traditional Configuration (for comparison):");
Console.WriteLine($"  Name: {traditionalConfig.Name}");
Console.WriteLine($"  VM Size: {traditionalConfig.VmSize}");
Console.WriteLine($"  Region: {traditionalConfig.Region}");
Console.WriteLine($"  Custom Image: {traditionalConfig.CustomImageId ?? "None (will use base Ubuntu)"}");
Console.WriteLine();

Console.WriteLine("Traditional approach characteristics:");
Console.WriteLine("⚠ Startup time: 5+ minutes");
Console.WriteLine("⚠ Installs dependencies via cloud-init every time");
Console.WriteLine("⚠ Downloads and builds Sandstorm agent from source");
Console.WriteLine("⚠ Network-dependent startup process");
Console.WriteLine();

Console.WriteLine("To create your own custom image, use the Sandstorm.ImageBuilder tool:");
Console.WriteLine("dotnet run --project Sandstorm.ImageBuilder -- <resource-group> <image-name> [orchestrator-endpoint] [region]");
Console.WriteLine();

return 0;
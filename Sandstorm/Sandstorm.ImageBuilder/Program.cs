using Azure.Identity;
using Azure.ResourceManager;
using dotenv.net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sandstorm.Core.Providers;

// Load environment variables
DotEnv.Load(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 3));

// Setup configuration
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<Program>();

// Get Azure credentials
var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? 
               Environment.GetEnvironmentVariable("CLIENT_ID") ?? 
               throw new Exception("Azure Client ID must be set in environment variables");
var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? 
                   Environment.GetEnvironmentVariable("CLIENT_SECRET") ?? 
                   throw new Exception("Azure Client Secret must be set in environment variables");
var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? 
               Environment.GetEnvironmentVariable("TENANT_ID") ?? 
               throw new Exception("Azure Tenant ID must be set in environment variables");
var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID") ?? 
                     Environment.GetEnvironmentVariable("SUBSCRIPTION_ID") ?? 
                     throw new Exception("Azure Subscription ID must be set in environment variables");

logger.LogInformation("Starting Sandstorm Custom Image Builder");

try
{
    // Parse command line arguments
    var commandArgs = Environment.GetCommandLineArgs();
    if (commandArgs.Length < 3)
    {
        Console.WriteLine("Usage: Sandstorm.ImageBuilder <resource-group-name> <image-name> [orchestrator-endpoint] [region]");
        Console.WriteLine("Example: Sandstorm.ImageBuilder my-rg sandstorm-custom-image https://orchestrator.example.com westus2");
        return 1;
    }

    var resourceGroupName = commandArgs[1];
    var imageName = commandArgs[2];
    var orchestratorEndpoint = commandArgs.Length > 3 ? commandArgs[3] : null;
    var region = commandArgs.Length > 4 ? commandArgs[4] : "westus2";

    logger.LogInformation("Creating custom image:");
    logger.LogInformation("  Resource Group: {ResourceGroup}", resourceGroupName);
    logger.LogInformation("  Image Name: {ImageName}", imageName);
    logger.LogInformation("  Orchestrator Endpoint: {Endpoint}", orchestratorEndpoint ?? "None");
    logger.LogInformation("  Region: {Region}", region);

    // Create Azure ARM client
    var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
    var armClient = new ArmClient(credential, subscriptionId);

    // Create image builder
    var imageBuilder = new AzureImageBuilder(armClient, logger);

    // Create the custom image
    var imageId = await imageBuilder.CreateCustomImageAsync(
        resourceGroupName, 
        imageName, 
        region, 
        orchestratorEndpoint);

    logger.LogInformation("Custom image created successfully!");
    logger.LogInformation("Image ID: {ImageId}", imageId);
    
    Console.WriteLine();
    Console.WriteLine("=== Custom Image Created Successfully ===");
    Console.WriteLine($"Image ID: {imageId}");
    Console.WriteLine();
    Console.WriteLine("To use this custom image, set the CustomImageId property in your SandboxConfiguration:");
    Console.WriteLine($"config.CustomImageId = \"{imageId}\";");
    Console.WriteLine();
    Console.WriteLine("This will significantly reduce VM startup time from ~5 minutes to ~10 seconds!");

    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to create custom image");
    return 1;
}
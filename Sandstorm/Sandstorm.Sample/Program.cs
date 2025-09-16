using Azure.Identity;
using dotenv.net;
using Microsoft.Extensions.Logging;
using Sandstorm.Core;
using Sandstorm.Core.Providers;


// Example usage of the Sandstorm Cloud Sandbox Platform
Console.WriteLine("=== Sandstorm Cloud Sandbox Platform Demo ===");
Console.WriteLine();

// Load environment variables from .env file if present
DotEnv.Load();

Console.WriteLine("🎯 END-TO-END SANDBOX PROVISIONING DEMO");
Console.WriteLine("=======================================");
Console.WriteLine();

try
{
    // Create Azure provider and SandstormClient - the ONLY interface the sample should use
    Console.WriteLine("Initializing Sandstorm client...");
    
    // Use environment variables for Azure credentials - REAL credentials required
    var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
    var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
    var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
    var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
    
    // Validate that all required Azure credentials are provided
    if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || 
        string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(subscriptionId))
    {
        throw new InvalidOperationException(
            "Azure credentials are required for real end-to-end provisioning. " +
            "Please set the following environment variables:\n" +
            "- AZURE_TENANT_ID\n" +
            "- AZURE_CLIENT_ID\n" +
            "- AZURE_CLIENT_SECRET\n" +
            "- AZURE_SUBSCRIPTION_ID");
    }
    
    // Create the real Azure cloud provider - NO MOCKING
    Console.WriteLine("🔧 Using real Azure provider");
    var cloudProvider = new AzureProvider(tenantId, clientId, clientSecret, subscriptionId);
    
    // Create the SandstormClient - this is the ONLY interface the sample should use
    var client = new SandstormClient(cloudProvider, "http://localhost:5000");
    
    Console.WriteLine("✅ Sandstorm client initialized");
    Console.WriteLine($"   Orchestrator endpoint: {client.OrchestratorEndpoint}");
    Console.WriteLine();
    
    // Create sandbox configuration
    Console.WriteLine("📋 Configuring sandbox...");
    var config = new SandboxConfiguration
    {
        Name = "demo-sandbox",
        Region = "westus2",
        VmSize = "Standard_B2s",
        Tags = 
        {
            ["Environment"] = "Demo",
            ["Purpose"] = "End-to-end sandbox provisioning demo",
            ["Owner"] = "SandstormDemo"
        }
    };
    
    Console.WriteLine($"   Name: {config.Name}");
    Console.WriteLine($"   Region: {config.Region}");
    Console.WriteLine($"   VM Size: {config.VmSize}");
    Console.WriteLine($"   Admin Username: {config.AdminUsername}");
    Console.WriteLine();
    
    // Create the sandbox - this will provision VM and install agent
    Console.WriteLine("🚀 Creating sandbox (this will provision VM and install agent)...");
    var sandbox = await client.Sandboxes.CreateAsync(config);
    
    Console.WriteLine($"✅ Sandbox created successfully!");
    Console.WriteLine($"   Sandbox ID: {sandbox.SandboxId}");
    Console.WriteLine($"   Status: {sandbox.Status}");
    Console.WriteLine($"   Public IP: {sandbox.PublicIpAddress}");
    Console.WriteLine();
    
    // Wait for the sandbox to be ready (agent connected)
    Console.WriteLine("⏳ Waiting for sandbox to be ready (agent to connect)...");
    await sandbox.WaitForReadyAsync();
    
    Console.WriteLine($"✅ Sandbox is ready!");
    Console.WriteLine($"   Status: {sandbox.Status}");
    Console.WriteLine();
    
    // Execute a command on the sandbox
    Console.WriteLine("🔧 Executing command on sandbox...");
    var command = "echo 'Hello from Sandstorm!' && whoami && pwd";
    var process = await sandbox.RunCommandAsync(command);
    var result = await process.WaitForCompletionAsync();
    
    Console.WriteLine($"📋 Command execution result:");
    Console.WriteLine($"   Command: {command}");
    Console.WriteLine($"   Exit Code: {result.ExitCode}");
    Console.WriteLine($"   Output: {result.StandardOutput}");
    Console.WriteLine($"   Error: {result.StandardError}");
    Console.WriteLine($"   Duration: {result.Duration.TotalMilliseconds}ms");
    Console.WriteLine();
    
    if (result.ExitCode == 0)
    {
        Console.WriteLine("🎉 SUCCESS: End-to-end sandbox provisioning and command execution completed!");
        Console.WriteLine("   ✓ VM provisioned");
        Console.WriteLine("   ✓ Agent installed and connected");
        Console.WriteLine("   ✓ Command executed through orchestrator-agent architecture");
    }
    else
    {
        Console.WriteLine("❌ Command execution failed");
    }
    
    Console.WriteLine();
    Console.WriteLine("🏗️  Architecture flow:");
    Console.WriteLine("   SandstormClient → SandboxManager → CloudProvider → VM Provisioning");
    Console.WriteLine("   VM → Agent Installation → Agent connects to Orchestrator");
    Console.WriteLine("   Command → Orchestrator → Agent → VM Execution → Results back");
    
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("💡 To run the real end-to-end demo:");
    Console.WriteLine("   1. Set Azure environment variables (AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, AZURE_SUBSCRIPTION_ID)");
    Console.WriteLine("   2. Start orchestrator: cd Sandstorm.Orchestrator && dotnet run");
    Console.WriteLine("   3. Run this demo for real Azure VM provisioning and command execution");
    Console.WriteLine();
    Console.WriteLine("💡 For local testing with existing agent:");
    Console.WriteLine("   1. Start orchestrator: cd Sandstorm.Orchestrator && dotnet run");
    Console.WriteLine("   2. Start agent: cd Sandstorm.Agent && SANDSTORM_SANDBOX_ID=demo-sandbox dotnet run");
    Console.WriteLine("   3. Set Azure credentials and run this demo");
}

Console.WriteLine("\nDemo completed. Press any key to exit...");
Console.ReadKey();

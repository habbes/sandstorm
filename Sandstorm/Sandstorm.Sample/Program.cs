using Azure.Identity;
using dotenv.net;
using Microsoft.Extensions.Logging;
using Sandstorm.Core;
using Sandstorm.Core.Providers;
using System.Diagnostics;


// Example usage of the Sandstorm Cloud Sandbox Platform
Console.WriteLine("=== Sandstorm Cloud Sandbox Platform Demo ===");
Console.WriteLine();

// Load environment variables from .env file if present
DotEnv.Load(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 3));

try
{
    var tenantId = Environment.GetEnvironmentVariable("TENANT_ID") ?? throw new Exception("TENANT_ID env var must be set");
    var clientId = Environment.GetEnvironmentVariable("CLIENT_ID") ?? throw new Exception("CLIENT_ID env var must be set");
    var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET") ?? throw new Exception("CLIENT_SECRET env var must be set");
    var subscriptionId = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID") ?? throw new Exception("SUBSCRIPTION_ID env var must be set");
    var orchestratorEndpoint = Environment.GetEnvironmentVariable("ORCHESTRATOR_ENDPOINT") ?? "http://localhost:5000";

    Console.WriteLine("🔧 Using real Azure provider");
    var cloudProvider = new AzureProvider(tenantId, clientId, clientSecret, subscriptionId);
    
    // Create the SandstormClient - this is the ONLY interface the sample should use
    var client = new SandstormClient(cloudProvider, orchestratorEndpoint);
    
    Console.WriteLine("✅ Sandstorm client initialized");
    Console.WriteLine($"   Orchestrator endpoint: {client.OrchestratorEndpoint}");
    Console.WriteLine();
    
    // Create sandbox configuration
    Console.WriteLine("📋 Configuring sandbox...");
    // Create the sandbox - this will provision VM and install agent
    Console.WriteLine("🚀 Creating sandbox (this will provision VM and install agent)...");
    var sw = Stopwatch.StartNew();
    await using var sandbox = await client.Sandboxes.CreateAsync();
    Console.WriteLine($"   Sandbox creation initiated in {sw.ElapsedMilliseconds} ms");

    Console.WriteLine($"✅ Sandbox created successfully!");
    Console.WriteLine($"   Sandbox ID: {sandbox.SandboxId}");
    Console.WriteLine($"   Status: {sandbox.Status}");
    Console.WriteLine($"   Public IP: {sandbox.PublicIpAddress}");
    Console.WriteLine();
    
    // Wait for the sandbox to be ready (agent connected)
    Console.WriteLine("⏳ Waiting for sandbox to be ready (agent to connect)...");
    sw.Restart();
    await sandbox.WaitForReadyAsync();
    
    Console.WriteLine($"✅ Sandbox is ready after {sw.ElapsedMilliseconds} ms!");
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
}

Console.WriteLine("\nDemo completed. Press any key to exit...");
Console.ReadKey();

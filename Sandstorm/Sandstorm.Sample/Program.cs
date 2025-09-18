using Microsoft.Extensions.Logging;
using Sandstorm.Core;
using System.Diagnostics;


// Example usage of the Sandstorm Cloud Sandbox Platform
Console.WriteLine("=== Sandstorm Cloud Sandbox Platform Demo ===");
Console.WriteLine();

try
{
    // Get the Sandstorm API endpoint from environment or use default
    var apiEndpoint = Environment.GetEnvironmentVariable("SANDSTORM_API_ENDPOINT") ?? "http://localhost:5000";

    Console.WriteLine("🔧 Using Sandstorm API service");
    Console.WriteLine($"   API endpoint: {apiEndpoint}");
    
    // Create the SandstormClient using the new API-based approach
    // No Azure credentials or cloud provider configuration needed!
    var client = new SandstormClient(apiEndpoint);
    
    Console.WriteLine("✅ Sandstorm client initialized");
    Console.WriteLine($"   API endpoint: {client.ApiEndpoint}");
    Console.WriteLine();
    
    // Create the sandbox - this will call the API service which manages everything
    Console.WriteLine("🚀 Creating sandbox (this will be handled by the API service)...");
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
        Console.WriteLine("   ✓ API service handled VM provisioning");
        Console.WriteLine("   ✓ Agent installed and connected");
        Console.WriteLine("   ✓ Command executed through API → orchestrator → agent architecture");
    }
    else
    {
        Console.WriteLine("❌ Command execution failed");
    }
    
    Console.WriteLine();
    Console.WriteLine("🏗️  New Architecture flow:");
    Console.WriteLine("   SandstormClient → HTTP API → SandboxManager → CloudProvider → VM Provisioning");
    Console.WriteLine("   VM → Agent Installation → Agent connects to Orchestrator");
    Console.WriteLine("   Command → HTTP API → Orchestrator → Agent → VM Execution → Results back");
    Console.WriteLine();
    Console.WriteLine("🎯 Benefits:");
    Console.WriteLine("   ✓ No Azure credentials needed in client");
    Console.WriteLine("   ✓ Simple configuration (just API endpoint)");
    Console.WriteLine("   ✓ Centralized sandbox management");
    Console.WriteLine("   ✓ Clean separation of concerns");
    
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    
    if (ex.Message.Contains("connection") || ex.Message.Contains("timeout"))
    {
        Console.WriteLine();
        Console.WriteLine("💡 Make sure the Sandstorm API service is running:");
        Console.WriteLine("   cd Sandstorm.Orchestrator && dotnet run");
        Console.WriteLine("   Default endpoint: http://localhost:5000");
    }
}

Console.WriteLine("\nDemo completed. Press any key to exit...");
Console.ReadKey();

using Azure.Identity;
using dotenv.net;
using Microsoft.Extensions.Logging;
using Sandstorm.Core;
using Sandstorm.Core.Providers;


DotEnv.Load(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 3));

// Example usage of the Sandstorm Cloud Sandbox Platform
Console.WriteLine("=== Sandstorm Cloud Sandbox Platform Demo ===");

// Create a logger for better diagnostics
using var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));

var logger = loggerFactory.CreateLogger<AzureProvider>();

try
{
    // For this demo, we'll simulate the example from the issue
    // In production, you would provide real Azure credentials
    
    Console.WriteLine("Creating sandbox client...");
    
    // Example 1: Using environment variables (like in the playground)
    var clientId = Environment.GetEnvironmentVariable("CLIENT_ID") ?? throw new Exception("Please set the CLIENT_ID env variable.");
    var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET") ?? throw new Exception("Please set the CLIENT_SECRET env variable.");
    var tenantId = Environment.GetEnvironmentVariable("TENANT_ID") ?? throw new Exception("Please set the TENANT_ID env variable.");
    var subscriptionId = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID") ?? throw new Exception("Please set the SUBSCRIPTION_ID env variable.");

    var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

    // Create the Azure provider
    var azureProvider = new AzureProvider(credential, subscriptionId, logger: logger);
    
    // Create the Sandstorm client
    var client = new SandstormClient(azureProvider);
    
    // Example 2: Create a sandbox with orchestrator-agent architecture
    Console.WriteLine("Creating sandbox with orchestrator-agent architecture...");
    
    var config = new SandboxConfiguration
    {
        Name = "demo-sandbox",
        Region = "westus2",
        VmSize = "Standard_B2s",
        // Configure orchestrator endpoint for agent communication
        OrchestratorEndpoint = "http://localhost:5000", // Default value
        Tags = 
        {
            ["Environment"] = "Demo",
            ["Purpose"] = "Code execution with orchestrator-agent architecture",
            ["Owner"] = "SandstormDemo"
        }
    };
    
    Console.WriteLine("Sandbox configuration (Orchestrator-Agent Architecture):");
    Console.WriteLine($"  Name: {config.Name}");
    Console.WriteLine($"  Region: {config.Region}");
    Console.WriteLine($"  VM Size: {config.VmSize}");
    Console.WriteLine($"  Admin Username: {config.AdminUsername}");
    Console.WriteLine($"  Orchestrator Endpoint: {config.OrchestratorEndpoint}");
    Console.WriteLine($"  Architecture: Agent-based (no SSH required)");
    Console.WriteLine();
    
    Console.WriteLine("Key advantages of the new architecture:");
    Console.WriteLine("✓ No direct SSH connections required");
    Console.WriteLine("✓ No public IP addresses needed for VMs");
    Console.WriteLine("✓ Agent initiates connection to orchestrator (more secure)");
    Console.WriteLine("✓ Centralized command execution and logging");
    Console.WriteLine("✓ Automatic agent installation via cloud-init");
    Console.WriteLine();

    // Note: In this demo, we won't actually create Azure resources
    // as that requires valid credentials and will incur costs
    // But we can demonstrate the orchestrator client directly
    
    Console.WriteLine("Demonstrating orchestrator client (simulated):");
    using var orchestratorClient = new Sandstorm.Core.Services.OrchestratorClient("http://localhost:5000");
    
    var demoResult = await orchestratorClient.ExecuteCommandAsync("demo-sandbox", "echo 'Hello from orchestrator!'", TimeSpan.FromMinutes(1));
    Console.WriteLine($"Command execution result:");
    Console.WriteLine($"  Exit Code: {demoResult.ExitCode}");
    Console.WriteLine($"  Output: {demoResult.StandardOutput}");
    Console.WriteLine($"  Duration: {demoResult.Duration.TotalMilliseconds}ms");
    Console.WriteLine();

    // This would be the actual usage once orchestrator and credentials are configured:
    // await using var sandbox = await client.Sandboxes.CreateAsync(config);

    /*
    // Code execution examples (would work with real orchestrator):
    
    await using var sandbox = await client.Sandboxes.CreateAsync(config);

    // Execute C# code
    var process = await sandbox.RunCodeAsync(new CSharpCode
    {
        Code = "Console.WriteLine(\"Hello from C# in the sandbox!\");",
        Dependencies = new() { "Newtonsoft.Json" }
    });

    var logs = process.GetLogStreamAsync();

    var logCancellation = new CancellationTokenSource();
    var logTask = Task.Run(async () =>
    {
        await foreach (var log in logs.WithCancellation(logCancellation.Token))
        {
            Console.WriteLine($"[LOG] {log}");
        }
    });

    // Execute shell command
    await sandbox.RunCommandAsync("echo 'Hello from shell!'");

    // Run AI agent
    await sandbox.RunAgentAsync(new OpenAiAgentTask
    {
        Prompt = "Write a simple Python script that prints 'Hello World'",
        Model = "gpt-4"
    });

    logCancellation.Cancel();
    await logTask;
    */
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("Note: This demo shows the new orchestrator-agent architecture.");
    Console.WriteLine("To use with real Azure resources:");
    Console.WriteLine("1. Set environment variables: CLIENT_ID, CLIENT_SECRET, TENANT_ID, SUBSCRIPTION_ID");
    Console.WriteLine("2. Deploy and run the Sandstorm.Orchestrator service");
    Console.WriteLine("3. Configure the orchestrator endpoint in your sandbox configuration");
}

Console.WriteLine("\nDemo completed. Press any key to exit...");
Console.ReadKey();

using Azure.Identity;
using Microsoft.Extensions.Logging;
using Sandstorm.Core;
using Sandstorm.Core.Providers;

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
    var credential = new EnvironmentCredential();
    var subscriptionId = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID") ?? "demo-subscription";
    
    // Create the Azure provider
    var azureProvider = new AzureProvider(credential, subscriptionId, logger: logger);
    
    // Create the Sandstorm client
    var client = new SandstormClient(azureProvider);
    
    // Example 2: Create a sandbox with custom configuration
    Console.WriteLine("Creating sandbox with custom configuration...");
    
    var config = new SandboxConfiguration
    {
        Name = "demo-sandbox",
        Region = "westus2",
        VmSize = "Standard_B2s",
        Tags = 
        {
            ["Environment"] = "Demo",
            ["Purpose"] = "Code execution",
            ["Owner"] = "SandstormDemo"
        }
    };
    
    // Note: In this demo, we won't actually create Azure resources
    // as that requires valid credentials and will incur costs
    Console.WriteLine("Would create sandbox with configuration:");
    Console.WriteLine($"  Name: {config.Name}");
    Console.WriteLine($"  Region: {config.Region}");
    Console.WriteLine($"  VM Size: {config.VmSize}");
    Console.WriteLine($"  Admin Username: {config.AdminUsername}");
    
    // Example usage as shown in the issue:
    Console.WriteLine("\n=== Example Usage (Simulated) ===");
    
    // This would be the actual usage once credentials are configured:
    /*
    var sandbox = await client.Sandboxes.CreateAsync(config);
    
    // Execute C# code
    var process = await sandbox.RunCodeAsync(new CSharpCode 
    { 
        Code = "Console.WriteLine(\"Hello from C# in the sandbox!\");", 
        Dependencies = new() { "Newtonsoft.Json" } 
    });
    
    // Get log stream
    var logs = process.GetLogStreamAsync();
    await foreach (var log in logs)
    {
        Console.WriteLine($"[LOG] {log}");
    }
    
    // Execute shell command
    await sandbox.RunCommandAsync("echo 'Hello from shell!'");
    
    // Run AI agent
    await sandbox.RunAgentAsync(new OpenAiAgentTask
    {
        Prompt = "Write a simple Python script that prints 'Hello World'",
        Model = "gpt-4"
    });
    
    // Clean up
    await sandbox.DeleteAsync();
    */
    
    Console.WriteLine("// Create sandbox");
    Console.WriteLine("var sandbox = await client.Sandboxes.CreateAsync();");
    Console.WriteLine();
    
    Console.WriteLine("// Execute C# code");
    Console.WriteLine("var process = await sandbox.RunCodeAsync(new CSharpCode");
    Console.WriteLine("{");
    Console.WriteLine("    Code = \"Console.WriteLine(\\\"Hello from C#!\\\");\",");
    Console.WriteLine("    Dependencies = new() { \"Newtonsoft.Json\" }");
    Console.WriteLine("});");
    Console.WriteLine();
    
    Console.WriteLine("// Get live log stream");
    Console.WriteLine("var logs = process.GetLogStreamAsync();");
    Console.WriteLine("await foreach (var log in logs)");
    Console.WriteLine("{");
    Console.WriteLine("    Console.WriteLine($\"[LOG] {log}\");");
    Console.WriteLine("}");
    Console.WriteLine();
    
    Console.WriteLine("// Execute shell commands");
    Console.WriteLine("await sandbox.RunCommandAsync(\"pip install requests\");");
    Console.WriteLine("await sandbox.RunCommandAsync(\"python --version\");");
    Console.WriteLine();
    
    Console.WriteLine("// Run Python code");
    Console.WriteLine("var pythonProcess = await sandbox.RunCodeAsync(new PythonCode");
    Console.WriteLine("{");
    Console.WriteLine("    Code = \"print('Hello from Python!')\",");
    Console.WriteLine("    PipPackages = new() { \"requests\", \"numpy\" }");
    Console.WriteLine("});");
    Console.WriteLine();
    
    Console.WriteLine("// Run JavaScript code");
    Console.WriteLine("var jsProcess = await sandbox.RunCodeAsync(new JavaScriptCode");
    Console.WriteLine("{");
    Console.WriteLine("    Code = \"console.log('Hello from Node.js!')\",");
    Console.WriteLine("    NpmPackages = new() { \"lodash\", \"moment\" }");
    Console.WriteLine("});");
    Console.WriteLine();
    
    Console.WriteLine("// Run AI agent task");
    Console.WriteLine("await sandbox.RunAgentAsync(new OpenAiAgentTask");
    Console.WriteLine("{");
    Console.WriteLine("    Prompt = \"Write a simple Python script that calculates fibonacci numbers\",");
    Console.WriteLine("    Model = \"gpt-4\"");
    Console.WriteLine("});");
    Console.WriteLine();
    
    Console.WriteLine("// Clean up resources");
    Console.WriteLine("await sandbox.DeleteAsync();");
    
    Console.WriteLine("\n=== Features ===");
    Console.WriteLine("✓ Fast sandbox provisioning (target < 20 seconds)");
    Console.WriteLine("✓ Multi-language support (C#, Python, JavaScript)");
    Console.WriteLine("✓ Shell command execution");
    Console.WriteLine("✓ AI agent integration");
    Console.WriteLine("✓ Live log streaming");
    Console.WriteLine("✓ Automatic resource cleanup");
    Console.WriteLine("✓ Azure cloud integration");
    Console.WriteLine("✓ Configurable VM sizes and regions");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine("Note: This demo requires valid Azure credentials to create actual sandboxes.");
    Console.WriteLine("Set environment variables: CLIENT_ID, CLIENT_SECRET, TENANT_ID, SUBSCRIPTION_ID");
}

Console.WriteLine("\nDemo completed. Press any key to exit...");
Console.ReadKey();

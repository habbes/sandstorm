# Sandstorm Cloud Sandbox Platform

Programmatically provision sandboxes on demand to run AI agents and other workloads

## Features

- **Fast Provisioning**: Sandboxes boot in less than 20 seconds
- **Multi-Language Support**: Execute C#, Python, and JavaScript code
- **Shell Command Execution**: Run any shell commands in the sandbox
- **AI Agent Integration**: Run OpenAI-powered AI agents
- **Live Log Streaming**: Get real-time output from running processes
- **Automatic Cleanup**: Resources are automatically cleaned up when done
- **Azure Integration**: Built on Azure Virtual Machines for reliability and scale

## Quick Start

### Installation

Add the Sandstorm.Core package to your project:

```bash
dotnet add package Sandstorm.Core
```

### Basic Usage

```csharp
using Azure.Identity;
using Sandstorm.Core;
using Sandstorm.Core.Providers;

// Create Azure provider with credentials
var credential = new DefaultAzureCredential();
var client = new SandstormClient(new AzureProvider(credential, subscriptionId));

// Create a sandbox
var sandbox = await client.Sandboxes.CreateAsync();

// Execute C# code
var process = await sandbox.RunCodeAsync(new CSharpCode 
{ 
    Code = "Console.WriteLine(\"Hello from C#!\");", 
    Dependencies = new() { "Newtonsoft.Json" } 
});

// Get log stream
await foreach (var log in process.GetLogStreamAsync())
{
    Console.WriteLine($"[LOG] {log}");
}

// Execute shell commands
await sandbox.RunCommandAsync("echo 'Hello World!'");

// Run Python code
await sandbox.RunCodeAsync(new PythonCode
{
    Code = "print('Hello from Python!')",
    PipPackages = new() { "requests", "numpy" }
});

// Run AI agent
await sandbox.RunAgentAsync(new OpenAiAgentTask
{
    Prompt = "Write a simple script",
    Model = "gpt-4"
});

// Clean up
await sandbox.DeleteAsync();
```

### Configuration

```csharp
var config = new SandboxConfiguration
{
    Name = "my-sandbox",
    Region = "westus2",
    VmSize = "Standard_B2s",
    Tags = { ["Environment"] = "Development" }
};

var sandbox = await client.Sandboxes.CreateAsync(config);
```

## Azure Setup

1. Create an Azure subscription
2. Register an application in Azure AD
3. Grant necessary permissions for VM creation
4. Set environment variables:
   - `CLIENT_ID`: Your application client ID
   - `CLIENT_SECRET`: Your application client secret  
   - `TENANT_ID`: Your Azure tenant ID
   - `SUBSCRIPTION_ID`: Your Azure subscription ID

## Examples

See the `Sandstorm.Sample` project for complete usage examples.

## Local Development

For local development and testing, use the provided AppHost which orchestrates all services:

```bash
cd Sandstorm/Sandstorm.AppHost
dotnet run
```

This starts:
- **Orchestrator** service on `http://localhost:5000` 
- **Agent** service with proper configuration
- **Sample** application demonstrating SDK usage

All services run with coordinated logging and graceful shutdown. See `Sandstorm.AppHost/README.md` for details.

## Architecture

The library consists of:

- **Core Library** (`Sandstorm.Core`): Main interfaces and client
- **Azure Provider**: Implementation for Azure Virtual Machines
- **Sample Project**: Demonstration of usage patterns

## Performance

- Sandbox creation: Target < 20 seconds
- Code execution: Near real-time with live streaming
- Resource cleanup: Automatic and immediate

## Contributing

Contributions welcome! Please read the contributing guidelines and submit pull requests.

## License

MIT License - see LICENSE file for details.
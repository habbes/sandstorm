# Sandstorm Orchestrator-Agent Architecture

This document describes the new orchestrator-agent architecture implemented to replace direct SSH connections to sandbox VMs.

## Overview

The Sandstorm platform now uses a secure orchestrator-agent architecture instead of direct SSH connections. This provides better security, centralized management, and eliminates the need for public IP addresses on sandbox VMs.

## Architecture Components

### 1. Orchestrator Service (`Sandstorm.Orchestrator`)

The orchestrator is a gRPC service that:
- Manages agent connections and registrations
- Receives command execution requests from clients
- Forwards commands to appropriate agents
- Collects logs and metrics from agents
- Monitors agent health via heartbeats

**Key Features:**
- Secure gRPC communication
- Agent lifecycle management
- Command routing and result aggregation
- Centralized logging

### 2. Sandbox Agent (`Sandstorm.Agent`)

The agent runs inside each sandbox VM and:
- Connects to the orchestrator at startup
- Executes commands received from orchestrator
- Sends heartbeats and system metrics
- Reports command results and logs

**Key Features:**
- Outbound-only connections (no open ports)
- Automatic installation via cloud-init
- Resource monitoring
- Command execution isolation

### 3. Core Library Updates (`Sandstorm.Core`)

Updated to support the new architecture:
- `OrchestratorClient` for orchestrator communication
- Modified `AzureSandbox` to use orchestrator instead of SSH
- Enhanced cloud-init scripts for agent installation
- Configuration support for orchestrator endpoints

## Security Benefits

1. **No SSH Required**: Eliminates SSH attack surface
2. **No Public IPs**: VMs don't need public IP addresses
3. **Outbound Connections**: Agents initiate connections to orchestrator
4. **Centralized Control**: All commands go through orchestrator
5. **Encrypted Communication**: gRPC with TLS support

## Configuration

### Sandbox Configuration

```csharp
var config = new SandboxConfiguration
{
    Name = "my-sandbox",
    Region = "westus2",
    VmSize = "Standard_B2s",
    OrchestratorEndpoint = "https://orchestrator.example.com:5000"
};
```

### Environment Variables for Agent

The agent reads these environment variables (set via cloud-init):
- `SANDSTORM_SANDBOX_ID`: Unique sandbox identifier
- `SANDSTORM_VM_ID`: VM identifier
- `SANDSTORM_ORCHESTRATOR_ENDPOINT`: Orchestrator endpoint URL

## Deployment

### 1. Deploy Orchestrator

```bash
cd Sandstorm.Orchestrator
dotnet run --urls "https://0.0.0.0:5000"
```

### 2. Create Sandbox with Agent

When creating a sandbox, the agent is automatically installed via cloud-init:

```csharp
var sandbox = await client.Sandboxes.CreateAsync(config);
```

### 3. Execute Commands

Commands are executed through the orchestrator:

```csharp
var process = await sandbox.RunCommandAsync("echo 'Hello World'");
var result = await process.WaitForCompletionAsync();
```

## Monitoring

### Agent Health

Agents send heartbeats every 30 seconds. The orchestrator automatically cleans up inactive agents.

### Command Execution

All command execution is logged centrally through the orchestrator with:
- Command details
- Execution time
- Exit codes
- Output/error streams

## Migration from SSH

For existing code, minimal changes are required:

1. **Add orchestrator endpoint** to `SandboxConfiguration`
2. **Deploy orchestrator service** in your infrastructure
3. **Existing API calls work unchanged** - the SDK handles the communication

### Backward Compatibility

If no orchestrator endpoint is configured, the system will return a helpful error message directing users to the new architecture.

## Development

### Building the Solution

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Project Structure

```
Sandstorm/
├── Sandstorm.Core/          # Core SDK library
├── Sandstorm.Orchestrator/  # Orchestrator service
├── Sandstorm.Agent/         # VM agent
├── Sandstorm.Sample/        # Usage examples
└── Sandstorm.Core.Tests/    # Unit tests
```

## Future Enhancements

1. **Load Balancing**: Multiple orchestrator instances
2. **Persistence**: Database backing for agent state
3. **Metrics**: Detailed performance monitoring
4. **Auto-scaling**: Dynamic VM management
5. **Security**: Certificate-based agent authentication
# Sandstorm Local Development Environment

This project provides an Aspire-style local development orchestration for the Sandstorm platform, making it easy to run all services together for development and testing.

## What it does

The AppHost coordinates the startup of all Sandstorm services:

- **Orchestrator** - gRPC service on `http://localhost:5000`
- **Agent** - Worker service that connects to the orchestrator
- **Sample** - Demo application showing SDK usage

## Quick Start

```bash
cd Sandstorm/Sandstorm.AppHost
dotnet run
```

This will:
1. Start the orchestrator service
2. Wait for it to be ready 
3. Start the agent service with proper environment variables
4. Run the sample application
5. Display logs from all services in a unified view

## Features

✅ **Unified Service Orchestration** - All services start in the correct order with proper dependencies

✅ **Automatic Configuration** - Environment variables and endpoints are configured automatically

✅ **Centralized Logging** - See logs from all services in one place with clear labeling

✅ **Graceful Shutdown** - Press Ctrl+C to stop all services cleanly

✅ **Health Checks** - Ensures services are ready before starting dependent services

## Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Sample App    │    │     Agent       │    │  Orchestrator   │
│                 │───▶│                 │───▶│   (gRPC)        │
│ SDK Demo        │    │ Command Executor│    │ localhost:5000  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Environment Variables

The AppHost automatically configures these environment variables for the services:

### Agent Service
- `SANDSTORM_SANDBOX_ID=demo-sandbox`
- `SANDSTORM_AGENT_ID=demo-agent-01` 
- `SANDSTORM_VM_ID=demo-vm-01`
- `SANDSTORM_ORCHESTRATOR_ENDPOINT=http://localhost:5000`

### Sample Application
- `CLIENT_ID=demo-client-id`
- `CLIENT_SECRET=demo-client-secret`
- `TENANT_ID=demo-tenant-id`
- `SUBSCRIPTION_ID=demo-subscription-id`

## Benefits over Manual Setup

- 🚀 **Faster Development** - One command instead of managing multiple terminals
- 🔍 **Better Debugging** - Centralized logs with service labels
- ⚙️ **Consistent Configuration** - No manual environment variable setup
- 🛡️ **Error Handling** - Proper service dependency management
- 🔄 **Easy Restart** - Single Ctrl+C to stop everything

## Logs Example

```
info: LocalOrchestrationService[0]
      🚀 Starting orchestrator service...
info: LocalOrchestrationService[0]
      ✅ Orchestrator running on http://localhost:5000
info: LocalOrchestrationService[0]
      🤖 Starting agent service...
info: LocalOrchestrationService[0]
      [Sandstorm.Agent] Sandstorm Agent starting up. Agent ID: demo-agent-01
info: LocalOrchestrationService[0]
      [Sandstorm.Orchestrator] Agent registration request from demo-agent-01
info: LocalOrchestrationService[0]
      [Sandstorm.Sample] Command execution result: Exit Code: 0
```

## Future Enhancements

- Add service discovery for dynamic endpoint configuration
- Include database services when needed
- Add development-specific debugging features
- Support for multiple agent instances
- Integration with container orchestration for cloud development
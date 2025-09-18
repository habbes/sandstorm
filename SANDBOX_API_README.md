# Sandstorm API Implementation

This implementation adds a REST API layer to the Sandstorm platform, allowing clients to interact with sandboxes without requiring direct Azure credentials or cloud provider configuration.

## New Architecture

### Before (Direct Cloud Provider)
```csharp
// Required Azure credentials and configuration
var cloudProvider = new AzureProvider(tenantId, clientId, clientSecret, subscriptionId);
var client = new SandstormClient(cloudProvider, orchestratorEndpoint);
```

### After (API Service)
```csharp
// Only API endpoint needed - no credentials!
var client = new SandstormClient("http://localhost:5000");
```

## API Endpoints

The Sandstorm.Orchestrator now exposes a comprehensive REST API:

### Sandbox Management
- `POST /api/sandboxes` - Create a new sandbox
- `GET /api/sandboxes/{id}` - Get sandbox information
- `GET /api/sandboxes` - List all sandboxes  
- `DELETE /api/sandboxes/{id}` - Delete a sandbox

### Command Execution
- `POST /api/sandboxes/{id}/commands` - Execute a command
- `GET /api/sandboxes/{id}/commands/{pid}/status` - Get command status
- `GET /api/sandboxes/{id}/commands/{pid}/logs` - Stream command logs
- `DELETE /api/sandboxes/{id}/commands/{pid}` - Terminate a process

## Running the Demo

1. **Start the API service:**
   ```bash
   cd Sandstorm.Orchestrator
   dotnet run
   ```

2. **Run the sample client:**
   ```bash
   cd Sandstorm.Sample
   dotnet run
   ```

3. **Test API endpoints directly:**
   ```bash
   # Create sandbox
   curl -X POST http://localhost:5000/api/sandboxes \
     -H "Content-Type: application/json" \
     -d '{}'

   # List sandboxes
   curl http://localhost:5000/api/sandboxes
   ```

## Benefits

✅ **Simplified Client Configuration**: No Azure credentials needed in client applications  
✅ **Centralized Management**: All sandbox operations managed by the API service  
✅ **Clean Separation**: Clients only need to know the API endpoint  
✅ **Backwards Compatibility**: Legacy constructor still available (marked obsolete)  
✅ **RESTful Design**: Standard HTTP endpoints with proper error handling  
✅ **Scalable**: Multiple clients can share the same API service instance  

## Configuration

### Client Projects (Simple)
```bash
export SANDSTORM_API_ENDPOINT=http://localhost:5000
```

### API Service (Azure credentials moved here)
```bash
export TENANT_ID=your-tenant-id
export CLIENT_ID=your-client-id  
export CLIENT_SECRET=your-client-secret
export SUBSCRIPTION_ID=your-subscription-id
```

## Testing

The implementation includes comprehensive error handling and maintains full compatibility with the existing ISandbox and IProcess interfaces. The HTTP-based implementation transparently handles API communication while providing the same developer experience.

## Migration Guide

### For existing code using cloud providers directly:
```csharp
// Old way (still works but deprecated)
var cloudProvider = new AzureProvider(tenantId, clientId, clientSecret, subscriptionId);
var client = new SandstormClient(cloudProvider, orchestratorEndpoint);

// New way (recommended)
var client = new SandstormClient("http://localhost:5000");
```

### All existing sandbox operations work unchanged:
```csharp
var sandbox = await client.Sandboxes.CreateAsync();
await sandbox.WaitForReadyAsync();
var process = await sandbox.RunCommandAsync("echo 'Hello World'");
var result = await process.WaitForCompletionAsync();
```

The API service handles all the complexity of cloud provider management, VM provisioning, and orchestrator communication behind the scenes.
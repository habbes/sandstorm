# VM Image Optimization for Fast Startup

## Overview

This feature allows you to create custom VM images with all Sandstorm dependencies pre-installed, dramatically reducing VM startup time from ~5+ minutes to ~10 seconds.

## How It Works

### Traditional Approach (Slow)
1. Create VM from base Ubuntu image (~30s-1m)
2. Run cloud-init script that:
   - Installs packages (Docker, .NET, Node.js, Python, etc.)
   - Downloads and builds Sandstorm agent from source
   - Configures systemd services
   - Total: ~5+ minutes

### Optimized Approach (Fast)
1. Create VM from custom pre-baked image (~30s-1m)
2. Run minimal cloud-init script that:
   - Sets environment variables
   - Starts pre-installed Sandstorm agent
   - Total: ~10 seconds

## Creating a Custom Image

Use the `Sandstorm.ImageBuilder` tool to create optimized images:

```bash
# Set Azure credentials
export AZURE_CLIENT_ID="your-client-id"
export AZURE_CLIENT_SECRET="your-client-secret"
export AZURE_TENANT_ID="your-tenant-id"
export AZURE_SUBSCRIPTION_ID="your-subscription-id"

# Create custom image
dotnet run --project Sandstorm.ImageBuilder -- \
  sandstorm-images-rg \
  sandstorm-optimized-v1 \
  https://your-orchestrator.example.com \
  westus2
```

The tool will:
1. Create a temporary VM from base Ubuntu image
2. Install all dependencies (Docker, .NET, Node.js, Python, etc.)
3. Clone and build Sandstorm agent
4. Configure systemd services
5. Generalize and capture VM as custom image
6. Clean up temporary resources

## Using Custom Images

### With SandboxConfiguration

```csharp
var config = new SandboxConfiguration
{
    Name = "fast-sandbox",
    VmSize = "Standard_B2s", 
    Region = "westus2",
    // Use custom image for fast startup
    CustomImageId = "/subscriptions/your-sub/resourceGroups/sandstorm-images-rg/providers/Microsoft.Compute/images/sandstorm-optimized-v1"
};

var client = new SandstormClient(azureCredentials, orchestratorEndpoint);
var sandbox = await client.CreateSandboxAsync(config);

// Sandbox will be ready in ~10 seconds instead of 5+ minutes!
await sandbox.WaitForReadyAsync();
```

### Backward Compatibility

If `CustomImageId` is not specified, the system uses the traditional approach with base Ubuntu images. Existing code continues to work without changes.

## Benefits

- **90% faster startup**: ~10 seconds vs 5+ minutes
- **Consistent environment**: All VMs have identical setup
- **Reduced network dependency**: No downloading/building during startup
- **Better reliability**: Pre-tested image reduces cloud-init failures
- **Cost savings**: Faster provisioning = lower compute costs

## Image Contents

Custom images include:
- Ubuntu 22.04 LTS base
- Docker CE
- .NET 8.0 SDK
- Node.js and npm
- Python 3 and pip
- Essential tools (git, curl, vim, htop, etc.)
- Pre-built Sandstorm agent
- Configured systemd services

## Best Practices

1. **Regular Updates**: Rebuild images monthly to include security updates
2. **Regional Images**: Create images in each Azure region you use
3. **Version Control**: Use semantic versioning for image names
4. **Testing**: Test images thoroughly before production use
5. **Cleanup**: Remove old/unused images to reduce costs

## Example Scripts

### Image Creation Script
```bash
#!/bin/bash
# create-sandstorm-image.sh

# Load environment variables
source .env

RESOURCE_GROUP="sandstorm-images-rg"
IMAGE_NAME="sandstorm-optimized-$(date +%Y%m%d)"
ORCHESTRATOR_ENDPOINT="https://orchestrator.yourcompany.com"
REGION="westus2"

echo "Creating Sandstorm custom image: $IMAGE_NAME"

dotnet run --project Sandstorm.ImageBuilder -- \
  "$RESOURCE_GROUP" \
  "$IMAGE_NAME" \
  "$ORCHESTRATOR_ENDPOINT" \
  "$REGION"

if [ $? -eq 0 ]; then
    echo "Image created successfully!"
    echo "Image ID: /subscriptions/$AZURE_SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/images/$IMAGE_NAME"
else
    echo "Image creation failed!"
    exit 1
fi
```

### Usage Example
```csharp
// Program.cs
using Sandstorm.Core;

var config = new SandboxConfiguration
{
    CustomImageId = "/subscriptions/your-sub/resourceGroups/sandstorm-images-rg/providers/Microsoft.Compute/images/sandstorm-optimized-20240101"
};

// Fast sandbox creation
var sandbox = await client.CreateSandboxAsync(config);
await sandbox.WaitForReadyAsync(); // ~10 seconds

// Ready to execute code!
var result = await sandbox.RunCommandAsync("echo 'Hello, fast world!'");
```

## Troubleshooting

### Image Builder Issues
- Ensure Azure credentials are properly set
- Check resource group permissions
- Verify sufficient quota in target region

### Startup Issues
- Verify image ID is correct and accessible
- Check orchestrator endpoint connectivity
- Review Azure Activity Log for deployment errors

### Performance Issues
- Use Premium SSD storage (default)
- Choose appropriate VM sizes
- Consider proximity to orchestrator
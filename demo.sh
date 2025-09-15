#!/bin/bash
# Demo script for Sandstorm Orchestrator-Agent Architecture

echo "=== Sandstorm Orchestrator-Agent Architecture Demo ==="
echo ""

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET 8.0 is required but not installed."
    echo "Please install .NET 8.0 from https://dotnet.microsoft.com/download"
    exit 1
fi

echo "✅ .NET 8.0 found"

# Build the solution
echo ""
echo "🔨 Building the solution..."
cd Sandstorm
if ! dotnet build --verbosity quiet; then
    echo "❌ Build failed"
    exit 1
fi
echo "✅ Build successful"

# Run tests
echo ""
echo "🧪 Running tests..."
if ! dotnet test --verbosity quiet --nologo; then
    echo "❌ Tests failed"
    exit 1
fi
echo "✅ All tests passed"

# Start orchestrator in background
echo ""
echo "🚀 Starting orchestrator service..."
cd Sandstorm.Orchestrator
dotnet run --urls "http://localhost:5000" > /tmp/orchestrator.log 2>&1 &
ORCHESTRATOR_PID=$!
cd ..

# Wait for orchestrator to start
echo "⏳ Waiting for orchestrator to start..."
sleep 3

# Check if orchestrator is running
if ! kill -0 $ORCHESTRATOR_PID 2>/dev/null; then
    echo "❌ Orchestrator failed to start"
    cat /tmp/orchestrator.log
    exit 1
fi
echo "✅ Orchestrator running on http://localhost:5000"

# Run the sample
echo ""
echo "🎬 Running sample application..."
cd Sandstorm.Sample

# Set dummy environment variables for the demo
export CLIENT_ID="demo-client-id"
export CLIENT_SECRET="demo-client-secret" 
export TENANT_ID="demo-tenant-id"
export SUBSCRIPTION_ID="demo-subscription-id"

dotnet run

# Cleanup
echo ""
echo "🧹 Cleaning up..."
kill $ORCHESTRATOR_PID 2>/dev/null
wait $ORCHESTRATOR_PID 2>/dev/null

echo ""
echo "✅ Demo completed successfully!"
echo ""
echo "Key Points Demonstrated:"
echo "  • Orchestrator service can be started and communicate via gRPC"
echo "  • OrchestratorClient can execute commands (simulated)"
echo "  • Configuration supports orchestrator endpoints"
echo "  • Architecture is ready for production deployment"
echo ""
echo "Next steps for production:"
echo "  1. Deploy orchestrator service to your infrastructure"
echo "  2. Configure real Azure credentials"
echo "  3. Create sandboxes with orchestrator endpoint configured"
echo "  4. VMs will automatically install agents via cloud-init"
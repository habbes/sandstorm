using Xunit;
using Sandstorm.Core;
using Sandstorm.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Sandstorm.Core.Tests;

public class OrchestratorIntegrationTests
{
    [Fact]
    public void OrchestratorClient_CanBeCreated()
    {
        // Arrange & Act
        using var client = new OrchestratorClient("http://localhost:5000");
        
        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public async Task OrchestratorClient_ExecuteCommandAsync_ReturnsResult()
    {
        // Arrange
        using var client = new OrchestratorClient("http://localhost:5000");
        
        // Act
        var result = await client.ExecuteCommandAsync("test-sandbox", "echo hello", TimeSpan.FromMinutes(1));
        
        // Assert - Since we're not running a real orchestrator, we expect an error result
        Assert.NotNull(result);
        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("Orchestrator communication failed", result.StandardError);
    }

    [Fact]
    public async Task OrchestratorClient_IsSandboxReadyAsync_ReturnsTrue()
    {
        // Arrange
        using var client = new OrchestratorClient("http://localhost:5000");
        
        // Act
        var isReady = await client.IsSandboxReadyAsync("test-sandbox");
        
        // Assert - Since we're not running a real orchestrator, we expect false
        Assert.False(isReady);
    }

    [Fact(Skip = "Integration test - requires running orchestrator and agent")]
    public async Task OrchestratorClient_WithRunningOrchestrator_ExecutesRealCommand()
    {
        // This test demonstrates how to test with a real orchestrator
        // To run this test:
        // 1. Start the orchestrator: dotnet run --project Sandstorm.Orchestrator
        // 2. Start an agent with SANDSTORM_SANDBOX_ID=integration-test
        // 3. Remove the Skip attribute from this test
        
        // Arrange
        using var client = new OrchestratorClient("http://localhost:5000");
        var sandboxId = "integration-test";
        
        // Wait for agent to be ready
        var ready = false;
        for (int i = 0; i < 30; i++)
        {
            ready = await client.IsSandboxReadyAsync(sandboxId);
            if (ready) break;
            await Task.Delay(1000);
        }
        
        Assert.True(ready, "Agent should be ready for integration test");
        
        // Act
        var result = await client.ExecuteCommandAsync(sandboxId, "echo 'Hello from real agent!'", TimeSpan.FromMinutes(1));
        
        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello from real agent!", result.StandardOutput);
    }

    [Fact]
    public void SandstormClient_HasOrchestratorEndpoint()
    {
        // Arrange
        var mockProvider = new Mock<ICloudProvider>();
        
        // Act
        var client = new SandstormClient(mockProvider.Object);
        
        // Assert
        Assert.Equal("http://localhost:5000", client.OrchestratorEndpoint);
    }

    [Fact]
    public void SandstormClient_OrchestratorEndpoint_CanBeCustomized()
    {
        // Arrange
        var mockProvider = new Mock<ICloudProvider>();
        var customEndpoint = "https://orchestrator.example.com";
        
        // Act
        var client = new SandstormClient(mockProvider.Object, customEndpoint);
        
        // Assert
        Assert.Equal(customEndpoint, client.OrchestratorEndpoint);
    }
}
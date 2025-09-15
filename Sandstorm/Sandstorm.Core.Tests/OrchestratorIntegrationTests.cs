using Xunit;
using Sandstorm.Core;
using Sandstorm.Core.Services;
using Microsoft.Extensions.Logging;

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
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains("Command executed via agent", result.StandardOutput);
    }

    [Fact]
    public async Task OrchestratorClient_IsSandboxReadyAsync_ReturnsTrue()
    {
        // Arrange
        using var client = new OrchestratorClient("http://localhost:5000");
        
        // Act
        var isReady = await client.IsSandboxReadyAsync("test-sandbox");
        
        // Assert
        Assert.True(isReady);
    }

    [Fact]
    public void SandboxConfiguration_OrchestratorEndpoint_HasDefaultValue()
    {
        // Arrange & Act
        var config = new SandboxConfiguration();
        
        // Assert
        Assert.Equal("http://localhost:5000", config.OrchestratorEndpoint);
    }

    [Fact]
    public void SandboxConfiguration_OrchestratorEndpoint_CanBeSet()
    {
        // Arrange
        var config = new SandboxConfiguration();
        var customEndpoint = "https://orchestrator.example.com";
        
        // Act
        config.OrchestratorEndpoint = customEndpoint;
        
        // Assert
        Assert.Equal(customEndpoint, config.OrchestratorEndpoint);
    }
}
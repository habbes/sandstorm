using Moq;
using Sandstorm.Core;
using Sandstorm.Core.Providers;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Sandstorm.Core.Tests;

public class SandstormClientTests
{
    [Fact]
    public void SandstormClient_Constructor_RequiresCloudProvider()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SandstormClient(null!));
    }

    [Fact]
    public void SandstormClient_Constructor_AcceptsValidProvider()
    {
        // Arrange
        var mockProvider = new Mock<ICloudProvider>();
        
        // Act
        var client = new SandstormClient(mockProvider.Object);
        
        // Assert
        Assert.NotNull(client);
        Assert.NotNull(client.Sandboxes);
    }

    [Fact]
    public async Task SandboxManager_CreateAsync_WithoutConfig_UsesDefaultConfiguration()
    {
        // Arrange
        var mockProvider = new Mock<ICloudProvider>();
        var mockSandbox = new Mock<ISandbox>();
        
        mockProvider.Setup(p => p.CreateSandboxAsync(It.IsAny<SandboxConfiguration>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(mockSandbox.Object);
        
        var client = new SandstormClient(mockProvider.Object);
        
        // Act
        var sandbox = await client.Sandboxes.CreateAsync();
        
        // Assert
        Assert.NotNull(sandbox);
        mockProvider.Verify(p => p.CreateSandboxAsync(It.IsAny<SandboxConfiguration>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SandboxManager_CreateAsync_WithConfig_UsesProvidedConfiguration()
    {
        // Arrange
        var mockProvider = new Mock<ICloudProvider>();
        var mockSandbox = new Mock<ISandbox>();
        var config = new SandboxConfiguration { Name = "test-sandbox" };
        
        mockProvider.Setup(p => p.CreateSandboxAsync(config, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(mockSandbox.Object);
        
        var client = new SandstormClient(mockProvider.Object);
        
        // Act
        var sandbox = await client.Sandboxes.CreateAsync(config);
        
        // Assert
        Assert.NotNull(sandbox);
        mockProvider.Verify(p => p.CreateSandboxAsync(config, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class SandboxConfigurationTests
{
    [Fact]
    public void SandboxConfiguration_DefaultValues_AreValid()
    {
        // Act
        var config = new SandboxConfiguration();
        
        // Assert
        Assert.NotNull(config.Name);
        Assert.NotEmpty(config.Name);
        Assert.Equal("westus2", config.Region);
        Assert.Equal("Standard_B2s", config.VmSize);
        Assert.Equal("sandboxuser", config.AdminUsername);
        Assert.NotNull(config.AdminPassword);
        Assert.NotEmpty(config.AdminPassword);
        Assert.NotNull(config.Tags);
    }

    [Fact]
    public void SandboxConfiguration_GeneratesUniqueNames()
    {
        // Act
        var config1 = new SandboxConfiguration();
        var config2 = new SandboxConfiguration();
        
        // Assert
        Assert.NotEqual(config1.Name, config2.Name);
    }
}

public class CodeRequestTests
{
    [Fact]
    public void CSharpCode_DefaultTimeout_IsFiveMinutes()
    {
        // Act
        var code = new CSharpCode { Code = "Console.WriteLine(\"Hello\");" };
        
        // Assert
        Assert.Equal(TimeSpan.FromMinutes(5), code.Timeout);
        Assert.NotNull(code.Dependencies);
        Assert.NotNull(code.NuGetPackages);
    }

    [Fact]
    public void PythonCode_DefaultValues_AreCorrect()
    {
        // Act
        var code = new PythonCode { Code = "print('Hello')" };
        
        // Assert
        Assert.Equal(TimeSpan.FromMinutes(5), code.Timeout);
        Assert.Equal("3.11", code.PythonVersion);
        Assert.NotNull(code.Dependencies);
        Assert.NotNull(code.PipPackages);
    }

    [Fact]
    public void JavaScriptCode_DefaultValues_AreCorrect()
    {
        // Act
        var code = new JavaScriptCode { Code = "console.log('Hello')" };
        
        // Assert
        Assert.Equal(TimeSpan.FromMinutes(5), code.Timeout);
        Assert.Equal("18", code.NodeVersion);
        Assert.NotNull(code.Dependencies);
        Assert.NotNull(code.NpmPackages);
    }
}

public class AzureProviderTests
{
    [Fact]
    public void AzureProvider_Constructor_RequiresCredentials()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AzureProvider(null!, "subscription"));
    }

    [Fact]
    public void AzureProvider_Constructor_RequiresSubscriptionId()
    {
        // Arrange
        var credential = new Mock<Azure.Core.TokenCredential>();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AzureProvider(credential.Object, null!));
    }

    [Fact]
    public void AzureProvider_Constructor_AcceptsValidParameters()
    {
        // Arrange
        var credential = new DefaultAzureCredential();
        var subscriptionId = "test-subscription";
        
        // Act
        var provider = new AzureProvider(credential, subscriptionId);
        
        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void AzureProvider_ClientSecretConstructor_AcceptsValidParameters()
    {
        // Arrange
        var tenantId = "test-tenant";
        var clientId = "test-client";
        var clientSecret = "test-secret";
        var subscriptionId = "test-subscription";
        
        // Act
        var provider = new AzureProvider(tenantId, clientId, clientSecret, subscriptionId);
        
        // Assert
        Assert.NotNull(provider);
    }
}
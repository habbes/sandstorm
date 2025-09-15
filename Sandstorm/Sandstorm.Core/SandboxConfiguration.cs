namespace Sandstorm.Core;

/// <summary>
/// Configuration options for creating a sandbox
/// </summary>
public class SandboxConfiguration
{
    /// <summary>
    /// The name of the sandbox (will be used for VM naming)
    /// </summary>
    public string Name { get; set; } = $"sandbox-{Guid.NewGuid():N}";

    /// <summary>
    /// The Azure region where the sandbox should be created
    /// </summary>
    public string Region { get; set; } = "westus2";

    /// <summary>
    /// The VM size to use for the sandbox
    /// </summary>
    public string VmSize { get; set; } = "Standard_B2s";

    /// <summary>
    /// The admin username for the VM
    /// </summary>
    public string AdminUsername { get; set; } = "sandboxuser";

    /// <summary>
    /// The admin password for the VM
    /// </summary>
    public string AdminPassword { get; set; } = GenerateRandomPassword();

    /// <summary>
    /// Tags to apply to the created resources
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    /// <summary>
    /// The resource group name to use (if null, will create a new one)
    /// </summary>
    public string? ResourceGroupName { get; set; }

    /// <summary>
    /// Whether to auto-delete the sandbox after a specified time
    /// </summary>
    public TimeSpan? AutoDeleteAfter { get; set; }

    /// <summary>
    /// The orchestrator endpoint for agent communication (if null, uses direct SSH)
    /// </summary>
    public string? OrchestratorEndpoint { get; set; } = "http://localhost:5000";

    private static string GenerateRandomPassword()
    {
        // Generate a random password that meets Azure VM requirements
        var random = new Random();
        var chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var special = "!@#$%^&*";
        
        var password = new string(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray());
        password += special[random.Next(special.Length)];
        password += random.Next(10).ToString();
        
        return password;
    }
}
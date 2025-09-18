namespace Sandstorm.Core;

/// <summary>
/// Configuration options for creating a sandbox
/// </summary>
public class SandboxConfiguration
{
    /// <summary>
    /// The name of the sandbox)
    /// </summary>
    public string Name { get; set; } = $"{Guid.NewGuid():N}";
    public string ImageId { get; set; }
}
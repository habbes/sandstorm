namespace Sandstorm.Core;

/// <summary>
/// Represents code to be executed in a sandbox
/// </summary>
public abstract class CodeRequest
{
    /// <summary>
    /// The code content to execute
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Dependencies required for the code
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Timeout for code execution
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// C# code execution request
/// </summary>
public class CSharpCode : CodeRequest
{
    /// <summary>
    /// NuGet packages to install
    /// </summary>
    public List<string> NuGetPackages { get; set; } = new();
}

/// <summary>
/// Python code execution request
/// </summary>
public class PythonCode : CodeRequest
{
    /// <summary>
    /// Python packages to install via pip
    /// </summary>
    public List<string> PipPackages { get; set; } = new();

    /// <summary>
    /// Python version to use (e.g., "3.11", "3.10")
    /// </summary>
    public string PythonVersion { get; set; } = "3.11";
}

/// <summary>
/// JavaScript/Node.js code execution request
/// </summary>
public class JavaScriptCode : CodeRequest
{
    /// <summary>
    /// NPM packages to install
    /// </summary>
    public List<string> NpmPackages { get; set; } = new();

    /// <summary>
    /// Node.js version to use
    /// </summary>
    public string NodeVersion { get; set; } = "18";
}

/// <summary>
/// Shell command execution request
/// </summary>
public class ShellCommand
{
    /// <summary>
    /// The command to execute
    /// </summary>
    public required string Command { get; set; }

    /// <summary>
    /// Working directory for the command
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Environment variables for the command
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Timeout for command execution
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// AI agent task configuration
/// </summary>
public class OpenAiAgentTask
{
    /// <summary>
    /// The prompt for the AI agent
    /// </summary>
    public required string Prompt { get; set; }

    /// <summary>
    /// The OpenAI model to use
    /// </summary>
    public required string Model { get; set; }

    /// <summary>
    /// Maximum tokens for the response
    /// </summary>
    public int MaxTokens { get; set; } = 1000;

    /// <summary>
    /// Temperature for response generation
    /// </summary>
    public double Temperature { get; set; } = 0.7;
}
using Azure.Identity;
using dotenv.net;
using Sandstorm.Core;
using Sandstorm.Core.Providers;
using Sandstorm.Orchestrator;
using Sandstorm.Orchestrator.Services;
using Sandstorm.Orchestrator.Services.SandboxManagement;

DotEnv.Load(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 3));


var tenantId = Environment.GetEnvironmentVariable("TENANT_ID") ?? throw new Exception("TENANT_ID env var must be set");
var clientId = Environment.GetEnvironmentVariable("CLIENT_ID") ?? throw new Exception("CLIENT_ID env var must be set");
var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET") ?? throw new Exception("CLIENT_SECRET env var must be set");
var subscriptionId = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID") ?? throw new Exception("SUBSCRIPTION_ID env var must be set");
var orchestratorEndpoint = Environment.GetEnvironmentVariable("ORCHESTRATOR_ENDPOINT") ?? "http://localhost:5000";

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to support HTTP/2 without TLS for gRPC
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5000, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});

builder.Services.AddLogging();
builder.Services.AddSingleton<OrchestratorEndpoint>(_ => new OrchestratorEndpoint(orchestratorEndpoint));
builder.Services.AddSingleton<OrchestratorState>();
builder.Services.AddSingleton<ICloudProvider>(sp => new AzureProvider(
    new ClientSecretCredential(tenantId, clientId, clientSecret),
    subscriptionId,
    tenantId,
    sp.GetRequiredService<ILogger<AzureProvider>>()));
builder.Services.AddSingleton<Sandstorm.Orchestrator.SandboxManager>();
builder.Services.AddSingleton<SandboxManagementService>();
builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<OrchestratorAgentService>();
app.MapGet("/", () => "Sandstorm API - Communication with gRPC endpoints must be made through a gRPC client. For sandbox management, use the REST API endpoints.");

// Sandbox management endpoints
app.MapPost("/api/sandboxes", async (SandboxManagementService service, CreateSandboxRequest? request) => 
{
    try
    {
        var result = await service.CreateSandbox(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.MapGet("/api/sandboxes/{sandboxId}", async (SandboxManagementService service, string sandboxId) => 
{
    try
    {
        var result = await service.GetSandbox(sandboxId);
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.MapGet("/api/sandboxes", async (SandboxManagementService service) => 
{
    try
    {
        var result = await service.ListSandboxes();
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.MapDelete("/api/sandboxes/{sandboxId}", async (SandboxManagementService service, string sandboxId) => 
{
    try
    {
        await service.DeleteSandbox(sandboxId);
        return Results.Ok(new { Message = "Sandbox deletion initiated" });
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

// Command execution endpoints
app.MapPost("/api/sandboxes/{sandboxId}/commands", async (SandboxManagementService service, string sandboxId, SendCommandRequest request) => 
{
    if (request.SandboxId != sandboxId)
    {
        return Results.BadRequest("SandboxId in URL and request body must match");
    }
    
    try
    {
        var result = await service.SendCommand(request);
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.MapGet("/api/sandboxes/{sandboxId}/commands/{processId}/status", async (SandboxManagementService service, string sandboxId, string processId) => 
{
    try
    {
        var result = await service.GetCommandStatus(new GetCommandStatusRequest(sandboxId, processId));
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.MapGet("/api/sandboxes/{sandboxId}/commands/{processId}/logs", async (SandboxManagementService service, string sandboxId, string processId) => 
{
    try
    {
        var result = await service.GetCommandLogs(new GetCommandLogsRequest(sandboxId, processId));
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.MapDelete("/api/sandboxes/{sandboxId}/commands/{processId}", async (SandboxManagementService service, string sandboxId, string processId) => 
{
    try
    {
        await service.TerminateProcess(new TerminateProcessRequest(sandboxId, processId));
        return Results.Ok(new { Message = "Process terminated" });
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.Run();
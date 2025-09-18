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
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.MapPost("/sandboxes", (SandboxManagementService sandboxes) => sandboxes.CreateSandbox());

app.MapPost("/commands", (SandboxManagementService sandboxes, SendCommandRequest req) => sandboxes.SendCommand(req));

app.Run();
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sandstorm.Agent;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<AgentWorker>();
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

var host = builder.Build();
await host.RunAsync();
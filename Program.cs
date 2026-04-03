using MaxiumDoorsFunctionApp;
using MaxiumDoorsFunctionApp.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

var cosmosEndpoint = GetRequiredSetting(builder.Configuration, "CosmosDb:Endpoint");
var cosmosKey = GetRequiredSetting(builder.Configuration, "CosmosDb:Key");

builder.Services.AddSingleton(_ => new CosmosClient(
    cosmosEndpoint,
    cosmosKey,
    new CosmosClientOptions
    {
        ConnectionMode = ConnectionMode.Gateway
    }));

builder.Services.AddSingleton<CosmosOrderRepository>();
builder.Services.AddSingleton<IEmailService, SendGridEmailService>();

builder.Build().Run();

static string? GetSetting(IConfiguration configuration, string key)
{
    return configuration[key]
        ?? configuration[key.Replace(":", "__", StringComparison.Ordinal)]
        ?? configuration[$"Values:{key}"]
        ?? configuration[$"Values:{key.Replace(":", "__", StringComparison.Ordinal)}"];
}

static string GetRequiredSetting(IConfiguration configuration, string key)
{
    return GetSetting(configuration, key)
        ?? throw new InvalidOperationException($"Configuration value '{key}' is missing.");
}
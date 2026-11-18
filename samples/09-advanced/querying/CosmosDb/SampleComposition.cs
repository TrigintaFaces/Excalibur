// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Data.CosmosDb;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

internal static class SampleComposition
{
    internal static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddExcaliburCosmosDb(cosmos =>
        {
            // The host owns this selected client; the provider borrows it.
            cosmos.ClientFactory(CreateClient)
                .DatabaseName(builder.Configuration["CosmosDb:DatabaseName"] ?? "ExcaliburSample")
                .ContainerName(builder.Configuration["CosmosDb:DefaultContainerName"] ?? "Items");
        });

        // Advanced options via Configure<CosmosDbOptions> post-configure
        builder.Services.Configure<CosmosDbOptions>(options =>
        {
            options.Name = "SampleProvider";
            options.Client.AccountEndpoint = builder.Configuration["CosmosDb:Client:AccountEndpoint"] ?? "https://localhost:8081";
            options.Client.AccountKey = builder.Configuration["CosmosDb:Client:AccountKey"] ??
                "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            options.DefaultPartitionKeyPath = "/category";

            // 7. Multi-region awareness -- configure preferred regions for geo-redundancy
            //    (The emulator uses a single region; in production, list your Azure regions.)
            options.Client.PreferredRegions = ["West US", "East US"];
            // The documented Linux vNext emulator supports gateway only; cloud deployments can opt in.
            var configuredDirectMode = builder.Configuration["CosmosDb:Client:UseDirectMode"];
            options.Client.UseDirectMode = configuredDirectMode != null && bool.Parse(configuredDirectMode);
            options.Client.Resilience.MaxRetryAttempts = 5;
            options.Client.Resilience.MaxRetryWaitTimeInSeconds = 30;
            options.Client.Resilience.RequestTimeoutInSeconds = 15;

            options.AllowBulkExecution = false;
            options.MaxConnectionsPerEndpoint = 50;
            options.EnableTcpConnectionEndpointRediscovery = true;
        });

        // ---------------------------------------------------------------------------
        // 6. Health Check registration
        // ---------------------------------------------------------------------------
        builder.Services.AddHealthChecks()
            .AddCosmosDb(name: "cosmosdb-sample", tags: ["ready", "cosmosdb"]);
        return builder.Build();
    }

    private static Microsoft.Azure.Cosmos.CosmosClient CreateClient(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
        var clientOptions = new Microsoft.Azure.Cosmos.CosmosClientOptions
        {
            ConnectionMode = options.Client.UseDirectMode
                ? Microsoft.Azure.Cosmos.ConnectionMode.Direct
                : Microsoft.Azure.Cosmos.ConnectionMode.Gateway,
            UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = ProductJsonContext.Default,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            },
            ApplicationPreferredRegions = options.Client.PreferredRegions,
            MaxRetryAttemptsOnRateLimitedRequests = options.Client.Resilience.MaxRetryAttempts,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(options.Client.Resilience.MaxRetryWaitTimeInSeconds),
            RequestTimeout = TimeSpan.FromSeconds(options.Client.Resilience.RequestTimeoutInSeconds),
            AllowBulkExecution = options.AllowBulkExecution,
        };

        if (options.Client.UseDirectMode)
        {
            clientOptions.MaxTcpConnectionsPerEndpoint = options.MaxConnectionsPerEndpoint;
            clientOptions.EnableTcpConnectionEndpointRediscovery = options.EnableTcpConnectionEndpointRediscovery;
        }
        else
        {
            clientOptions.GatewayModeMaxConnectionLimit = options.MaxConnectionsPerEndpoint;
        }

        return new Microsoft.Azure.Cosmos.CosmosClient(
            options.Client.AccountEndpoint ?? throw new InvalidOperationException("Cosmos endpoint is required."),
            options.Client.AccountKey ?? throw new InvalidOperationException("Cosmos key is required."),
            clientOptions);
    }
}

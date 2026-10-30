// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Examples.TransactionalInline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var provider = (Environment.GetEnvironmentVariable("TRANSACTIONAL_INLINE_PROVIDER") ?? "sqlserver").ToLowerInvariant();
var connectionString = Environment.GetEnvironmentVariable("TRANSACTIONAL_INLINE_SQL")
    ?? (provider == "sqlite" ? "Data Source=transactional-inline.db" : throw new InvalidOperationException("Set TRANSACTIONAL_INLINE_SQL to an existing disposable database."));
var schema = await SampleSchema.CreateAsync(connectionString, provider);
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddScoped<OrderApplicationService>();
SampleProviders.RegisterModels(builder.Services, provider, connectionString, schema);
builder.Services.AddExcalibur(x => x.AddEventSourcing(es =>
{
    SampleProviders.UseEventStore(es, provider, connectionString, schema);
    es.AddRepository<Order>(_ => new Order());
    es.RegisterEventTypesFromAssembly(typeof(Order).Assembly);
    es.AddProjection<OrderSummary>(p => p.TransactionalInline(nameof(Order))
        .When<AmountAdded>((view, e) =>
        {
            if (e.Amount < 0) { throw new InvalidOperationException("Sample projection refuses negative amounts."); }
            view.Total += e.Amount;
        }));
}));
using var host = builder.Build();
await host.StartAsync();
try
{
    await ConsumerScenario.RunAsync(host.Services, CancellationToken.None);
}
catch (AppendOutcomeUnknownException)
{
    await Console.Error.WriteLineAsync("Commit outcome is unknown. Reconcile the original operation before submitting another command.");
    throw;
}
await host.StopAsync();

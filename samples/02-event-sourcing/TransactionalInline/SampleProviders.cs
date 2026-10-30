// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Oracle;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Examples.TransactionalInline;

internal static class SampleProviders
{
    internal static void RegisterModels(IServiceCollection services, string provider, string connectionString, string schema)
    {
        switch (provider)
        {
            case "sqlserver":
                services.AddSqlServerProjectionStore<OrderSummary>(options => { options.ConnectionString = connectionString; options.TableName = "TransactionalOrderSummary"; });
                break;
            case "postgres":
                services.AddPostgresProjectionStore<OrderSummary>(options => { options.ConnectionString = connectionString; options.TableName = "TransactionalOrderSummary"; });
                break;
            case "sqlite":
                services.AddSqliteProjectionStore<OrderSummary>(connectionString, "TransactionalOrderSummary");
                break;
            case "oracle":
                services.AddOracleProjectionStore<OrderSummary>(connectionString, "TransactionalOrderSummary", schemaName: schema);
                break;
        }
    }

    internal static void UseEventStore(IEventSourcingBuilder es, string provider, string connectionString, string schema)
    {
        switch (provider)
        {
            case "sqlserver": es.UseSqlServer(sql => sql.ConnectionString(connectionString)); break;
            case "postgres": es.UsePostgres(pg => pg.ConnectionString(connectionString)); break;
            case "sqlite": es.UseSqlite(sqlite => sqlite.ConnectionString = connectionString); break;
            case "oracle": es.UseOracle(oracle => { oracle.ConnectionString = connectionString; oracle.Schema = schema; }); break;
        }
    }
}

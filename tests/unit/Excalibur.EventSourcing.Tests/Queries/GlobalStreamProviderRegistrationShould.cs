// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.InMemory;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Data.Sqlite;

using global::Oracle.ManagedDataAccess.Client;

namespace Excalibur.EventSourcing.Tests.Queries;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class GlobalStreamProviderRegistrationShould
{
	[Theory]
	[InlineData("sqlserver")]
    [InlineData("sqlserver-event-options")]
    [InlineData("sqlserver-event-factory")]
    [InlineData("sqlserver-all-options")]
    [InlineData("sqlserver-all-factory")]
	[InlineData("postgres")]
	[InlineData("sqlite")]
	[InlineData("inmemory")]
	[InlineData("oracle-factory")]
	[InlineData("oracle-options")]
	public async Task EnforceContiguityThroughEachPublicRegistration(string providerName)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(x => x.AddEventSourcing(es =>
		{
			switch (providerName)
			{
                case "sqlserver-event-options":
                    services.AddSqlServerEventStore(o => o.ConnectionString = "Server=localhost;Database=tests;Integrated Security=true");
                    break;
                case "sqlserver-event-factory":
                    services.AddSqlServerEventStore(() => new Microsoft.Data.SqlClient.SqlConnection("Server=localhost;Database=tests;Integrated Security=true"));
                    break;
                case "sqlserver-all-options":
                    services.AddSqlServerEventSourcing(o => o.ConnectionString = "Server=localhost;Database=tests;Integrated Security=true");
                    break;
                case "sqlserver-all-factory":
                    services.AddSqlServerEventSourcing(() => new Microsoft.Data.SqlClient.SqlConnection("Server=localhost;Database=tests;Integrated Security=true"));
                    break;
				case "sqlserver":
					_ = es.UseSqlServer(sql => sql.ConnectionString("Server=localhost;Database=tests;Integrated Security=true;TrustServerCertificate=true"));
					break;
				case "postgres":
					_ = es.UsePostgres(pg => pg.ConnectionString("Host=localhost;Database=tests;Username=tests;Password=tests"));
					break;
				case "sqlite":
					_ = es.UseSqlite(options => options.ConnectionString = "Data Source=:memory:");
					break;
				case "inmemory":
					_ = es.UseInMemory();
					break;
				case "oracle-factory":
					_ = services.AddOracleEventStore(() => new OracleConnection("Data Source=localhost;User Id=tests;Password=tests"));
					break;
				case "oracle-options":
					_ = services.AddOracleEventStore(options => options.ConnectionString = "Data Source=localhost;User Id=tests;Password=tests");
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(providerName));
			}
		}));
		await using var provider = services.BuildServiceProvider();
		var query = provider.GetRequiredService<IGlobalStreamQuery>();

		query.ShouldBeOfType<ContiguousGlobalStreamQuery>();
		// The guard must run before touching a database; a missing wrapper would attempt I/O here.
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
			await query.ReadAllAsync(new GlobalStreamPosition(-1, DateTimeOffset.MinValue), 10,
				TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task StopAtAnActualSqliteGapAndResumeOnceTheMissingPositionReturns()
	{
		var database = Path.Combine(Path.GetTempPath(), $"excalibur-contiguous-{Guid.NewGuid():N}.db");
		var connectionString = $"Data Source={database};Pooling=False";
		try
		{
			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddExcalibur(x => x.AddEventSourcing(es =>
			{
				_ = es.UseSqlite(options =>
				{
					options.ConnectionString = connectionString;
					options.EventStoreTable = "Events";
				});
			}));
			await using var provider = services.BuildServiceProvider();
			var store = provider.GetRequiredService<IEventStore>();
			var appended = await store.AppendAsync("order", "Order",
				[new TestOrderPlaced(), new TestOrderShipped(), new TestOrderPlaced()], -1,
				TestContext.Current.CancellationToken);
			appended.Success.ShouldBeTrue();
			var query = provider.GetRequiredService<IGlobalStreamQuery>();
			var initial = await query.ReadAllAsync(GlobalStreamPosition.Start, 10, TestContext.Current.CancellationToken);
			initial.Select(e => e.GlobalPosition).ShouldBe([1L, 2L, 3L]);
			(await query.GetHeadPositionAsync(TestContext.Current.CancellationToken)).ShouldBe(3);
			var sparse = await query.ReadByEventTypeAsync(initial[0].EventType, GlobalStreamPosition.Start, 10,
				TestContext.Current.CancellationToken);
			sparse.Select(e => e.GlobalPosition).ShouldBe([1L, 3L], "event-type reads are legitimately sparse");

			await using var connection = new SqliteConnection(connectionString);
			await connection.OpenAsync(TestContext.Current.CancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE Events SET GlobalPosition = 4 WHERE GlobalPosition = 2";
			(await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken)).ShouldBe(1);

			var stopped = await query.ReadAllAsync(GlobalStreamPosition.Start, 10, TestContext.Current.CancellationToken);
			stopped.Select(e => e.GlobalPosition).ShouldBe([1L]);

			command.CommandText = "UPDATE Events SET GlobalPosition = 2 WHERE GlobalPosition = 4";
			(await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
			var resumed = await query.ReadAllAsync(GlobalStreamPosition.Start, 10, TestContext.Current.CancellationToken);
			resumed.Select(e => e.GlobalPosition).ShouldBe([1L, 2L, 3L]);
		}
		finally
		{
			File.Delete(database);
		}
	}
}

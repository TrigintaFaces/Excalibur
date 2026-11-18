// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;
using System.Transactions;

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Postgres;

using Npgsql;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

[Collection(PostgresLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "Postgres")]
public sealed class PostgresFencingTokenAutonomyShould(PostgresContainerFixture fixture)
{
	[Fact]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "DDL identifiers are exclusively a test-generated GUID in N format or the provider's lowercase SHA256 sequence name; no external input is interpolated.")]
	public async Task PreserveFirstIssuedCounterAcrossCallerRollback()
	{
		fixture.DockerAvailable.ShouldBeTrue("this control requires an actual PostgreSQL sequence and transaction rollback");
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(45));
		var token = timeout.Token;
		var resource = "ambient-first-mint-" + Guid.NewGuid().ToString("N");
		var marker = "fence_business_" + Guid.NewGuid().ToString("N");
		var sequence = "fencing_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(resource)))[..55];
		var independent = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Enlist = false }.ConnectionString;
		var enlisted = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Enlist = true }.ConnectionString;
		await using var observer = new NpgsqlConnection(independent);
		await observer.OpenAsync(token).ConfigureAwait(false);
		await using (var create = new NpgsqlCommand($"CREATE TABLE {marker} (id integer PRIMARY KEY);", observer))
		{
			_ = await create.ExecuteNonQueryAsync(token).ConfigureAwait(false);
		}
		try
		{
			long first;
			using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
			{
				var ambient = Transaction.Current;
				ambient.ShouldNotBeNull();
				// Close the business connection before opening the fencing connection: this control
				// needs no simultaneous enlisted connections or distributed transaction promotion.
				await using (var business = new NpgsqlConnection(enlisted))
				{
					await business.OpenAsync(token).ConfigureAwait(false);
					await using var insert = new NpgsqlCommand($"INSERT INTO {marker} (id) VALUES (1);", business);
					_ = await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
				}
				await using var services = CreateServices(enlisted);
				var provider = services.GetRequiredService<IFencingTokenIssuer>();
				first = await provider.IssueTokenAsync(resource, token).ConfigureAwait(false);
				(await provider.GetTokenAsync(resource, token).ConfigureAwait(false)).ShouldBe(first);
				(await provider.ValidateTokenAsync(resource, first, token).ConfigureAwait(false)).ShouldBeTrue();
				Transaction.Current.ShouldBeSameAs(ambient);
				// Deliberately do not Complete: business state must roll back, infrastructure must not.
			}
			await using (var count = new NpgsqlCommand($"SELECT count(*) FROM {marker};", observer))
			{
				Convert.ToInt64(await count.ExecuteScalarAsync(token).ConfigureAwait(false)).ShouldBe(0);
			}
			await using var freshServices = CreateServices(enlisted);
			var fresh = freshServices.GetRequiredService<IFencingTokenIssuer>();
			var next = await fresh.IssueTokenAsync(resource, token).ConfigureAwait(false);
			next.ShouldBeGreaterThan(first);
			(await fresh.GetTokenAsync(resource, token).ConfigureAwait(false)).ShouldBe(next);
			(await fresh.ValidateTokenAsync(resource, first, token).ConfigureAwait(false)).ShouldBeFalse();
			(await fresh.ValidateTokenAsync(resource, next, token).ConfigureAwait(false)).ShouldBeTrue();
		}
		finally
		{
			using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			await using var cleanup = new NpgsqlCommand($"DROP TABLE IF EXISTS {marker}; DROP SEQUENCE IF EXISTS {sequence};", observer);
			_ = await cleanup.ExecuteNonQueryAsync(cleanupTimeout.Token).ConfigureAwait(false);
		}
	}

	private static ServiceProvider CreateServices(string connectionString)
	{
		var services = new ServiceCollection();
		_ = services.AddPostgresFencingTokenProvider(connectionString);
		return services.BuildServiceProvider();
	}
}

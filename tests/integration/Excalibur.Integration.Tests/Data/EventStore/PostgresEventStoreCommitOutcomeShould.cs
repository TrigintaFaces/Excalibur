// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Shouldly;
using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "FaultInjection")]
public sealed class PostgresEventStoreCommitOutcomeShould(PostgresEventStoreContainerFixture fixture)
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RefuseAmbientTransactionBeforeDatabaseAccess(bool transactional)
	{
		using var scope = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
		var store = new PostgresEventStore("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1",
			NullLogger<PostgresEventStore>.Instance, SingleTenantTestContext.Instance);
		var error = await Should.ThrowAsync<InvalidOperationException>(() => AppendAsync(store, "ambient-refusal", transactional).AsTask());
		error.Message.ShouldContain("ambient transaction");
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task PropagateCommitCancellationWithoutClaimingRollback(bool transactional, bool committed)
	{
		await InitializeAsync();
		var id = Guid.NewGuid().ToString("N");
		using var cancellationSource = new CancellationTokenSource();
		var cancellation = new OperationCanceledException("Injected commit cancellation", cancellationSource.Token);
		var store = new PostgresEventStore(fixture.ConnectionString, NullLogger<PostgresEventStore>.Instance, SingleTenantTestContext.Instance)
		{
			CommitTransactionAsync = async (transaction, token) =>
			{
				if (committed)
				{
					await transaction.CommitAsync(token);
				}
				await cancellationSource.CancelAsync();
				throw cancellation;
			}
		};

		var actual = await Should.ThrowAsync<OperationCanceledException>(() => AppendAsync(store, id, transactional).AsTask());
		actual.CancellationToken.ShouldBe(cancellationSource.Token);
		actual.CancellationToken.IsCancellationRequested.ShouldBeTrue();
		(await store.LoadAsync(id, "CommitOutcome", CancellationToken.None)).Count.ShouldBe(committed ? 1 : 0);
		(await CountOutboxAsync(id)).ShouldBe(transactional && committed ? 1L : 0L);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task ReportUnknownOnEitherSideOfCommitWithoutInventingAConflict(bool transactional, bool committed)
	{
		await InitializeAsync();
		var id = Guid.NewGuid().ToString("N");
		var store = new PostgresEventStore(fixture.ConnectionString, NullLogger<PostgresEventStore>.Instance, SingleTenantTestContext.Instance)
		{
			CommitTransactionAsync = async (transaction, token) =>
			{
				if (committed)
				{
					await transaction.CommitAsync(token);
				}
				throw new NpgsqlException("Injected loss of commit acknowledgement");
			}
		};

		var result = await AppendAsync(store, id, transactional);

		result.Outcome.ShouldBe(AppendOutcome.Unknown);
		result.NextExpectedVersion.ShouldBeNull();
		result.IsConcurrencyConflict.ShouldBeFalse();
		(await store.LoadAsync(id, "CommitOutcome", CancellationToken.None)).Count.ShouldBe(committed ? 1 : 0);
		(await CountOutboxAsync(id)).ShouldBe(transactional && committed ? 1L : 0L);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task PreserveAcknowledgedCommitWhenLoggingThrows(bool transactional, bool cancellation)
	{
		await InitializeAsync();
		var id = Guid.NewGuid().ToString("N");
		var logger = new ThrowAfterAppendLogger(cancellation);
		var store = new PostgresEventStore(fixture.ConnectionString, logger, SingleTenantTestContext.Instance);

		var result = await AppendAsync(store, id, transactional);

		result.Outcome.ShouldBe(AppendOutcome.Committed);
		logger.FaultCount.ShouldBe(1, "the post-acknowledgement fault must actually execute");
		(await store.LoadAsync(id, "CommitOutcome", CancellationToken.None)).Count.ShouldBe(1);
		(await CountOutboxAsync(id)).ShouldBe(transactional ? 1L : 0L);
	}

	private async Task InitializeAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue("Commit-outcome evidence requires real PostgreSQL; never skip.");
		await fixture.EnsureInitializedAsync();
		await using var connection = new NpgsqlConnection(fixture.ConnectionString);
		await connection.OpenAsync();
		await using var command = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS commit_outcome_outbox (id text PRIMARY KEY)", connection);
		await command.ExecuteNonQueryAsync();
	}

	private static ValueTask<AppendResult> AppendAsync(PostgresEventStore store, string id, bool transactional)
	{
		TestDomainEvent[] events = [new() { AggregateId = id, Data = "stable-payload" }];
		return transactional
			? store.AppendWithOutboxStagingAsync(id, "CommitOutcome", events, -1, StageAsync, CancellationToken.None)
			: store.AppendAsync(id, "CommitOutcome", events, -1, CancellationToken.None);

		async ValueTask StageAsync(IDbTransaction transaction, CancellationToken token)
		{
			var postgresTransaction = (NpgsqlTransaction)transaction;
			await using var command = new NpgsqlCommand("INSERT INTO commit_outcome_outbox (id) VALUES (@id)", postgresTransaction.Connection, postgresTransaction);
			command.Parameters.AddWithValue("id", id);
			await command.ExecuteNonQueryAsync(token);
		}
	}

	private async Task<long> CountOutboxAsync(string id)
	{
		await using var connection = new NpgsqlConnection(fixture.ConnectionString);
		await connection.OpenAsync();
		await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM commit_outcome_outbox WHERE id = @id", connection);
		command.Parameters.AddWithValue("id", id);
		return (long)(await command.ExecuteScalarAsync())!;
	}

	private sealed class ThrowAfterAppendLogger(bool cancellation) : ILogger<PostgresEventStore>
	{
		public int FaultCount { get; private set; }
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public bool IsEnabled(LogLevel logLevel) => true;
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (formatter(state, exception).StartsWith("Appended", StringComparison.Ordinal))
			{
				FaultCount++;
				if (cancellation)
				{
					throw new OperationCanceledException("Injected post-acknowledgement logging cancellation");
				}
				throw new NpgsqlException("Injected post-acknowledgement logging failure");
			}
		}
	}
}

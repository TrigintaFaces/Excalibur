// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Integration.Tests.Data.DeadLetter;

/// <summary>Real-provider counterexample: a nonquery transport count is not authoritative CAS evidence.</summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerDeadLetterTestCollection.CollectionName)]
public sealed class SqlServerDeadLetterNoCountShould(SqlServerContainerFixture fixture) : IAsyncLifetime
{
	private const string Tenant = "nocount-owner";
	private readonly string _tableName = "DeadLetterNoCount_" + Guid.NewGuid().ToString("N");

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task AdmitOneWinnerAndRefuseTheNextCasRegardlessOfNoCount(bool noCount)
	{
		var id = await Queue(() => new SqlConnection(fixture.ConnectionString)).EnqueueAsync(
			new Payload("order"), DeadLetterReason.MaxRetriesExceeded, CancellationToken.None).ConfigureAwait(false);
		await using var sessions = await PreparedSessions.CreateAsync(fixture.ConnectionString, noCount, 4).ConfigureAwait(false);
		var deliveries = 0;
		var queue = Queue(sessions.Take, _ => { _ = Interlocked.Increment(ref deliveries); return Task.CompletedTask; });

		var first = await queue.ReplayAsync(id, CancellationToken.None).ConfigureAwait(false);
		var second = await queue.ReplayAsync(id, CancellationToken.None).ConfigureAwait(false);

		Volatile.Read(ref deliveries).ShouldBe(1, "a zero-row CAS on the second call must never authorize another handler invocation, including when ExecuteNonQuery returns -1");
		first.ShouldBeTrue();
		second.ShouldBeFalse();
		var state = await StateAsync(id).ConfigureAwait(false);
		state.IsReplayed.ShouldBeTrue();
		state.ReplayedAt.ShouldNotBeNull();
		state.TenantId.ShouldBe(Tenant);
	}

	[Fact]
	public async Task NeverDeliverIfTheClaimStatementRollsBack()
	{
		var id = await Queue(() => new SqlConnection(fixture.ConnectionString)).EnqueueAsync(
			new Payload("rollback"), DeadLetterReason.MaxRetriesExceeded, CancellationToken.None).ConfigureAwait(false);
		await using (var setup = new SqlConnection(fixture.ConnectionString))
		{
			_ = await setup.ExecuteAsync($"""
				CREATE TRIGGER [dbo].[Throw_{_tableName}] ON [dbo].[{_tableName}] AFTER UPDATE AS
				BEGIN
				    SET NOCOUNT ON;
				    THROW 51000, 'injected claim rollback', 1;
				END;
				""").ConfigureAwait(false);
		}
		await using var sessions = await PreparedSessions.CreateAsync(fixture.ConnectionString, noCount: true, count: 2).ConfigureAwait(false);
		var deliveries = 0;
		var queue = Queue(sessions.Take, _ => { _ = Interlocked.Increment(ref deliveries); return Task.CompletedTask; });

		var error = await Should.ThrowAsync<SqlException>(() => queue.ReplayAsync(id, CancellationToken.None));

		error.Number.ShouldBe(51000);
		Volatile.Read(ref deliveries).ShouldBe(0);
		var state = await StateAsync(id).ConfigureAwait(false);
		state.IsReplayed.ShouldBeFalse();
		state.ReplayedAt.ShouldBeNull();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task IgnoreTriggerDmlCountsAndMisleadingScalarResults(bool noCount)
	{
		var id = await Queue(() => new SqlConnection(fixture.ConnectionString)).EnqueueAsync(
			new Payload("trigger"), DeadLetterReason.MaxRetriesExceeded, CancellationToken.None).ConfigureAwait(false);
		await using var setup = new SqlConnection(fixture.ConnectionString);
		_ = await setup.ExecuteAsync($"CREATE TABLE [dbo].[Audit_{_tableName}] (Value INT NOT NULL)").ConfigureAwait(false);
		try
		{
			_ = await setup.ExecuteAsync($"""
				CREATE TRIGGER [dbo].[AuditTrigger_{_tableName}] ON [dbo].[{_tableName}] AFTER UPDATE AS
				BEGIN
				    SET NOCOUNT OFF;
				    INSERT INTO [dbo].[Audit_{_tableName}] (Value) VALUES (1), (2), (3);
				    SELECT CAST(-777 AS INT) AS MisleadingMutationCount;
				END;
				""").ConfigureAwait(false);
			await using var sessions = await PreparedSessions.CreateAsync(fixture.ConnectionString, noCount, 4).ConfigureAwait(false);
			var deliveries = 0;
			var queue = Queue(sessions.Take, _ => { _ = Interlocked.Increment(ref deliveries); return Task.CompletedTask; });
			(await queue.ReplayAsync(id, CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();
			(await queue.ReplayAsync(id, CancellationToken.None).ConfigureAwait(false)).ShouldBeFalse();
			Volatile.Read(ref deliveries).ShouldBe(1);
			(await setup.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM [dbo].[Audit_{_tableName}]").ConfigureAwait(false)).ShouldBe(6,
				"the trigger executed for both the winning and zero-row UPDATE, emitting a different count and scalar result");
			var state = await StateAsync(id).ConfigureAwait(false);
			state.IsReplayed.ShouldBeTrue();
			state.ReplayedAt.ShouldNotBeNull();
			state.TenantId.ShouldBe(Tenant);
		}
		finally
		{
			_ = await setup.ExecuteAsync($"DROP TRIGGER IF EXISTS [dbo].[AuditTrigger_{_tableName}]; DROP TABLE IF EXISTS [dbo].[Audit_{_tableName}]").ConfigureAwait(false);
		}
	}

	public async ValueTask InitializeAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue("this control requires real SQL Server; infrastructure absence is a failure");
		await using var connection = new SqlConnection(fixture.ConnectionString);
		_ = await connection.ExecuteAsync($"""
			CREATE TABLE [dbo].[{_tableName}] (
			 Id UNIQUEIDENTIFIER NOT NULL, TenantId NVARCHAR(255) NOT NULL,
			 MessageType NVARCHAR(500) NOT NULL, Payload VARBINARY(MAX) NOT NULL, Reason INT NOT NULL,
			 ExceptionMessage NVARCHAR(MAX) NULL, ExceptionStackTrace NVARCHAR(MAX) NULL,
			 EnqueuedAt DATETIMEOFFSET NOT NULL, OriginalAttempts INT NOT NULL,
			 Metadata NVARCHAR(MAX) NULL, CorrelationId NVARCHAR(255) NULL, CausationId NVARCHAR(255) NULL,
			 SourceQueue NVARCHAR(255) NULL, IsReplayed BIT NOT NULL, ReplayedAt DATETIMEOFFSET NULL,
			 PRIMARY KEY (Id, TenantId));
			""").ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		_ = await connection.ExecuteAsync($"DROP TABLE IF EXISTS [dbo].[{_tableName}]").ConfigureAwait(false);
	}

	private SqlServerDeadLetterQueue Queue(Func<SqlConnection> factory, Func<object, Task>? handler = null) => new(
		factory, new SqlServerDeadLetterQueueOptions { TableName = _tableName },
		NullLogger<SqlServerDeadLetterQueue>.Instance, new FixedTenant(), handler);

	private async Task<Row> StateAsync(Guid id)
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		return await connection.QuerySingleAsync<Row>($"SELECT IsReplayed, ReplayedAt, TenantId FROM [dbo].[{_tableName}] WHERE Id=@Id AND TenantId=@TenantId",
			new { Id = id, TenantId = Tenant }).ConfigureAwait(false);
	}

	private sealed class FixedTenant : ITenantContext { public string? TenantId => Tenant; public bool HasTenant => true; }
	private sealed record Payload(string OrderId);
	private sealed class Row { public bool IsReplayed { get; set; } public DateTimeOffset? ReplayedAt { get; set; } public string TenantId { get; set; } = ""; }

	private sealed class PreparedSessions : IAsyncDisposable
	{
		private readonly ConcurrentQueue<SqlConnection> _available = new();
		private readonly List<SqlConnection> _all = [];
		internal static async Task<PreparedSessions> CreateAsync(string connectionString, bool noCount, int count)
		{
			var sessions = new PreparedSessions();
			try
			{
				for (var index = 0; index < count; index++)
				{
					var connection = new SqlConnection(connectionString);
					sessions._all.Add(connection);
					await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
					_ = await connection.ExecuteAsync(noCount ? "SET NOCOUNT ON" : "SET NOCOUNT OFF").ConfigureAwait(false);
					(await connection.ExecuteScalarAsync<int>("SELECT @@OPTIONS & 512").ConfigureAwait(false)).ShouldBe(noCount ? 512 : 0);
					sessions._available.Enqueue(connection);
				}
				return sessions;
			}
			catch { await sessions.DisposeAsync().ConfigureAwait(false); throw; }
		}
		internal SqlConnection Take() => _available.TryDequeue(out var connection) ? connection : throw new InvalidOperationException("The control exhausted its prepared sessions.");
		public async ValueTask DisposeAsync() { foreach (var connection in _all) { await connection.DisposeAsync().ConfigureAwait(false); } }
	}
}

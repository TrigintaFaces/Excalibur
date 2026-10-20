// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json;

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Integration.Tests.Data.DeadLetter;

/// <summary>Verifies that SQL replay completion records actual callback completion.</summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerDeadLetterTestCollection.CollectionName)]
public sealed class SqlServerDeadLetterReplayReadinessShould(SqlServerContainerFixture fixture) : IAsyncLifetime
{
	private const string StoredTenant = "replay-readiness-tenant";
	private readonly string _tableName = "DeadLetterReadiness_" + Guid.NewGuid().ToString("N");

	[Fact]
	public async Task RejectReplayWithoutAHandlerAndPreserveTheEntry()
	{
		var id = await EnqueueAsync().ConfigureAwait(false);
		var before = await ReadRowAsync(id).ConfigureAwait(false);
		_ = await Should.ThrowAsync<InvalidOperationException>(() => Queue().ReplayAsync(id, CancellationToken.None));
		await AssertUnchangedAsync(id, before).ConfigureAwait(false);
		(await Queue().GetEntryAsync(id, CancellationToken.None).ConfigureAwait(false)).ShouldNotBeNull();
	}

	[Fact]
	public async Task RejectJsonNullBeforeCallingTheHandlerOrMarkingReplay()
	{
		var id = await EnqueueAsync().ConfigureAwait(false);
		await ReplacePayloadAsync(id, "null").ConfigureAwait(false);
		var before = await ReadRowAsync(id).ConfigureAwait(false);
		var calls = 0;
		_ = await Should.ThrowAsync<InvalidOperationException>(() => Queue(_ =>
		{
			calls++;
			return Task.CompletedTask;
		}).ReplayAsync(id, CancellationToken.None));
		calls.ShouldBe(0);
		await AssertUnchangedAsync(id, before).ConfigureAwait(false);
	}

	[Fact]
	public async Task PreserveCorruptJsonAndPropagateTheDeserializationFailure()
	{
		var id = await EnqueueAsync().ConfigureAwait(false);
		await ReplacePayloadAsync(id, "{invalid-json").ConfigureAwait(false);
		var before = await ReadRowAsync(id).ConfigureAwait(false);
		var calls = 0;
		_ = await Should.ThrowAsync<JsonException>(() => Queue(_ =>
		{
			calls++;
			return Task.CompletedTask;
		}).ReplayAsync(id, CancellationToken.None));
		calls.ShouldBe(0);
		await AssertUnchangedAsync(id, before).ConfigureAwait(false);
	}

	[Fact]
	public async Task PreserveTheEntryAndOriginalCallbackFault()
	{
		var id = await EnqueueAsync().ConfigureAwait(false);
		var before = await ReadRowAsync(id).ConfigureAwait(false);
		var fault = new InvalidOperationException("callback failed");
		var calls = 0;
		var observed = await Should.ThrowAsync<InvalidOperationException>(() => Queue(_ =>
		{
			calls++;
			return Task.FromException(fault);
		}).ReplayAsync(id, CancellationToken.None));
		observed.ShouldBeSameAs(fault);
		calls.ShouldBe(1);
		await AssertUnchangedAsync(id, before).ConfigureAwait(false);
	}

	[Fact]
	public async Task PropagateBatchReadinessFailureWithoutMarkingTheFailedRow()
	{
		var id = await EnqueueAsync().ConfigureAwait(false);
		var before = await ReadRowAsync(id).ConfigureAwait(false);
		_ = await Should.ThrowAsync<InvalidOperationException>(() => Queue().ReplayBatchAsync(
			new DeadLetterQueryFilter(), 10, CancellationToken.None));
		await AssertUnchangedAsync(id, before).ConfigureAwait(false);
	}

	[Fact]
	public async Task MarkCompletionOnlyAfterTheCallbackAndRestoreTheStoredTenant()
	{
		var id = await EnqueueAsync().ConfigureAwait(false);
		var before = await ReadRowAsync(id).ConfigureAwait(false);
		var calls = 0;
		async Task Replay(object value)
		{
			calls++;
			TenantContextHolder.Current.ShouldBe(StoredTenant);
			var payload = value.ShouldBeOfType<JsonElement>();
			payload.GetProperty("orderId").GetString().ShouldBe("order-readiness");
			await AssertUnchangedAsync(id, before).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope("operator-tenant"))
		{
			(await Queue(Replay).ReplayAllTenantsEntryAsync(id, CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();
			TenantContextHolder.Current.ShouldBe("operator-tenant");
		}

		calls.ShouldBe(1);
		var after = await ReadRowAsync(id).ConfigureAwait(false);
		after.IsReplayed.ShouldBeTrue();
		after.ReplayedAt.ShouldNotBeNull();
		after.Payload.ShouldBe(before.Payload);
		after.TenantId.ShouldBe(before.TenantId);
	}

	[Fact]
	public async Task ReturnFalseForAnAbsentEntryEvenWithoutAHandler()
	{
		(await Queue().ReplayAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false)).ShouldBeFalse();
	}

	public async ValueTask InitializeAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue("replay readiness requires a real SQL Server; infrastructure absence is a failure");
		await using var connection = new SqlConnection(fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
		_ = await connection.ExecuteAsync($"""
			CREATE TABLE [dbo].[{_tableName}] (
			    Id UNIQUEIDENTIFIER NOT NULL,
			    TenantId NVARCHAR(255) NOT NULL,
			    MessageType NVARCHAR(500) NOT NULL,
			    Payload VARBINARY(MAX) NOT NULL,
			    Reason INT NOT NULL,
			    ExceptionMessage NVARCHAR(MAX) NULL,
			    ExceptionStackTrace NVARCHAR(MAX) NULL,
			    EnqueuedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
			    OriginalAttempts INT NOT NULL DEFAULT 0,
			    Metadata NVARCHAR(MAX) NULL,
			    CorrelationId NVARCHAR(255) NULL,
			    CausationId NVARCHAR(255) NULL,
			    SourceQueue NVARCHAR(255) NULL,
			    IsReplayed BIT NOT NULL DEFAULT 0,
			    ReplayedAt DATETIMEOFFSET NULL,
			    PRIMARY KEY (Id, TenantId)
			);
			""").ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		_ = await connection.ExecuteAsync($"DROP TABLE IF EXISTS [dbo].[{_tableName}]").ConfigureAwait(false);
	}

	private SqlServerDeadLetterQueue Queue(Func<object, Task>? handler = null) => new(
		() => new SqlConnection(fixture.ConnectionString),
		new SqlServerDeadLetterQueueOptions { TableName = _tableName },
		NullLogger<SqlServerDeadLetterQueue>.Instance,
		new FixedTenantContext(), handler);

	private Task<Guid> EnqueueAsync() => Queue().EnqueueAsync(
		new OrderPayload("order-readiness"), DeadLetterReason.MaxRetriesExceeded, CancellationToken.None);

	private async Task ReplacePayloadAsync(Guid id, string json)
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		(await connection.ExecuteAsync($"UPDATE [dbo].[{_tableName}] SET Payload = @Payload WHERE Id = @Id AND TenantId = @TenantId",
			new { Id = id, TenantId = StoredTenant, Payload = Encoding.UTF8.GetBytes(json) }).ConfigureAwait(false)).ShouldBe(1);
	}

	private async Task<ReplayRow> ReadRowAsync(Guid id)
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		return await connection.QuerySingleAsync<ReplayRow>(
			$"SELECT TenantId, Payload, IsReplayed, ReplayedAt FROM [dbo].[{_tableName}] WHERE Id = @Id AND TenantId = @TenantId",
			new { Id = id, TenantId = StoredTenant }).ConfigureAwait(false);
	}

	private async Task AssertUnchangedAsync(Guid id, ReplayRow before)
	{
		var after = await ReadRowAsync(id).ConfigureAwait(false);
		after.IsReplayed.ShouldBeFalse();
		after.ReplayedAt.ShouldBeNull();
		after.Payload.ShouldBe(before.Payload);
		after.TenantId.ShouldBe(before.TenantId);
	}

	private sealed class FixedTenantContext : ITenantContext
	{
		public string? TenantId => StoredTenant;
		public bool HasTenant => true;
	}

	private sealed class ReplayRow
	{
		public string TenantId { get; set; } = string.Empty;
		public byte[] Payload { get; set; } = [];
		public bool IsReplayed { get; set; }
		public DateTimeOffset? ReplayedAt { get; set; }
	}

	private sealed record OrderPayload(string OrderId);
}

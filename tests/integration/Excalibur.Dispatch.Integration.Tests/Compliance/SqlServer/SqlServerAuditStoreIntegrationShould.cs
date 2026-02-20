// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch.AuditLogging.SqlServer;
using Excalibur.Dispatch.Compliance;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", TestComponents.AuditLogging)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
public sealed class SqlServerAuditStoreIntegrationShould : IntegrationTestBase
{
	private readonly SqlServerFixture _fixture;

	public SqlServerAuditStoreIntegrationShould(SqlServerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Store_and_get_by_id_round_trip()
	{
		await InitializeAuditTableAsync().ConfigureAwait(true);
		using var store = CreateStore();
		var evt = CreateAuditEvent("evt-roundtrip", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-2));

		var stored = await store.StoreAsync(evt, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.GetByIdAsync(evt.EventId, TestCancellationToken).ConfigureAwait(true);

		stored.SequenceNumber.ShouldBeGreaterThan(0);
		loaded.ShouldNotBeNull();
		loaded!.EventId.ShouldBe(evt.EventId);
		loaded.Action.ShouldBe(evt.Action);
		loaded.ActorId.ShouldBe(evt.ActorId);
		loaded.Metadata.ShouldNotBeNull();
		loaded.Metadata!["scenario"].ShouldBe("integration");
	}

	[Fact]
	public async Task Query_and_count_with_filters()
	{
		await InitializeAuditTableAsync().ConfigureAwait(true);
		using var store = CreateStore();

		await store.StoreAsync(CreateAuditEvent("evt-q-1", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-4), actorId: "actor-a", action: "read"), TestCancellationToken).ConfigureAwait(true);
		await store.StoreAsync(CreateAuditEvent("evt-q-2", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-3), actorId: "actor-a", action: "write"), TestCancellationToken).ConfigureAwait(true);
		await store.StoreAsync(CreateAuditEvent("evt-q-3", "tenant-2", DateTimeOffset.UtcNow.AddMinutes(-2), actorId: "actor-b", action: "read"), TestCancellationToken).ConfigureAwait(true);

		var query = new AuditQuery
		{
			ActorId = "actor-a",
			TenantId = "tenant-1",
			MaxResults = 10,
			Skip = 0
		};

		var results = await store.QueryAsync(query, TestCancellationToken).ConfigureAwait(true);
		var count = await store.CountAsync(query, TestCancellationToken).ConfigureAwait(true);

		results.Count.ShouldBe(2);
		count.ShouldBe(2);
	}

	[Fact]
	public async Task Verify_chain_integrity_detects_tampering()
	{
		await InitializeAuditTableAsync().ConfigureAwait(true);
		using var store = CreateStore();

		var first = CreateAuditEvent("evt-v-1", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-2));
		var second = CreateAuditEvent("evt-v-2", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-1));

		await store.StoreAsync(first, TestCancellationToken).ConfigureAwait(true);
		await store.StoreAsync(second, TestCancellationToken).ConfigureAwait(true);

		var start = DateTimeOffset.UtcNow.AddHours(-1);
		var end = DateTimeOffset.UtcNow.AddHours(1);

		var validResult = await store.VerifyChainIntegrityAsync(start, end, TestCancellationToken).ConfigureAwait(true);
		validResult.IsValid.ShouldBeTrue();

		await using (var connection = new SqlConnection(_fixture.ConnectionString))
		{
			await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
			_ = await connection.ExecuteAsync(
				"UPDATE [audit].[AuditEvents] SET EventHash = @BadHash WHERE EventId = @EventId",
				new { BadHash = new string('F', 64), EventId = second.EventId }).ConfigureAwait(true);
		}

		var invalidResult = await store.VerifyChainIntegrityAsync(start, end, TestCancellationToken).ConfigureAwait(true);

		invalidResult.IsValid.ShouldBeFalse();
		invalidResult.ViolationCount.ShouldBeGreaterThan(0);
		invalidResult.FirstViolationEventId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Verify_chain_integrity_detects_chain_link_break()
	{
		await InitializeAuditTableAsync().ConfigureAwait(true);
		using var store = CreateStore();

		var first = CreateAuditEvent("evt-chain-1", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-2));
		var second = CreateAuditEvent("evt-chain-2", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-1));

		await store.StoreAsync(first, TestCancellationToken).ConfigureAwait(true);
		await store.StoreAsync(second, TestCancellationToken).ConfigureAwait(true);

		var start = DateTimeOffset.UtcNow.AddHours(-1);
		var end = DateTimeOffset.UtcNow.AddHours(1);

		await using (var connection = new SqlConnection(_fixture.ConnectionString))
		{
			await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
			_ = await connection.ExecuteAsync(
				"UPDATE [audit].[AuditEvents] SET PreviousEventHash = @BadHash WHERE EventId = @EventId",
				new { BadHash = new string('F', 64), EventId = second.EventId }).ConfigureAwait(true);
		}

		var invalidResult = await store.VerifyChainIntegrityAsync(start, end, TestCancellationToken).ConfigureAwait(true);

		invalidResult.IsValid.ShouldBeFalse();
		invalidResult.ViolationCount.ShouldBeGreaterThan(0);
		invalidResult.FirstViolationEventId.ShouldBe(second.EventId);
	}

	[Fact]
	public async Task Get_last_event_supports_tenant_filter()
	{
		await InitializeAuditTableAsync().ConfigureAwait(true);
		using var store = CreateStore();

		await store.StoreAsync(CreateAuditEvent("evt-last-1", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-3)), TestCancellationToken).ConfigureAwait(true);
		await store.StoreAsync(CreateAuditEvent("evt-last-2", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-2)), TestCancellationToken).ConfigureAwait(true);
		await store.StoreAsync(CreateAuditEvent("evt-last-3", "tenant-2", DateTimeOffset.UtcNow.AddMinutes(-1)), TestCancellationToken).ConfigureAwait(true);

		var tenantLast = await store.GetLastEventAsync("tenant-1", TestCancellationToken).ConfigureAwait(true);
		var overallLast = await store.GetLastEventAsync(null, TestCancellationToken).ConfigureAwait(true);

		tenantLast.ShouldNotBeNull();
		tenantLast!.EventId.ShouldBe("evt-last-2");
		overallLast.ShouldNotBeNull();
		overallLast!.EventId.ShouldBe("evt-last-3");
	}

	[Fact]
	public async Task Enforce_retention_deletes_old_rows_in_batches()
	{
		await InitializeAuditTableAsync().ConfigureAwait(true);
		using var store = CreateStore(options =>
		{
			options.RetentionCleanupBatchSize = 1;
		});

		await store.StoreAsync(CreateAuditEvent("evt-old", "tenant-1", DateTimeOffset.UtcNow.AddDays(-40)), TestCancellationToken).ConfigureAwait(true);
		await store.StoreAsync(CreateAuditEvent("evt-new", "tenant-1", DateTimeOffset.UtcNow.AddDays(-1)), TestCancellationToken).ConfigureAwait(true);

		var deleted = await store.EnforceRetentionAsync(DateTimeOffset.UtcNow.AddDays(-30), TestCancellationToken).ConfigureAwait(true);
		var remaining = await store.CountAsync(new AuditQuery(), TestCancellationToken).ConfigureAwait(true);

		deleted.ShouldBe(1);
		remaining.ShouldBe(1);
	}

	[Fact]
	public async Task Store_batch_persists_all_events()
	{
		await InitializeAuditTableAsync().ConfigureAwait(true);
		using var store = CreateStore();
		var events = new List<AuditEvent>
		{
			CreateAuditEvent("evt-batch-1", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-2)),
			CreateAuditEvent("evt-batch-2", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-1)),
		};

		var ids = await store.StoreBatchAsync(events, TestCancellationToken).ConfigureAwait(true);
		var count = await store.CountAsync(new AuditQuery(), TestCancellationToken).ConfigureAwait(true);

		ids.Count.ShouldBe(2);
		ids.All(id => id.SequenceNumber > 0).ShouldBeTrue();
		count.ShouldBe(2);
	}

	private SqlServerAuditStore CreateStore(Action<SqlServerAuditOptions>? configure = null)
	{
		var options = new SqlServerAuditOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "audit",
			TableName = "AuditEvents",
			CommandTimeoutSeconds = 30,
			RetentionCleanupBatchSize = 100
		};

		configure?.Invoke(options);

		return new SqlServerAuditStore(
			Microsoft.Extensions.Options.Options.Create(options),
			EnabledTestLogger.Create<SqlServerAuditStore>());
	}

	private async Task InitializeAuditTableAsync()
	{
		const string createSchemaAndTableSql = """
			IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'audit')
			BEGIN
			    EXEC('CREATE SCHEMA [audit]');
			END;

			IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[audit].[AuditEvents]') AND type in (N'U'))
			BEGIN
			    CREATE TABLE [audit].[AuditEvents] (
			        [SequenceNumber] BIGINT IDENTITY(1,1) NOT NULL,
			        [EventId] NVARCHAR(64) NOT NULL,
			        [EventType] INT NOT NULL,
			        [Action] NVARCHAR(100) NOT NULL,
			        [Outcome] INT NOT NULL,
			        [Timestamp] DATETIMEOFFSET(7) NOT NULL,
			        [ActorId] NVARCHAR(256) NOT NULL,
			        [ActorType] NVARCHAR(50) NULL,
			        [ResourceId] NVARCHAR(256) NULL,
			        [ResourceType] NVARCHAR(100) NULL,
			        [ResourceClassification] INT NULL,
			        [TenantId] NVARCHAR(64) NULL,
			        [CorrelationId] NVARCHAR(64) NULL,
			        [SessionId] NVARCHAR(64) NULL,
			        [IpAddress] NVARCHAR(45) NULL,
			        [UserAgent] NVARCHAR(500) NULL,
			        [Reason] NVARCHAR(1000) NULL,
			        [Metadata] NVARCHAR(MAX) NULL,
			        [PreviousEventHash] NVARCHAR(64) NULL,
			        [EventHash] NVARCHAR(64) NOT NULL,
			        CONSTRAINT [PK_AuditEvents] PRIMARY KEY CLUSTERED ([SequenceNumber] ASC),
			        CONSTRAINT [UQ_AuditEvents_EventId] UNIQUE NONCLUSTERED ([EventId])
			    );
			END;

			DELETE FROM [audit].[AuditEvents];
			""";

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		_ = await connection.ExecuteAsync(createSchemaAndTableSql).ConfigureAwait(true);
	}

	private static AuditEvent CreateAuditEvent(
		string id,
		string tenantId,
		DateTimeOffset timestamp,
		string actorId = "actor-1",
		string action = "read")
	{
		return new AuditEvent
		{
			EventId = id,
			EventType = AuditEventType.DataAccess,
			Action = action,
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp,
			ActorId = actorId,
			ActorType = "User",
			ResourceId = "resource-1",
			ResourceType = "Document",
			ResourceClassification = DataClassification.Confidential,
			TenantId = tenantId,
			CorrelationId = $"corr-{id}",
			SessionId = $"session-{id}",
			IpAddress = "127.0.0.1",
			UserAgent = "integration-test",
			Reason = "coverage",
			Metadata = new Dictionary<string, string>
			{
				["scenario"] = "integration"
			}
		};
	}
}

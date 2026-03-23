// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data.Postgres.Audit;
using Excalibur.LeaderElection.Postgres;
using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.LeaderElection;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", TestComponents.Data)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait("Category", "Integration")]
[Trait("Component", "Platform")]
public sealed class PostgresAuditAndLeaderElectionIntegrationShould : IntegrationTestBase
{
	private readonly PostgresFixture _fixture;

	public PostgresAuditAndLeaderElectionIntegrationShould(PostgresFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Store_and_get_by_id_round_trip()
	{
		await InitializeAuditTableAsync();
		await using var store = CreateAuditStore();
		var evt = CreateAuditEvent("pg-evt-1", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-2));

		var stored = await store.StoreAsync(evt, TestCancellationToken);
		var loaded = await store.GetByIdAsync(evt.EventId, TestCancellationToken);

		stored.SequenceNumber.ShouldBeGreaterThan(0);
		loaded.ShouldNotBeNull();
		loaded!.EventId.ShouldBe(evt.EventId);
		loaded.ActorId.ShouldBe(evt.ActorId);
		loaded.Metadata.ShouldNotBeNull();
		loaded.Metadata!["source"].ShouldBe("integration");
	}

	[Fact]
	public async Task Query_count_and_get_last_event_work_with_tenant_filters()
	{
		await InitializeAuditTableAsync();
		await using var store = CreateAuditStore();

		await store.StoreAsync(CreateAuditEvent("pg-evt-2", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-4), actorId: "actor-a"), TestCancellationToken);
		await store.StoreAsync(CreateAuditEvent("pg-evt-3", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-3), actorId: "actor-a"), TestCancellationToken);
		await store.StoreAsync(CreateAuditEvent("pg-evt-4", "tenant-2", DateTimeOffset.UtcNow.AddMinutes(-2), actorId: "actor-b"), TestCancellationToken);

		var query = new AuditQuery
		{
			ActorId = "actor-a",
			TenantId = "tenant-1",
			MaxResults = 10,
			Skip = 0
		};

		var results = await store.QueryAsync(query, TestCancellationToken);
		var count = await store.CountAsync(query, TestCancellationToken);
		var tenantLast = await store.GetLastEventAsync("tenant-1", TestCancellationToken);
		var overallLast = await store.GetLastEventAsync(null, TestCancellationToken);

		results.Count.ShouldBe(2);
		count.ShouldBe(2);
		tenantLast.ShouldNotBeNull();
		tenantLast!.TenantId.ShouldBe("tenant-1");
		overallLast.ShouldNotBeNull();
		overallLast!.EventId.ShouldBe("pg-evt-4");
	}

	[Fact]
	public async Task Verify_chain_integrity_detects_tampering()
	{
		await InitializeAuditTableAsync();
		await using var store = CreateAuditStore();
		var first = CreateAuditEvent("pg-evt-5", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-2));
		var second = CreateAuditEvent("pg-evt-6", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-1));

		await store.StoreAsync(first, TestCancellationToken);
		await store.StoreAsync(second, TestCancellationToken);

		var start = DateTimeOffset.UtcNow.AddHours(-1);
		var end = DateTimeOffset.UtcNow.AddHours(1);
		var valid = await store.VerifyChainIntegrityAsync(start, end, TestCancellationToken);
		valid.IsValid.ShouldBeTrue();

		await using (var connection = new NpgsqlConnection(_fixture.ConnectionString))
		{
			await connection.OpenAsync(TestCancellationToken);
			_ = await connection.ExecuteAsync(
				"UPDATE audit.audit_events SET previous_event_hash = @Hash WHERE event_id = @EventId",
				new { Hash = new string('F', 64), EventId = second.EventId });
		}

		var invalid = await store.VerifyChainIntegrityAsync(start, end, TestCancellationToken);
		invalid.IsValid.ShouldBeFalse();
		invalid.ViolationCount.ShouldBeGreaterThan(0);
		invalid.FirstViolationEventId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Leader_election_start_and_stop_acquires_and_releases_lock()
	{
		await using var election = CreateLeaderElection();
		var becameLeader = false;
		var lostLeadership = false;
		election.BecameLeader += (_, _) => becameLeader = true;
		election.LostLeadership += (_, _) => lostLeadership = true;

		await election.StartAsync(TestCancellationToken);
		await election.StopAsync(TestCancellationToken);

		becameLeader.ShouldBeTrue();
		lostLeadership.ShouldBeTrue();
		election.IsLeader.ShouldBeFalse();
		election.CurrentLeaderId.ShouldBeNull();
	}

	[Fact]
	public async Task Leader_election_dispose_while_started_is_safe()
	{
		var election = CreateLeaderElection();

		await election.StartAsync(TestCancellationToken);

		await Should.NotThrowAsync(() => election.DisposeAsync().AsTask());
	}

	[Fact]
	public async Task Leader_election_second_candidate_remains_follower_while_lock_is_held()
	{
		var sharedLockKey = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		await using var leader = CreateLeaderElection(lockKey: sharedLockKey);
		await using var follower = CreateLeaderElection(lockKey: sharedLockKey);
		var leaderAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		leader.BecameLeader += (_, _) => leaderAcquired.TrySetResult();

		await leader.StartAsync(TestCancellationToken);
		await follower.StartAsync(TestCancellationToken);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				leaderAcquired.Task,
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(5)),
				cancellationToken: TestCancellationToken)
			;

		leader.IsLeader.ShouldBeTrue();
		follower.IsLeader.ShouldBeFalse();

		await follower.StopAsync(TestCancellationToken);
		await leader.StopAsync(TestCancellationToken);
	}

	[Fact]
	public async Task Leader_election_second_candidate_takes_leadership_after_primary_stops()
	{
		var sharedLockKey = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		await using var leader = CreateLeaderElection(lockKey: sharedLockKey);
		await using var follower = CreateLeaderElection(lockKey: sharedLockKey);
		var leaderAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var followerAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		leader.BecameLeader += (_, _) => leaderAcquired.TrySetResult();
		follower.BecameLeader += (_, _) => followerAcquired.TrySetResult();

		await leader.StartAsync(TestCancellationToken);
		await follower.StartAsync(TestCancellationToken);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				leaderAcquired.Task,
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(5)),
				cancellationToken: TestCancellationToken)
			;

		leader.IsLeader.ShouldBeTrue();
		follower.IsLeader.ShouldBeFalse();

		await leader.StopAsync(TestCancellationToken);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				followerAcquired.Task,
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)),
				cancellationToken: TestCancellationToken)
			;

		follower.IsLeader.ShouldBeTrue();
		await follower.StopAsync(TestCancellationToken);
	}

	[Fact]
	public async Task Leader_election_loses_leadership_when_connection_breaks_past_grace_period()
	{
		var sharedLockKey = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		await using var leader = CreateLeaderElection(
			lockKey: sharedLockKey,
			renewInterval: TimeSpan.FromMilliseconds(100),
			gracePeriod: TimeSpan.FromMilliseconds(100));
		var leaderAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var leadershipLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		leader.BecameLeader += (_, _) => leaderAcquired.TrySetResult();
		leader.LostLeadership += (_, _) => leadershipLost.TrySetResult();

		await leader.StartAsync(TestCancellationToken);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				leaderAcquired.Task,
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)),
				cancellationToken: TestCancellationToken)
			;

		var connectionField = typeof(PostgresLeaderElection).GetField(
			"_connection",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		connectionField.SetValue(leader, null);

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				leadershipLost.Task,
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)),
				cancellationToken: TestCancellationToken)
			;

		leader.IsLeader.ShouldBeFalse();
		await leader.StopAsync(TestCancellationToken);
	}

	private PostgresAuditStore CreateAuditStore(Action<PostgresAuditOptions>? configure = null)
	{
		var options = new PostgresAuditOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "audit",
			TableName = "audit_events",
			AutoCreateTable = true,
			CommandTimeoutSeconds = 30
		};

		configure?.Invoke(options);

		return new PostgresAuditStore(
			Microsoft.Extensions.Options.Options.Create(options),
			EnabledTestLogger.Create<PostgresAuditStore>());
	}

	private PostgresLeaderElection CreateLeaderElection(
		long? lockKey = null,
		TimeSpan? leaseDuration = null,
		TimeSpan? renewInterval = null,
		TimeSpan? gracePeriod = null)
	{
		var pgOptions = new PostgresLeaderElectionOptions
		{
			ConnectionString = _fixture.ConnectionString,
			LockKey = lockKey ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			CommandTimeoutSeconds = 5
		};

		var electionOptions = new LeaderElectionOptions
		{
			LeaseDuration = leaseDuration ?? TimeSpan.FromSeconds(5),
			RenewInterval = renewInterval ?? TimeSpan.FromMilliseconds(200),
			GracePeriod = gracePeriod ?? TimeSpan.FromSeconds(2)
		};

		return new PostgresLeaderElection(
			Microsoft.Extensions.Options.Options.Create(pgOptions),
			Microsoft.Extensions.Options.Options.Create(electionOptions),
			EnabledTestLogger.Create<PostgresLeaderElection>());
	}

	private async Task InitializeAuditTableAsync()
	{
		const string sql = """
			CREATE SCHEMA IF NOT EXISTS audit;

			CREATE TABLE IF NOT EXISTS audit.audit_events (
				sequence_number BIGSERIAL PRIMARY KEY,
				event_id TEXT NOT NULL UNIQUE,
				event_type INTEGER NOT NULL,
				action TEXT NOT NULL,
				outcome INTEGER NOT NULL,
				timestamp TIMESTAMPTZ NOT NULL,
				actor_id TEXT NOT NULL,
				actor_type TEXT,
				resource_id TEXT,
				resource_type TEXT,
				tenant_id TEXT,
				correlation_id TEXT,
				session_id TEXT,
				ip_address TEXT,
				user_agent TEXT,
				reason TEXT,
				metadata JSONB,
				previous_event_hash TEXT,
				event_hash TEXT NOT NULL
			);

			TRUNCATE TABLE audit.audit_events RESTART IDENTITY;
			""";

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		_ = await connection.ExecuteAsync(sql);
	}

	private static AuditEvent CreateAuditEvent(
		string id,
		string tenantId,
		DateTimeOffset timestamp,
		string actorId = "actor-1")
	{
		return new AuditEvent
		{
			EventId = id,
			EventType = AuditEventType.DataAccess,
			Action = "read",
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp,
			ActorId = actorId,
			ActorType = "User",
			ResourceId = "resource-1",
			ResourceType = "Document",
			TenantId = tenantId,
			CorrelationId = $"corr-{id}",
			SessionId = $"sess-{id}",
			IpAddress = "127.0.0.1",
			UserAgent = "integration-test",
			Reason = "coverage",
			Metadata = new Dictionary<string, string>
			{
				["source"] = "integration"
			}
		};
	}
}

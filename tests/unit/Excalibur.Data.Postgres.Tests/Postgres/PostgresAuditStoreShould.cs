// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Audit;
using Excalibur.Dispatch.Compliance;


namespace Excalibur.Data.Tests.Postgres;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresAuditStoreShould
{
	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new PostgresAuditStore(null!, EnabledTestLogger.Create<PostgresAuditStore>()));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=test;"
		});

		Should.Throw<ArgumentNullException>(
			() => new PostgresAuditStore(options, null!));
	}

	[Fact]
	public void ThrowWhenConnectionStringIsEmpty()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = string.Empty
		});

		Should.Throw<InvalidOperationException>(
			() => new PostgresAuditStore(options, EnabledTestLogger.Create<PostgresAuditStore>()));
	}

	[Fact]
	public void ThrowWhenConnectionStringIsWhitespace()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "   "
		});

		Should.Throw<InvalidOperationException>(
			() => new PostgresAuditStore(options, EnabledTestLogger.Create<PostgresAuditStore>()));
	}

	[Fact]
	public async Task DisposeAsync_DoesNotThrow()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=test;"
		});
		var store = new PostgresAuditStore(options, EnabledTestLogger.Create<PostgresAuditStore>());

		await Should.NotThrowAsync(() => store.DisposeAsync().AsTask());
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=test;"
		});
		var store = new PostgresAuditStore(options, EnabledTestLogger.Create<PostgresAuditStore>());

		await store.DisposeAsync();
		await Should.NotThrowAsync(() => store.DisposeAsync().AsTask());
	}

	[Fact]
	public async Task StoreAsync_ThrowsWhenDisposed()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=test;"
		});
		var store = new PostgresAuditStore(options, EnabledTestLogger.Create<PostgresAuditStore>());
		await store.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.StoreAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetByIdAsync_ThrowsWhenDisposed()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=test;"
		});
		var store = new PostgresAuditStore(options, EnabledTestLogger.Create<PostgresAuditStore>());
		await store.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.GetByIdAsync("test", CancellationToken.None));
	}

	[Fact]
	public async Task QueryAsync_ThrowsWhenDisposed()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=test;"
		});
		var store = new PostgresAuditStore(options, EnabledTestLogger.Create<PostgresAuditStore>());
		await store.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.QueryAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task CountAsync_ThrowsWhenDisposed()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=test;"
		});
		var store = new PostgresAuditStore(options, EnabledTestLogger.Create<PostgresAuditStore>());
		await store.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.CountAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task VerifyChainIntegrityAsync_ThrowsWhenDisposed()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=test;"
		});
		var store = new PostgresAuditStore(options, EnabledTestLogger.Create<PostgresAuditStore>());
		await store.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.VerifyChainIntegrityAsync(
				DateTimeOffset.UtcNow.AddDays(-1),
				DateTimeOffset.UtcNow,
				CancellationToken.None));
	}

	[Fact]
	public async Task GetLastEventAsync_ThrowsWhenDisposed()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=test;"
		});
		var store = new PostgresAuditStore(options, EnabledTestLogger.Create<PostgresAuditStore>());
		await store.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.GetLastEventAsync(null, CancellationToken.None));
	}

	[Fact]
	public void ComputeEventHash_IsDeterministicForSameInput()
	{
		var method = typeof(PostgresAuditStore).GetMethod(
			"ComputeEventHash",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

		var auditEvent = new AuditEvent
		{
			EventId = "evt-1",
			EventType = AuditEventType.Authorization,
			Action = "read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.Parse("2026-01-01T00:00:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
			ActorId = "user-1"
		};

		var hash1 = (string)method.Invoke(null, [auditEvent, "prev"])!;
		var hash2 = (string)method.Invoke(null, [auditEvent, "prev"])!;

		hash1.ShouldBe(hash2);
		hash1.Length.ShouldBe(64);
	}

	[Fact]
	public void ComputeEventHash_ChangesWhenPreviousHashChanges()
	{
		var method = typeof(PostgresAuditStore).GetMethod(
			"ComputeEventHash",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

		var auditEvent = new AuditEvent
		{
			EventId = "evt-2",
			EventType = AuditEventType.Authentication,
			Action = "login",
			Outcome = AuditOutcome.Failure,
			Timestamp = DateTimeOffset.Parse("2026-01-02T00:00:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
			ActorId = "user-2"
		};

		var hash1 = (string)method.Invoke(null, [auditEvent, "prev-a"])!;
		var hash2 = (string)method.Invoke(null, [auditEvent, "prev-b"])!;

		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void BuildWhereClause_ReturnsEmptyClauseForDefaultQuery()
	{
		var method = typeof(PostgresAuditStore).GetMethod(
			"BuildWhereClause",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

		var query = new AuditQuery();
		var tuple = method.Invoke(null, [query])!;
		var whereClause = (string)(tuple.GetType().GetField("Item1")?.GetValue(tuple)
			?? tuple.GetType().GetProperty("Item1")?.GetValue(tuple))!;
		var parameters = (Dapper.DynamicParameters)(tuple.GetType().GetField("Item2")?.GetValue(tuple)
			?? tuple.GetType().GetProperty("Item2")?.GetValue(tuple))!;

		whereClause.ShouldBeEmpty();
		parameters.ParameterNames.ShouldBeEmpty();
	}

	[Fact]
	public void BuildWhereClause_IncludesAllSupportedFilters()
	{
		var method = typeof(PostgresAuditStore).GetMethod(
			"BuildWhereClause",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

		var query = new AuditQuery
		{
			StartDate = DateTimeOffset.UtcNow.AddHours(-1),
			EndDate = DateTimeOffset.UtcNow,
			ActorId = "actor-1",
			ResourceId = "res-1",
			ResourceType = "document",
			TenantId = "tenant-1",
			CorrelationId = "corr-1",
			Action = "update",
			IpAddress = "127.0.0.1"
		};

		var tuple = method.Invoke(null, [query])!;
		var whereClause = (string)(tuple.GetType().GetField("Item1")?.GetValue(tuple)
			?? tuple.GetType().GetProperty("Item1")?.GetValue(tuple))!;
		var parameters = (Dapper.DynamicParameters)(tuple.GetType().GetField("Item2")?.GetValue(tuple)
			?? tuple.GetType().GetProperty("Item2")?.GetValue(tuple))!;

		whereClause.ShouldContain("timestamp >= @StartDate");
		whereClause.ShouldContain("timestamp <= @EndDate");
		whereClause.ShouldContain("actor_id = @ActorId");
		whereClause.ShouldContain("resource_id = @ResourceId");
		whereClause.ShouldContain("resource_type = @ResourceType");
		whereClause.ShouldContain("tenant_id = @TenantId");
		whereClause.ShouldContain("correlation_id = @CorrelationId");
		whereClause.ShouldContain("action = @Action");
		whereClause.ShouldContain("ip_address = @IpAddress");

		parameters.ParameterNames.ShouldContain("StartDate");
		parameters.ParameterNames.ShouldContain("EndDate");
		parameters.ParameterNames.ShouldContain("ActorId");
		parameters.ParameterNames.ShouldContain("ResourceId");
		parameters.ParameterNames.ShouldContain("ResourceType");
		parameters.ParameterNames.ShouldContain("TenantId");
		parameters.ParameterNames.ShouldContain("CorrelationId");
		parameters.ParameterNames.ShouldContain("Action");
		parameters.ParameterNames.ShouldContain("IpAddress");
	}

	[Fact]
	public void AuditRow_ToAuditEvent_MapsAllFields()
	{
		var rowType = typeof(PostgresAuditStore).GetNestedType(
			"AuditRow",
			System.Reflection.BindingFlags.NonPublic)!;
		rowType.ShouldNotBeNull();

		var row = Activator.CreateInstance(rowType!)!;

		rowType.GetProperty("event_id")!.SetValue(row, "evt-99");
		rowType.GetProperty("event_type")!.SetValue(row, (int)AuditEventType.Authorization);
		rowType.GetProperty("action")!.SetValue(row, "read");
		rowType.GetProperty("outcome")!.SetValue(row, (int)AuditOutcome.Success);
		rowType.GetProperty("timestamp")!.SetValue(row, DateTimeOffset.UtcNow);
		rowType.GetProperty("actor_id")!.SetValue(row, "actor-99");
		rowType.GetProperty("actor_type")!.SetValue(row, "User");
		rowType.GetProperty("resource_id")!.SetValue(row, "resource-99");
		rowType.GetProperty("resource_type")!.SetValue(row, "Record");
		rowType.GetProperty("tenant_id")!.SetValue(row, "tenant-99");
		rowType.GetProperty("correlation_id")!.SetValue(row, "corr-99");
		rowType.GetProperty("session_id")!.SetValue(row, "sess-99");
		rowType.GetProperty("ip_address")!.SetValue(row, "10.1.2.3");
		rowType.GetProperty("user_agent")!.SetValue(row, "agent");
		rowType.GetProperty("reason")!.SetValue(row, "reason");
		rowType.GetProperty("metadata")!.SetValue(row, "{\"k\":\"v\"}");
		rowType.GetProperty("previous_event_hash")!.SetValue(row, "PREV");
		rowType.GetProperty("event_hash")!.SetValue(row, "CURR");

		var toAuditEvent = rowType.GetMethod("ToAuditEvent")!;
		var mapped = (AuditEvent)toAuditEvent.Invoke(row, null)!;

		mapped.EventId.ShouldBe("evt-99");
		mapped.EventType.ShouldBe(AuditEventType.Authorization);
		mapped.Action.ShouldBe("read");
		mapped.Outcome.ShouldBe(AuditOutcome.Success);
		mapped.ActorId.ShouldBe("actor-99");
		mapped.Metadata.ShouldNotBeNull();
		mapped.Metadata!["k"].ShouldBe("v");
		mapped.PreviousEventHash.ShouldBe("PREV");
		mapped.EventHash.ShouldBe("CURR");
	}

	[Fact]
	public async Task StoreAsync_ThrowsWhenDatabaseIsUnavailable()
	{
		await using var store = CreateUnavailableStore();

		var auditEvent = new AuditEvent
		{
			EventId = "evt-unavailable",
			EventType = AuditEventType.Authorization,
			Action = "read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "actor-1"
		};

		await Should.ThrowAsync<Exception>(() => store.StoreAsync(auditEvent, CancellationToken.None));
	}

	[Fact]
	public async Task GetByIdAsync_ThrowsWhenDatabaseIsUnavailable()
	{
		await using var store = CreateUnavailableStore();

		await Should.ThrowAsync<Exception>(() => store.GetByIdAsync("evt-unavailable", CancellationToken.None));
	}

	[Fact]
	public async Task QueryAsync_ThrowsWhenDatabaseIsUnavailable()
	{
		await using var store = CreateUnavailableStore();

		await Should.ThrowAsync<Exception>(() => store.QueryAsync(new AuditQuery(), CancellationToken.None));
	}

	[Fact]
	public async Task CountAsync_ThrowsWhenDatabaseIsUnavailable()
	{
		await using var store = CreateUnavailableStore();

		await Should.ThrowAsync<Exception>(() => store.CountAsync(new AuditQuery(), CancellationToken.None));
	}

	[Fact]
	public async Task VerifyChainIntegrityAsync_ThrowsWhenDatabaseIsUnavailable()
	{
		await using var store = CreateUnavailableStore();

		await Should.ThrowAsync<Exception>(() => store.VerifyChainIntegrityAsync(
			DateTimeOffset.UtcNow.AddDays(-1),
			DateTimeOffset.UtcNow,
			CancellationToken.None));
	}

	[Fact]
	public async Task GetLastEventAsync_ThrowsWhenDatabaseIsUnavailable()
	{
		await using var store = CreateUnavailableStore();

		await Should.ThrowAsync<Exception>(() => store.GetLastEventAsync(null, CancellationToken.None));
	}

	private static PostgresAuditStore CreateUnavailableStore()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=127.0.0.1;Port=1;Database=test;Username=test;Password=test;Timeout=1;Command Timeout=1;",
			AutoCreateTable = false,
			CommandTimeoutSeconds = 1
		});

		return new PostgresAuditStore(options, EnabledTestLogger.Create<PostgresAuditStore>());
	}
}


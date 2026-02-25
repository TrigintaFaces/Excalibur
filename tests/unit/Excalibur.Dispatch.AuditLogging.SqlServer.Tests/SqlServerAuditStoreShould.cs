namespace Excalibur.Dispatch.AuditLogging.SqlServer.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SqlServerAuditStoreShould
{
	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqlServerAuditStore(
				null!,
				EnabledTestLogger.Create<SqlServerAuditStore>()));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit"
		});

		Should.Throw<ArgumentNullException>(() =>
			new SqlServerAuditStore(options, null!));
	}

	[Fact]
	public void Throw_for_empty_connection_string()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
		{
			ConnectionString = string.Empty
		});

		Should.Throw<ArgumentException>(() =>
			new SqlServerAuditStore(
				options,
				EnabledTestLogger.Create<SqlServerAuditStore>()));
	}

	[Fact]
	public void Throw_for_null_connection_string()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
		{
			ConnectionString = null!
		});

		Should.Throw<ArgumentException>(() =>
			new SqlServerAuditStore(
				options,
				EnabledTestLogger.Create<SqlServerAuditStore>()));
	}

	[Theory]
	[InlineData("invalid-schema")]
	[InlineData("schema.name")]
	[InlineData("schema name")]
	[InlineData("schema;drop")]
	[InlineData("schema'name")]
	public void Throw_for_invalid_schema_name(string schemaName)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit",
			SchemaName = schemaName
		});

		Should.Throw<ArgumentException>(() =>
			new SqlServerAuditStore(
				options,
				EnabledTestLogger.Create<SqlServerAuditStore>()));
	}

	[Theory]
	[InlineData("invalid-table")]
	[InlineData("table.name")]
	[InlineData("table name")]
	[InlineData("table;drop")]
	[InlineData("table'name")]
	public void Throw_for_invalid_table_name(string tableName)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit",
			TableName = tableName
		});

		Should.Throw<ArgumentException>(() =>
			new SqlServerAuditStore(
				options,
				EnabledTestLogger.Create<SqlServerAuditStore>()));
	}

	[Theory]
	[InlineData("audit")]
	[InlineData("AUDIT")]
	[InlineData("audit_events")]
	[InlineData("my_schema_123")]
	[InlineData("dbo")]
	public void Accept_valid_schema_names(string schemaName)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit",
			SchemaName = schemaName
		});

		var store = new SqlServerAuditStore(
			options,
			EnabledTestLogger.Create<SqlServerAuditStore>());

		store.ShouldNotBeNull();
		store.Dispose();
	}

	[Theory]
	[InlineData("AuditEvents")]
	[InlineData("AUDIT_EVENTS")]
	[InlineData("events123")]
	[InlineData("T")]
	public void Accept_valid_table_names(string tableName)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit",
			TableName = tableName
		});

		var store = new SqlServerAuditStore(
			options,
			EnabledTestLogger.Create<SqlServerAuditStore>());

		store.ShouldNotBeNull();
		store.Dispose();
	}

	[Fact]
	public void Be_disposable()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit"
		});

		var store = new SqlServerAuditStore(
			options,
			EnabledTestLogger.Create<SqlServerAuditStore>());

		// Should not throw
		store.Dispose();
	}

	[Fact]
	public void Handle_double_dispose_safely()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit"
		});

		var store = new SqlServerAuditStore(
			options,
			EnabledTestLogger.Create<SqlServerAuditStore>());

		// Double dispose should not throw
		store.Dispose();
		store.Dispose();
	}

	[Fact]
	public void Implement_IAuditStore()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit"
		});

		var store = new SqlServerAuditStore(
			options,
			EnabledTestLogger.Create<SqlServerAuditStore>());

		store.ShouldBeAssignableTo<Excalibur.Dispatch.Compliance.IAuditStore>();
		store.Dispose();
	}

	[Fact]
	public void Implement_IDisposable()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit"
		});

		var store = new SqlServerAuditStore(
			options,
			EnabledTestLogger.Create<SqlServerAuditStore>());

		store.ShouldBeAssignableTo<IDisposable>();
		store.Dispose();
	}

	[Fact]
	public async Task Throw_for_null_audit_event_in_store_async()
	{
		using var store = CreateUnavailableStore();

		await Should.ThrowAsync<ArgumentNullException>(() =>
			store.StoreAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_for_empty_event_id_in_get_by_id_async()
	{
		using var store = CreateUnavailableStore();

		await Should.ThrowAsync<ArgumentException>(() =>
			store.GetByIdAsync(string.Empty, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_sql_unavailable_for_get_by_id_async()
	{
		using var store = CreateUnavailableStore();

		await Should.ThrowAsync<Exception>(() =>
			store.GetByIdAsync("evt-1", CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_sql_unavailable_for_query_async()
	{
		using var store = CreateUnavailableStore();

		await Should.ThrowAsync<Exception>(() =>
			store.QueryAsync(new Excalibur.Dispatch.Compliance.AuditQuery(), CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_sql_unavailable_for_count_async()
	{
		using var store = CreateUnavailableStore();

		await Should.ThrowAsync<Exception>(() =>
			store.CountAsync(new Excalibur.Dispatch.Compliance.AuditQuery(), CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_sql_unavailable_for_verify_chain_integrity_async()
	{
		using var store = CreateUnavailableStore();

		await Should.ThrowAsync<Exception>(() =>
			store.VerifyChainIntegrityAsync(
				DateTimeOffset.UtcNow.AddHours(-1),
				DateTimeOffset.UtcNow,
				CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_sql_unavailable_for_get_last_event_async()
	{
		using var store = CreateUnavailableStore();

		await Should.ThrowAsync<Exception>(() =>
			store.GetLastEventAsync(null, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_sql_unavailable_for_enforce_retention_async()
	{
		using var store = CreateUnavailableStore();

		await Should.ThrowAsync<Exception>(() =>
			store.EnforceRetentionAsync(DateTimeOffset.UtcNow.AddDays(-30), CancellationToken.None));
	}

	[Fact]
	public async Task Throw_for_null_batch_in_store_batch_async()
	{
		using var store = CreateUnavailableStore();

		await Should.ThrowAsync<ArgumentNullException>(() =>
			store.StoreBatchAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task Return_empty_result_for_empty_batch()
	{
		using var store = CreateUnavailableStore();

		var result = await store.StoreBatchAsync(Array.Empty<Excalibur.Dispatch.Compliance.AuditEvent>(), CancellationToken.None);

		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task Honor_cancellation_before_store_batch_processing()
	{
		using var store = CreateUnavailableStore();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		var events = new[]
		{
			CreateAuditEvent("evt-cancel")
		};

		await Should.ThrowAsync<OperationCanceledException>(() =>
			store.StoreBatchAsync(events, cts.Token));
	}

	[Fact]
	public void Compute_event_hash_is_deterministic()
	{
		var method = typeof(SqlServerAuditStore).GetMethod(
			"ComputeEventHash",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

		var evt = CreateAuditEvent("evt-hash");

		var hash1 = (string)method.Invoke(null, [evt, "prev"])!;
		var hash2 = (string)method.Invoke(null, [evt, "prev"])!;

		hash1.ShouldBe(hash2);
		hash1.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Build_query_clauses_include_all_filters()
	{
		var method = typeof(SqlServerAuditStore).GetMethod(
			"BuildQueryClauses",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
		var query = new Excalibur.Dispatch.Compliance.AuditQuery
		{
			StartDate = DateTimeOffset.UtcNow.AddDays(-1),
			EndDate = DateTimeOffset.UtcNow,
			EventTypes = [Excalibur.Dispatch.Compliance.AuditEventType.DataAccess],
			Outcomes = [Excalibur.Dispatch.Compliance.AuditOutcome.Success],
			ActorId = "actor-1",
			ResourceId = "resource-1",
			ResourceType = "document",
			MinimumClassification = Excalibur.Dispatch.Compliance.DataClassification.Confidential,
			TenantId = "tenant-1",
			CorrelationId = "corr-1",
			Action = "read",
			IpAddress = "127.0.0.1"
		};

		var tuple = method.Invoke(null, [query])!;
		var whereClauses = (List<string>)(tuple.GetType().GetProperty("WhereClauses")?.GetValue(tuple)
			?? tuple.GetType().GetField("Item1")?.GetValue(tuple)!)!;

		whereClauses.ShouldContain("[Timestamp] >= @StartDate");
		whereClauses.ShouldContain("[Timestamp] <= @EndDate");
		whereClauses.ShouldContain("EventType IN @EventTypes");
		whereClauses.ShouldContain("Outcome IN @Outcomes");
		whereClauses.ShouldContain("ActorId = @ActorId");
		whereClauses.ShouldContain("ResourceId = @ResourceId");
		whereClauses.ShouldContain("ResourceType = @ResourceType");
		whereClauses.ShouldContain("ResourceClassification >= @MinClassification");
		whereClauses.ShouldContain("TenantId = @TenantId");
		whereClauses.ShouldContain("CorrelationId = @CorrelationId");
		whereClauses.ShouldContain("[Action] = @Action");
		whereClauses.ShouldContain("IpAddress = @IpAddress");
	}

	[Fact]
	public void Map_to_audit_event_maps_metadata_and_core_fields()
	{
		var rowType = typeof(SqlServerAuditStore).GetNestedType(
			"AuditEventRow",
			System.Reflection.BindingFlags.NonPublic)!;
		var row = Activator.CreateInstance(rowType)!;

		SetInitProperty(rowType, row, "EventId", "evt-map");
		SetInitProperty(rowType, row, "EventType", (int)Excalibur.Dispatch.Compliance.AuditEventType.DataAccess);
		SetInitProperty(rowType, row, "Action", "read");
		SetInitProperty(rowType, row, "Outcome", (int)Excalibur.Dispatch.Compliance.AuditOutcome.Success);
		SetInitProperty(rowType, row, "Timestamp", DateTimeOffset.UtcNow);
		SetInitProperty(rowType, row, "ActorId", "actor-1");
		SetInitProperty(rowType, row, "Metadata", "{\"k\":\"v\"}");
		SetInitProperty(rowType, row, "EventHash", "hash");

		var mapMethod = typeof(SqlServerAuditStore).GetMethod(
			"MapToAuditEvent",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
		var mapped = (Excalibur.Dispatch.Compliance.AuditEvent)mapMethod.Invoke(null, [row])!;

		mapped.EventId.ShouldBe("evt-map");
		mapped.Action.ShouldBe("read");
		mapped.Metadata.ShouldNotBeNull();
		mapped.Metadata!["k"].ShouldBe("v");
	}

	private static void SetInitProperty(Type rowType, object row, string propertyName, object? value)
	{
		var property = rowType.GetProperty(propertyName)!;
		property.SetValue(row, value);
	}

	private static SqlServerAuditStore CreateUnavailableStore()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
		{
			ConnectionString = "Server=tcp:127.0.0.1,1;Database=AuditDb;User Id=sa;Password=Password123!;TrustServerCertificate=True;Connect Timeout=1;",
			CommandTimeoutSeconds = 1
		});

		return new SqlServerAuditStore(options, EnabledTestLogger.Create<SqlServerAuditStore>());
	}

	private static Excalibur.Dispatch.Compliance.AuditEvent CreateAuditEvent(string eventId) => new()
	{
		EventId = eventId,
		EventType = Excalibur.Dispatch.Compliance.AuditEventType.DataAccess,
		Action = "read",
		Outcome = Excalibur.Dispatch.Compliance.AuditOutcome.Success,
		Timestamp = DateTimeOffset.UtcNow,
		ActorId = "actor-1",
		ResourceClassification = Excalibur.Dispatch.Compliance.DataClassification.Confidential,
		EventHash = "hash"
	};
}

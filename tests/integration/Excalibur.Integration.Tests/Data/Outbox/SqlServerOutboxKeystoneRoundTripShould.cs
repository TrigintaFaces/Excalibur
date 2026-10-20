// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Linq;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Keystone K1.1 real-infrastructure lock (owxhc8): a fully-populated <see cref="OutboundMessage"/> that
/// is staged through the single canonical mapping seam round-trips byte-identical from SQL Server — with
/// <see cref="OutboundMessage.CreatedAt"/> as the load-bearing RED-pre-fix arm.
/// </summary>
/// <remarks>
/// <para>
/// Before the fix the SQL Server INSERT stamped <c>SYSDATETIMEOFFSET()</c> for <c>CreatedAt</c>, silently
/// discarding the caller's value. The seam now binds the caller's <c>@CreatedAt</c>, so a message staged
/// with an explicit historical timestamp must reload with that exact timestamp — not "now". A populated
/// message is used deliberately: the empty-headers path is a NULL→<c>'{}'</c> align-up, so the round-trip
/// is proven against a message that exercises every persisted canonical field.
/// </para>
/// <para>Never skipped: when Docker is unavailable the fixture fails fast rather than passing silently.</para>
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "Serialization")]
public sealed class SqlServerOutboxKeystoneRoundTripShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerOutboxKeystoneRoundTripShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task PreserveEveryCanonicalField_IncludingTheCallersCreatedAt_OnStageThenReload()
	{
		await EnsureReadyAsync().ConfigureAwait(false);
		var store = CreateStore();

		// An explicit historical CreatedAt — deliberately NOT "now" — is the RED-pre-fix arm: the old
		// INSERT overwrote it with SYSDATETIMEOFFSET(), so a pre-fix impl would reload ~today, not 2020.
		var createdAt = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
		// A PAST scheduled instant so the message stays drainable.
		var scheduledAt = new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);

		var message = new OutboundMessage("Keystone.MessageType", "keystone-payload"u8.ToArray(), "keystone-destination")
		{
			Id = Guid.NewGuid().ToString(),
			CreatedAt = createdAt,
			ScheduledAt = scheduledAt,
			CorrelationId = "corr-keystone",
			CausationId = "cause-keystone",
			TenantId = "tenant-keystone",
			Priority = 7,
			PartitionKey = "partition-A",
			GroupKey = "group-A",
			SequenceNumber = 42,
			TargetTransports = "kafka,rabbitmq",
			IsMultiTransport = true,
		};
		message.Headers["header-one"] = "value-one";
		message.Headers["Header"] = "upper";
		message.Headers["header"] = "lower";
		message.Headers["integer"] = 42;
		message.Headers["decimal"] = 1.25m;
		message.Headers["true"] = true;
		message.Headers["false"] = false;
		message.Headers["null"] = null!;
		using var structuredHeaders = JsonDocument.Parse("{\"array\":[1,\"two\",null],\"object\":{\"Name\":\"nested\"}}");
		message.Headers["array"] = structuredHeaders.RootElement.GetProperty("array").Clone();
		message.Headers["object"] = structuredHeaders.RootElement.GetProperty("object").Clone();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// A fresh store must read persisted JSON; a retained in-memory message cannot satisfy this lock.
		var reader = CreateStore();
		var reloaded = (await reader.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
			.Single(m => m.Id == message.Id);

		// The keystone guarantee: the caller's CreatedAt survives (RED against the pre-fix NOW() stamp).
		reloaded.CreatedAt.ToUniversalTime().ShouldBe(createdAt.ToUniversalTime());

		// FULL consumer field set byte-identical through the real drain-reload — catches the Dapper alias
		// silent-drop CLASS (a mismatched/absent RETURNING/OUTPUT alias hydrates nothing, no error), not
		// just CreatedAt. (RetryCount is store-managed → normalized to 0 on fresh stage, out of scope.)
		reloaded.ScheduledAt!.Value.ToUniversalTime().ShouldBe(scheduledAt.ToUniversalTime());
		reloaded.MessageType.ShouldBe("Keystone.MessageType");
		reloaded.Destination.ShouldBe("keystone-destination");
		reloaded.Payload.ShouldBe("keystone-payload"u8.ToArray());
		reloaded.CorrelationId.ShouldBe("corr-keystone");
		reloaded.CausationId.ShouldBe("cause-keystone");
		reloaded.TenantId.ShouldBe("tenant-keystone");
		reloaded.Priority.ShouldBe(7);
		reloaded.PartitionKey.ShouldBe("partition-A");
		reloaded.GroupKey.ShouldBe("group-A");
		reloaded.SequenceNumber.ShouldBe(42);
		reloaded.TargetTransports.ShouldBe("kafka,rabbitmq");
		reloaded.IsMultiTransport.ShouldBeTrue();
		reloaded.Headers.ShouldContainKey("header-one");
		reloaded.Headers.Count.ShouldBe(10);
		reloaded.Headers["header-one"].ShouldBeOfType<JsonElement>().GetString().ShouldBe("value-one");
		reloaded.Headers["Header"].ShouldBeOfType<JsonElement>().GetString().ShouldBe("upper");
		reloaded.Headers["header"].ShouldBeOfType<JsonElement>().GetString().ShouldBe("lower");
		reloaded.Headers["integer"].ShouldBeOfType<JsonElement>().GetInt32().ShouldBe(42);
		reloaded.Headers["decimal"].ShouldBeOfType<JsonElement>().GetDecimal().ShouldBe(1.25m);
		reloaded.Headers["true"].ShouldBeOfType<JsonElement>().GetBoolean().ShouldBeTrue();
		reloaded.Headers["false"].ShouldBeOfType<JsonElement>().GetBoolean().ShouldBeFalse();
		reloaded.Headers["null"].ShouldBeNull();
		var array = reloaded.Headers["array"].ShouldBeOfType<JsonElement>();
		array.ValueKind.ShouldBe(JsonValueKind.Array);
		array.GetArrayLength().ShouldBe(3);
		array[0].GetInt32().ShouldBe(1);
		array[1].GetString().ShouldBe("two");
		array[2].ValueKind.ShouldBe(JsonValueKind.Null);
		var nested = reloaded.Headers["object"].ShouldBeOfType<JsonElement>();
		nested.ValueKind.ShouldBe(JsonValueKind.Object);
		nested.GetProperty("Name").GetString().ShouldBe("nested");
	}

	private async Task EnsureReadyAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - this keystone real-infra lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	private SqlServerOutboxStore CreateStore()
	{
		var options = Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			ProcessorId = "proc-keystone",
			Tables =
			{
				SchemaName = _fixture.SchemaName,
				OutboxTableName = _fixture.OutboxTableName,
				TransportsTableName = _fixture.TransportsTableName,
			},
			Processing = { CommandTimeoutSeconds = 30 },
		});

		return new SqlServerOutboxStore(options, NullLogger<SqlServerOutboxStore>.Instance);
	}
}

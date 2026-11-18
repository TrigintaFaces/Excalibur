// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Outbox;
using Excalibur.Outbox.SqlServer;

using FakeItEasy;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
public sealed class SqlServerGeneratedMetadataStageShould(SqlServerOutboxStoreContainerFixture fixture)
	: IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private const string ExpectedHeaders = """{"DomainHeader":{"value":"header","state":"Ready","optional":"Waiting","nested":{"value":"nested"}},"null-entry":null,"integer":7}""";
	private static readonly DateTimeOffset Created = new(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(5.5));
	private static readonly DateTimeOffset Scheduled = new(2020, 6, 1, 2, 3, 4, TimeSpan.FromHours(-4));

	[Theory]
	[InlineData(0, 0)]
	[InlineData(0, 1)]
	[InlineData(0, 2)]
	[InlineData(1, 0)]
	[InlineData(1, 1)]
	[InlineData(1, 2)]
	[InlineData(2, 0)]
	[InlineData(2, 1)]
	[InlineData(2, 2)]
	public async Task StageClaimAndPublishThroughEveryPublicSqlRegistrationAndStageMode(int registration, int stageMode)
	{
		await PrepareAsync().ConfigureAwait(false);
		var delivered = new ConcurrentBag<string>();
		var factoryCalls = 0;
		var metadata = new OutboxMetadataSerialization(options => { factoryCalls++; return new SqlDeliveryMetadataContext(options); });
		await using var provider = BuildComposition(registration, metadata, delivered);
		var store = provider.GetRequiredService<IOutboxStore>();
		var id = Guid.NewGuid().ToString("N");
		var payload = provider.GetRequiredService<IPayloadSerializer>().SerializeObject(new SqlMetadataPayload(id, "payload"), typeof(SqlMetadataPayload));
		var message = Message(id, payload);
		MessageTypeRegistry.RegisterType<SqlMetadataPayload>();

		switch (stageMode)
		{
			case 0:
				await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
				break;
			case 1:
				await using (var connection = fixture.CreateConnection())
				{
					await connection.OpenAsync().ConfigureAwait(false);
					await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);
					await provider.GetRequiredService<ITransactionalOutboxWriter>().StageMessageAsync(message, transaction, CancellationToken.None).ConfigureAwait(false);
					connection.State.ShouldBe(ConnectionState.Open, "the caller still owns its transaction connection");
					await using var inTransaction = new SqlCommand("SELECT COUNT(*) FROM [dbo].[OutboxMessages] WHERE Id=@Id", connection, transaction);
					_ = inTransaction.Parameters.AddWithValue("@Id", id);
					Convert.ToInt32(await inTransaction.ExecuteScalarAsync().ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture).ShouldBe(1);
					await transaction.CommitAsync().ConfigureAwait(false);
				}
				break;
			case 2:
				await provider.GetRequiredService<IMultiTransportOutboxStore>().StageMessageWithTransportsAsync(
					message, Array.Empty<OutboundMessageTransport>(), CancellationToken.None).ConfigureAwait(false);
				break;
		}

		await AssertPersistedEnvelopeAsync(message).ConfigureAwait(false);
		var result = await provider.GetRequiredService<IOutboxPublisher>().PublishPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);
		result.IsSuccess.ShouldBeTrue();
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(0);
		delivered.ShouldHaveSingleItem().ShouldBe(id);
		await using var verify = fixture.CreateConnection();
		await verify.OpenAsync().ConfigureAwait(false);
		await using var status = new SqlCommand("SELECT Status FROM [dbo].[OutboxMessages] WHERE Id=@Id", verify);
		_ = status.Parameters.AddWithValue("@Id", id);
		Convert.ToInt32(await status.ExecuteScalarAsync().ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture).ShouldBe(2);
		factoryCalls.ShouldBe(1, "claiming and publishing must reuse the store's writer rather than resolving another factory");
	}

	[Fact]
	public async Task LeaveTheCallerTransactionUsableAfterMissingMetadataAndHonorItsRollback()
	{
		await PrepareAsync().ConfigureAwait(false);
		var metadata = new OutboxMetadataSerialization(options => new SqlDeliveryMetadataContext(options));
		await using var provider = BuildComposition(0, metadata, new ConcurrentBag<string>());
		var id = Guid.NewGuid().ToString("N");
		var bad = Message(id, [1, 2, 255]);
		bad.Headers["unregistered"] = new SqlMetadataUnknownHeader("hidden");
		await using var connection = fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);
		var writer = provider.GetRequiredService<ITransactionalOutboxWriter>();
		_ = await Should.ThrowAsync<NotSupportedException>(async () =>
			await writer.StageMessageAsync(bad, transaction, CancellationToken.None).ConfigureAwait(false));
		await using (var state = new SqlCommand("SELECT XACT_STATE()", connection, transaction))
		{
			Convert.ToInt32(await state.ExecuteScalarAsync().ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture).ShouldBe(1);
		}

		bad.Headers.Remove("unregistered").ShouldBeTrue();
		await writer.StageMessageAsync(bad, transaction, CancellationToken.None).ConfigureAwait(false);
		await transaction.RollbackAsync().ConfigureAwait(false);
		connection.State.ShouldBe(ConnectionState.Open);
		await using var absent = new SqlCommand("SELECT COUNT(*) FROM [dbo].[OutboxMessages] WHERE Id=@Id", connection);
		_ = absent.Parameters.AddWithValue("@Id", id);
		Convert.ToInt32(await absent.ExecuteScalarAsync().ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture).ShouldBe(0);
	}

	[Fact]
	public async Task KeepTheRegisteredPayloadCodecConvenienceWorkingWithGeneratedHeaders()
	{
		await PrepareAsync().ConfigureAwait(false);
		var delivered = new ConcurrentBag<string>();
		var id = Guid.NewGuid().ToString("N");
		await using var provider = BuildComposition(0,
			new OutboxMetadataSerialization(options => new SqlDeliveryMetadataContext(options)), delivered, id);
		MessageTypeRegistry.RegisterType<SqlMetadataPayload>();
		await provider.GetRequiredService<IOutboxStore>().EnqueueAsync(new SqlMetadataPayload(id, "payload"),
			new MessageContext { MessageId = id, CorrelationId = id }, CancellationToken.None).ConfigureAwait(false);
		await using var connection = fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new SqlCommand("SELECT Id,Payload FROM [dbo].[OutboxMessages] WHERE CorrelationId=@CorrelationId", connection);
		_ = command.Parameters.AddWithValue("@CorrelationId", id);
		string persistedId;
		byte[] persistedPayload;
		await using (var row = await command.ExecuteReaderAsync().ConfigureAwait(false))
		{
			(await row.ReadAsync().ConfigureAwait(false)).ShouldBeTrue();
			persistedId = row.GetString(0);
			persistedPayload = await row.GetFieldValueAsync<byte[]>(1).ConfigureAwait(false);
			(await row.ReadAsync().ConfigureAwait(false)).ShouldBeFalse();
		}
		persistedId.ShouldNotBeNullOrWhiteSpace();
		provider.GetRequiredService<IPayloadSerializer>().Deserialize<SqlMetadataPayload>(persistedPayload)
			.ShouldBe(new SqlMetadataPayload(id, "payload"));
		var result = await provider.GetRequiredService<IOutboxPublisher>().PublishPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(0);
		delivered.ShouldHaveSingleItem().ShouldBe(persistedId);
		await using var status = new SqlCommand("SELECT Status,SentAt,Payload FROM [dbo].[OutboxMessages] WHERE Id=@Id", connection);
		_ = status.Parameters.AddWithValue("@Id", persistedId);
		await using var sent = await status.ExecuteReaderAsync().ConfigureAwait(false);
		(await sent.ReadAsync().ConfigureAwait(false)).ShouldBeTrue();
		sent.GetInt32(0).ShouldBe((int)OutboxStatus.Sent);
		sent.IsDBNull(1).ShouldBeFalse();
		(await sent.GetFieldValueAsync<byte[]>(2).ConfigureAwait(false)).ShouldBe(persistedPayload);
		(await sent.ReadAsync().ConfigureAwait(false)).ShouldBeFalse();
	}

	[Fact]
	public async Task PreserveTheLazyManagedPayloadFallbackWithoutForcingItDuringGeneratedConstruction()
	{
		await PrepareAsync().ConfigureAwait(false);
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSqlServerOutboxStore(new OutboxMetadataSerialization(options => new SqlDeliveryMetadataContext(options)), Configure);
		await using var provider = services.BuildServiceProvider();
		var id = Guid.NewGuid().ToString("N");
		await provider.GetRequiredService<IOutboxStore>().EnqueueAsync(new SqlMetadataPayload(id, "payload"),
			new MessageContext { MessageId = id, CorrelationId = id }, CancellationToken.None).ConfigureAwait(false);
		await using var connection = fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new SqlCommand("SELECT Payload FROM [dbo].[OutboxMessages] WHERE CorrelationId=@CorrelationId", connection);
		_ = command.Parameters.AddWithValue("@CorrelationId", id);
		await using var row = await command.ExecuteReaderAsync().ConfigureAwait(false);
		(await row.ReadAsync().ConfigureAwait(false)).ShouldBeTrue();
		var payload = await row.GetFieldValueAsync<byte[]>(0).ConfigureAwait(false);
		(await row.ReadAsync().ConfigureAwait(false)).ShouldBeFalse();
		using var document = JsonDocument.Parse(payload);
		document.RootElement.GetProperty("persistedId").GetString().ShouldBe(id);
		document.RootElement.GetProperty("payload").GetString().ShouldBe("payload");
	}

	private async Task PrepareAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	private static OutboundMessage Message(string id, byte[] payload) => new(typeof(SqlMetadataPayload).FullName!, payload, "destination",
		new Dictionary<string, object>
		{
			["DomainHeader"] = new SqlMetadataHeader("header", SqlMetadataState.Ready, SqlMetadataState.Waiting, new SqlMetadataNested("nested", null), null),
			["null-entry"] = null!, ["integer"] = 7,
		})
	{
		Id = id, CreatedAt = Created, ScheduledAt = Scheduled, TenantId = "tenant-generated", CorrelationId = "correlation",
		CausationId = "causation", Priority = 9, PartitionKey = "partition", GroupKey = "group", SequenceNumber = 42,
	};

	private void Configure(SqlServerOutboxOptions options)
	{
		options.ConnectionString = fixture.ConnectionString;
		options.Tables.SchemaName = fixture.SchemaName;
		options.Tables.OutboxTableName = fixture.OutboxTableName;
		options.Tables.TransportsTableName = fixture.TransportsTableName;
	}

	private ServiceProvider BuildComposition(int registration, OutboxMetadataSerialization metadata, ConcurrentBag<string> delivered,
		string? expectedPayloadId = null)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		switch (registration)
		{
			case 0: services.AddSqlServerOutboxStore(metadata, Configure); break;
			case 1: services.AddSqlServerOutboxStore(_ => fixture.CreateConnection, metadata, Configure); break;
			case 2:
				services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseSqlServer(metadata, sql => sql
					.ConnectionString(fixture.ConnectionString).SchemaName(fixture.SchemaName)
					.TableName(fixture.OutboxTableName).TransportsTableName(fixture.TransportsTableName))));
				break;
		}

		_ = services.AddPluggableSerializer(SerializerIds.SystemTextJson,
			SystemTextJsonSerializer.Create(SqlDeliveryMetadataContext.Default), setAsCurrent: true);
		var bus = A.Fake<IMessageBusAdapter>();
		_ = A.CallTo(() => bus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily((IDispatchMessage message, IMessageContext context, CancellationToken _) =>
			{
				var payload = message.ShouldBeOfType<SqlMetadataPayload>();
				payload.PersistedId.ShouldBe(expectedPayloadId ?? context.MessageId);
				payload.Payload.ShouldBe("payload");
				delivered.Add(context.MessageId);
				return Task.FromResult<IMessageResult>(MessageResult.Success());
			});
		_ = services.AddSingleton(bus);
		_ = services.AddSingleton<IOutboxPublisher>(static sp => new Excalibur.Dispatch.Outbox.MessageBusOutboxPublisher(
			sp.GetRequiredService<IOutboxStore>(), sp.GetRequiredService<IPayloadSerializer>(), sp.GetRequiredService<IMessageBusAdapter>(),
			sp, sp.GetRequiredService<ILogger<Excalibur.Dispatch.Outbox.MessageBusOutboxPublisher>>()));
		return services.BuildServiceProvider();
	}

	private async Task AssertPersistedEnvelopeAsync(OutboundMessage expected)
	{
		await using var connection = fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new SqlCommand("SELECT Headers,Payload,CreatedAt,ScheduledAt,TenantId,CorrelationId,CausationId,Priority,PartitionKey,GroupKey,SequenceNumber FROM [dbo].[OutboxMessages] WHERE Id=@Id", connection);
		_ = command.Parameters.AddWithValue("@Id", expected.Id);
		await using var row = await command.ExecuteReaderAsync().ConfigureAwait(false);
		(await row.ReadAsync().ConfigureAwait(false)).ShouldBeTrue();
		row.GetString(0).ShouldBe(ExpectedHeaders);
		(await row.GetFieldValueAsync<byte[]>(1).ConfigureAwait(false)).ShouldBe(expected.Payload);
		var created = await row.GetFieldValueAsync<DateTimeOffset>(2).ConfigureAwait(false);
		created.ShouldBe(Created);
		created.Offset.ShouldBe(Created.Offset);
		var scheduled = await row.GetFieldValueAsync<DateTimeOffset>(3).ConfigureAwait(false);
		scheduled.ShouldBe(Scheduled);
		scheduled.Offset.ShouldBe(Scheduled.Offset);
		row.GetString(4).ShouldBe("tenant-generated");
		row.GetString(5).ShouldBe("correlation");
		row.GetString(6).ShouldBe("causation");
		row.GetInt32(7).ShouldBe(9);
		row.GetString(8).ShouldBe("partition");
		row.GetString(9).ShouldBe("group");
		row.GetInt64(10).ShouldBe(42);
	}
}

internal sealed record SqlMetadataPayload(string PersistedId, string Payload) : IDispatchEvent;
internal sealed record SqlMetadataHeader(string Value, SqlMetadataState State, SqlMetadataState? Optional, SqlMetadataNested Nested, string? Omitted);
internal sealed record SqlMetadataNested(string Value, string? Omitted);
internal sealed record SqlMetadataUnknownHeader(string Value);
internal enum SqlMetadataState { Waiting, Ready }

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SqlMetadataPayload))]
[JsonSerializable(typeof(SqlMetadataHeader))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
internal sealed partial class SqlDeliveryMetadataContext : JsonSerializerContext
{
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Transport;
using Excalibur.Outbox.SqlServer;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-DI + real-SQL-Server arms for the <c>outbox-delivery</c> manifest row: a staged message is delivered
/// to its transport at least once,.
/// </summary>
/// <remarks>
/// <para>
/// <b>Guarantee under test.</b> <c>Excalibur.Outbox/ARCHITECTURE.md</c> - "At-least-once. Every staged
/// message is delivered to its transport at least once." Staging and draining both go through the
/// composition a consumer writes, and the outcome is read back from the real database.
/// </para>
/// <para>
/// <b>Why this is not the existing drain-lease suite.</b> <c>OutboxDrainLeaseTestBase</c> proves lease
/// semantics against real SQL Server, but builds its publisher with <c>new MessageBusOutboxPublisher(...)</c>.
/// That proves the publisher behaves when handed its dependencies; it cannot observe whether a consumer's own
/// registration produces a working drain at all. These arms resolve the drain from a real container.
/// </para>
/// <para>
/// <b>Which drain, and why it matters.</b> <c>OutboxProcessor.cs:289</c> records that the shipped default
/// drain is <c>OutboxBackgroundService</c> to <see cref="IOutboxPublisher"/>, and that it "never constructs
/// an OutboxProcessor". An arm pointed at <c>IOutboxProcessor</c> therefore exercises a sibling entry point
/// rather than the path a consumer's host runs, so these arms drive the publisher.
/// </para>
/// <para>
/// <b>Why the consumer supplies the publisher here.</b> <see cref="IOutboxPublisher"/> has no framework
/// registration by design - the root design decisions list it as pluggable so a consumer chooses their
/// transport, with no forced infrastructure dependency. Registering it is therefore part of faithfully
/// modelling the consumer, not a test convenience.
/// </para>
/// <para>
/// <b>Why the transport is a recording double while the store is real.</b> The guarantee is delivery <i>to
/// the transport</i>, so the transport is the OBSERVATION POINT, not the subject. The lease, the claim and
/// the mark-sent all live in SQL Server, and those are real here.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
public sealed class SqlServerOutboxDeliveryEndToEndShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerOutboxDeliveryEndToEndShould(SqlServerOutboxStoreContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// LIVENESS and SAFETY for at-least-once: a staged message reaches the transport, and is then recorded as
	/// sent in the real database so it is not redelivered forever.
	/// </summary>
	[Fact]
	public async Task DeliverAStagedMessageToItsTransport_ThroughTheRealDIRegistration()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		await PrepareDatabaseAsync().ConfigureAwait(false);

		var delivered = new ConcurrentBag<string>();
		await using var provider = BuildConsumerComposition(delivered);

		var store = provider.GetRequiredService<IOutboxStore>();
		var message = new OutboundMessage("E2EDeliveryMessage", [1, 2, 3], "orders");
		await store.StageMessageAsync(message, cancellationToken).ConfigureAwait(false);

		var publisher = provider.GetRequiredService<IOutboxPublisher>();
		_ = await publisher.PublishPendingMessagesAsync(cancellationToken).ConfigureAwait(false);

		// LIVENESS: the message actually reached the transport. A drain that resolves but delivers nothing
		// is the advertised-but-unreachable shape this manifest row exists to catch, and it satisfies any
		// assertion that only checks "no exception was thrown".
		delivered.ShouldContain(
			message.Id,
			"the staged message was never handed to the transport, so at-least-once delivery does not hold "
			+ "through the public registration");

		// SAFETY: the delivery is recorded in the REAL database, so it is not redelivered forever.
		var stillPending = await store.GetUnsentMessagesAsync(10, cancellationToken).ConfigureAwait(false);
		stillPending.Select(m => m.Id).ShouldNotContain(
			message.Id,
			"the message was delivered but never marked sent in SQL Server, so every later drain would "
			+ "deliver it again - at-least-once must not degrade into unbounded redelivery");
	}

	private async Task PrepareDatabaseAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"at-least-once delivery is the outbox's headline guarantee - these real-SQL-Server arms must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Composes the outbox the way a consumer does, supplying the pluggable publisher and a recording
	/// transport as the observation point.
	/// </summary>
	private ServiceProvider BuildConsumerComposition(ConcurrentBag<string> delivered)
	{
		var connectionString = _fixture.ConnectionString;
		var schemaName = _fixture.SchemaName;
		var outboxTableName = _fixture.OutboxTableName;
		var transportsTableName = _fixture.TransportsTableName;

		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddExcalibur(excalibur =>
			excalibur.AddOutbox(outbox =>
				outbox.UseSqlServer(sql => sql
					.ConnectionString(connectionString)
					.SchemaName(schemaName)
					.TableName(outboxTableName)
					.TransportsTableName(transportsTableName))));

		// The payload serializer the drain uses to materialise a staged body. Not registered by AddExcalibur,
		// so a consumer running the outbox supplies it - same pluggable shape as the publisher itself.
		_ = services.AddPluggableSerialization();

		var bus = A.Fake<IMessageBusAdapter>();
		_ = A.CallTo(() => bus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily((IDispatchMessage _, IMessageContext context, CancellationToken _) =>
			{
				delivered.Add(context.MessageId);

				// Succeeded MUST be set explicitly. FakeItEasy defaults a bool to false, so an unconfigured
				// IMessageResult reports FAILURE and the drain measures the error path instead of delivery.
				var result = A.Fake<IMessageResult>();
				_ = A.CallTo(() => result.Succeeded).Returns(true);
				return Task.FromResult(result);
			});

		_ = services.AddSingleton(bus);

		// The pluggable publisher a consumer selects for a message-bus transport, constructed explicitly.
		// ActivatorUtilities cannot be used: MessageBusOutboxPublisher exposes two public constructors whose
		// parameter types are both satisfiable from the container (single-transport IMessageBusAdapter and
		// multi-transport ITransportRegistry), so it throws "Multiple constructors accepting all given
		// argument types". Naming the single-transport constructor is what a consumer must do too.
		_ = services.AddSingleton<IOutboxPublisher>(static sp =>
			new Excalibur.Dispatch.Outbox.MessageBusOutboxPublisher(
				sp.GetRequiredService<IOutboxStore>(),
				sp.GetRequiredService<IPayloadSerializer>(),
				sp.GetRequiredService<IMessageBusAdapter>(),
				sp,
				sp.GetRequiredService<ILogger<Excalibur.Dispatch.Outbox.MessageBusOutboxPublisher>>()));

		return services.BuildServiceProvider();
	}
}

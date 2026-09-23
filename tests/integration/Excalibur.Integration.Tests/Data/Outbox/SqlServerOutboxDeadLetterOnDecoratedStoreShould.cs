// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Outbox.SqlServer;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-DI + real-SQL-Server lock: a message that exhausts its delivery attempts must reach a terminal
/// state on a DECORATED store, rather than throwing and being re-claimed forever.
/// </summary>
/// <remarks>
/// <para>
/// <b>Requirement.</b> <c>Excalibur.Outbox/ARCHITECTURE.md</c> guarantees at-least-once delivery. The
/// dead-letter path is how a message that cannot be delivered stops being re-claimed; without it,
/// at-least-once degrades into unbounded redelivery of a poison message, which never drains.
/// </para>
/// <para>
/// <b>Predicate this arm tests.</b> With the store decorated - which is what the documented composition
/// produces, telemetry decoration being on by default - a retry-exhausted message reaches termination
/// without the drain throwing.
/// </para>
/// <para>
/// <b>Why the DECORATED case is the whole point.</b> Capability discovery has two spellings in this
/// codebase and only one survives decoration. <c>OutboxDeadLetterCapabilityValidator</c> PROBES via
/// <c>GetService(typeof(IDeadLetterableOutboxStore))</c>, and <c>TelemetryOutboxStoreDecorator</c>
/// forwards that probe - so the startup guard passes. A consumer of the capability that TYPE-TESTS the
/// store instead sees a store that "lacks" a capability the underlying store implements. That is the exact
/// failure <c>MessageBusOutboxPublisher</c>'s own constructor comment warns against: "a cast sees only the
/// outermost decorator's type". An undecorated store would pass this arm for the wrong reason, so the
/// decoration is asserted rather than assumed.
/// </para>
/// <para>
/// <b>Why this drives the processor and not the publisher.</b> <c>OutboxProcessor.cs:289</c> records that
/// the publisher drain "never constructs an OutboxProcessor", and <c>MessageBusOutboxPublisher</c> carries
/// no dead-letter path at all. An arm on the publisher is therefore green whether or not this defect
/// exists - measured, after building exactly that arm first and finding it green at HEAD.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
public sealed class SqlServerOutboxDeadLetterOnDecoratedStoreShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerOutboxDeadLetterOnDecoratedStoreShould(SqlServerOutboxStoreContainerFixture fixture) =>
		_fixture = fixture;

	[Fact]
	public async Task TerminateARetryExhaustedMessage_RatherThanThrowingAndReclaimingItForever()
	{
		var cancellationToken = TestContext.Current.CancellationToken;

		_fixture.DockerAvailable.ShouldBeTrue(
			"a poison message that can never terminate is an unbounded redelivery loop - this real-SQL-Server "
			+ "lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		await using var provider = BuildConsumerComposition();

		var store = provider.GetRequiredService<IOutboxStore>();

		// The composition really is decorated. Asserted, because an undecorated store satisfies this arm for
		// a reason that has nothing to do with the property under test.
		store.GetType().Name.ShouldNotBe(
			nameof(SqlServerOutboxStore),
			"this lock only means something on a decorated store - the capability is reachable by probe and "
			+ "invisible to a type test only once a decorator is in the way");

		var message = new OutboundMessage("PoisonMessage", [9], "orders");
		await store.StageMessageAsync(message, cancellationToken).ConfigureAwait(false);

		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("e2e-dead-letter");

		// One attempt is configured, so the first dispatch failure exhausts the message and takes the
		// termination path immediately - no sleeps, no backoff window to wait out.
		var failure = await Record.ExceptionAsync(
			() => processor.DispatchPendingMessagesAsync(cancellationToken)).ConfigureAwait(false);

		failure.ShouldBeNull(
			"terminating a retry-exhausted message threw instead of dead-lettering it. The decorated store "
			+ "exposes the dead-letter capability through its probe; a consumer that type-tests the store "
			+ "cannot see it, so the message never reaches a terminal state and every later drain re-claims "
			+ "it - at-least-once becomes unbounded redelivery of a message that can never succeed");
	}

	/// <summary>
	/// Composes the outbox as the documented consumer path does, with a dispatcher that always fails so the
	/// message reaches termination on its first attempt.
	/// </summary>
	private ServiceProvider BuildConsumerComposition()
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

		_ = services.AddPluggableSerialization();

		// One attempt, so exhaustion is immediate and deterministic.
		_ = services.Configure<OutboxDeliveryOptions>(o => o.MaxAttempts = 1);

		// The dispatch always fails: this arm is about what happens AFTER delivery is impossible, not about
		// delivery itself.
		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(dispatcher)
			.Where(call => call.Method.Name == nameof(IDispatcher.DispatchAsync))
			.Throws(() => new InvalidOperationException("transport is unavailable"));
		_ = services.AddSingleton(dispatcher);

		return services.BuildServiceProvider();
	}
}

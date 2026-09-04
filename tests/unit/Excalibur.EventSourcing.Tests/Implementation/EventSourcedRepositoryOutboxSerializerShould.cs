// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1506 // Excessive class coupling -- repository wiring needs many DI collaborators by design

using System.Text;

using Excalibur.Dispatch;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Implementation;

using FakeItEasy;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

using IEventStore = Excalibur.EventSourcing.IEventStore;
using AppendResult = Excalibur.EventSourcing.AppendResult;

namespace Excalibur.EventSourcing.Tests.Implementation;

/// <summary>
/// Author≠impl regression lock for <c>jv85yi</c> (§2): the outbox payload MUST be produced through the
/// injected <see cref="IEventSerializer"/> (the single serialization seam), NOT a raw
/// <c>JsonSerializer</c> call with default PascalCase — which produced a third, divergent wire variant.
/// </summary>
/// <remarks>
/// Seam: <c>EventSourcedRepository.CreateOutboundMessage</c> sets
/// <c>Payload = _eventSerializer.SerializeEvent(@event)</c>. This lock injects a serializer that returns a
/// distinctive SENTINEL byte sequence and asserts the staged <see cref="OutboundMessage.Payload"/> equals
/// it byte-for-byte — proving the injected serializer produced the payload. NON-VACUOUS: the pre-fix raw
/// <c>JsonSerializer.SerializeToUtf8Bytes(@event)</c> would emit JSON bytes (never the sentinel) → RED.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventSourcedRepositoryOutboxSerializerShould
{
	[MessageName("Test.Es.OutboxSerializerIntegrationEvent")]
	internal sealed record OutboxIntegrationEvent : DomainEvent, IIntegrationEvent
	{
		public string Payload { get; init; } = string.Empty;
	}

	internal sealed class OutboxSerializerAggregate : AggregateRoot
	{
		public OutboxSerializerAggregate() { }
		public OutboxSerializerAggregate(string id) : base(id) { }

		public void DoWork(string payload)
			=> RaiseEvent(new OutboxIntegrationEvent { Payload = payload });

		protected override bool ApplyEventInternal(IDomainEvent @event)
		{
			// No state mutation needed for this lock.
					return true;
		}
	}

	[Fact]
	public async Task StageOutboxPayloadViaInjectedEventSerializerNotRawJson()
	{
		// Arrange
		var aggregate = new OutboxSerializerAggregate("agg-jv85yi");
		aggregate.DoWork("order-placed");

		// A distinctive payload only the INJECTED serializer could have produced (never valid JSON of the event).
		var sentinel = Encoding.UTF8.GetBytes("INJECTED-EVENT-SERIALIZER-SENTINEL");
		var serializer = A.Fake<IEventSerializer>();
		_ = A.CallTo(() => serializer.SerializeEvent(A<IDomainEvent>._)).Returns(sentinel);

		OutboundMessage? staged = null;
		var outboxStore = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Invokes((OutboundMessage m, CancellationToken _) => staged = m);

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));

		var repository = new EventSourcedRepository<OutboxSerializerAggregate>(
			eventStore,
			serializer,
			id => new OutboxSerializerAggregate(id),
			Options.Create(new EventSourcedRepositoryOptions { OutboxStagingStrategy = OutboxStagingStrategy.EventuallyConsistent }),
			outboxStore: outboxStore);

		// Act
		await repository.SaveAsync(aggregate, CancellationToken.None).ConfigureAwait(false);

		// Assert — the staged outbox payload was produced by the injected serializer (RED on raw JsonSerializer).
		staged.ShouldNotBeNull("the integration event must be staged to the outbox");
		staged!.Payload.ShouldBe(sentinel,
			"outbox payload must be produced via the injected IEventSerializer, not a raw JsonSerializer default variant");
		_ = A.CallTo(() => serializer.SerializeEvent(A<IDomainEvent>._)).MustHaveHappened();
	}
}

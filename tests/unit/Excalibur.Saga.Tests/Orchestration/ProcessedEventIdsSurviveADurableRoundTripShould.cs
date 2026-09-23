// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// The saga idempotency guard must survive the serializer every durable store persists through.
/// </summary>
/// <remarks>
/// <para>
/// <c>SagaState.ProcessedEventIds</c> is a get-only collection. System.Text.Json WRITES such a property
/// and, under its default Replace handling, DISCARDS it on read — so the set comes back empty, every
/// <c>TryMarkEventProcessed</c> returns true, and a redelivered event re-executes its step. The
/// documented 1000-entry bound is unreachable because the effective retention is zero.
/// </para>
/// <para>
/// This arm round-trips through the real <see cref="DispatchJsonSerializer"/> on a DERIVED state type,
/// because that is what the durable stores serialize and because per-type creation handling has to
/// reach the consumer's own subclass to be worth anything. The existing dedup suite cannot detect this:
/// it fakes the store to hand back the SAME OBJECT REFERENCE, so nothing is ever deserialized.
/// </para>
/// </remarks>
public sealed class ProcessedEventIdsSurviveADurableRoundTripShould
{
	private sealed class OrderSagaState : SagaState
	{
		public string? OrderId { get; set; }
	}

	[Fact]
	public void KeepTheProcessedEventIdsThroughSerializeAndDeserialize()
	{
		// SAFETY. The guard is the whole point of the property; losing it silently re-executes steps.
		var serializer = new DispatchJsonSerializer();

		var saga = new OrderSagaState { OrderId = "order-1", Version = 3 };
		saga.TryMarkEventProcessed("evt-1").ShouldBeTrue();
		saga.TryMarkEventProcessed("evt-2").ShouldBeTrue();

		var json = serializer.Serialize(saga);
		var restored = serializer.Deserialize<OrderSagaState>(json);

		restored.ShouldNotBeNull();

		// CONTROL, asserted first: if a plain settable property did not survive either, the round trip
		// itself is broken and the assertion below would be measuring the wrong thing.
		restored!.Version.ShouldBe(3, "the round trip itself is broken; this arm proves nothing about dedup");
		restored.OrderId.ShouldBe("order-1");

		restored.ProcessedEventIds.Count.ShouldBe(2,
			"the processed-event set was discarded on deserialize, so every redelivered event is treated "
			+ "as new and its step re-executes — the idempotency guard is inert in every durable store");
		restored.TryMarkEventProcessed("evt-1").ShouldBeFalse(
			"a already-processed event must be refused after a reload, which is the guard's entire job");
	}

	[Fact]
	public void StillAcceptAnEventItHasNotSeen()
	{
		// LIVENESS. A guard that refused everything after a reload would satisfy the arm above while
		// deadlocking every saga, so the permitted case is asserted too.
		var serializer = new DispatchJsonSerializer();

		var saga = new OrderSagaState { OrderId = "order-2" };
		saga.TryMarkEventProcessed("evt-1").ShouldBeTrue();

		var restored = serializer.Deserialize<OrderSagaState>(serializer.Serialize(saga));

		restored!.TryMarkEventProcessed("evt-NEW").ShouldBeTrue(
			"an unseen event must still be accepted after a reload");
	}
}

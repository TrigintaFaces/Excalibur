// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

using Tests.Shared.TestTypes;

namespace Excalibur.Dispatch.Tests.CloudEvents;

/// <summary>
/// Binds what happens to an event that cannot fit in any batch.
/// </summary>
/// <remarks>
/// <para>
/// <b>The batching loop used to lose such an event silently, and the loss was invisible to every
/// caller.</b> When an event was refused by a batch that was already empty, the loop built one more
/// empty batch, added the event to it, and discarded the result — which at that point could only ever
/// be false, because a fresh batch carries the same size limit that had just rejected it. The event was
/// dropped and an empty batch was emitted in its place, so the caller received a batch list that looked
/// well-formed and published nothing.
/// </para>
/// <para>
/// The safety arm alone is satisfied by a processor that refuses everything, which is why the liveness
/// arm below is not decoration: it is the only one that fails if batching stops working entirely.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DefaultCloudEventBatchProcessorShould
{
	private static DefaultCloudEventBatchProcessor CreateProcessor(CloudEventBatchOptions options) =>
		new(options, A.Fake<IDispatcher>(), A.Fake<IServiceProvider>());

	// A bare fake returns "" for every string property, and an empty message id is rejected by the
	// CloudEvents library before the batching logic under test is ever reached -- so the id is supplied
	// deliberately rather than left to the fake's default.
	// The shared TestEvent declares no stable message name, and conversion to a CloudEvent refuses a
	// type without one -- correctly, since that name becomes the CloudEvents 'type' external subscribers
	// filter on. Declared here so the arms exercise batching rather than that refusal.
	[MessageName("test.cloudevents.batching")]
	private sealed class NamedTestEvent : TestEvent;

	private static IMessageContext Context()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("m-1");
		return context;
	}

	[Fact]
	public void Refuse_an_event_too_large_for_any_batch_rather_than_drop_it()
	{
		// A one-byte ceiling makes every event oversized, which is the same state a genuinely huge
		// payload reaches — without needing to build one.
		var processor = CreateProcessor(new CloudEventBatchOptions { MaxBatchSizeBytes = 1 });

		var thrown = Should.Throw<InvalidOperationException>(
			() => processor.CreateBatches([new NamedTestEvent()], Context()));

		thrown.Message.ShouldContain(
			"batch",
			Case.Insensitive,
			customMessage: "the refusal must tell the caller which limit it hit, or they cannot act on it");
	}

	[Fact]
	public void Batch_an_event_that_fits_so_the_refusal_is_not_the_only_outcome()
	{
		var processor = CreateProcessor(new CloudEventBatchOptions());

		var batches = processor.CreateBatches([new NamedTestEvent()], Context());

		var batch = batches.ShouldHaveSingleItem();
		batch.Count.ShouldBe(1, "an ordinary event must still reach a batch");
	}

	/// <summary>
	/// LIVENESS for the MIDDLE branch: an event the CURRENT batch refuses but an EMPTY batch accepts must
	/// roll over into a fresh batch, not be refused.
	/// </summary>
	/// <remarks>
	/// Without this arm the pair above cannot distinguish the shipped fix from a simpler one that throws on
	/// any refusal. Both existing arms use a SINGLE event, so neither ever executes the roll-over: the
	/// refusal arm is refused by an already-empty batch and skips it, and the liveness arm is never refused
	/// at all. A change to "throw whenever TryAdd returns false" would keep both of them green while turning
	/// a working path into an exception for every consumer who batches more events than one batch holds.
	/// <para>
	/// The ceiling is expressed as a COUNT rather than a size because the batch checks its event-count limit
	/// before it measures bytes, so this reaches the roll-over exactly and without depending on how any
	/// payload happens to serialise.
	/// </para>
	/// </remarks>
	[Fact]
	public void Roll_an_event_the_current_batch_refuses_into_a_fresh_batch()
	{
		var processor = CreateProcessor(new CloudEventBatchOptions { MaxEvents = 1 });

		var batches = processor.CreateBatches([new NamedTestEvent(), new NamedTestEvent()], Context());

		batches.Count.ShouldBe(
			2,
			"the second event does not fit the first batch and does fit an empty one, so it belongs in a "
			+ "second batch - refusing it would break a consumer whose traffic simply exceeds one batch");
		batches[0].Count.ShouldBe(1);
		batches[1].Count.ShouldBe(1, "the rolled-over event is CARRIED, not dropped into an empty batch");
	}
}

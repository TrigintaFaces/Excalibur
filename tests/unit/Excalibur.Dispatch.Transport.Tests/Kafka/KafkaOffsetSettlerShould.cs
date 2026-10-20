// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class KafkaOffsetSettlerShould
{
	private static readonly TopicPartition Partition0 = new("orders", new Partition(0));
	private static readonly string[] ExpectedSettlementCalls = ["store", "commit"];

	// A StoreOffset callback can revoke ownership after the position was computed but before Commit.
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Refuse_commit_if_store_callback_revokes_assignment(bool storeAlsoThrows)
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var progress = AssignedProgress(out var generation);
		var settler = new KafkaOffsetSettler(consumer, progress, "orders", NullLogger.Instance);
		A.CallTo(() => consumer.StoreOffset(A<TopicPartitionOffset>._)).Invokes(() =>
		{
			lock (progress.OperationGate)
			{
				progress.OnPartitionsRevoked([Partition0]);
			}
			if (storeAlsoThrows)
			{
				throw new KafkaException(ErrorCode.Local_State);
			}
		});

		settler.SettleTerminal(Partition0, 7, generation).ShouldBeFalse();
		A.CallTo(() => consumer.StoreOffset(A<TopicPartitionOffset>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).MustNotHaveHappened();
		progress.IsCurrentGeneration(Partition0, generation).ShouldBeFalse();
	}

	[Fact]
	public void Commit_current_assignment_position_after_store()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var progress = AssignedProgress(out var generation);
		var settler = new KafkaOffsetSettler(consumer, progress, "orders", NullLogger.Instance);
		var calls = new List<string>();
		A.CallTo(() => consumer.StoreOffset(A<TopicPartitionOffset>._)).Invokes(() => calls.Add("store"));
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).Invokes(() => calls.Add("commit"));

		settler.SettleTerminal(Partition0, 7, generation).ShouldBeTrue();
		calls.ShouldBe(ExpectedSettlementCalls);
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>.That.Matches(offsets =>
			offsets.Count() == 1 && offsets.Single().TopicPartition.Equals(Partition0) && offsets.Single().Offset.Value == 8)))
			.MustHaveHappenedOnceExactly();
		progress.TrySettle(Partition0, 7, generation, out _).ShouldBeFalse("the successful commit must be recorded");
	}

	[Fact]
	public void Keep_absolute_commit_retry_when_store_fails_without_revocation()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var progress = AssignedProgress(out var generation);
		var settler = new KafkaOffsetSettler(consumer, progress, "orders", NullLogger.Instance);
		A.CallTo(() => consumer.StoreOffset(A<TopicPartitionOffset>._)).Throws(new KafkaException(ErrorCode.Local_State));

		settler.SettleTerminal(Partition0, 7, generation).ShouldBeTrue();
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).MustHaveHappenedOnceExactly();
	}

	private static KafkaPartitionProgress AssignedProgress(out long generation)
	{
		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		progress.TryBeginDelivery(Partition0, 7, out generation).ShouldBeTrue();
		return progress;
	}
}

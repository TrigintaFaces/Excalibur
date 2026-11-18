// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using KafkaConsumeResult = global::Confluent.Kafka.ConsumeResult<string, byte[]>;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// KafkaOptions.Consumer.MaxBatchSize is a documented, Range-validated option with a fluent builder
/// behind it. The receiver used a private const instead, so the value was carried through three types
/// and ignored: a host calling MaxBatchSize(2) still got batches of up to 100.
/// </summary>
/// <remarks>
/// The arm that matters is the one that resolves through the REAL registration. Binding only the
/// constructor would prove the receiver honours a value it is handed, which it always would have --
/// the defect was that nothing handed it one.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class KafkaBatchSizeOptionShould
{
	private const string Topic = "orders";
	private const string TransportName = "batch-size-test";

	/// <summary>
	/// THE DEFECT-BINDING ARM. RED before the fix: the registration never read the option, so this
	/// returned 5. RED again if the registration stops passing it, which is the regression to catch.
	/// </summary>
	[Fact]
	public async Task HonourTheConfiguredBatchCeiling_WhenResolvedThroughTheRealRegistration()
	{
		await using var provider = BuildProvider(maxBatchSize: 2, available: 5);
		var receiver = provider.GetRequiredKeyedService<ITransportReceiver>(TransportName);

		var batch = await receiver.ReceiveAsync(10, CancellationToken.None);

		batch.Count.ShouldBe(2, "the configured ceiling bounds the batch, not the caller's request");
	}

	/// <summary>
	/// LIVENESS. The ceiling must not be the only thing that decides: a caller asking for fewer than
	/// the ceiling still gets its own smaller number, so a transport that always returned the ceiling
	/// would fail here. Without this, the arm above is satisfied by a receiver hard-coded to 2.
	/// </summary>
	[Fact]
	public async Task StillHonourASmallerRequestThanTheCeiling()
	{
		await using var provider = BuildProvider(maxBatchSize: 10, available: 5);
		var receiver = provider.GetRequiredKeyedService<ITransportReceiver>(TransportName);

		var batch = await receiver.ReceiveAsync(3, CancellationToken.None);

		batch.Count.ShouldBe(3, "the caller's request bounds the batch when it is the smaller of the two");
	}

	/// <summary>
	/// SAFETY on the codomain. A ceiling of zero is not a smaller batch -- it is a receiver that
	/// returns an empty batch while reporting success, which no caller can tell apart from an empty
	/// topic. It must be refused where it is supplied rather than silently becoming a no-op.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void RefuseACeilingThatCanNeverYieldAMessage(int ceiling)
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();

		_ = Should.Throw<ArgumentOutOfRangeException>(() => new KafkaTransportReceiver(
			consumer,
			Topic,
			NullLogger<KafkaTransportReceiver>.Instance,
			maxBatchSize: ceiling));
	}

	private static ServiceProvider BuildProvider(int maxBatchSize, int available)
	{
		var queue = new Queue<KafkaConsumeResult>(Enumerable.Range(0, available).Select(i => Record(i)));
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		var services = new ServiceCollection();
		_ = services.AddLogging();

		// FIRST, so it wins: the transport registers its own consumer with TryAddSingleton, which is
		// first-one-wins, so registering a fake ahead of it keeps this arm off a real broker while
		// leaving every other part of the real registration in place.
		services.TryAddSingleton(consumer);

		_ = services.AddKafkaTransport(TransportName, builder => builder
			.BootstrapServers("localhost:9092")
			.ConfigureConsumer(c => c.MaxBatchSize(maxBatchSize)));

		return services.BuildServiceProvider();
	}

	private static KafkaConsumeResult Record(long offset) =>
		new()
		{
			Topic = Topic,
			Partition = new Partition(0),
			Offset = new Offset(offset),
			Message = new Message<string, byte[]> { Key = $"m{offset}", Value = new byte[4] },
		};
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.BatchProcessing;
using Excalibur.Dispatch.Options.Performance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.BatchProcessing;

/// <summary>
/// Binds the batch processor's constructor preconditions on the values it is handed.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are preconditions, not configuration validation, and the distinction is the point.</b>
/// <c>MicroBatchOptions</c> is the value handed to this constructor; nothing binds it from configuration
/// and no options-pipeline validator can see it. Consumer configuration is checked at host start against
/// the batching options instead. What remains here is the ordinary obligation of a constructor that
/// accepts a value: reject one it cannot work with, at the moment it is handed over, naming the member
/// at fault.
/// </para>
/// <para>
/// Each value fails differently if allowed through, and the batch-size case fails SILENTLY — a batch that
/// is never full flushes only on the timer, which is a throughput collapse with no error to find. That is
/// why the guard names the member rather than letting the bounded channel throw about a channel.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class BatchProcessorOptionGuardShould
{
	private static BatchProcessor<string> Construct(MicroBatchOptions options) =>
		new(
			batchProcessor: _ => ValueTask.CompletedTask,
			logger: NullLogger<BatchProcessor<string>>.Instance,
			meterFactory: null,
			options: options);

	/// <summary>
	/// SAFETY. A batch size of zero is never full, so the batcher degrades to timer-only flushing.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Reject_a_non_positive_batch_size(int size)
	{
		var ex = Should.Throw<ArgumentOutOfRangeException>(
			() => Construct(new MicroBatchOptions { MaxBatchSize = size }));

		ex.ParamName.ShouldContain(nameof(MicroBatchOptions.MaxBatchSize));
	}

	/// <summary>
	/// SAFETY. Channel capacity bounds a bounded channel, which otherwise throws from its own constructor
	/// naming the channel rather than the member that caused it; this fails earlier and names the member.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Reject_a_non_positive_channel_capacity(int capacity)
	{
		var ex = Should.Throw<ArgumentOutOfRangeException>(
			() => Construct(new MicroBatchOptions { ChannelCapacity = capacity }));

		ex.ParamName.ShouldContain(nameof(MicroBatchOptions.ChannelCapacity));
	}

	/// <summary>
	/// SAFETY. A non-positive delay turns the flush timer into a busy loop.
	/// </summary>
	[Fact]
	public void Reject_a_non_positive_batch_delay()
	{
		var ex = Should.Throw<ArgumentOutOfRangeException>(
			() => Construct(new MicroBatchOptions { MaxBatchDelay = TimeSpan.Zero }));

		ex.ParamName.ShouldContain(nameof(MicroBatchOptions.MaxBatchDelay));
	}

	/// <summary>
	/// PRECISION. The shipped defaults must still build a working processor.
	/// </summary>
	/// <remarks>
	/// Without this, every rejection arm above is satisfied by a constructor that refuses everything.
	/// </remarks>
	[Fact]
	public void Construct_normally_on_the_shipped_defaults()
	{
		Should.NotThrow(() => Construct(new MicroBatchOptions()));
	}
}

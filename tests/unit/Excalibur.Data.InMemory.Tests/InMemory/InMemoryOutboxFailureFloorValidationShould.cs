// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.InMemory;
using Excalibur.Outbox.Outbox;
using Excalibur.Outbox.Partitioning;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Tests.InMemory;

/// <summary>
/// lz7us9 — the InMemory 5th conformance arm at the WIRE level: proof that the cross-options floor-vs-poll
/// validator is REGISTERED and FIRES through the production DI path (<c>AddInMemoryOutboxStore</c> +
/// <c>ValidateOnStart</c>, S873 discipline). <c>InMemoryOutboxOptions.FailureBackoffFloorSeconds</c> (F) must
/// exceed <c>effectivePoll = partitionActive ? Max(processing, partition) : processing</c>, or a failed
/// message is re-claimable on the next poll (the zero-backoff hot-loop the floor prevents). Uniform with the
/// Postgres/Oracle/SqlServer validators (SA-confirmed, 34487).
/// </summary>
/// <remarks>
/// NON-VACUOUS + wording-robust. The partition-poll arm sets F above the processing poll but at/below the
/// partition poll: only the partition-poll branch can reject it, so the throw itself proves the partition
/// interval is wired-in. The liveness control proves no over-rejection. RED if the validator registration or
/// its <c>ValidateOnStart</c> wiring is dropped. The <c>InMemoryOutboxOptionsValidator</c> is
/// <c>internal</c>, so this exercises it through the public DI seam rather than constructing it directly.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class InMemoryOutboxFailureFloorValidationShould
{
	private static ServiceProvider BuildProvider(
		int floorSeconds,
		TimeSpan processingPoll,
		(OutboxPartitionStrategy Strategy, TimeSpan Poll)? partition = null)
	{
		var services = new ServiceCollection();

		_ = services.AddInMemoryOutboxStore(o => o.FailureBackoffFloorSeconds = floorSeconds);
		_ = services.Configure<OutboxProcessingOptions>(o => o.PollingInterval = processingPoll);

		if (partition is { } part)
		{
			_ = services.Configure<OutboxPartitionOptions>(o =>
			{
				o.Strategy = part.Strategy;
				o.PollingInterval = part.Poll;
			});
		}

		return services.BuildServiceProvider();
	}

	// Materializing .Value runs the registered IValidateOptions<InMemoryOutboxOptions> (ValidateOnStart wiring).
	private static InMemoryOutboxOptions Resolve(ServiceProvider provider) =>
		provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>().Value;

	// SAFETY (processing-poll bound, partitioning OFF): F <= processing poll => ValidateOnStart throws.
	[Fact]
	public async Task Throws_WhenFloorAtOrBelowProcessingPoll()
	{
		using var provider = BuildProvider(floorSeconds: 5, processingPoll: TimeSpan.FromSeconds(10));

		var ex = await Should.ThrowAsync<OptionsValidationException>(() => Task.Run(() => Resolve(provider)));
		ex.Message.ShouldContain("FailureBackoffFloorSeconds");
	}

	// SAFETY (partition-poll bound): F above processing poll (5s) but at/below partition poll (20s). Only the
	// partition-poll branch can reject F(10) > processing(5) — the throw proves the partition interval is wired.
	[Fact]
	public async Task Throws_WhenFloorAtOrBelowPartitionPoll_EvenWhenAboveProcessingPoll()
	{
		using var provider = BuildProvider(
			floorSeconds: 10,
			processingPoll: TimeSpan.FromSeconds(5),
			partition: (OutboxPartitionStrategy.ByTenantHash, TimeSpan.FromSeconds(20)));

		_ = await Should.ThrowAsync<OptionsValidationException>(() => Task.Run(() => Resolve(provider)));
	}

	// LIVENESS (no over-rejection): F strictly above every active poll resolves cleanly.
	[Fact]
	public void DoesNotThrow_WhenFloorAboveEveryActivePoll()
	{
		using var provider = BuildProvider(
			floorSeconds: 30,
			processingPoll: TimeSpan.FromSeconds(5),
			partition: (OutboxPartitionStrategy.ByTenantHash, TimeSpan.FromSeconds(20)));

		var options = Resolve(provider);
		options.FailureBackoffFloorSeconds.ShouldBe(30);
	}
}

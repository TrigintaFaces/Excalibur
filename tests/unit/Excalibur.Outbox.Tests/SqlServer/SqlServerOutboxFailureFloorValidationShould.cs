// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Partitioning;
using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Tests.SqlServer;

/// <summary>
/// lz7us9 — the 5th conformance arm for SQL Server: the cross-options Lamport-R1 <b>misconfiguration
/// validator</b>. The failure-anchored re-claim floor <c>Processing.FailureBackoffFloorSeconds</c> (F) must
/// exceed the drain poll interval on <b>every active drain path</b>, or a failed message is re-claimable on
/// the very next poll (the zero-backoff retry hot-loop the floor exists to prevent). The validator fails fast
/// at startup (<c>ValidateOnStart</c>) when F is <b>≤</b> the effective poll interval:
/// <c>effectivePoll = partitionActive ? max(processing.PollingInterval, partition.PollingInterval) : processing.PollingInterval</c>.
/// </summary>
/// <remarks>
/// <para>
/// This exercises the REAL DI wiring (S873 WIRE-lock discipline): a real <see cref="ServiceProvider"/> built
/// through the production <c>AddExcaliburOutbox(...).UseSqlServer(...)</c> path, with the validator triggered
/// exactly as a host triggers it — by materializing <c>IOptions&lt;SqlServerOutboxOptions&gt;.Value</c>. It is
/// NOT a direct <c>validator.Validate(...)</c> unit test: the point is to prove the validator is actually
/// registered + wired via <c>ValidateOnStart</c>, so a future regression that drops the registration is RED.
/// </para>
/// <para>
/// NON-VACUOUS + wording-robust. The partition-poll arm (<see cref="Throws_WhenFloorAtOrBelowPartitionPoll_EvenWhenAboveProcessingPoll"/>)
/// sets F <b>above</b> the processing poll but at/below the partition poll: the throw can ONLY come from the
/// partition-poll branch — a validator that checked processing alone would PASS — so the throw itself is the
/// proof that the partition interval is enforced, with no dependency on the failure message wording. The valid
/// control arms prove the validator does not over-reject (liveness): a floor above every active poll resolves
/// cleanly.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class SqlServerOutboxFailureFloorValidationShould
{
	private const string TestConnectionString = "Server=localhost;Database=Test;Integrated Security=True";

	private static ServiceProvider BuildProvider(
		int floorSeconds,
		TimeSpan processingPoll,
		(OutboxPartitionStrategy Strategy, TimeSpan Poll)? partition = null)
	{
		var services = new ServiceCollection();

		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.WithProcessing(p => p.PollingInterval(processingPoll));
			_ = outbox.UseSqlServer(sql => sql.ConnectionString(TestConnectionString));

			if (partition is { } part)
			{
				_ = outbox.UsePartitionedProcessing(p =>
				{
					p.Strategy = part.Strategy;
					p.PollingInterval = part.Poll;
				});
			}
		});

		// The failure-backoff floor is not exposed on the SqlServer fluent builder, so set it through the real
		// options pipeline via PostConfigure (runs before validation). The validator reads exactly this value.
		_ = services.PostConfigure<SqlServerOutboxOptions>(o => o.Processing.FailureBackoffFloorSeconds = floorSeconds);

		return services.BuildServiceProvider();
	}

	// Materializing .Value runs the registered IValidateOptions<SqlServerOutboxOptions> (ValidateOnStart wiring).
	private static SqlServerOutboxOptions Resolve(ServiceProvider provider) =>
		provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;

	// SAFETY (processing-poll bound, partitioning OFF): F ≤ processing poll ⇒ ValidateOnStart throws.
	// RED against a store with no floor-vs-poll validator (it would resolve cleanly and hot-loop at runtime).
	[Fact]
	public async Task Throws_WhenFloorAtOrBelowProcessingPoll()
	{
		using var provider = BuildProvider(floorSeconds: 5, processingPoll: TimeSpan.FromSeconds(10));

		var ex = await Should.ThrowAsync<OptionsValidationException>(() => Task.Run(() => Resolve(provider)));
		// The validation failure must name the offending floor option (Shouldly's string ShouldContain 2nd
		// positional arg is Case, not a custom message — keep it single-arg).
		ex.Message.ShouldContain("FailureBackoffFloorSeconds");
	}

	// SAFETY (partition-poll bound): F is ABOVE the processing poll (5s) but AT/BELOW the partition poll (20s).
	// A validator that checked ONLY the processing poll would PASS here — so the throw itself proves the
	// partition PollingInterval is enforced on the partitioned drain path (wording-independent, structural).
	[Fact]
	public async Task Throws_WhenFloorAtOrBelowPartitionPoll_EvenWhenAboveProcessingPoll()
	{
		using var provider = BuildProvider(
			floorSeconds: 10,
			processingPoll: TimeSpan.FromSeconds(5),
			partition: (OutboxPartitionStrategy.ByTenantHash, TimeSpan.FromSeconds(20)));

		// F(10) > processing(5) but F(10) <= partition(20): only the partition-poll branch can reject this.
		_ = await Should.ThrowAsync<OptionsValidationException>(() => Task.Run(() => Resolve(provider)));
	}

	// LIVENESS (no over-rejection, partitioning OFF): F strictly above the processing poll resolves cleanly.
	[Fact]
	public void DoesNotThrow_WhenFloorAboveProcessingPoll()
	{
		using var provider = BuildProvider(floorSeconds: 30, processingPoll: TimeSpan.FromSeconds(10));

		var options = Resolve(provider);
		options.Processing.FailureBackoffFloorSeconds.ShouldBe(30);
	}

	// LIVENESS (no over-rejection, partitioning ON): F strictly above BOTH poll intervals resolves cleanly.
	[Fact]
	public void DoesNotThrow_WhenFloorAboveEveryActivePoll()
	{
		using var provider = BuildProvider(
			floorSeconds: 30,
			processingPoll: TimeSpan.FromSeconds(5),
			partition: (OutboxPartitionStrategy.ByTenantHash, TimeSpan.FromSeconds(20)));

		var options = Resolve(provider);
		options.Processing.FailureBackoffFloorSeconds.ShouldBe(30);
	}
}

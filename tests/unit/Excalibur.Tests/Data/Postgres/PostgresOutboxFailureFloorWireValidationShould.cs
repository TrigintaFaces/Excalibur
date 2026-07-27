// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Partitioning;
using Excalibur.Outbox.Postgres;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Tests.Data.Postgres;

/// <summary>
/// lz7us9 — the Postgres 5th conformance arm at the WIRE level: proof that the cross-options floor-vs-poll
/// validator is actually REGISTERED and FIRES through the production DI path (S873 WIRE-lock discipline), not
/// merely that the validator class returns the right verdict in isolation (that is
/// <c>PostgresOutboxStoreOptionsValidatorShould</c>). A real <see cref="ServiceProvider"/> is built through
/// <c>AddExcaliburOutbox(...).UsePostgres(...)</c> and the validator is triggered exactly as a host triggers
/// it — by materializing <c>IOptions&lt;PostgresOutboxStoreOptions&gt;.Value</c> (<c>ValidateOnStart</c>).
/// </summary>
/// <remarks>
/// NON-VACUOUS + wording-robust. The partition-poll arm sets the floor ABOVE the processing poll but at/below
/// the partition poll: a validator checking only the processing poll would resolve cleanly, so the throw can
/// only originate from the partition-poll branch — the throw itself proves partition enforcement, independent
/// of the failure message text. The liveness controls prove the wired validator does not over-reject. RED if
/// the validator registration or its <c>ValidateOnStart</c> wiring is ever dropped.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxFailureFloorWireValidationShould
{
	private const string TestConnectionString = "Host=localhost;Database=test;Username=test;Password=test";

	private static ServiceProvider BuildProvider(
		int floorSeconds,
		TimeSpan processingPoll,
		(OutboxPartitionStrategy Strategy, TimeSpan Poll)? partition = null)
	{
		var services = new ServiceCollection();

		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.WithProcessing(p => p.PollingInterval(processingPoll));
			_ = outbox.UsePostgres(pg => pg.ConnectionString(TestConnectionString));

			if (partition is { } part)
			{
				_ = outbox.UsePartitionedProcessing(p =>
				{
					p.Strategy = part.Strategy;
					p.PollingInterval = part.Poll;
				});
			}
		});

		// The failure-backoff floor is an advanced option not surfaced on the fluent builder; set it through the
		// real options pipeline via PostConfigure (runs before validation) so the wired validator reads it.
		_ = services.PostConfigure<PostgresOutboxStoreOptions>(o => o.FailureBackoffFloorSeconds = floorSeconds);

		return services.BuildServiceProvider();
	}

	// Materializing .Value runs the registered IValidateOptions<PostgresOutboxStoreOptions> (ValidateOnStart wiring).
	private static PostgresOutboxStoreOptions Resolve(ServiceProvider provider) =>
		provider.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>().Value;

	// SAFETY (processing-poll bound, partitioning OFF): F <= processing poll => ValidateOnStart throws.
	[Fact]
	public async Task Throws_WhenFloorAtOrBelowProcessingPoll()
	{
		using var provider = BuildProvider(floorSeconds: 5, processingPoll: TimeSpan.FromSeconds(10));

		var ex = await Should.ThrowAsync<OptionsValidationException>(() => Task.Run(() => Resolve(provider)));
		ex.Message.ShouldContain("FailureBackoffFloorSeconds");
	}

	// SAFETY (partition-poll bound): F ABOVE the processing poll (5s) but AT/BELOW the partition poll (20s).
	// Only the partition-poll branch can reject this — the throw proves the partition interval is wired-in.
	[Fact]
	public async Task Throws_WhenFloorAtOrBelowPartitionPoll_EvenWhenAboveProcessingPoll()
	{
		using var provider = BuildProvider(
			floorSeconds: 10,
			processingPoll: TimeSpan.FromSeconds(5),
			partition: (OutboxPartitionStrategy.ByTenantHash, TimeSpan.FromSeconds(20)));

		_ = await Should.ThrowAsync<OptionsValidationException>(() => Task.Run(() => Resolve(provider)));
	}

	// LIVENESS (no over-rejection): floor strictly above every active poll resolves cleanly.
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

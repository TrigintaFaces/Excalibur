// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Binds the startup precondition for async projection processing: a host that opts in without a stream
/// to poll stops at startup rather than running healthy and processing nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was wrong.</b> The processing host is a background service that logged a warning and returned
/// when no <see cref="IGlobalStreamQuery"/> was registered. A background service that returns is
/// indistinguishable from one that is idle, so the application started, reported healthy, and processed no
/// projection for the rest of its life. One warning line at startup was the only signal.
/// </para>
/// <para>
/// The sibling precondition on projection REBUILD already threw for exactly this, on exactly this
/// argument. These arms bind the same behaviour for the catch-up path.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ProjectionProcessingPrerequisiteShould
{
	/// <summary>
	/// SAFETY: the silent-no-op configuration is refused at startup, and the message names the missing
	/// service so the consumer knows what to register.
	/// </summary>
	[Fact]
	public void Refuse_a_host_that_enabled_projection_processing_with_no_stream_to_poll()
	{
		using var provider = HostWith(globalStreamQuery: false);

		var thrown = Should.Throw<InvalidOperationException>(() => provider.ValidateStartupGates());

		thrown.Message.Contains("IGlobalStreamQuery", StringComparison.Ordinal).ShouldBeTrue(
			"the consumer cannot act on a failure that does not name the service it must register.");
	}

	/// <summary>
	/// LIVENESS: a correctly configured host still starts.
	/// </summary>
	/// <remarks>
	/// The arm that keeps the safety arm above from being satisfied by a validator that refuses
	/// everything. Without it, a guard that threw unconditionally would pass the suite.
	/// </remarks>
	[Fact]
	public void Start_a_host_that_has_a_global_stream_query()
	{
		using var provider = HostWith();

		Should.NotThrow(() => provider.ValidateStartupGates());
	}

	/// <summary>
	/// LIVENESS: the guard is silent on a host that never opted in, so adding it cannot break an unrelated
	/// composition.
	/// </summary>
	[Fact]
	public void Stay_silent_on_a_host_that_never_enabled_projection_processing()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => provider.ValidateStartupGates());
	}

	/// <summary>
	/// SAFETY: a host that never said whether its checkpoints survive a restart is refused, and the
	/// message names the FAILURE rather than the policy.
	/// </summary>
	/// <remarks>
	/// Replaying an async projection is not idempotent and the framework cannot tell a safe handler from
	/// an unsafe one, so the decision is forced. A consumer who silently inherited the in-memory fallback
	/// is by definition the one who never read the guidance.
	/// </remarks>
	[Fact]
	public void Refuse_a_host_that_never_stated_whether_its_checkpoints_survive_a_restart()
	{
		using var provider = HostWith(checkpointDecision: CheckpointDecision.None);

		var thrown = Should.Throw<InvalidOperationException>(() => provider.ValidateStartupGates());

		thrown.Message.Contains("re-applied from the beginning", StringComparison.Ordinal).ShouldBeTrue(
			"the message must name what goes wrong, not merely that a policy was violated.");
		thrown.Message.Contains("AllowInMemoryProjectionCheckpoints", StringComparison.Ordinal).ShouldBeTrue(
			"a consumer cannot act on a failure that does not name both remedies.");
	}

	/// <summary>
	/// LIVENESS: declaring a durable checkpoint store satisfies the guard.
	/// </summary>
	[Fact]
	public void Start_a_host_that_registered_a_durable_checkpoint_store()
	{
		using var provider = HostWith(checkpointDecision: CheckpointDecision.Durable);

		Should.NotThrow(() => provider.ValidateStartupGates());
	}

	/// <summary>
	/// LIVENESS: explicitly accepting process-lifetime checkpoints satisfies the guard.
	/// </summary>
	/// <remarks>
	/// The escape must actually work, or the guard is not a forced choice but a ban — and every test and
	/// in-process host would be stranded by it.
	/// </remarks>
	[Fact]
	public void Start_a_host_that_explicitly_accepted_in_memory_checkpoints()
	{
		using var provider = HostWith(checkpointDecision: CheckpointDecision.AcknowledgedInMemory);

		Should.NotThrow(() => provider.ValidateStartupGates());
	}

	private enum CheckpointDecision
	{
		None,
		Durable,
		AcknowledgedInMemory,
	}

	private static ServiceProvider HostWith(
		bool globalStreamQuery = true,
		CheckpointDecision checkpointDecision = CheckpointDecision.AcknowledgedInMemory)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		if (globalStreamQuery)
		{
			_ = services.AddSingleton<IGlobalStreamQuery, StubGlobalStreamQuery>();
		}

		switch (checkpointDecision)
		{
			case CheckpointDecision.Durable:
				// Stands in for a provider's Add*SubscriptionCheckpointStore, which emits the marker from
				// the same call that wires its store.
				_ = services.AddSingleton<ISubscriptionCheckpointDurability, StubCheckpointDurability>();
				break;
			case CheckpointDecision.AcknowledgedInMemory:
				_ = services.AllowInMemoryProjectionCheckpoints();
				break;
			default:
				break;
		}

		var builder = A.Fake<IEventSourcingBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		_ = builder.EnableProjectionProcessing();

		return services.BuildServiceProvider();
	}

	private sealed class StubCheckpointDurability : ISubscriptionCheckpointDurability;

	/// <summary>
	/// Present so the registration can be observed; it is never read by these arms.
	/// </summary>
	private sealed class StubGlobalStreamQuery : IGlobalStreamQuery
	{
		public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
			GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
			string eventType, GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(0L);
	}
}

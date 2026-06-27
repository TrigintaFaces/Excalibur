// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Conformance.LeaderElection;

namespace Excalibur.LeaderElection.Tests.InMemory;

/// <summary>
/// Conformance tests for <see cref="InMemoryLeaderElection"/> using the shared
/// <see cref="LeaderElectionConformanceTestBase"/> contract kit.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 851 / <c>qxatfw</c>: this is the FIRST concrete deriver of
/// <see cref="LeaderElectionConformanceTestBase"/> — before it, the base's 16 contract facts had ZERO
/// derivers and executed against nothing (dead-contract / false confidence). Wiring the in-memory
/// provider makes the leadership/contention invariants actually run in unit CI. It is prioritized first
/// because the leader-election subsystem has an open split-brain defect (<c>zg4zga</c>): the contract
/// encodes the very single-leader invariant being violated.
/// </para>
/// <para>
/// Each test-class instance gets a FRESH <see cref="InMemoryLeaderElectionSharedState"/> and a unique
/// resource name so xUnit's per-method instances are isolated, while the primary and competing elections
/// within one test share that state + resource (the only way they can actually contend). A short
/// <see cref="LeaderElectionOptions.RenewInterval"/> makes re-acquisition after a leader stop fast and
/// deterministic within the base's event timeout.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class InMemoryLeaderElectionConformanceShould : LeaderElectionConformanceTestBase
{
	private readonly InMemoryLeaderElectionSharedState _sharedState = new();
	private readonly string _resourceName = $"conformance-{Guid.NewGuid():N}";

	/// <inheritdoc/>
	protected override Task<ILeaderElection> CreateElectionAsync() => Task.FromResult(CreateElection());

	/// <inheritdoc/>
	protected override Task<ILeaderElection> CreateCompetingElectionAsync() => Task.FromResult(CreateElection());

	/// <inheritdoc/>
	protected override Task CleanupAsync()
	{
		// Each ILeaderElection instance is disposed by the base (Election) / the contention tests
		// (competitors); the shared state is per-instance and is garbage-collected with this class.
		return Task.CompletedTask;
	}

	private ILeaderElection CreateElection()
	{
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = $"candidate-{Guid.NewGuid():N}",
			// Fast renewal so a competitor re-acquires promptly after the current leader stops,
			// keeping the contention/leader-change facts well inside the base EventTimeout.
			RenewInterval = TimeSpan.FromMilliseconds(100),
			EnableHealthChecks = false,
		});

		return new InMemoryLeaderElection(_resourceName, options, logger: null, _sharedState);
	}
}

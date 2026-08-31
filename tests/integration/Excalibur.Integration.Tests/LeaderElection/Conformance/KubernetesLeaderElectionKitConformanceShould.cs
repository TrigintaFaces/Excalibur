// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Integration.Tests.LeaderElection;
using Excalibur.LeaderElection.Kubernetes;
using Excalibur.Testing.Conformance;

using k8s;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.LeaderElection.Conformance;

/// <summary>
/// Binds the SHIPPED <see cref="LeaderElectionConformanceTestKit"/> to <see cref="KubernetesLeaderElection"/>,
/// constructed directly against a real k3s API server -- the same pattern the Redis sibling deriver uses.
/// </summary>
/// <remarks>
/// <para>
/// kbjd0b: <see cref="KubernetesLeaderElection"/> declares <see cref="IHealthBasedLeaderElection"/>
/// directly -- there is no separate plain/health-based split the way Postgres and SQL Server have one --
/// so a single deriver closes the gap for this provider. Until this class existed, Kubernetes had only a
/// mocked unit suite (<c>tests/unit/Excalibur.LeaderElection.Tests/Kubernetes/</c>): mutual exclusion is
/// arbitrated by the real API server's <c>coordination.k8s.io</c> Lease resource, which no in-process
/// substitute -- mocked or otherwise -- can establish.
/// </para>
/// <para>
/// WHY DIRECT CONSTRUCTION, NOT THE DI-REGISTERED FACTORY. <c>AddExcaliburKubernetesLeaderElection</c>
/// registers <see cref="ILeaderElectionFactory"/> behind a telemetry decorator shared by every provider, so
/// resolving through it exercises the same generic seam regardless of which backend is behind it -- it
/// proves nothing specific to this type. Constructing <see cref="KubernetesLeaderElection"/> directly (its
/// public constructor takes only <see cref="IKubernetes"/>, the resource name, and options) is what
/// actually binds this suite to this concrete implementation, and it is the pattern every other
/// hand-constructible provider in this file uses (see the Redis sibling deriver).
/// </para>
/// <para>
/// Leaving <c>LeaseName</c> unset on the options lets <see cref="KubernetesLeaderElection"/> derive the
/// lease from the resource name itself (<c>"{resourceName}-leader-election"</c>), which is what makes
/// candidates on the SAME resource name contend on the SAME lease while candidates on different resource
/// names never collide.
/// </para>
/// </remarks>
[Collection(KubernetesLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "Kubernetes")]
public sealed class KubernetesLeaderElectionKitConformanceShould
	: LeaderElectionConformanceTestKit, IAsyncLifetime
{
	private readonly KubernetesContainerFixture _fixture;
	private readonly List<ILeaderElection> _created = [];
	private IKubernetes? _client;

	public KubernetesLeaderElectionKitConformanceShould(KubernetesContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// Builds the real <see cref="IKubernetes"/> client once, up front. <see cref="CreateElection"/> is a
	/// synchronous override the kit calls from every arm; fetching the kubeconfig and building the client
	/// there would mean sync-over-async on every call for no benefit, since the client is safe to share
	/// across elections -- production hosts register exactly one, too.
	/// </summary>
	public async ValueTask InitializeAsync() => _client = await _fixture.CreateClientAsync().ConfigureAwait(false);

	/// <summary>
	/// Disposes every election this arm constructed.
	/// </summary>
	/// <remarks>
	/// The kit's own teardown is <c>(election as IDisposable)?.Dispose()</c>, and
	/// <see cref="KubernetesLeaderElection"/> implements only <see cref="IAsyncDisposable"/>, so that cast
	/// yields null and the kit disposes nothing. Left alone each arm strands a live Lease-renewal timer.
	/// </remarks>
	public async ValueTask DisposeAsync()
	{
		foreach (var election in _created)
		{
			try
			{
				await election.DisposeAsync().ConfigureAwait(false);
			}
			catch (ObjectDisposedException)
			{
				// Already disposed by an arm that owns its own teardown.
			}
		}

		_created.Clear();
	}

	/// <inheritdoc/>
	protected override ILeaderElection CreateElection(string resourceName, string? candidateId)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"LeaderElectionConformanceTestKit arms against KubernetesLeaderElection must run against a "
			+ "real k3s API server -- never skipped. " + (_fixture.InitializationError ?? "k3s container required."));

		var electionOptions = Options.Create(new KubernetesLeaderElectionOptions
		{
			Namespace = "default",
			CandidateId = candidateId ?? GenerateCandidateId()[..24],

			// A fresh cluster's coordination API answers well within a second; these values keep the
			// renewal loop and the safety-net self-demotion margin comfortably inside the kit's default
			// event timeout without depending on it.
			LeaseDuration = TimeSpan.FromSeconds(15),
			RenewInterval = TimeSpan.FromSeconds(1),
			RetryInterval = TimeSpan.FromMilliseconds(250),
			EnableHealthChecks = false,
		});

		var election = new KubernetesLeaderElection(
			_client ?? throw new InvalidOperationException($"{nameof(InitializeAsync)} must run before {nameof(CreateElection)}."),
			// Verbatim, matching the Redis sibling: the kit already draws a fresh GUID-suffixed name per
			// arm, so prefixing would add no isolation and would hide which lease an arm actually took.
			resourceName,
			electionOptions,
			NullLogger<KubernetesLeaderElection>.Instance);

		_created.Add(election);
		return election;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Every arm draws a fresh <see cref="LeaderElectionConformanceTestKit.GenerateResourceName"/>, which
	/// becomes a distinct Lease resource, so there is nothing shared across arms for a reset to remove.
	/// </remarks>
	protected override Task CleanupAsync() => Task.CompletedTask;

	[Fact]
	public Task StartAsync_ShouldInitiateParticipation_Test() => StartAsync_ShouldInitiateParticipation();

	[Fact]
	public Task StopAsync_ShouldRelinquishLeadership_Test() => StopAsync_ShouldRelinquishLeadership();

	[Fact]
	public Task StartAsync_AfterStop_ShouldRestartElection_Test() => StartAsync_AfterStop_ShouldRestartElection();

	[Fact]
	public Task StartAsync_SingleCandidate_ShouldBecomeLeader_Test() => StartAsync_SingleCandidate_ShouldBecomeLeader();

	[Fact]
	public Task StartAsync_SingleCandidate_IsLeaderShouldBeTrue_Test() => StartAsync_SingleCandidate_IsLeaderShouldBeTrue();

	[Fact]
	public Task StartAsync_SingleCandidate_CurrentLeaderIdShouldMatchCandidateId_Test() => StartAsync_SingleCandidate_CurrentLeaderIdShouldMatchCandidateId();

	[Fact]
	public Task MultipleCandidate_OnlyOneBecomesLeader_Test() => MultipleCandidate_OnlyOneBecomesLeader();

	[Fact]
	public Task MultipleCandidate_ReportedLeaderIdShouldNameTheLeader_Test() => MultipleCandidate_ReportedLeaderIdShouldNameTheLeader();

	[Fact]
	public Task MultipleCandidate_IncumbentShouldExcludeLaterCandidate_Test() => MultipleCandidate_IncumbentShouldExcludeLaterCandidate();

	[Fact]
	public Task ConcurrentContention_ExactlyOneLeader_Test() => ConcurrentContention_ExactlyOneLeader();

	[Fact]
	public Task BecameLeader_ShouldFireWhenElected_Test() => BecameLeader_ShouldFireWhenElected();

	[Fact]
	public Task LostLeadership_ShouldFireWhenStopped_Test() => LostLeadership_ShouldFireWhenStopped();

	[Fact]
	public Task LeaderChanged_ShouldFireOnLeadershipChange_Test() => LeaderChanged_ShouldFireOnLeadershipChange();

	[Fact]
	public Task CandidateId_ShouldBeUniquePerInstance_Test() => CandidateId_ShouldBeUniquePerInstance();

	[Fact]
	public Task CurrentLeadership_AfterStop_ShouldBeNull_Test() => CurrentLeadership_AfterStop_ShouldBeNull();

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}

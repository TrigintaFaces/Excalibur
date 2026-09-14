// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Kubernetes;

using k8s;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Real-k3s concurrent-atomicity coverage for lb5ckv/pc9azv's Kubernetes leg, against the actual shape of
/// <see cref="KubernetesFencingTokenProvider"/> rather than the N-concurrent-mint-&gt;N-distinct shape the
/// self-minting providers (Postgres/Consul/MongoDB/Redis/SqlServer) use.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not the self-minting test shape.</b> <see cref="KubernetesFencingTokenProvider"/> does not mint a
/// token — it <em>reads</em> the Lease's own <c>spec.leaseTransitions</c> counter, which
/// <see cref="KubernetesLeaderElection"/> advances only on a genuine leadership transition (a successful
/// CAS write of the Lease's holder identity). Racing N concurrent <c>IssueTokenAsync</c> calls against a
/// STABLE lease — the shape every other provider's concurrent-atomicity lock uses — would all read the
/// SAME value here, because nothing is incrementing between reads; that is not a coverage gap, it is a
/// structural difference in what "atomic mint" means for a read-native-counter provider. This was
/// discovered and recorded on pc9azv (2026-09-07) before this file existed.
/// </para>
/// <para>
/// The real atomicity question for THIS provider is therefore two-sided, and both arms are covered here
/// against a real k3s API server (never skipped -- <see cref="ContainerFixtureBase.DockerAvailable"/> is
/// asserted true in every fact):
/// </para>
/// <list type="number">
/// <item>Racing concurrent <b>acquisitions</b> (not token reads) for the same Lease must yield exactly one
/// winner, and that winner's fencing token must be internally consistent with the Lease's own transition
/// count -- proving the "mint" (a transition) and the "read" (the token) never disagree.</item>
/// <item>Concurrent <b>reads</b> of a token while only ONE genuine transition has occurred must all
/// observe the SAME value -- proving a read never spuriously advances the counter it is only supposed to
/// observe (the inverse of the self-minting providers' "N distinct" proof: here, N reads of one real
/// transition must NOT be N distinct values).</item>
/// </list>
/// <para>
/// <b>RED-on-mutant:</b> if <see cref="KubernetesLeaderElection"/>'s CAS-guarded Lease write were replaced
/// by a non-atomic read-then-write, <see cref="ConcurrentAcquisition_YieldsExactlyOneWinner_WithAConsistentFencingToken"/>
/// would observe more than one candidate believing it holds leadership (the same mutant the shared
/// conformance kit's <c>ConcurrentContention_ExactlyOneLeader</c> arm already guards against).
/// </para>
/// </remarks>
[Collection(KubernetesLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "Kubernetes")]
public sealed class KubernetesFencingConcurrentAcquisitionShould
{
	private readonly KubernetesContainerFixture _fixture;

	public KubernetesFencingConcurrentAcquisitionShould(KubernetesContainerFixture fixture) => _fixture = fixture;

	private static string UniqueResourceName() => $"fencing-concurrency-{Guid.NewGuid():N}";

	private KubernetesLeaderElection CreateElection(IKubernetes client, string resourceName, string candidateId) =>
		new(
			client,
			resourceName,
			Options.Create(new KubernetesLeaderElectionOptions
			{
				Namespace = "default",
				CandidateId = candidateId,
				LeaseDuration = TimeSpan.FromSeconds(15),
				RenewInterval = TimeSpan.FromSeconds(1),
				RetryInterval = TimeSpan.FromMilliseconds(250),
				EnableHealthChecks = false,
			}),
			NullLogger<KubernetesLeaderElection>.Instance);

	[Fact]
	public async Task ConcurrentAcquisition_YieldsExactlyOneWinner_WithAConsistentFencingToken()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"lb5ckv/pc9azv Kubernetes concurrent-atomicity coverage is a split-brain safety control -- this real-k3s lock must never be skipped");

		var client = await _fixture.CreateClientAsync();
		var resourceName = UniqueResourceName();
		const int concurrency = 8;

		var elections = Enumerable.Range(0, concurrency)
			.Select(i => CreateElection(client, resourceName, $"candidate-{i}"))
			.ToList();

		try
		{
			// Act -- every candidate starts (and therefore attempts to acquire) concurrently.
			await Task.WhenAll(elections.Select(e => e.StartAsync(TestContext.Current.CancellationToken)));

			// Give the losers' retry loop one interval to settle rather than racing the assertion against
			// the first CAS round.
			await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

			// Assert -- SAFETY: exactly one winner, the same guarantee ConcurrentContention_ExactlyOneLeader
			// proves generically, restated here because the fencing-token assertion below depends on it.
			var leaders = elections.Where(e => e.IsLeader).ToList();
			leaders.Count.ShouldBe(1, "the real k3s API server's Lease CAS must admit exactly one winner among concurrent candidates");

			// Assert -- the winner's own fencing token (read via the SAME provider the auto-registered DI
			// path constructs) must be present and >= 1: a genuine transition happened, and the provider
			// observed it.
			var fencingProvider = CreateFencingProvider(client, out var fencingServices);
			await using var _fencingServices = fencingServices;
			var token = await fencingProvider.GetTokenAsync(resourceName, TestContext.Current.CancellationToken);
			token.ShouldNotBeNull("a real leadership transition occurred, so the Lease's leaseTransitions counter must be observable");
			token!.Value.ShouldBeGreaterThanOrEqualTo(1L);

			leaders[0].CurrentLeadership.ShouldNotBeNull();
			leaders[0].CurrentLeadership!.Value.FencingToken.ShouldBe(token,
				"the winning election's own tenure token must agree with the provider read independently against the same Lease");
		}
		finally
		{
			await Task.WhenAll(elections.Select(async e =>
			{
				try
				{
					await e.DisposeAsync();
				}
				catch (ObjectDisposedException)
				{
				}
			}));
		}
	}

	[Fact]
	public async Task ConcurrentReads_NeverAdvanceTheCounter_ForOneGenuineTransition()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"lb5ckv/pc9azv Kubernetes concurrent-atomicity coverage is a split-brain safety control -- this real-k3s lock must never be skipped");

		var client = await _fixture.CreateClientAsync();
		var resourceName = UniqueResourceName();

		var election = CreateElection(client, resourceName, "sole-candidate");
		try
		{
			await election.StartAsync(TestContext.Current.CancellationToken);
			await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
			election.IsLeader.ShouldBeTrue("the sole candidate must acquire leadership -- one real transition");

			var fencingProvider = CreateFencingProvider(client, out var fencingServices);
			await using var _fencingServices = fencingServices;

			// Act -- N concurrent reads against a Lease with exactly ONE genuine transition behind it.
			const int concurrency = 16;
			var tokens = await Task.WhenAll(Enumerable.Range(0, concurrency)
				.Select(_ => fencingProvider.GetTokenAsync(resourceName, TestContext.Current.CancellationToken).AsTask()));

			// Assert -- ATOMICITY, the read-native-counter shape: every read observes the SAME value. A
			// provider that mutated state on read (or misread a torn value) would show >1 distinct token
			// here despite zero additional transitions.
			var distinct = tokens.Distinct().ToList();
			distinct.Count.ShouldBe(1,
				$"reading a fencing token must never itself advance the counter -- got {distinct.Count} distinct values " +
				$"({string.Join(", ", distinct)}) from {concurrency} concurrent reads against one real transition");
			distinct[0].ShouldNotBeNull();
		}
		finally
		{
			await election.DisposeAsync();
		}
	}

	[Fact]
	public async Task SequentialHandover_YieldsAStrictlyGreaterFencingToken()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"lb5ckv/pc9azv Kubernetes concurrent-atomicity coverage is a split-brain safety control -- this real-k3s lock must never be skipped");

		var client = await _fixture.CreateClientAsync();
		var resourceName = UniqueResourceName();
		var fencingProvider = CreateFencingProvider(client, out var fencingServices);
		await using var _fencingServices = fencingServices;

		var electionA = CreateElection(client, resourceName, "candidate-a");
		await electionA.StartAsync(TestContext.Current.CancellationToken);
		await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
		electionA.IsLeader.ShouldBeTrue();
		var firstToken = await fencingProvider.GetTokenAsync(resourceName, TestContext.Current.CancellationToken);
		firstToken.ShouldNotBeNull();
		await electionA.StopAsync(TestContext.Current.CancellationToken);
		await electionA.DisposeAsync();

		var electionB = CreateElection(client, resourceName, "candidate-b");
		try
		{
			await electionB.StartAsync(TestContext.Current.CancellationToken);
			await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
			electionB.IsLeader.ShouldBeTrue("the successor must acquire the now-vacant Lease");

			var secondToken = await fencingProvider.GetTokenAsync(resourceName, TestContext.Current.CancellationToken);
			secondToken.ShouldNotBeNull();
			secondToken!.Value.ShouldBeGreaterThan(firstToken!.Value,
				"a genuine handover is a second real transition, so leaseTransitions -- and therefore the fencing token -- must strictly increase");
		}
		finally
		{
			await electionB.DisposeAsync();
		}
	}

	/// <summary>
	/// Builds <see cref="IFencingTokenProvider"/> through the PUBLIC registration path
	/// (<c>AddKubernetesFencingTokenProvider()</c>) rather than constructing the internal
	/// <c>KubernetesFencingTokenProvider</c> type directly -- this test project is not granted
	/// <c>InternalsVisibleTo</c> by <c>Excalibur.LeaderElection.Kubernetes</c>, and going through the same
	/// public seam a consumer uses is the more faithful proof anyway.
	/// </summary>
	private static IFencingTokenProvider CreateFencingProvider(IKubernetes client, out ServiceProvider services)
	{
		var collection = new ServiceCollection();
		_ = collection.AddSingleton(client);
		_ = collection.AddSingleton(Options.Create(new KubernetesLeaderElectionOptions { Namespace = "default" }));
		_ = collection.AddKubernetesFencingTokenProvider();
		services = collection.BuildServiceProvider();
		return services.GetRequiredService<IFencingTokenProvider>();
	}
}

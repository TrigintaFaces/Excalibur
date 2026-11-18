// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Kubernetes;

using k8s;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.LeaderElection;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Runs the leader-election conformance kit against the Kubernetes provider on a real API server.
/// </summary>
/// <remarks>
/// <para>
/// THIS SEAM HAS PRIOR SPLIT-BRAIN HISTORY, which is why it is the one of the three worth running first.
/// The loser of a Lease compare-and-swap race once derived its leadership from its own in-memory copy of the
/// Lease, whose holder identity it had already overwritten with its own — so every loser read its own
/// identity back and concluded it had won. The kit's mutual-exclusion arm is what detects that class, and
/// until now no arm of it ran against this provider.
/// </para>
/// <para>
/// Candidates share one exact resource/Lease so they genuinely contend. Distinct display IDs retain
/// the original workload; private per-tenure nonces additionally distinguish same-display instances.
/// </para>
/// <para>
/// ONE CLIENT SHARED ACROSS CANDIDATES IS CORRECT HERE, unlike the Consul deriver. Leadership is arbitrated
/// by the holder identity written into the Lease and by the API server's own resourceVersion precondition,
/// not by anything the client owns, so candidates sharing a client still contend genuinely. A client per
/// candidate would cost connections and prove nothing extra.
/// </para>
/// <para>
/// Teardown deletes the Lease. The k3s container is shared across this collection, so a Lease left behind
/// outlives the arm that made it and becomes state on the namespace that every later arm can read — and
/// unlike the fencing arms, these use one resource name per class instance rather than per operation, so a
/// takeover arm reading a predecessor's leftover Lease is a real way to pass for the wrong reason.
/// </para>
/// </remarks>
[Collection(KubernetesLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "Kubernetes")]
public sealed class KubernetesLeaderElectionConformanceShould : LeaderElectionConformanceTestBase, IAsyncLifetime
{
	private const string Namespace = "default";

	private readonly KubernetesContainerFixture _fixture;

	/// <summary>Shared by the primary and every competitor; randomised per test-class instance.</summary>
	private readonly string _resourceName = $"le-conformance-{Guid.NewGuid():N}";

	private IKubernetes? _client;
    private readonly KubernetesElectionTestLifetime _ownership = new();

	public KubernetesLeaderElectionConformanceShould(KubernetesContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	/// <remarks>
	/// A takeover waits on the predecessor's Lease release and on the successor's next retry against a real
	/// API server. The base polls to this deadline rather than sleeping.
	/// </remarks>
	protected override TimeSpan EventTimeout => TimeSpan.FromSeconds(60);

	/// <inheritdoc/>
	protected override async Task<ILeaderElection> CreateElectionAsync() =>
		await CreateCandidateAsync().ConfigureAwait(false);

	/// <inheritdoc/>
	protected override async Task<ILeaderElection> CreateCompetingElectionAsync() =>
		await CreateCandidateAsync().ConfigureAwait(false);

	/// <inheritdoc/>
    protected override Task CleanupAsync() => _ownership.DisposeAsync().AsTask();

    // The shared base otherwise waits for its primary before initiating competitor retirement.
    public new ValueTask DisposeAsync() => _ownership.DisposeAsync();

	private async Task<ILeaderElection> CreateCandidateAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"mutual exclusion is the guarantee whose violation is split brain, so these arms must never be "
			+ "skipped for want of a k3s API server");

		_client ??= _ownership.OwnClient(await _fixture.CreateClientAsync().ConfigureAwait(false));

		return _ownership.Track(new KubernetesLeaderElection(
			_client,
			_resourceName,
			Options.Create(new KubernetesLeaderElectionOptions
			{
				Namespace = Namespace,
				InstanceId = $"cand-{Guid.NewGuid():N}"[..24],
				LeaseDuration = TimeSpan.FromSeconds(5),
				RenewInterval = TimeSpan.FromSeconds(1),
                GracePeriod = TimeSpan.FromSeconds(1),
				RetryInterval = TimeSpan.FromMilliseconds(250),
				EnableHealthChecks = false,
			}),
			NullLogger<KubernetesLeaderElection>.Instance), _client, $"{_resourceName}-leader-election", Namespace);
	}
}

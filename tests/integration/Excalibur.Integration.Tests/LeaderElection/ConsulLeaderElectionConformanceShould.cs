// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Consul;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Consul;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.LeaderElection;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Runs the leader-election conformance kit against the Consul provider on a real agent.
/// </summary>
/// <remarks>
/// <para>
/// Consul arbitrates leadership by acquiring a KV key against a session, so the candidates MUST share the
/// resource name and the key prefix: candidates on different keys never contend, and the mutual-exclusion
/// arm then passes without racing. Both are single fields read by both factory methods. Each candidate MUST
/// also have a different instance id, or the arm contends with itself.
/// </para>
/// <para>
/// ONE CONSUL CLIENT PER CANDIDATE, which is the detail that makes the contention real. A session belongs to
/// the client that created it, so candidates sharing a client would share the session the lock is held
/// against — they would stop being independent holders and the arm would be measuring one participant. The
/// clients are disposed in teardown.
/// </para>
/// </remarks>
[Collection(ConsulLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "Consul")]
public sealed class ConsulLeaderElectionConformanceShould : LeaderElectionConformanceTestBase
{
	private readonly ConsulContainerFixture _fixture;
	private readonly string _resourceName = $"le-conformance-{Guid.NewGuid():N}";
	private readonly string _keyPrefix = $"le-conformance/{Guid.NewGuid():N}";
	private readonly List<IConsulClient> _clients = [];

	public ConsulLeaderElectionConformanceShould(ConsulContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	/// <remarks>
	/// Consul's session TTL has a documented floor of ten seconds, so a takeover that waits on session
	/// expiry rather than on an explicit release cannot complete inside the base's default deadline. The
	/// base polls rather than sleeps, so a release that is immediate costs nothing.
	/// </remarks>
	protected override TimeSpan EventTimeout => TimeSpan.FromSeconds(60);

	/// <inheritdoc/>
	protected override Task<ILeaderElection> CreateElectionAsync() => Task.FromResult(CreateCandidate());

	/// <inheritdoc/>
	protected override Task<ILeaderElection> CreateCompetingElectionAsync() =>
		Task.FromResult(CreateCandidate());

	/// <inheritdoc/>
	protected override Task CleanupAsync()
	{
		foreach (var client in _clients)
		{
			client.Dispose();
		}

		_clients.Clear();
		return Task.CompletedTask;
	}

	private ILeaderElection CreateCandidate()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"mutual exclusion is the guarantee whose violation is split brain, so these arms must never be "
			+ "skipped for want of a container");

		var client = new ConsulClient(config => config.Address = new Uri(_fixture.ConsulAddress));
		_clients.Add(client);

		return new ConsulLeaderElection(
			_resourceName,
			Options.Create(new ConsulLeaderElectionOptions
			{
				KeyPrefix = _keyPrefix,
				InstanceId = $"cand-{Guid.NewGuid():N}"[..24],
			}),
			client,
			NullLogger<ConsulLeaderElection>.Instance);
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.LeaderElection.Kubernetes;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.TimeProviderHonoured;

/// <summary>
/// Binds the Kubernetes provider's health timestamp to the injected time provider.
/// </summary>
/// <remarks>
/// <para>
/// This needs no cluster. The provider takes <see cref="IKubernetes"/> through its constructor, so a fake
/// carries a candidate all the way through acquisition: the fake server preserves native UID/resourceVersion and the private owner across reads and
/// conditional writes, and the candidate becomes leader — which is what opens the
/// health path, since that path is reached only while this candidate holds the lease.
/// </para>
/// <para>
/// The assertion is made on the annotation written into the <see cref="V1Lease"/> handed to the client,
/// because that annotation is what a second node and an operator inspecting the lease actually read.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KubernetesLeaderElectionHonoursTimeProviderShould
{
	private const string Candidate = "candidate-a";
	private const string LeaseName = "orders-leader-election";

	private readonly IKubernetes _client = A.Fake<IKubernetes>();
	private readonly ICoordinationV1Operations _coordination = A.Fake<ICoordinationV1Operations>();
    private readonly List<V1Lease> _written = [];
    private readonly Lock _serverGate = new();
    private V1Lease _record = Acquirable();
    private int _version = 1;

	public KubernetesLeaderElectionHonoursTimeProviderShould() =>
		A.CallTo(() => _client.CoordinationV1).Returns(_coordination);

	/// <summary>An unheld, long-expired lease: this candidate sees it as acquirable.</summary>
	private static V1Lease Acquirable() => new()
	{
		Metadata = new V1ObjectMeta
        {
            Name = LeaseName, NamespaceProperty = "default", Uid = "timestamp-test-uid", ResourceVersion = "1",
            Annotations = new Dictionary<string, string>(StringComparer.Ordinal),
        },
		Spec = new V1LeaseSpec
		{
			HolderIdentity = null,
			LeaseDurationSeconds = 15,
			RenewTime = DateTime.UtcNow.AddHours(-1),
			LeaseTransitions = 1,
		},
	};

	private void ArrangeAcquirableLease()
	{
		_ = A.CallTo(() => _coordination.ReadNamespacedLeaseWithHttpMessagesAsync(
				A<string>._, A<string>._, A<bool?>._,
				A<IReadOnlyDictionary<string, IReadOnlyList<string>>>._, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult(new HttpOperationResponse<V1Lease> { Body = Snapshot() }));

        // Model actual native UID/resourceVersion CAS and return separate immutable snapshots.
        _ = A.CallTo(() => _coordination.ReplaceNamespacedLeaseWithHttpMessagesAsync(
                A<V1Lease>._, A<string>._, A<string>._, A<string>._, A<string>._, A<string>._, A<bool?>._,
                A<IReadOnlyDictionary<string, IReadOnlyList<string>>>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                var requested = (V1Lease)call.Arguments[0]!;
                lock (_serverGate)
                {
                    if (requested.Metadata.Uid != _record.Metadata.Uid || requested.Metadata.ResourceVersion != _record.Metadata.ResourceVersion)
                    {
                        throw new HttpOperationException("native CAS conflict")
                        {
                            Response = new HttpResponseMessageWrapper(new HttpResponseMessage(System.Net.HttpStatusCode.Conflict), string.Empty),
                        };
                    }
                    _record = KubernetesLeaseProtocol.Clone(requested);
                    _record.Metadata.ResourceVersion = (++_version).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    _written.Add(KubernetesLeaseProtocol.Clone(_record));
                    return Task.FromResult(new HttpOperationResponse<V1Lease> { Body = KubernetesLeaseProtocol.Clone(_record) });
                }
            });
    }

    private V1Lease Snapshot()
    {
        lock (_serverGate) { return KubernetesLeaseProtocol.Clone(_record); }
    }

	private KubernetesLeaderElection CreateSut(TimeProvider? clock) =>
		new(
			_client,
			"orders",
			Options.Create(new KubernetesLeaderElectionOptions
			{
				LeaseName = LeaseName,
				Namespace = "default",
				InstanceId = Candidate,

				// Far longer than the arm, so the background loop makes a single pass and no renewal
				// timer fires: every write this arm reads is one it provoked.
                LeaseDuration = TimeSpan.FromHours(2),
                GracePeriod = TimeSpan.FromSeconds(5),
				RenewInterval = TimeSpan.FromHours(1),
				RetryInterval = TimeSpan.FromHours(1),
			}),
			logger: null,
			timeProvider: clock);

	/// <summary>
	/// Waits for the background election loop's first pass, without a fixed sleep.
	/// </summary>
	private static async Task WaitUntilLeaderAsync(KubernetesLeaderElection sut)
	{
        // Poll the actual public publication using a bounded real-time controller. The injected clock
        // remains unchanged until the test's explicit23-minute advance.
		for (var attempt = 0; attempt < 100 && !sut.IsLeader; attempt++)
		{
			await Task.Delay(20); // delay-ok: poll pacing over 100 attempts; the loop exits on sut.IsLeader, and the test waits for the actual published acquisition without advancing its injected clock
		}

		sut.IsLeader.ShouldBeTrue(
			"arrangement failed: the candidate never acquired the lease, so the health path below is never "
			+ "entered and the arm would prove nothing.");
	}

	/// <summary>
	/// Finds the health instant in whichever write carries this candidate's health annotation.
	/// </summary>
	/// <remarks>
	/// It searches rather than taking the most recent write: the background renewal path writes the lease
	/// too, so "the last write" is not reliably the health write, and an arm that assumed it would fail
	/// for a reason unrelated to the timestamp it is testing. The candidate id is read from the election
	/// rather than assumed, because the provider derives it (pod name, hostname, then the configured
	/// instance id) instead of using the configured value verbatim.
	/// </remarks>
	private DateTimeOffset HealthInstantWrittenFor(KubernetesLeaderElection sut)
	{
		_written.ShouldNotBeEmpty("no lease was written, so there is no annotation to read.");

		var key = $"leader-election.excalibur.io/health-{sut.CandidateId}";
		var carrying = _written
			.Where(l => l.Metadata?.Annotations?.ContainsKey(key) == true)
			.ToList();

		carrying.ShouldNotBeEmpty(
			$"no write carried '{key}'. Writes seen: {_written.Count}; annotation keys: "
			+ string.Join(" | ", _written.Select(l =>
				l.Metadata?.Annotations is null ? "NULL" : string.Join(",", l.Metadata.Annotations.Keys))));

		using var document = JsonDocument.Parse(carrying[^1].Metadata!.Annotations[key]);
		foreach (var member in document.RootElement.EnumerateObject())
		{
			if (string.Equals(member.Name, "LastUpdated", StringComparison.OrdinalIgnoreCase))
			{
				return member.Value.GetDateTimeOffset();
			}
		}

		throw new InvalidOperationException(
			"the health annotation carries no LastUpdated member, so this arm cannot compare an instant. "
			+ $"Members present: {string.Join(", ", document.RootElement.EnumerateObject().Select(m => m.Name))}.");
	}

	/// <summary>
	/// SAFETY. The health instant written into the lease comes from the injected provider.
	/// </summary>
	/// <remarks>
	/// The clock is advanced first, so a wall-clock read cannot pass by coincidence and a value captured
	/// once at construction cannot pass either.
	/// </remarks>
	[Fact]
	public async Task Stamp_the_health_annotation_from_the_injected_provider()
	{
		ArrangeAcquirableLease();
		var clock = new FakeTimeProvider();

		await using var sut = CreateSut(clock);
		await sut.StartAsync(CancellationToken.None);
		await WaitUntilLeaderAsync(sut);

		clock.Advance(TimeSpan.FromMinutes(23));
		_written.Clear();

		await sut.UpdateHealthAsync(isHealthy: true, metadata: null, CancellationToken.None);

		HealthInstantWrittenFor(sut).ShouldBe(clock.GetUtcNow());
	}

	/// <summary>
	/// LIVENESS. The health annotation still records a real instant when no provider is injected.
	/// </summary>
	/// <remarks>
	/// Without this, the arm above is satisfied by a provider that stops writing health altogether.
	/// </remarks>
	[Fact]
	public async Task Record_a_real_instant_when_no_provider_is_injected()
	{
		ArrangeAcquirableLease();
		var before = DateTimeOffset.UtcNow.AddMinutes(-1);

		await using var sut = CreateSut(clock: null);
		await sut.StartAsync(CancellationToken.None);
		await WaitUntilLeaderAsync(sut);

		_written.Clear();
		await sut.UpdateHealthAsync(isHealthy: true, metadata: null, CancellationToken.None);

		HealthInstantWrittenFor(sut).ShouldBeGreaterThan(before);
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Kubernetes;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Tests.Kubernetes;

/// <summary>
/// Split-brain lock. A candidate that LOSES the acquisition race must not report itself leader.
/// </summary>
/// <remarks>
/// The conformance kit found three leaders among four candidates against a real API server. The cause
/// was here rather than in the API server's concurrency control: on a 409 the code re-derived
/// leadership from its own in-memory lease copy, whose HolderIdentity it had already overwritten with
/// its own id before attempting the write. Every loser therefore read its own identity back and
/// concluded it had won. This is a unit lock because the defect is in what the loser does with the
/// conflict, not in whether the server produces one -- so it needs no cluster and cannot flake.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KubernetesLostRaceShould
{
	private const string Us = "candidate-us";
	private const string Winner = "candidate-winner";

	private readonly IKubernetes _client = A.Fake<IKubernetes>();
	private readonly ICoordinationV1Operations _coordination = A.Fake<ICoordinationV1Operations>();

	public KubernetesLostRaceShould() =>
		A.CallTo(() => _client.CoordinationV1).Returns(_coordination);

	private KubernetesLeaderElection CreateSut() =>
		new(
			_client,
			"orders",
			Options.Create(new KubernetesLeaderElectionOptions
			{
				LeaseName = "orders-leader-election",
				Namespace = "default",
				InstanceId = Us,
			}),
			logger: null);

	/// <summary>An unheld, long-expired lease: every candidate sees it as acquirable.</summary>
	private static V1Lease Expired() => new()
	{
		Metadata = new V1ObjectMeta { Name = "orders-leader-election", ResourceVersion = "1" },
		Spec = new V1LeaseSpec
		{
			HolderIdentity = null,
			LeaseDurationSeconds = 15,
			RenewTime = DateTime.UtcNow.AddHours(-1),
			LeaseTransitions = 3,
		},
	};

	/// <summary>What the server actually holds after another candidate won.</summary>
	private static V1Lease HeldByWinner() => new()
	{
		Metadata = new V1ObjectMeta { Name = "orders-leader-election", ResourceVersion = "2" },
		Spec = new V1LeaseSpec
		{
			HolderIdentity = Winner,
			LeaseDurationSeconds = 15,
			RenewTime = DateTime.UtcNow,
			LeaseTransitions = 4,
		},
	};

	private void ReadReturns(params V1Lease[] sequence)
	{
		var call = A.CallTo(() => _coordination.ReadNamespacedLeaseWithHttpMessagesAsync(
			A<string>._, A<string>._, A<bool?>._,
			A<IReadOnlyDictionary<string, IReadOnlyList<string>>>._, A<CancellationToken>._));

		call.ReturnsNextFromSequence(
			sequence.Select(l => Task.FromResult(new HttpOperationResponse<V1Lease> { Body = l })).ToArray());
	}

	private void ReplaceConflicts() =>
		A.CallTo(() => _coordination.ReplaceNamespacedLeaseWithHttpMessagesAsync(
				A<V1Lease>._, A<string>._, A<string>._, A<string>._, A<string>._, A<string>._, A<bool?>._,
				A<IReadOnlyDictionary<string, IReadOnlyList<string>>>._, A<CancellationToken>._))
			.Throws(() => new HttpOperationException("the object has been modified")
			{
				Response = new HttpResponseMessageWrapper(
					new HttpResponseMessage(System.Net.HttpStatusCode.Conflict), string.Empty),
			});

	[Fact]
	public async Task NotClaimLeadershipWhenTheWriteConflicts()
	{
		// SAFETY. The lease looks acquirable, we stamp our own id onto our copy, the write loses the
		// race, and the server says the winner is somebody else. We must believe the server.
		ReadReturns(Expired(), HeldByWinner());
		ReplaceConflicts();

		var sut = CreateSut();
		await sut.TryAcquireOrRenewLeaseAsync(CancellationToken.None).ConfigureAwait(false);

		sut.IsLeader.ShouldBeFalse(
			"a candidate whose write was rejected with 409 did not acquire the lease; deriving leadership " +
			"from its own mutated copy is what elected three leaders among four candidates");
		sut.CurrentLeaderId.ShouldBe(Winner, "the holder is whoever the API server says it is");
	}

	[Fact]
	public async Task FailClosedWhenTheHolderCannotBeConfirmed()
	{
		// SAFETY. We lost the race AND cannot re-read. Unknown is not the same as ours.
		var reads = 0;
		A.CallTo(() => _coordination.ReadNamespacedLeaseWithHttpMessagesAsync(
				A<string>._, A<string>._, A<bool?>._,
				A<IReadOnlyDictionary<string, IReadOnlyList<string>>>._, A<CancellationToken>._))
			.ReturnsLazily(() => ++reads == 1
				? Task.FromResult(new HttpOperationResponse<V1Lease> { Body = Expired() })
				: Task.FromException<HttpOperationResponse<V1Lease>>(
					new HttpRequestException("API server unreachable")));
		ReplaceConflicts();

		var sut = CreateSut();
		await sut.TryAcquireOrRenewLeaseAsync(CancellationToken.None).ConfigureAwait(false);

		sut.IsLeader.ShouldBeFalse(
			"leadership that cannot be confirmed must not be claimed; an unconfirmed read is the same " +
			"defect one layer down");
	}

	[Fact]
	public async Task StillAcquireWhenTheWriteSucceeds()
	{
		// LIVENESS. Without this the safety arms above are satisfied by an election that never elects.
		ReadReturns(Expired());
		A.CallTo(() => _coordination.ReplaceNamespacedLeaseWithHttpMessagesAsync(
				A<V1Lease>._, A<string>._, A<string>._, A<string>._, A<string>._, A<string>._, A<bool?>._,
				A<IReadOnlyDictionary<string, IReadOnlyList<string>>>._, A<CancellationToken>._))
			.ReturnsLazily(call => Task.FromResult(new HttpOperationResponse<V1Lease>
			{
				Body = (V1Lease)call.Arguments[0]!,
			}));

		var sut = CreateSut();
		await sut.TryAcquireOrRenewLeaseAsync(CancellationToken.None).ConfigureAwait(false);

		sut.IsLeader.ShouldBeTrue("a candidate whose write was accepted did acquire the lease");
		sut.CurrentLeaderId.ShouldBe(sut.CandidateId, "the winner records itself as the holder");
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;

using global::Consul;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.Consul;

/// <summary>
/// A candidate that took the KV lock and then could not mint a fencing token must RELEASE the lock,
/// whatever the mint failed for.
/// </summary>
/// <remarks>
/// <para>
/// THE DEFECT THESE ARMS BIND. The mint was wrapped in a catch of the exhaustion type alone, so only
/// exhaustion reached the relinquish branch. Every other mint failure — a transient store error, an
/// unreadable fencing mark, a provider that throws — unwound to the acquisition path's generic handler,
/// which reports a failed acquisition and releases nothing. The candidate then held the lock it had
/// taken while never announcing leadership: no other candidate can lead either, so the resource has a
/// leader that may not write and no successor until the Consul session's own TTL expires.
/// </para>
/// <para>
/// WHY A RELEASE AND NOT A HOLD. A node that cannot mint cannot fence its writes, so it must not lead —
/// that half was already right. The half that was wrong is what it does with the lock: holding it is
/// fail-closed for safety and fail-closed for LIVENESS too, which is a cost nobody chose. The three
/// sibling providers that implement this correctly make no distinction between exhaustion and any other
/// cause, and that undifferentiated shape IS the contract.
/// </para>
/// <para>
/// WHAT MAKES EACH ARM RED. Narrowing the mint's catch back to the exhaustion type reddens the
/// transient arm at the release assertion. Releasing unconditionally — on a mint that SUCCEEDED —
/// reddens the liveness arm. The exhaustion arm is a regression: it was green before the fix and must
/// stay green, so the merged path cannot be satisfied by dropping the behaviour that already worked.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class ConsulRelinquishesOnMintFailureShould
{
	private const string Us = "candidate-a";
	private const string Resource = "test-resource";

	[Fact]
	public async Task ReleaseTheLockWhenTheMintFailsForATransientReason()
	{
		// The reachable case, and the one with no exhaustion anywhere: the provider's store answered with
		// an error. Pre-fix this reached the generic acquisition handler, which released nothing.
		var provider = A.Fake<IFencingTokenIssuer>();
		_ = A.CallTo(() => provider.IssueTokenAsync(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("the fencing store is unreachable"));

		var (sut, client, failures) = await StartAsync(provider).ConfigureAwait(false);

		sut.IsLeader.ShouldBeFalse("a candidate that cannot fence its writes must not lead");
		A.CallTo(() => client.KV.Release(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._)).MustHaveHappened();

		failures.ShouldHaveSingleItem().Exception.ShouldBeOfType<InvalidOperationException>(
			"the cause must reach a subscriber; a reason string alone cannot say what failed");

		await sut.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task ReleaseTheLockWhenTheTokenDomainIsExhausted()
	{
		// REGRESSION. This arm was green before the fix — it is here so the merged path cannot be
		// satisfied by a provider that stopped handling the one cause it already handled.
		var provider = A.Fake<IFencingTokenIssuer>();
		_ = A.CallTo(() => provider.IssueTokenAsync(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new FencingTokenExhaustedException("no values left") { ResourceId = Resource });

		var (sut, client, failures) = await StartAsync(provider).ConfigureAwait(false);

		sut.IsLeader.ShouldBeFalse("an exhausted domain still relinquishes");
		A.CallTo(() => client.KV.Release(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._)).MustHaveHappened();
		failures.ShouldHaveSingleItem();

		await sut.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task LeadAndHoldTheLockWhenTheMintSucceeds()
	{
		// LIVENESS. Every arm above asserts the lock was GIVEN BACK; a provider that released
		// unconditionally — or never acquired at all — passes all of them while leading nobody.
		var provider = A.Fake<IFencingTokenIssuer>();
		_ = A.CallTo(() => provider.IssueTokenAsync(A<string>._, A<CancellationToken>._)).Returns(7L);

		var (sut, client, failures) = await StartAsync(provider).ConfigureAwait(false);

		sut.IsLeader.ShouldBeTrue("a minted fence is the whole precondition for leading");
		A.CallTo(() => client.KV.Release(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
		failures.ShouldBeEmpty();

		await sut.DisposeAsync().ConfigureAwait(false);
	}

	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// A candidate that wins the KV acquisition against a healthy faked agent, with the supplied fencing
	/// provider as the only thing that varies between arms. <c>StartAsync</c> awaits one acquisition
	/// attempt, so the outcome is observable on the next statement with no polling and no clock advance.
	/// </summary>
	private static async Task<(ConsulLeaderElection Sut, IConsulClient Client, List<LeaderElectionAcquisitionFailedEventArgs> Failures)> StartAsync(
		IFencingTokenIssuer provider)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ConsulLeaderElectionOptions
		{
			ConsulAddress = "http://localhost:8500",
			InstanceId = Us,

			// The injected clock is frozen after start; valid timings retain server TTL headroom.
			RenewInterval = TimeSpan.FromSeconds(5),
			GracePeriod = TimeSpan.FromSeconds(5),
			SessionTTL = TimeSpan.FromSeconds(30),
			MaxRetryAttempts = 0,
		});

		var client = A.Fake<IConsulClient>();
		KVPair? current = null;
		_ = A.CallTo(() => client.Session.Create(A<SessionEntry>._, A<WriteOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new WriteResult<string> { Response = "session-1" }));
		_ = A.CallTo(() => client.Session.Renew(A<string>._, A<WriteOptions>._, A<CancellationToken>._))
			.ReturnsLazily((string session, WriteOptions _, CancellationToken _) => Task.FromResult(
				new WriteResult<SessionEntry> { Response = new SessionEntry { ID = session, TTL = TimeSpan.FromSeconds(30) } }));
		_ = A.CallTo(() => client.KV.Acquire(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._))
			.ReturnsLazily((KVPair pair, WriteOptions _, CancellationToken _) =>
			{
				current = pair; // Preserve the actual private nonce and payload supplied by the owner.
				return Task.FromResult(new WriteResult<bool> { Response = true });
			});
		_ = A.CallTo(() => client.KV.Get(A<string>._, A<QueryOptions>._, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult(new QueryResult<KVPair> { Response = current! }));
		_ = A.CallTo(() => client.KV.Put(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new WriteResult<bool> { Response = true }));
		_ = A.CallTo(() => client.KV.Release(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new WriteResult<bool> { Response = true }));
		_ = A.CallTo(() => client.Session.Destroy(A<string>._, A<WriteOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new WriteResult<bool> { Response = true }));

		var sut = new ConsulLeaderElection(
			Resource, options, client, NullLogger<ConsulLeaderElection>.Instance, provider, new FakeTimeProvider());

		var failures = new List<LeaderElectionAcquisitionFailedEventArgs>();
		var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		sut.AcquisitionFailed += (_, e) => { failures.Add(e); failed.TrySetResult(); };
		sut.BecameLeader += (_, _) => published.TrySetResult();

		await sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
		if (sut.IsLeader) { await published.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
		else
		{
			await failed.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
			await sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
		}
		return (sut, client, failures);
	}
}

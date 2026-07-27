// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text;

using Consul;

using Excalibur.Dispatch.LeaderElection.Fencing;

using k8s.Autorest;
using k8s.Models;

namespace Excalibur.LeaderElection.Tests.Fencing;

// Independent regression lock (author != implementer) for the S887 nxjn2k fencing-token EXHAUSTION invariant.
//
// A fencing token identifies a leadership tenure and MUST be strictly monotonic. If a provider's backing
// counter WRAPS/overflows, a reused token lets a stale leader validate as current => split-brain catastrophe.
// The contract (ADR / FencingTokenExhaustedException remarks): when the token domain is exhausted the provider
// MUST FAIL CLOSED — throw FencingTokenExhaustedException — never wrap and mint a non-monotonic token.
//
// Before this lock, grep of tests/** for FencingTokenExhaustedException asserted against a PROVIDER = 0 hits.
//
// Each provider is locked with BOTH arms (testing-patterns §3 — a safety-only assertion is satisfied by a
// blanket-throw guard, which is itself a liveness bug that bricks all leadership):
//   * SAFETY   — the backing store reports a counter AT/OVER its domain max (or, for Redis, the defensive
//                non-positive INCR result) => IssueTokenAsync THROWS FencingTokenExhaustedException.
//   * LIVENESS — the backing store reports a NORMAL value => IssueTokenAsync RETURNS a valid positive token
//                and does NOT throw (proves the guard is exhaustion-specific, not a blanket refusal).
//
// The three providers here are unit-lockable because their exhaustion guard fires purely from a value the
// FAKE backing client returns — no real infrastructure required. The Redis/Mongo/Postgres SERVER-SIDE
// INCR/$inc overflow paths (a RedisServerException "overflow", etc.) are real-infra-only and are NOT covered
// here (see the deferred-coverage note in the sprint report).

/// <summary>
/// Exhaustion regression lock for <see cref="ConsulFencingTokenProvider"/>: the int64 KV counter at
/// <see cref="long.MaxValue"/> must fail closed rather than overflow.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConsulFencingTokenExhaustionShould
{
	private const string ResourceId = "orders-leader";

	private readonly IConsulClient _consulClient = A.Fake<IConsulClient>();
	private readonly IKVEndpoint _kv = A.Fake<IKVEndpoint>();

	public ConsulFencingTokenExhaustionShould() => A.CallTo(() => _consulClient.KV).Returns(_kv);

	private ConsulFencingTokenProvider CreateSut()
	{
		var options = Options.Create(new ConsulLeaderElectionOptions
		{
			ConsulAddress = "http://localhost:8500",
			KeyPrefix = "excalibur/leader-election",
			InstanceId = "consul-instance",
		});
		return new ConsulFencingTokenProvider(_consulClient, options);
	}

	private void StoredCounterIs(long value, ulong modifyIndex)
	{
		var pair = new KVPair($"excalibur/leader-election/fencing/{ResourceId}")
		{
			Value = Encoding.UTF8.GetBytes(value.ToString(CultureInfo.InvariantCulture)),
			ModifyIndex = modifyIndex,
		};
		A.CallTo(() => _kv.Get(A<string>._, A<CancellationToken>._))
			.Returns(new QueryResult<KVPair> { Response = pair });
	}

	[Fact]
	public async Task ThrowFencingTokenExhausted_WhenCounterIsAtInt64Max()
	{
		// SAFETY: stored counter == long.MaxValue => next mint (value+1) would overflow the int64 domain.
		StoredCounterIs(long.MaxValue, modifyIndex: 100UL);
		var sut = CreateSut();

		var ex = await Should.ThrowAsync<FencingTokenExhaustedException>(
			async () => await sut.IssueTokenAsync(ResourceId, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		ex.ResourceId.ShouldBe(ResourceId,
			"the fail-closed exhaustion error must identify the resource whose token domain is exhausted");

		// The overflowing value must NEVER be written back — no CAS attempt on the exhausted path.
		A.CallTo(() => _kv.CAS(A<KVPair>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task MintNextMonotonicToken_WhenCounterIsBelowMax()
	{
		// LIVENESS: a normal stored counter must still mint (guard is exhaustion-specific, not a blanket throw).
		StoredCounterIs(5L, modifyIndex: 42UL);
		A.CallTo(() => _kv.CAS(A<KVPair>._, A<CancellationToken>._))
			.Returns(new WriteResult<bool> { Response = true });
		var sut = CreateSut();

		var token = await sut.IssueTokenAsync(ResourceId, CancellationToken.None).ConfigureAwait(false);

		token.ShouldBe(6L, "a normal counter must advance monotonically (5 -> 6), not fail closed");
	}
}

/// <summary>
/// Exhaustion regression lock for <see cref="KubernetesFencingTokenProvider"/>: the 32-bit
/// <c>Lease.spec.leaseTransitions</c> counter at <see cref="int.MaxValue"/> must fail closed.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KubernetesFencingTokenExhaustionShould
{
	private const string ResourceId = "orders-leader";

	private readonly IKubernetes _client = A.Fake<IKubernetes>();
	private readonly ICoordinationV1Operations _coordination = A.Fake<ICoordinationV1Operations>();

	public KubernetesFencingTokenExhaustionShould() =>
		A.CallTo(() => _client.CoordinationV1).Returns(_coordination);

	private KubernetesFencingTokenProvider CreateSut()
	{
		var options = Options.Create(new KubernetesLeaderElectionOptions
		{
			LeaseName = $"{ResourceId}-leader-election",
			Namespace = "default",
			InstanceId = "k8s-instance",
		});
		return new KubernetesFencingTokenProvider(_client, options);
	}

	private void LeaseTransitionsIs(int transitions)
	{
		var lease = new V1Lease { Spec = new V1LeaseSpec { LeaseTransitions = transitions } };
		A.CallTo(() => _coordination.ReadNamespacedLeaseWithHttpMessagesAsync(
				A<string>._, A<string>._, A<bool?>._,
				A<IReadOnlyDictionary<string, IReadOnlyList<string>>>._, A<CancellationToken>._))
			.Returns(new HttpOperationResponse<V1Lease> { Body = lease });
	}

	[Fact]
	public async Task ThrowFencingTokenExhausted_WhenLeaseTransitionsAtInt32Max()
	{
		// SAFETY: leaseTransitions == int.MaxValue => the next transition would overflow the 32-bit counter.
		LeaseTransitionsIs(int.MaxValue);
		var sut = CreateSut();

		var ex = await Should.ThrowAsync<FencingTokenExhaustedException>(
			async () => await sut.IssueTokenAsync(ResourceId, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		ex.ResourceId.ShouldBe(ResourceId,
			"the fail-closed exhaustion error must identify the resource whose token domain is exhausted");
	}

	[Fact]
	public async Task ReturnNativeTransitionCount_WhenBelowInt32Max()
	{
		// LIVENESS: a normal leaseTransitions value must be returned as the token, not fail closed.
		LeaseTransitionsIs(7);
		var sut = CreateSut();

		var token = await sut.IssueTokenAsync(ResourceId, CancellationToken.None).ConfigureAwait(false);

		token.ShouldBe(7L, "a normal leaseTransitions count is the fencing token; only int.MaxValue fails closed");
	}
}

/// <summary>
/// Exhaustion regression lock for <see cref="RedisFencingTokenProvider"/>: the defensive non-positive
/// <c>INCR</c> result must fail closed rather than mint a non-monotonic token. (The server-side INCR
/// overflow -> RedisServerException path is real-infra-only and covered separately.)
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RedisFencingTokenExhaustionShould
{
	private const string ResourceId = "orders-leader";

	private readonly IConnectionMultiplexer _redis = A.Fake<IConnectionMultiplexer>();
	private readonly IDatabase _database = A.Fake<IDatabase>();

	public RedisFencingTokenExhaustionShould() =>
		A.CallTo(() => _redis.GetDatabase(A<int>._, A<object>._)).Returns(_database);

	private void IncrementReturns(long value) =>
		A.CallTo(() => _database.StringIncrementAsync(A<RedisKey>._, A<long>._, A<CommandFlags>._))
			.Returns(value);

	[Fact]
	public async Task ThrowFencingTokenExhausted_WhenIncrementResultIsNonPositive()
	{
		// SAFETY: a non-positive INCR result is non-monotonic (a wrapped/undefined counter); the provider
		// must fail closed rather than mint an unsafe token.
		IncrementReturns(0L);
		var sut = new RedisFencingTokenProvider(_redis);

		var ex = await Should.ThrowAsync<FencingTokenExhaustedException>(
			async () => await sut.IssueTokenAsync(ResourceId, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		ex.ResourceId.ShouldBe(ResourceId,
			"the fail-closed exhaustion error must identify the resource whose token domain is exhausted");
	}

	[Fact]
	public async Task MintToken_WhenIncrementReturnsPositiveValue()
	{
		// LIVENESS: a normal positive INCR result must be returned as the token, not fail closed.
		IncrementReturns(5L);
		var sut = new RedisFencingTokenProvider(_redis);

		var token = await sut.IssueTokenAsync(ResourceId, CancellationToken.None).ConfigureAwait(false);

		token.ShouldBe(5L, "a positive INCR result is the minted monotonic token; only a non-positive result fails closed");
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.Integration.Tests.Data;
using Excalibur.LeaderElection.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Author≠impl regression lock for bd-nxmjpm (ptm2bb child, ADR-339): the SQL Server leader-election
/// fencing path mints a <b>strictly-monotonic</b> token from a dedicated per-resource <c>SEQUENCE</c> and
/// is <b>fail-CLOSED</b> — leadership is structurally coupled to an advanced fence (mint-before-grant;
/// relinquish on bounded-retry mint exhaustion). Mirrors the committed Redis reference
/// (<c>RedisLeaderElectionFencingShould</c>, bd-762uzn/msmwr9), different provider.
/// </summary>
/// <remarks>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> both facts run against a real SQL Server (TestContainers) and
/// assert observable BEHAVIOR through the real seam — a real <c>NEXT VALUE FOR</c> high-water mark advancing
/// across handovers (so a prior token is rejected on a fresh provider instance), and a candidate that cannot
/// advance its fence releasing the real <c>sp_getapplock</c> so a second candidate acquires it.
/// <c>DockerAvailable.ShouldBeTrue(...)</c> makes the lock NON-SKIPPED (a skipped fencing safety test is the
/// exact gap that ships a split-brain bug). Serial (<c>-m:1</c>); per-test isolation via a unique resource id
/// so each per-resource sequence/applock starts fresh.
/// </para>
/// <para>
/// <b>Fact 1 — monotonicity + stale-token-rejected (RED on a non-monotonic mint):</b> the provider is resolved
/// through the public <c>AddSqlServerFencingTokenProvider(connectionString)</c> registration (the type is
/// internal). A fresh provider instance (a new leader) mints strictly greater than the prior token, and
/// <c>ValidateTokenAsync</c> then rejects the now-stale prior token (it falls below the advanced high-water
/// mark). A mutant that reused/reset the counter (non-monotonic) or that validated a below-water token as
/// valid would fail these assertions.
/// </para>
/// <para>
/// <b>Fact 2 — fail-CLOSED (RED on the mint-after-grant + swallow mutant):</b> a throwing
/// <see cref="IFencingTokenProvider"/> is injected into the public <see cref="SqlServerLeaderElection"/> ctor.
/// On the pre-fix fail-OPEN ordering (declare leadership first, mint afterward, swallow the failure)
/// <see cref="SqlServerLeaderElection.IsLeader"/> would be <c>true</c> and <c>BecameLeader</c> would fire →
/// both RED. The fix mints BEFORE declaring leadership and, on bounded-retry exhaustion, calls
/// <c>ReleaseLockAsync()</c> + returns without ever leading — so the applock is freed and a second candidate
/// acquires it. Production RED-proof against the reverted impl is deferred to post-commit batch verification
/// (the impl file is owned by the implementer lane; Platform committed the impl).
/// </para>
/// <para>
/// NFR-6: no stale sibling found — this is a new file alongside <c>SqlServerLeaderElectionDisposeShould</c>
/// (whose fixture wiring it mirrors) and no existing test asserts the pre-fix fail-OPEN / non-monotonic contract.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerLeaderElectionFencingShould : IntegrationTestBase
{
	private readonly SqlServerContainerFixture _fixture;

	public SqlServerLeaderElectionFencingShould(SqlServerContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task MintStrictlyMonotonicTokensAndRejectStaleTokenAcrossHandovers()
	{
		// nxmjpm fact 1 — the real SQL SEQUENCE mint is strictly monotonic and validation is fail-closed.
		_fixture.DockerAvailable.ShouldBeTrue(
			"nxmjpm monotonic fencing is a split-brain safety control — this real-SqlServer lock must never be skipped");

		// The fencing resource id mirrors the leader-election lock resource (an invariant string).
		var resourceId = $"nxmjpm-mono-{Guid.NewGuid():N}";

		// Resolve the real SQL Server fencing provider through the public registration (the type is internal) —
		// the same SEQUENCE-backed provider the production wiring uses. "Instance A" = the first leader.
		var servicesA = new ServiceCollection();
		_ = servicesA.AddSqlServerFencingTokenProvider(_fixture.ConnectionString);
		await using var serviceProviderA = servicesA.BuildServiceProvider();
		var providerA = serviceProviderA.GetRequiredService<IFencingTokenProvider>();

		var firstToken = await providerA.IssueTokenAsync(resourceId, TestCancellationToken);
		firstToken.ShouldBeGreaterThanOrEqualTo(1L,
			"the first minted token on a fresh per-resource SEQUENCE advances the high-water mark to >= 1");

		// A separate, fresh provider instance ("instance B" = the next leader, after a handover) over the same
		// real database must mint a STRICTLY GREATER token — the monotonic-across-handovers invariant.
		var servicesB = new ServiceCollection();
		_ = servicesB.AddSqlServerFencingTokenProvider(_fixture.ConnectionString);
		await using var serviceProviderB = servicesB.BuildServiceProvider();
		var providerB = serviceProviderB.GetRequiredService<IFencingTokenProvider>();

		var afterHandoverToken = await providerB.IssueTokenAsync(resourceId, TestCancellationToken);
		afterHandoverToken.ShouldBeGreaterThan(firstToken,
			"a new leader's token must be strictly greater than the prior leader's (monotonic across handovers)");

		// Fail-closed validation: the now-stale prior token falls below the advanced high-water mark -> rejected.
		(await providerB.ValidateTokenAsync(resourceId, firstToken, TestCancellationToken)).ShouldBeFalse(
			"the stale prior token must be REJECTED once the fence advanced (fail-closed high-water-mark check)");

		// The current high-water token is still accepted (sanity: validation is not rejecting everything).
		(await providerA.ValidateTokenAsync(resourceId, afterHandoverToken, TestCancellationToken)).ShouldBeTrue(
			"the current high-water token must validate (token >= current high-water mark)");

		// Strictly increasing across a further handover.
		var thirdToken = await providerA.IssueTokenAsync(resourceId, TestCancellationToken);
		thirdToken.ShouldBeGreaterThan(afterHandoverToken,
			"every subsequent mint must be strictly greater than the last (monotonic)");
	}

	[Fact]
	public async Task RelinquishWithoutBecomingLeaderWhenFencingMintFails()
	{
		// nxmjpm fact 2 — fail-CLOSED: a fencing-enabled candidate that cannot mint its fence must NOT lead,
		// and must release the real applock so another candidate can acquire it.
		_fixture.DockerAvailable.ShouldBeTrue(
			"nxmjpm fail-closed fencing is a split-brain safety control — this real-SqlServer lock must never be skipped");

		var lockResource = $"nxmjpm-failclosed-{Guid.NewGuid():N}";

		var optionsA = Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions
		{
			InstanceId = "candidate-A",
			LeaseDuration = TimeSpan.FromMinutes(5),
			// Neutralize the background renewal loop for the deterministic test window: its first iteration waits
			// one RenewInterval, so a long interval guarantees the loop never re-attempts acquire+mint before we
			// assert and StopAsync.
			RenewInterval = TimeSpan.FromMinutes(5),
			GracePeriod = TimeSpan.FromSeconds(5),
			RetryInterval = TimeSpan.FromSeconds(5),
		});

		var becameLeaderFired = 0;
		await using var election = new SqlServerLeaderElection(
			_fixture.ConnectionString,
			lockResource,
			optionsA,
			NullLogger<SqlServerLeaderElection>.Instance,
			fencingTokenProvider: new AlwaysThrowingFencingTokenProvider());
		election.BecameLeader += (_, _) => Interlocked.Increment(ref becameLeaderFired);

		// Act — StartAsync runs the initial acquire: sp_getapplock acquires the resource, then every bounded mint
		// attempt throws -> the impl releases the applock and returns WITHOUT declaring leadership. StopAsync then
		// halts the (neutralized) renewal loop deterministically.
		await election.StartAsync(TestCancellationToken);
		await election.StopAsync(TestCancellationToken);

		// Assert — leadership was NEVER declared (fail-CLOSED, not the pre-fix fail-OPEN).
		election.IsLeader.ShouldBeFalse(
			"a candidate that cannot advance its fence must NOT become leader (fail-closed, no un-fenced leadership)");
		Volatile.Read(ref becameLeaderFired).ShouldBe(0,
			"BecameLeader must never fire when the fence mint failed — leadership is structurally coupled to a minted fence");

		// Assert — relinquish proof through real infra: a SECOND candidate (no fencing) acquires the SAME applock
		// resource, proving candidate-A released sp_getapplock. A fail-OPEN impl would still hold the lock
		// connection (leadership declared) -> candidate-B could not acquire.
		var optionsB = Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions
		{
			InstanceId = "candidate-B",
			LeaseDuration = TimeSpan.FromMinutes(5),
			RenewInterval = TimeSpan.FromMinutes(5),
			GracePeriod = TimeSpan.FromSeconds(5),
			RetryInterval = TimeSpan.FromSeconds(5),
		});

		await using var electionB = new SqlServerLeaderElection(
			_fixture.ConnectionString,
			lockResource,
			optionsB,
			NullLogger<SqlServerLeaderElection>.Instance);

		await electionB.StartAsync(TestCancellationToken);
		electionB.IsLeader.ShouldBeTrue(
			"the relinquished applock must be free for another candidate to acquire (the real-infra relinquish proof)");
		await electionB.StopAsync(TestCancellationToken);
	}

	/// <summary>
	/// A fencing-token provider whose mint always fails — drives the nxmjpm fail-CLOSED relinquish path.
	/// </summary>
	private sealed class AlwaysThrowingFencingTokenProvider : IFencingTokenProvider
	{
		public ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken)
			=> throw new InvalidOperationException("Simulated fencing-store outage: token mint unavailable.");

		public ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken)
			=> throw new NotSupportedException("Not exercised by the fail-closed acquisition path.");

		public ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken)
			=> throw new NotSupportedException("Not exercised by the fail-closed acquisition path.");
	}
}

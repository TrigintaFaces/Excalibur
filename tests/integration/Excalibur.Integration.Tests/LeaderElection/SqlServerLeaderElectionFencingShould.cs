// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.Integration.Tests.Data;
using Excalibur.LeaderElection.SqlServer;

using Microsoft.Data.SqlClient;
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
/// <see cref="IFencingTokenIssuer"/> is injected into the public <see cref="SqlServerLeaderElection"/> ctor.
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
		var providerA = serviceProviderA.GetRequiredService<IFencingTokenIssuer>();

		var firstToken = await providerA.IssueTokenAsync(resourceId, TestCancellationToken);
		firstToken.ShouldBeGreaterThanOrEqualTo(1L,
			"the first minted token on a fresh per-resource SEQUENCE advances the high-water mark to >= 1");

		// A separate, fresh provider instance ("instance B" = the next leader, after a handover) over the same
		// real database must mint a STRICTLY GREATER token — the monotonic-across-handovers invariant.
		var servicesB = new ServiceCollection();
		_ = servicesB.AddSqlServerFencingTokenProvider(_fixture.ConnectionString);
		await using var serviceProviderB = servicesB.BuildServiceProvider();
		var providerB = serviceProviderB.GetRequiredService<IFencingTokenIssuer>();

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
	public async Task IssueDistinctTokens_UnderConcurrentMint_AtomicSequence()
	{
		// pblbmb (wj6b10) — the fencing-token mint must be ATOMIC under concurrency: N PARALLEL
		// IssueTokenAsync calls against a fresh per-resource SEQUENCE yield N DISTINCT tokens (no two
		// concurrent callers ever share one). The across-handover facts above prove SEQUENTIAL monotonicity;
		// this proves CONCURRENT atomicity — the split-brain-critical property a non-atomic read-modify-write
		// mint would violate.
		// NON-VACUITY: a non-atomic mint (read-then-write) lets two concurrent callers observe the same value
		// → Distinct().Count() < N → RED. SQL Server's NEXT VALUE FOR on a real SEQUENCE is atomic.
		_fixture.DockerAvailable.ShouldBeTrue(
			"pblbmb concurrent fencing-token atomicity is a split-brain safety control — this real-SqlServer lock must never be skipped");

		var resourceId = $"pblbmb-atomic-{Guid.NewGuid():N}";
		var services = new ServiceCollection();
		_ = services.AddSqlServerFencingTokenProvider(_fixture.ConnectionString);
		await using var serviceProvider = services.BuildServiceProvider();
		var provider = serviceProvider.GetRequiredService<IFencingTokenIssuer>();

		const int issuances = 40;
		var tasks = Enumerable.Range(0, issuances)
			.Select(_ => Task.Run(
				async () => await provider.IssueTokenAsync(resourceId, TestCancellationToken), TestCancellationToken));
		var tokens = await Task.WhenAll(tasks);

		tokens.Distinct().Count().ShouldBe(
			issuances,
			"pblbmb: every concurrently-minted fencing token must be unique — an atomic SEQUENCE mint never issues a duplicate to two racing callers.");
	}

	/// <summary>
	/// Mirrors <c>SqlServerFencingTokenProvider.SequenceName</c> exactly (documented on that type's own
	/// remarks: <c>"fencing_"</c> + the full uppercase-hex SHA-256 of the resource id) so this test can
	/// address the same real sequence the provider creates, without needing internals access.
	/// </summary>
	private static string FencingSequenceName(string resourceId)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(resourceId));
		return "fencing_" + Convert.ToHexString(hash);
	}

	[Fact]
	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "The sequence name comes from FencingSequenceName(), which is 'fencing_' + the hex " +
			"SHA-256 of the resource id -- exclusively [0-9A-F], never raw input, mirroring the production " +
			"provider's own identical justification. CREATE SEQUENCE cannot parameterize an identifier.")]
	public async Task RefuseEveryTokenWhenTheSequenceExistsButWasNeverDrawn()
	{
		// F1 fail-CLOSED on a never-drawn sequence. The issue path creates the per-resource SEQUENCE and
		// draws NEXT VALUE FOR in ONE batch with NO enclosing transaction, so an interruption between the
		// two statements -- a cancelled CancellationToken being the likeliest route -- leaves a sequence
		// that EXISTS and was NEVER DRAWN, with nothing to roll it back.
		//
		// THE DEFECT THIS BINDS IS AN INVERSION, NOT AN OFF-BY-ONE. sys.sequences.current_value is declared
		// sql_variant NOT NULL and documented as returning the START WITH value when the sequence has never
		// been used, so a provider reading it sees a high-water mark of 1 for a resource whose token was
		// never issued -- and the fail-closed comparison in ValidateTokenAsync then ADMITS 1 and every value
		// above it. The state that must refuse everything instead admits everything, and every admission is
		// logged as a success. last_used_value is sql_variant NULL, documented as the last value GENERATED
		// by NEXT VALUE FOR, so it can express never-issued and the fence refuses.
		//
		// ROUTE: the never-drawn sequence is created DIRECTLY here, with the provider's own DDL, rather than
		// by cancelling a real IssueTokenAsync. The cancel point sits INSIDE a single server round-trip,
		// between two statements of one batch, so it is a race no test can steer deterministically; creating
		// the object reproduces the resulting STATE exactly, and the state is what the catalog view answers
		// about. The LIVENESS tail below confirms the reproduction is faithful -- the provider's own issue
		// path adopts this sequence (its IF NOT EXISTS finds it) and the first real draw is 1, which holds
		// only if the object created here matches the one the provider would have created itself.
		_fixture.DockerAvailable.ShouldBeTrue(
			"fail-closed fencing on a never-drawn sequence is a split-brain safety control -- this real-SqlServer lock must never be skipped");

		var resourceId = $"neverdrawn-{Guid.NewGuid():N}";

		await using (var connection = new SqlConnection(_fixture.ConnectionString))
		{
			await connection.OpenAsync(TestCancellationToken);
			await using var create = new SqlCommand(
				$"CREATE SEQUENCE {FencingSequenceName(resourceId)} AS bigint START WITH 1 INCREMENT BY 1",
				connection);
			_ = await create.ExecuteNonQueryAsync(TestCancellationToken);
		}

		var services = new ServiceCollection();
		_ = services.AddSqlServerFencingTokenProvider(_fixture.ConnectionString);
		await using var serviceProvider = services.BuildServiceProvider();
		var provider = serviceProvider.GetRequiredService<IFencingTokenIssuer>();

		// SAFETY -- no token was ever issued, so there is no high-water mark to report. A value here is a
		// fabrication: the sequence has never generated one.
		(await provider.GetTokenAsync(resourceId, TestCancellationToken)).ShouldBeNull(
			"a sequence that exists but was never drawn has issued NO token, so the high-water mark must be "
			+ "reported as absent rather than as the START WITH value the sequence was created with");

		// SAFETY -- the arm that encodes the requirement. 1 is the EXACT value the inversion admitted,
		// because it is the START WITH value current_value reports for a never-used sequence.
		(await provider.ValidateTokenAsync(resourceId, 1L, TestCancellationToken)).ShouldBeFalse(
			"token 1 must be REFUSED on a never-drawn sequence -- 1 is the precise value a current_value read "
			+ "admits, so this is the assertion that separates fail-closed from the fail-OPEN inversion");
		(await provider.ValidateTokenAsync(resourceId, long.MaxValue, TestCancellationToken)).ShouldBeFalse(
			"with no token ever issued EVERY token must be refused -- including one far above any plausible "
			+ "high-water mark, which a comparison against a fabricated 1 would admit");
		(await provider.ValidateTokenAsync(resourceId, -1L, TestCancellationToken)).ShouldBeFalse(
			"a negative token must be refused too -- the refusal is 'nothing was issued', not 'this value is low'");

		// LIVENESS -- the refusal above is caused by the never-drawn state, NOT by a provider that refuses
		// everything always. Mint for real on this same sequence and the fence opens for the minted token.
		var minted = await provider.IssueTokenAsync(resourceId, TestCancellationToken);
		minted.ShouldBe(1L,
			"the provider's issue path must adopt the existing sequence and draw its first value -- 1 proves "
			+ "the object created above is the one the provider itself would have created");
		(await provider.ValidateTokenAsync(resourceId, minted, TestCancellationToken)).ShouldBeTrue(
			"once a token has genuinely been issued the fence must ACCEPT it -- a provider that refused every "
			+ "token unconditionally would satisfy the safety arms above while fencing nothing");
	}

	[Fact]
	public async Task ReportTheLastIssuedTokenAsTheHighWaterMark()
	{
		// The other direction of non-vacuity for the fail-closed read above. A provider that answered "no
		// token was ever issued" unconditionally would pass every safety arm of the never-drawn fact and
		// leave the fence permanently shut; this binds the high-water mark to the value actually issued.
		// Nothing else asserts GetTokenAsync's returned VALUE -- the sibling facts assert only the accept /
		// refuse verdicts derived from it.
		_fixture.DockerAvailable.ShouldBeTrue(
			"the high-water-mark read is the input to every fencing verdict -- this real-SqlServer lock must never be skipped");

		var resourceId = $"highwater-{Guid.NewGuid():N}";

		var services = new ServiceCollection();
		_ = services.AddSqlServerFencingTokenProvider(_fixture.ConnectionString);
		await using var serviceProvider = services.BuildServiceProvider();
		var provider = serviceProvider.GetRequiredService<IFencingTokenIssuer>();

		var first = await provider.IssueTokenAsync(resourceId, TestCancellationToken);
		var second = await provider.IssueTokenAsync(resourceId, TestCancellationToken);
		second.ShouldBeGreaterThan(first, "the mint is strictly monotonic");

		(await provider.GetTokenAsync(resourceId, TestCancellationToken)).ShouldBe(second,
			"the high-water mark must be the token LAST GENERATED, so a reader can tell an issued fence from "
			+ "an absent one by value and not merely by presence");

		(await provider.ValidateTokenAsync(resourceId, second, TestCancellationToken)).ShouldBeTrue(
			"the current token must be accepted");
		(await provider.ValidateTokenAsync(resourceId, second + 1, TestCancellationToken)).ShouldBeTrue(
			"a token above the high-water mark must be accepted -- the check is at-or-above, so a successor "
			+ "that already advanced the fence is not fenced out by a stale read");
		(await provider.ValidateTokenAsync(resourceId, first, TestCancellationToken)).ShouldBeFalse(
			"the superseded token must be refused -- a superseded leader presenting a stale token is rejected");
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
			LeaseDuration = TimeSpan.FromMinutes(10),
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
			LeaseDuration = TimeSpan.FromMinutes(10),
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
	private sealed class AlwaysThrowingFencingTokenProvider : IFencingTokenIssuer
	{
		public ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken)
			=> throw new InvalidOperationException("Simulated fencing-store outage: token mint unavailable.");

		public ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken)
			=> throw new NotSupportedException("Not exercised by the fail-closed acquisition path.");

		public ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken)
			=> throw new NotSupportedException("Not exercised by the fail-closed acquisition path.");
	}
}

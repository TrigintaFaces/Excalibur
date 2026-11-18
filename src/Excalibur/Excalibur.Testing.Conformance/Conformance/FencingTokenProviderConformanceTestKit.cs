// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Conformance arms for <see cref="IFencingTokenProvider"/>, derived once per real provider.
/// </summary>
/// <remarks>
/// <para>
/// THE SUBJECT OF THIS SUITE IS A REAL PROVIDER, AND THAT IS THE WHOLE POINT. The predecessor suite named
/// for this contract configured a mock of the interface and asserted the mock returned what it was told, so
/// no change to any real provider could redden it; a fake satisfies the interface's signature and none of its
/// behavioural obligations, which weakens every postcondition to "returns whatever it was stubbed with".
/// Every arm here runs against an implementation a consumer can actually resolve, so a mutation to any one
/// provider reddens at least one arm in that provider's deriving suite.
/// </para>
/// <para>
/// THE ARM THAT NO PROVIDER HAD, and the reason this suite exists rather than six per-provider ones:
/// <see cref="ValidateTokenAsync_RefusesEveryToken_WhenNoTokenWasEverIssued"/>. Every provider already had a
/// test asserting its mint is monotonic. None asserted the other half of fail-closed — that a resource for
/// which no token was ever issued refuses EVERY token rather than some plausible default. Validation is a
/// high-water comparison of the form <c>token &gt;= mark</c>, so a mark rendered as a low real-looking value
/// admits every token presented against it: the state that must refuse everything is exactly the state that
/// would otherwise admit everything. Three independent implementations shipped that inversion, because the
/// test that would have failed did not exist in any suite their authors could have copied from.
/// </para>
/// <para>
/// EVERY ARM TAKES A FRESH RESOURCE ID. Two arms assert the never-issued state and the rest establish a
/// mark, so a resource shared across arms would make the result depend on execution order.
/// </para>
/// <para>
/// PROVIDERS THAT READ A STORE'S NATIVE COUNTER RATHER THAN MINTING override
/// <see cref="EstablishTokenAsync"/> and <see cref="ProviderMintsItsOwnTokens"/>. The contract permits both:
/// a provider may increment its own persisted counter, or observe a counter the backing store itself advances
/// on each leadership change. The native reader does not implement <see cref="IFencingTokenIssuer"/>;
/// its deriving suite establishes marks through the substrate's real transition mechanism.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class FencingTokenProviderConformanceTestKit : ConformanceTestKit
{
	private static readonly long[] UnadvancedPresentedTokens = [0L, 1L, 2L, long.MaxValue];

	/// <summary>Gets the cancellation token used by every provider operation in the current arm.</summary>
	/// <value>None by default; override with the test runner’s per-test cancellation token.</value>
	protected virtual CancellationToken TestCancellationToken => CancellationToken.None;
	/// <summary>
	/// Gets the provider under test, built by <see cref="CreateProviderAsync"/> before each arm.
	/// </summary>
	protected IFencingTokenProvider Provider { get; private set; } = null!;

	/// <summary>
	/// Gets a value indicating whether the provider implements <see cref="IFencingTokenIssuer"/>
	/// and mints a new token of its own.
	/// </summary>
	/// <value>
	/// <see langword="true"/> for a self-minting provider, which is every provider that owns its counter;
	/// <see langword="false"/> for one that reads a store's native monotonic counter.
	/// </value>
	/// <remarks>
	/// A read-native provider cannot be used as an <see cref="IFencingTokenIssuer"/>, so
	/// <see cref="ConcurrentEstablishes_YieldDistinctTokens"/> is the one arm this gates. A suite that
	/// returns <see langword="false"/> owes its own concurrency arm against its substrate's transition
	/// mechanism; this suite cannot write it, because what "a transition" means is the substrate's answer.
	/// </remarks>
	protected virtual bool ProviderMintsItsOwnTokens => true;

	/// <summary>
	/// Gets a value indicating whether the provider's store can hold a mark-bearing artifact for a resource
	/// whose token was never issued.
	/// </summary>
	/// <value>
	/// <see langword="true"/> when the substrate has an artifact that exists before it carries a value — a
	/// database sequence is the case that shipped a defect — and <see cref="CreateUnadvancedMarkAsync"/> is
	/// then overridden to construct it. <see langword="false"/> when the artifact and its first value are
	/// created together, so the state is unreachable through the provider.
	/// </value>
	/// <remarks>
	/// This gates <see cref="UnadvancedMark_IsReportedAsNoTokenAndRefusesEveryToken"/>. Declaring it is a
	/// claim about the substrate that a reader can count across the deriving suites, which is why it is a
	/// property rather than a silent absence.
	/// </remarks>
	protected virtual bool StoreCanHoldAnUnadvancedMark => false;

	/// <summary>Creates the actual provider before each test instance.</summary>
	public async ValueTask InitializeAsync() => Provider = await CreateProviderAsync().ConfigureAwait(false);

	/// <summary>Releases provider resources after each test instance.</summary>
	public async ValueTask DisposeAsync() => await CleanupAsync().ConfigureAwait(false);

	/// <summary>
	/// A fencing token is strictly positive, so no token can ever be read back from a resource for which
	/// none was issued.
	/// </summary>

	public virtual async Task GetTokenAsync_ReturnsNull_WhenNoTokenWasEverIssued()
	{
		var resourceId = NewResourceId();

		var token = await Provider.GetTokenAsync(resourceId, TestCancellationToken)
			.ConfigureAwait(false);

		Require(token is null,
			"no token was ever issued for this resource, and the contract distinguishes that from a value: "
			+ "a provider reporting a number here licenses a fence against a mark nobody established");
	}

	/// <summary>
	/// A resource for which no token was ever issued refuses every token presented against it.
	/// </summary>
	/// <param name="presented"> The token presented to the fence. </param>
	/// <remarks>
	/// <para>
	/// Zero and one are not arbitrary members of this set. They are the exact values the three shipped
	/// inversions admitted: a sequence column that reports its START WITH value for a never-drawn sequence,
	/// an unreadable payload rendered as zero, and a native transition counter of zero reported as a real
	/// mark. <see cref="long.MaxValue"/> is here because the failure is an inversion rather than an
	/// off-by-one — under <c>token &gt;= mark</c> a low mark admits the top of the domain just as readily as
	/// the bottom, so a fabricated token is admitted too.
	/// </para>
	/// </remarks>







	public virtual async Task ValidateTokenAsync_RefusesEveryToken_WhenNoTokenWasEverIssued(long presented)
	{
		var resourceId = NewResourceId();

		var admitted = await Provider.ValidateTokenAsync(resourceId, presented, TestCancellationToken)
			.ConfigureAwait(false);

		Require(!admitted,
			$"no token was ever issued for this resource, so the fence must refuse {presented} as it must "
			+ "refuse every other value; a provider that renders the absent mark as a low number admits "
			+ "everything at or above it, which inverts the one state that must admit nothing");
	}

	/// <summary>
	/// A resource whose mark-bearing artifact exists but was never advanced is still never-issued, so it
	/// too refuses every token.
	/// </summary>
	/// <remarks>
	/// <para>
	/// THIS IS A SECOND, DISTINCT NEVER-ISSUED STATE, and conflating it with the first is what shipped. The
	/// arms above use a resource whose store holds nothing at all, which the sequence-backed providers answer
	/// correctly whichever column they read — so a suite containing only those arms stays green under the
	/// exact mutation that caused the defect. The reachable state is narrower: the issue path creates the
	/// artifact and advances it in one batch with no enclosing transaction, so an interruption between the
	/// two leaves an artifact that exists and was never advanced. A provider reading a column that cannot
	/// express never-advanced then reports the artifact's starting value as a high-water mark, and the fence
	/// admits that value and everything above it while logging each admission as a success.
	/// </para>
	/// <para>
	/// Only a provider whose substrate has this state declares <see cref="StoreCanHoldAnUnadvancedMark"/>.
	/// For the others the state is not reachable through the provider, and manufacturing it by hand would
	/// test a path no consumer can take.
	/// </para>
	/// </remarks>

	public virtual async Task UnadvancedMark_IsReportedAsNoTokenAndRefusesEveryToken()
	{
		if (!StoreCanHoldAnUnadvancedMark)
		{
			// Not a silent return: the declaration is asserted, so a provider that claims this state and
			// then leaves the branch unreachable reddens here rather than reporting a pass.
			Require(!StoreCanHoldAnUnadvancedMark,
				"a suite declaring that its store holds an unadvanced mark must reach the arm below");
			return;
		}

		var resourceId = NewResourceId();
		await CreateUnadvancedMarkAsync(resourceId).ConfigureAwait(false);

		var read = await Provider.GetTokenAsync(resourceId, TestCancellationToken)
			.ConfigureAwait(false);
		Require(read is null,
			"the mark-bearing artifact exists but was never advanced, so no token was ever issued for this "
			+ "resource and the read must say so rather than report the artifact's starting value");

		foreach (var presented in UnadvancedPresentedTokens)
		{
			var admitted = await Provider
				.ValidateTokenAsync(resourceId, presented, TestCancellationToken)
				.ConfigureAwait(false);

			Require(!admitted,
				$"no token was ever issued for this resource, so the fence must refuse {presented}; reading "
				+ "a starting value here turns an unfenced resource into one that reports itself fenced");
		}
	}

	/// <summary>
	/// An issued token is strictly positive, because zero is the contract's representation of unset.
	/// </summary>

	public virtual async Task EstablishedToken_IsStrictlyPositive()
	{
		var resourceId = NewResourceId();

		var token = await EstablishTokenAsync(resourceId).ConfigureAwait(false);

		Require(token > 0L,
			"zero is unset and never a token, so a provider returning it hands the caller a mark that "
			+ "validates every non-negative value presented against it");
	}

	/// <summary>
	/// The token read back for a resource is the one that was established for it.
	/// </summary>

	public virtual async Task GetTokenAsync_ReturnsTheEstablishedToken()
	{
		var resourceId = NewResourceId();
		var established = await EstablishTokenAsync(resourceId).ConfigureAwait(false);

		var read = await Provider.GetTokenAsync(resourceId, TestCancellationToken)
			.ConfigureAwait(false);

		// EXACT equality, not at-or-above, and that is the load-bearing part of this arm: it binds the
		// contract clause that every value the read returns must be one the issue path actually
		// returned. A provider deriving its mark from a backing store's own counter can read a value the
		// store OBLIGATED but never handed out -- a sequence whose cache reserved a block, where the
		// catalogue reports the end of the reservation. That value is above every real token, so the live
		// incumbent's pinned token compares lower and validation refuses the leader holding the lease.
		// An at-or-above assertion here would admit exactly that.
		Require(read == established,
			"the read must report the mark that was established and no other value -- both so the three "
			+ "outcomes stay distinguishable (a value, null for never-issued, a throw for "
			+ "could-not-determine) AND because a value that was never issued is above every real token, "
			+ "which refuses the incumbent rather than a stale leader");
	}

	/// <summary>
	/// A successor's token is strictly greater than its predecessor's.
	/// </summary>

	public virtual async Task EstablishToken_IsStrictlyMonotonicAcrossHandovers()
	{
		var resourceId = NewResourceId();

		var first = await EstablishTokenAsync(resourceId).ConfigureAwait(false);
		var second = await EstablishTokenAsync(resourceId).ConfigureAwait(false);

		Require(second > first,
			"a successor holding a token that is not strictly greater cannot be told apart from the "
			+ "predecessor it replaced, which is the whole distinction a fence is asked to make");
	}

	/// <summary>
	/// The incumbent's pinned token is admitted — the liveness half of the fence.
	/// </summary>
	/// <remarks>
	/// A fence that refuses everything satisfies every safety arm in this suite while being completely
	/// inert, and inaction is the cheapest way to be safe. This arm is what fails against it.
	/// </remarks>

	public virtual async Task ValidateTokenAsync_AdmitsThePinnedIncumbentToken()
	{
		var resourceId = NewResourceId();
		var incumbent = await EstablishTokenAsync(resourceId).ConfigureAwait(false);

		var admitted = await Provider.ValidateTokenAsync(resourceId, incumbent, TestCancellationToken)
			.ConfigureAwait(false);

		Require(admitted,
			"the leader holding the current token must be admitted; a fence that refuses every token "
			+ "passes every safety arm here while protecting nothing, because it permits nothing");
	}

	/// <summary>
	/// A superseded leader's token is refused once the mark has advanced past it.
	/// </summary>

	public virtual async Task ValidateTokenAsync_RefusesTheSupersededToken_AfterAHandover()
	{
		var resourceId = NewResourceId();
		var superseded = await EstablishTokenAsync(resourceId).ConfigureAwait(false);
		var successor = await EstablishTokenAsync(resourceId).ConfigureAwait(false);

		var staleAdmitted = await Provider
			.ValidateTokenAsync(resourceId, superseded, TestCancellationToken)
			.ConfigureAwait(false);
		var successorAdmitted = await Provider
			.ValidateTokenAsync(resourceId, successor, TestCancellationToken)
			.ConfigureAwait(false);

		Require(!staleAdmitted,
			"the superseded leader's token is below the advanced mark and must be refused, which is the "
			+ "split-brain write this pattern exists to reject");
		Require(successorAdmitted,
			"and the successor must still be admitted, so the refusal above is a fence rather than an "
			+ "outage");
	}

	/// <summary>
	/// Concurrent establishment never issues one token twice.
	/// </summary>

	[System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0030",
		Justification = "This conformance arm deliberately schedules eight contenders on the thread pool to race real synchronous or asynchronous token providers; it does not wrap a library operation in an asynchronous facade. Removing scheduling can serialize synchronously completed establishments and stop testing atomic minting.")]
	public virtual async Task ConcurrentEstablishes_YieldDistinctTokens()
	{
		if (!ProviderMintsItsOwnTokens)
		{
			// Not a silent skip: a read-native provider reads one stable value N times, so this arm would
			// pass without testing anything. Asserting the gate keeps the arm failing if a suite claims to
			// read natively and then leaves this unreachable branch as its only concurrency evidence.
			Require(!ProviderMintsItsOwnTokens,
				"a read-native suite owes a concurrency arm against its substrate's transition mechanism");
			return;
		}

		const int Racers = 8;
		var resourceId = NewResourceId();

		var tokens = await Task.WhenAll(
				Enumerable.Range(0, Racers)
					.Select(_ => Task.Run(() => EstablishTokenAsync(resourceId))))
			.ConfigureAwait(false);

		Require(tokens.Distinct().Count() == Racers,
			"two leaders holding the same token are indistinguishable to a fenced resource, so a mint "
			+ "that is not atomic admits the very write the token was issued to reject");
	}

	/// <summary>
	/// Builds the provider under test. Called once before each arm.
	/// </summary>
	/// <returns> A provider a consumer could resolve, never a fake of the interface. </returns>
	protected abstract Task<IFencingTokenProvider> CreateProviderAsync();

	/// <summary>
	/// Releases anything <see cref="CreateProviderAsync"/> allocated. Called once after each arm.
	/// </summary>
	/// <returns> A task that completes when cleanup is done. </returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Advances the resource's fencing mark by one leadership transition and returns the resulting token.
	/// </summary>
	/// <param name="resourceId"> The resource whose mark to advance. </param>
	/// <returns> The token now fencing the resource. </returns>
	/// <remarks>
	/// The default is a mint, which is correct for every provider that owns its counter. A provider that
	/// reads a counter its store advances overrides this to drive a real leadership transition, because
	/// a native reader exposes no mint capability. Its override must advance the real substrate.
	/// </remarks>
	protected virtual Task<long> EstablishTokenAsync(string resourceId) =>
		Provider is IFencingTokenIssuer issuer
			? issuer.IssueTokenAsync(resourceId, TestCancellationToken).AsTask()
			: throw new InvalidOperationException(
				"Override EstablishTokenAsync to drive the native transition of a read-only fencing provider.");

	/// <summary>
	/// Creates the resource's mark-bearing artifact without advancing it, reproducing the state an
	/// interrupted first issue leaves behind.
	/// </summary>
	/// <param name="resourceId"> The resource whose artifact to create. </param>
	/// <returns> A task that completes when the artifact exists and carries no value. </returns>
	/// <remarks>
	/// Overridden only by suites declaring <see cref="StoreCanHoldAnUnadvancedMark"/>. It writes to the
	/// substrate directly rather than through the provider, because the provider has no API for a
	/// half-completed issue — that is the point of the state.
	/// </remarks>
	protected virtual Task CreateUnadvancedMarkAsync(string resourceId) =>
		throw new NotSupportedException(
			$"{GetType().Name} does not declare {nameof(StoreCanHoldAnUnadvancedMark)}, so this should not "
			+ "have been called.");

	/// <summary>
	/// Gets a resource identifier no other arm or concurrently running suite uses.
	/// </summary>
	/// <returns> A fresh resource identifier. </returns>
	protected static string NewResourceId() => "fenceconf" + Guid.NewGuid().ToString("N");
	private static void Require(bool condition, string because)
	{
		if (!condition) { throw new TestFixtureAssertionException(because); }
	}
}

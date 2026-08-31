// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IDE0007 // Use implicit type (var)
#pragma warning disable IDE0270 // Null check can be simplified

using System.Data;

using Excalibur.Data;
using Excalibur.Data.Persistence;
using Excalibur.Data.Resilience;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for <see cref="IPersistenceProvider"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this kit and implement <see cref="CreateProvider(string)"/> and
/// <see cref="ExpectedProviderType"/> to verify that your <see cref="IPersistenceProvider"/>
/// implementation conforms to the contract, including its <see cref="IPersistenceProviderHealth"/>,
/// <see cref="IPersistenceProviderConnection"/> and <see cref="IPersistenceProviderTransaction"/>
/// sub-services resolved through <see cref="IPersistenceProvider.GetService"/>.
/// </para>
/// <para>
/// The kit exposes plain <c>public virtual</c> methods with no test-framework attributes; add the
/// attributes your test framework requires (for example <c>[Fact]</c>) on thin overrides in your derived
/// class.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyProviderConformanceTests : PersistenceProviderConformanceTestKit
/// {
///     protected override string ExpectedProviderType => "Document";
///
///     // Pass providerName straight through to whatever names an instance in your registration.
///     // Substituting your own value, or ignoring it, is the defect the name arms detect.
///     protected override IPersistenceProvider CreateProvider(string providerName) =>
///         new MyPersistenceProvider(new MyProviderOptions { Name = providerName, ... });
///
///     [Fact] public void Name() => Provider_ShouldHaveExpectedName();
///     [Fact] public void NameRoundTrips() => Provider_NameShouldRoundTripEveryConfiguredName();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class PersistenceProviderConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// The instance name this kit configures a provider with, and the only value its name arms accept.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <strong>The kit owns this value; a deriving suite must not supply its own.</strong> When the
	/// expected name is supplied by the same suite that supplies the implementation, the kit checks each
	/// implementation against a claim made about it in the same file — so every value passes and the arm
	/// can detect only a suite contradicting itself, never a provider diverging from the contract. A
	/// suite every provider passes demonstrates substitutability only if the thing they all pass is the
	/// same thing.
	/// </para>
	/// <para>
	/// It is deliberately not any engine's name. <see cref="IPersistenceProvider.Name"/> is the identity
	/// of a configured provider <em>instance</em> — the key it was registered under and is resolved by —
	/// never the database engine behind it. A sentinel that could coincide with an engine name would let
	/// an implementation returning its engine pass, which is precisely the confusion these arms exist to
	/// separate. Engine identity is <see cref="ExpectedProviderType"/>'s job.
	/// </para>
	/// </remarks>
	private const string ConformanceProviderName = "conformance-instance";

	/// <summary>
	/// Two further instance names, used to prove a provider reports the name it was configured with
	/// rather than a fixed value. Distinct from each other and from <see cref="ConformanceProviderName"/>.
	/// </summary>
	private static readonly string[] RoundTripNames = ["conformance-alpha", "conformance-beta"];

	/// <summary>
	/// Gets the expected provider type (for example "SQL", "Document", "KeyValue", "InMemory").
	/// </summary>
	protected abstract string ExpectedProviderType { get; }

	/// <summary>
	/// Creates a new instance of the provider under test, configured to report
	/// <paramref name="providerName"/> as its <see cref="IPersistenceProvider.Name"/>.
	/// </summary>
	/// <param name="providerName">
	/// The instance name to configure. Pass it through to whatever your provider's registration uses to
	/// name an instance; do not substitute a value of your own, and do not ignore it. Ignoring it is the
	/// defect the round-trip arm exists to catch.
	/// </param>
	/// <returns>A configured persistence provider instance.</returns>
	protected abstract IPersistenceProvider CreateProvider(string providerName);

	/// <summary>
	/// Creates a provider under the kit's own instance name, for arms that are not about naming.
	/// </summary>
	/// <returns>A configured persistence provider instance.</returns>
	/// <remarks>
	/// Deliberately not virtual: the name an arm asserts against must be the kit's, not a deriving
	/// suite's, or the name arms revert to checking each implementation against its own claim.
	/// </remarks>
	private IPersistenceProvider CreateProvider() => CreateProvider(ConformanceProviderName);

	/// <summary>
	/// Gets the optional capability contracts the provider under test is expected to offer through
	/// <see cref="IPersistenceProvider.GetService"/>. Defaults to none.
	/// </summary>
	/// <value>
	/// The capability contract types — typically <see cref="IPersistenceProviderHealth"/>,
	/// <see cref="IPersistenceProviderConnection"/> and <see cref="IPersistenceProviderTransaction"/> —
	/// that this provider must resolve. Empty by default.
	/// </value>
	/// <remarks>
	/// <para>
	/// Declining an optional capability is conformant, so the arms below return without asserting when
	/// <see cref="IPersistenceProvider.GetService"/> answers <see langword="null"/>. That is correct, and
	/// it costs the report its ability to tell "verified" from "never ran": a suite bound to a provider
	/// that declines everything reports the same green as one bound to a provider that passes every
	/// check. Nothing distinguishes them, which is how a suite can appear to cover a provider it has
	/// never actually exercised.
	/// </para>
	/// <para>
	/// Declaring a capability here says the provider under test <em>does</em> offer it, and the kit then
	/// treats a <see langword="null"/> answer as a failure rather than a licence to skip. Leaving this
	/// empty preserves the default: capabilities are optional, and a provider that declines is accepted.
	/// Declare only what the provider genuinely offers — declaring a capability it legitimately declines
	/// makes the suite fail on correct behaviour.
	/// </para>
	/// </remarks>
	protected virtual IReadOnlyCollection<Type> RequiredCapabilities => [];

	/// <summary>
	/// Resolves the optional health capability, or <see langword="null"/> when the provider declines it.
	/// </summary>
	/// <param name="provider">The provider under test.</param>
	/// <returns>The capability, or <see langword="null"/> if this provider does not offer it.</returns>
	/// <remarks>
	/// Declining is a documented, correct answer, not a conformance failure.
	/// <see cref="IPersistenceProvider.GetService"/> is specified as returning "the service instance, or
	/// <see langword="null"/> if not supported", describes these sub-services as <em>optional
	/// capabilities</em>, and supplies a default implementation returning <see langword="null"/>. A kit
	/// that treated absence as non-conformance would strengthen the precondition the interface states,
	/// and would tell a consumer whose provider is correct that it is not.
	/// </remarks>
	private IPersistenceProviderHealth? TryGetHealth(IPersistenceProvider provider) =>
		Resolve<IPersistenceProviderHealth>(provider);

	/// <summary>
	/// Resolves the optional connection capability, or <see langword="null"/> when the provider declines it.
	/// </summary>
	/// <param name="provider">The provider under test.</param>
	/// <returns>The capability, or <see langword="null"/> if this provider does not offer it.</returns>
	/// <remarks>
	/// Declining is a documented, correct answer; see <see cref="TryGetHealth"/> for the specification
	/// this follows. This capability is resolved separately from
	/// <see cref="IPersistenceProviderTransaction"/> because a provider can describe its connection and
	/// its retry policy without being able to run a transaction, and most document and key-value stores
	/// are exactly that provider.
	/// </remarks>
	private IPersistenceProviderConnection? TryGetConnection(IPersistenceProvider provider) =>
		Resolve<IPersistenceProviderConnection>(provider);

	/// <summary>
	/// Resolves the optional transaction capability, or <see langword="null"/> when the provider declines it.
	/// </summary>
	/// <param name="provider">The provider under test.</param>
	/// <returns>The capability, or <see langword="null"/> if this provider does not offer it.</returns>
	/// <remarks>
	/// <para>
	/// Declining is correct and is not asserted against; see <see cref="TryGetHealth"/> for the
	/// specification this follows.
	/// </para>
	/// <para>
	/// <strong>Offering the capability and then refusing to honour it is still a failure.</strong> A
	/// provider that returns a non-null instance here has answered "yes" to the only question a consumer
	/// can ask, so the arms below exercise it and a capability that throws on use fails them. That is
	/// deliberate: the way to decline is to return <see langword="null"/>, not to advertise the capability
	/// and throw when it is used.
	/// </para>
	/// </remarks>
	private IPersistenceProviderTransaction? TryGetTransaction(IPersistenceProvider provider) =>
		Resolve<IPersistenceProviderTransaction>(provider);

	/// <summary>
	/// Supplies a batch whose SECOND request fails, plus a way to observe whether the FIRST request
	/// persisted, so the documented all-or-nothing guarantee on
	/// <see cref="ISqlPersistenceProvider.ExecuteBatchAsync"/> can be checked. Returns
	/// <see langword="null"/> by default, which declines the arm.
	/// </summary>
	/// <param name="provider">The SQL provider under test.</param>
	/// <returns>A probe, or <see langword="null"/> when this suite cannot express a batch.</returns>
	/// <remarks>
	/// The requests are necessarily provider-specific SQL, so the kit cannot write them. Declining is
	/// correct for a suite whose provider is not a SQL provider at all -- most of them -- and costs those
	/// suites no change whatsoever.
	/// </remarks>
	protected virtual Task<(IReadOnlyList<IDataRequest<IDbConnection, object>> Requests, Func<Task<bool>> FirstEffectVisibleAsync, Func<Task<bool>> FirstEffectPersistsWhenBatchSucceedsAsync)?>
		CreateBatchAtomicityProbeAsync(ISqlPersistenceProvider provider) =>
		Task.FromResult<(IReadOnlyList<IDataRequest<IDbConnection, object>>, Func<Task<bool>>, Func<Task<bool>>)?>(null);

	/// <summary>
	/// Verifies a batch containing a failing request leaves NOTHING behind.
	/// </summary>
	/// <returns>A task that represents the asynchronous arm.</returns>
	/// <remarks>
	/// <para>
	/// <see cref="ISqlPersistenceProvider.ExecuteBatchAsync"/> documents "All requests must succeed or the
	/// entire batch will be rolled back". Nothing structural holds that: every implementation opens its own
	/// transaction by hand, so removing one leaves every other test green. This arm is the only thing that
	/// notices.
	/// </para>
	/// <para>
	/// Asserting only that the call throws would NOT be an atomicity test -- a provider that executes each
	/// request outside any transaction throws on the second one too, having already committed the first.
	/// The load-bearing assertion is that the first request left no trace.
	/// </para>
	/// </remarks>
	public virtual async Task ExecuteBatchAsync_WhenARequestFails_ShouldLeaveNothingCommitted()
	{
		using var provider = CreateProvider();

		// A plain cast, not GetService: the SQL providers implement this directly, so no capability
		// declaration stands between the suite and the arm to be forgotten.
		if (provider is not ISqlPersistenceProvider sqlProvider)
		{
			return;
		}

		var probe = await CreateBatchAtomicityProbeAsync(sqlProvider).ConfigureAwait(false);
		if (probe is null)
		{
			return;
		}

		var threw = false;
		try
		{
			_ = await sqlProvider.ExecuteBatchAsync(probe.Value.Requests, CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception) // the failing request is expected to surface; the shape is provider-specific
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				"ExecuteBatchAsync reported success for a batch containing a failing request. The caller "
				+ "cannot distinguish a batch that applied from one that did not.");
		}

		// LIVENESS FIRST. Without this the arm passes whenever the FIRST request also fails -- nothing
		// is written, nothing is visible, and "rolled back" is indistinguishable from "never ran". That is
		// how this arm passed a mutation that replaced the rollback with a commit.
		if (!await probe.Value.FirstEffectPersistsWhenBatchSucceedsAsync().ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException(
				"The probe's first request left no trace even when the whole batch SUCCEEDED, so the "
				+ "atomicity assertion below cannot observe anything: it would report a rollback for a "
				+ "request that never took effect. Fix the probe -- its requests must actually participate "
				+ "in the batch.");
		}

		if (await probe.Value.FirstEffectVisibleAsync().ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException(
				"The first request in the batch is still committed after a later request failed. "
				+ "ISqlPersistenceProvider documents the batch as all-or-nothing, so a caller that retries "
				+ "the whole batch double-applies it, and one that does not is left with a partial write "
				+ "nothing reports.");
		}
	}

	/// <summary>
	/// Resolves an optional capability, failing instead of returning <see langword="null"/> when the
	/// deriving suite has declared it in <see cref="RequiredCapabilities"/>.
	/// </summary>
	/// <typeparam name="TCapability">The capability contract to resolve.</typeparam>
	/// <param name="provider">The provider under test.</param>
	/// <returns>The capability, or <see langword="null"/> when it is neither offered nor declared.</returns>
	/// <remarks>
	/// The check lives here rather than only in a dedicated arm so that each individual arm reports the
	/// failure itself. A suite that declares a capability its provider declines does not produce one red
	/// arm and eight silent green ones; it produces a red arm everywhere the capability was needed, which
	/// is what makes the report distinguish an arm that verified something from an arm that returned early.
	/// </remarks>
	private TCapability? Resolve<TCapability>(IPersistenceProvider provider)
		where TCapability : class
	{
		var capability = provider.GetService(typeof(TCapability)) as TCapability;

		if (capability is null && IsRequired(typeof(TCapability)))
		{
			throw new TestFixtureAssertionException(
				$"{GetType().Name} declares {typeof(TCapability).Name} in RequiredCapabilities, but the "
				+ "provider under test declined it: GetService returned null. Either the suite is bound to "
				+ "a provider that does not offer this capability -- in which case the arms depending on it "
				+ "would return without asserting and report Passed having tested nothing -- or the "
				+ "declaration is wrong and should be removed.");
		}

		return capability;
	}

	/// <summary>
	/// Determines whether the deriving suite declared the specified capability as required.
	/// </summary>
	/// <param name="capability">The capability contract type.</param>
	/// <returns><see langword="true"/> if declared; otherwise, <see langword="false"/>.</returns>
	private bool IsRequired(Type capability)
	{
		foreach (var declared in RequiredCapabilities)
		{
			if (declared == capability)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Verifies the provider exposes a non-empty <see cref="IPersistenceProvider.Name"/>.</summary>
	public virtual void Provider_ShouldHaveNonNullName()
	{
		using var provider = CreateProvider();

		if (string.IsNullOrEmpty(provider.Name))
		{
			throw new TestFixtureAssertionException("Expected Name to be non-null and non-empty.");
		}
	}

	/// <summary>
	/// Verifies <see cref="IPersistenceProvider.Name"/> reports the instance name the kit configured.
	/// </summary>
	/// <remarks>
	/// The expected value is the kit's, not the deriving suite's, so this is a substitution check: every
	/// implementation is held to the same requirement and one that ignores its configured name fails for
	/// the same reason in every suite.
	/// </remarks>
	public virtual void Provider_ShouldHaveExpectedName()
	{
		using var provider = CreateProvider();

		if (provider.Name != ConformanceProviderName)
		{
			throw new TestFixtureAssertionException(
				$"Expected Name '{ConformanceProviderName}' -- the instance name this kit configured -- but "
				+ $"was '{provider.Name}'. Name is the identity of a configured provider instance, the key "
				+ "it was registered under; it is not the database engine, which is ProviderType's job. A "
				+ "provider that reports its engine here cannot be resolved by the key a host registered it "
				+ "under, and two instances of the same engine become indistinguishable.");
		}
	}

	/// <summary>
	/// LIVENESS: verifies <see cref="IPersistenceProvider.Name"/> reports whatever instance name it was
	/// configured with, not a fixed value.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The equality arm above is passed by a provider that returns a constant which happens to equal the
	/// kit's sentinel. This arm is the liveness counterpart: it configures two further, distinct names and
	/// requires each to come back. A provider that hardcodes anything at all — its engine, its type, the
	/// sentinel itself — returns the same value for both and fails here.
	/// </para>
	/// <para>
	/// This is the round-trip law that makes a name useful: a host registers N instances of one engine
	/// under N keys and resolves each by its key. A provider that cannot round-trip its configured name
	/// collapses those N instances into one, and every resolution after the first returns the wrong store.
	/// </para>
	/// </remarks>
	public virtual void Provider_NameShouldRoundTripEveryConfiguredName()
	{
		foreach (var configuredName in RoundTripNames)
		{
			using var provider = CreateProvider(configuredName);

			if (provider.Name != configuredName)
			{
				throw new TestFixtureAssertionException(
					$"Configured this provider instance with the name '{configuredName}', but it reports "
					+ $"'{provider.Name}'. Name must round-trip the configured instance identity; a "
					+ "provider that substitutes a value of its own cannot be resolved by the key a host "
					+ "registered it under.");
			}
		}

		using var first = CreateProvider(RoundTripNames[0]);
		using var second = CreateProvider(RoundTripNames[1]);

		// Reported separately: two instances agreeing is the constant-returning defect specifically, and
		// it is worth naming rather than leaving the reader to infer it from the loop above.
		if (first.Name == second.Name)
		{
			throw new TestFixtureAssertionException(
				$"Two provider instances configured with different names ('{RoundTripNames[0]}' and "
				+ $"'{RoundTripNames[1]}') both report '{first.Name}', so Name is a constant rather than "
				+ "this instance's identity. A host registering several instances of one engine cannot "
				+ "tell them apart.");
		}
	}

	/// <summary>
	/// Verifies <see cref="IPersistenceProvider.Name"/> is readable and unchanged before initialization
	/// and after disposal.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Name is total and stable for the object's lifetime: it never throws, and it is valid before the
	/// provider has been initialized and after it has been disposed. That is what lets diagnostics,
	/// logging and error reporting name the provider on the paths where it is least healthy — a provider
	/// whose name becomes unavailable once disposed cannot be identified in the teardown failure that
	/// needed it most.
	/// </para>
	/// <para>
	/// The post-initialization point is deliberately not exercised here: initializing requires
	/// provider-specific options and live infrastructure, which would make this arm untestable for
	/// implementations that are otherwise conformant. The two endpoints it does bind — before any
	/// initialization and after disposal — are the ones an implementation caching a name from a live
	/// connection actually gets wrong.
	/// </para>
	/// </remarks>
	public virtual async Task Provider_NameShouldBeStableAcrossLifecycle()
	{
		var provider = CreateProvider();

		var beforeInitialization = provider.Name;

		if (string.IsNullOrEmpty(beforeInitialization))
		{
			throw new TestFixtureAssertionException(
				"Name was null or empty on a provider that has not been initialized. Name is total and "
				+ "must be readable before initialization -- a host resolves providers by name before any "
				+ "of them has connected to anything.");
		}

		await provider.DisposeAsync().ConfigureAwait(false);

		string afterDisposal;
		try
		{
			afterDisposal = provider.Name;
		}
		catch (ObjectDisposedException ex)
		{
			throw new TestFixtureAssertionException(
				"Name threw ObjectDisposedException after the provider was disposed. Name is total and "
				+ "never throws: teardown and disposal-failure diagnostics read it precisely when the "
				+ "provider is no longer usable.",
				ex);
		}

		if (!string.Equals(afterDisposal, beforeInitialization, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"Name changed across the provider's lifetime: '{beforeInitialization}' before "
				+ $"initialization, '{afterDisposal}' after disposal. Name identifies the instance and is "
				+ "stable for its lifetime; a name that shifts cannot be correlated across the logs of a "
				+ "single provider.");
		}
	}

	/// <summary>Verifies the provider exposes a non-empty <see cref="IPersistenceProvider.ProviderType"/>.</summary>
	public virtual void Provider_ShouldHaveNonNullProviderType()
	{
		using var provider = CreateProvider();

		if (string.IsNullOrEmpty(provider.ProviderType))
		{
			throw new TestFixtureAssertionException("Expected ProviderType to be non-null and non-empty.");
		}
	}

	/// <summary>Verifies <see cref="IPersistenceProvider.ProviderType"/> matches <see cref="ExpectedProviderType"/>.</summary>
	public virtual void Provider_ShouldHaveExpectedProviderType()
	{
		using var provider = CreateProvider();

		if (provider.ProviderType != ExpectedProviderType)
		{
			throw new TestFixtureAssertionException(
				$"Expected ProviderType '{ExpectedProviderType}' but was '{provider.ProviderType}'.");
		}
	}

	/// <summary>Verifies the connection service exposes a non-null connection string.</summary>
	public virtual void Provider_ShouldHaveNonNullConnectionString()
	{
		using var provider = CreateProvider();
		var connection = TryGetConnection(provider);

		if (connection is null)
		{
			// Declined, which the interface documents as correct. Nothing to verify here.
			return;
		}

		if (connection.ConnectionString is null)
		{
			throw new TestFixtureAssertionException("Expected a non-null ConnectionString.");
		}
	}

	/// <summary>Verifies the connection service exposes a non-null <see cref="IDataRequestRetryPolicy"/>.</summary>
	public virtual void Provider_ShouldHaveNonNullRetryPolicy()
	{
		using var provider = CreateProvider();
		var connection = TryGetConnection(provider);

		if (connection is null)
		{
			// Declined, which the interface documents as correct. Nothing to verify here.
			return;
		}

		if (connection.RetryPolicy is null)
		{
			throw new TestFixtureAssertionException("Expected a non-null RetryPolicy.");
		}
	}

	/// <summary>Verifies <see cref="IPersistenceProviderTransaction.CreateTransactionScope"/> returns a non-null scope.</summary>
	public virtual void CreateTransactionScope_ShouldReturnNonNullScope()
	{
		using var provider = CreateProvider();
		var transaction = TryGetTransaction(provider);

		if (transaction is null)
		{
			// Declined, which the interface documents as correct. Nothing to verify here.
			return;
		}

		using var scope = transaction.CreateTransactionScope();

		if (scope is null)
		{
			throw new TestFixtureAssertionException("Expected a non-null transaction scope.");
		}
	}

	/// <summary>Verifies creating a transaction scope with an isolation level returns a non-null scope.</summary>
	public virtual void CreateTransactionScope_WithIsolationLevel_ShouldReturnScope()
	{
		using var provider = CreateProvider();
		var transaction = TryGetTransaction(provider);

		if (transaction is null)
		{
			// Declined, which the interface documents as correct. Nothing to verify here.
			return;
		}

		using var scope = transaction.CreateTransactionScope(IsolationLevel.Serializable);

		if (scope is null)
		{
			throw new TestFixtureAssertionException("Expected a non-null transaction scope.");
		}
	}

	/// <summary>Verifies creating a transaction scope with a timeout returns a non-null scope.</summary>
	public virtual void CreateTransactionScope_WithTimeout_ShouldReturnScope()
	{
		using var provider = CreateProvider();
		var transaction = TryGetTransaction(provider);

		if (transaction is null)
		{
			// Declined, which the interface documents as correct. Nothing to verify here.
			return;
		}

		using var scope = transaction.CreateTransactionScope(timeout: TimeSpan.FromMinutes(5));

		if (scope is null)
		{
			throw new TestFixtureAssertionException("Expected a non-null transaction scope.");
		}
	}

	/// <summary>
	/// THE ONLY ARM IN THIS KIT THAT REACHES THE DATABASE. Every other arm asserts shape - a name, a type,
	/// a non-null scope - and a provider that throws on every single operation satisfies all of them.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This exists because that is not hypothetical: two shipped providers threw on every operation while
	/// roughly 119,000 tests passed, and this suite - wired to a real emulator fixture, with a ten-minute
	/// container timeout and a hard availability requirement - completed in 57 milliseconds without the
	/// container ever starting, because no arm asked the provider to do anything.
	/// </para>
	/// <para>
	/// Nothing here calls <c>InitializeAsync</c>, deliberately. Readiness established at construction is
	/// sanctioned by the health contract, so a provider that requires an explicit initialize before it can
	/// answer is the defect this arm is for. The provider is built exactly as its own DI extension builds
	/// it, and is then asked a question only the database can answer.
	/// </para>
	/// </remarks>
	/// <returns> A task representing the asynchronous operation. </returns>
	public virtual async Task TestConnectionAsync_ShouldReachTheDatabase_WithoutAnExplicitInitialize()
	{
		using var provider = CreateProvider();
		var health = TryGetHealth(provider);

		if (health is null)
		{
			// Declined, which the interface documents as correct. A suite that needs this arm to mean
			// something declares IPersistenceProviderHealth in RequiredCapabilities, and Resolve then fails
			// here rather than letting the arm return green having tested nothing.
			return;
		}

		var reachable = await health.TestConnectionAsync(CancellationToken.None).ConfigureAwait(false);

		if (!reachable)
		{
			throw new TestFixtureAssertionException(
				$"{GetType().Name}: the provider did not reach its database as its own DI extension constructs "
				+ "it, with nothing having called InitializeAsync. That means either the store cannot connect "
				+ "at all -- the failure mode every shape-only arm in this kit is blind to -- or it connects "
				+ "only after an explicit initialize the supported registration never performs.");
		}
	}

	/// <summary>Verifies <see cref="IPersistenceProviderHealth.GetMetricsAsync"/> returns a non-null dictionary.</summary>
	public virtual async Task GetMetricsAsync_ShouldReturnNonNullDictionary()
	{
		using var provider = CreateProvider();
		var health = TryGetHealth(provider);

		if (health is null)
		{
			// Declined, which the interface documents as correct. Nothing to verify here.
			return;
		}

		var metrics = await health.GetMetricsAsync(CancellationToken.None).ConfigureAwait(false);

		if (metrics is null)
		{
			throw new TestFixtureAssertionException("Expected a non-null metrics dictionary.");
		}
	}

	/// <summary>The metric keys every provider must emit; ordinal, so casing is part of the contract.</summary>
	private static readonly string[] RequiredMetricKeys = ["Provider", "Name", "IsAvailable"];

	/// <summary>
	/// Verifies the metrics dictionary carries the keys a consumer can attribute a measurement by.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>Provider</c> names the engine and <c>Name</c> the configured instance. A host that runs several
	/// instances of one engine cannot tell their measurements apart from <c>Provider</c> alone, so a
	/// payload omitting <c>Name</c> is unattributable — which is what the metrics exist for.
	/// <c>IsAvailable</c> says whether the measurement was taken against a reachable provider at all.
	/// </para>
	/// <para>
	/// The keys are compared with an ordinal comparer, so casing is part of the contract.
	/// </para>
	/// </remarks>
	public virtual async Task GetMetricsAsync_ShouldContainProviderKey()
	{
		using var provider = CreateProvider();
		var health = TryGetHealth(provider);

		if (health is null)
		{
			// Declined, which the interface documents as correct. Nothing to verify here.
			return;
		}

		var metrics = await health.GetMetricsAsync(CancellationToken.None).ConfigureAwait(false);

		if (metrics is null)
		{
			throw new TestFixtureAssertionException("Expected a metrics dictionary, but the provider returned null.");
		}

		var missing = RequiredMetricKeys
			.Where(key => !metrics.ContainsKey(key))
			.ToList();

		if (missing.Count > 0)
		{
			throw new TestFixtureAssertionException(
				$"The metrics dictionary is missing {string.Join(", ", missing)}. 'Provider' names the engine, "
				+ "'Name' the configured instance (a host running several instances of one engine cannot "
				+ "attribute a measurement without it), and 'IsAvailable' says whether the provider was "
				+ "reachable when the measurement was taken. Keys are ordinal, so casing is part of the contract.");
		}
	}

	/// <summary>Verifies <see cref="IDisposable.Dispose"/> does not throw.</summary>
	public virtual void Dispose_ShouldNotThrow()
	{
		var provider = CreateProvider();

		// A failure surfaces as an unhandled exception failing the test.
		provider.Dispose();
	}

	/// <summary>Verifies disposing the provider multiple times does not throw (idempotent disposal).</summary>
	public virtual void Dispose_CalledMultipleTimes_ShouldNotThrow()
	{
		var provider = CreateProvider();

		provider.Dispose();
		provider.Dispose();
	}

	/// <summary>Verifies <see cref="IAsyncDisposable.DisposeAsync"/> does not throw.</summary>
	public virtual async Task DisposeAsync_ShouldNotThrow()
	{
		var provider = CreateProvider();

		await provider.DisposeAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Verifies <see cref="IPersistenceProviderHealth.IsAvailable"/> reports true while the provider is
	/// usable and false once it is disposed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The disposal half alone is a safety-only assertion, and safety-only assertions about a boolean are
	/// satisfied by a provider that never returns true at all. One gated on an explicit
	/// <c>InitializeAsync</c> — which nothing in this kit calls, deliberately — passes it having
	/// demonstrated nothing: it was already false before the disposal the arm is named for.
	/// </para>
	/// <para>
	/// The liveness half is what makes the safety half mean something: it establishes the true state so the
	/// transition this arm is named for is observable at all. Readiness-without-an-explicit-initialize is
	/// pinned separately by <c>TestConnectionAsync_ShouldReachTheDatabase_WithoutAnExplicitInitialize</c>;
	/// this arm verifies disposal, and re-deriving readiness from a probe-latched flag would only fail
	/// every provider that latches <c>IsAvailable</c> in its probe rather than in its constructor.
	/// </para>
	/// </remarks>
	public virtual void IsAvailable_AfterDispose_ShouldBeFalse()
	{
		var provider = CreateProvider();
		var health = TryGetHealth(provider);

		if (health is null)
		{
			// Declined, which the interface documents as correct. Nothing to verify here.
			return;
		}

		// LIVENESS: without this, a provider whose IsAvailable is permanently false passes the arm below.
		// IsAvailable is LATCHED BY THE HEALTH PROBE rather than set at construction, so it must be probed
		// before it can report true. Probing here is what makes the true -> false transition observable; it
		// does not soften the arm, which still fails unless that transition actually happens.
		var reachable = health.TestConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();

		if (!reachable || !health.IsAvailable)
		{
			provider.Dispose();

			throw new TestFixtureAssertionException(
				$"{GetType().Name}: the provider reports IsAvailable false after a health probe against the "
				+ "provider its own DI extension constructs. The after-disposal assertion below would then "
				+ "pass without observing any transition, so this arm would certify a provider that is never "
				+ "available at all.");
		}

		provider.Dispose();

		if (health.IsAvailable)
		{
			throw new TestFixtureAssertionException("Expected IsAvailable to be false after disposal.");
		}
	}
	/// <summary>Verifies the provider is assignable to <see cref="IDisposable"/>.</summary>
	public virtual void Provider_ShouldImplementIDisposable()
	{
		using var provider = CreateProvider();

		if (provider is not IDisposable)
		{
			throw new TestFixtureAssertionException("Expected the provider to implement IDisposable.");
		}
	}

	/// <summary>Verifies the provider is assignable to <see cref="IAsyncDisposable"/>.</summary>
	public virtual void Provider_ShouldImplementIAsyncDisposable()
	{
		using var provider = CreateProvider();

		if (provider is not IAsyncDisposable)
		{
			throw new TestFixtureAssertionException("Expected the provider to implement IAsyncDisposable.");
		}
	}

	/// <summary>
	/// The optional capability contracts this kit knows how to exercise. A provider offering one of these
	/// while its suite leaves it undeclared is what
	/// <see cref="ConformanceSuite_ShouldDeclareEveryCapabilityTheProviderOffers"/> reports.
	/// </summary>
	private static readonly Type[] KnownOptionalCapabilities =
	[
		typeof(IPersistenceProviderHealth),
		typeof(IPersistenceProviderConnection),
		typeof(IPersistenceProviderTransaction),
	];

	/// <summary>
	/// Verifies this suite declared every optional capability its provider actually offers.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The inverse of <see cref="Provider_ShouldOfferRequiredCapabilities"/>, and the one that catches the
	/// failure nothing else can see. Declining an optional capability is conformant, so the arms that use
	/// one return without asserting when <see cref="IPersistenceProvider.GetService"/> answers
	/// <see langword="null"/>. That is correct — but it means a suite which never declared a capability
	/// its provider DOES offer reports Passed on every arm that needed it, having executed no assertion at
	/// all. The report cannot distinguish that from a suite which verified everything.
	/// </para>
	/// <para>
	/// So the check is inverted: rather than asking whether the provider meets the declaration, ask
	/// whether the declaration covers the provider. An undeclared capability the provider offers is a
	/// suite that is silently testing less than it appears to.
	/// </para>
	/// <para>
	/// This cannot fire on a provider that genuinely offers nothing — there is then nothing to declare —
	/// so it costs a legitimately minimal provider nothing, and needs no change to the default. That
	/// matters: making declaration mandatory by default would fail correct implementations, because the
	/// interface documents declining as the right answer for a capability a provider does not support.
	/// </para>
	/// </remarks>
	public virtual void ConformanceSuite_ShouldDeclareEveryCapabilityTheProviderOffers()
	{
		using var provider = CreateProvider();

		var undeclared = new List<string>();

		foreach (var capability in KnownOptionalCapabilities)
		{
			if (IsRequired(capability))
			{
				continue;
			}

			if (provider.GetService(capability) is not null)
			{
				undeclared.Add(capability.Name);
			}
		}

		if (undeclared.Count > 0)
		{
			throw new TestFixtureAssertionException(
				$"{GetType().Name} does not declare {undeclared.Count} capability/capabilities its provider "
				+ "actually offers, so every arm that needs one returned early and reported Passed without "
				+ "asserting anything. The suite is verifying less than its result suggests. Add these to "
				+ $"RequiredCapabilities: {string.Join(", ", undeclared)}.");
		}
	}

	/// <summary>
	/// Verifies the provider under test offers every capability declared in
	/// <see cref="RequiredCapabilities"/>.
	/// </summary>
	/// <remarks>
	/// The single diagnostic for the declaration: it names every declared capability the provider
	/// declined, rather than leaving that to be inferred from whichever arms happened to need one. A
	/// suite that declares nothing passes this arm trivially, which is the intended default -- declining
	/// an optional capability is conformant.
	/// </remarks>
	public virtual void Provider_ShouldOfferRequiredCapabilities()
	{
		using var provider = CreateProvider();

		var unmet = new List<string>();

		foreach (var capability in RequiredCapabilities)
		{
			var resolved = provider.GetService(capability);

			if (resolved is null)
			{
				unmet.Add($"{capability.Name} (declined: GetService returned null)");
			}
			else if (!capability.IsInstanceOfType(resolved))
			{
				unmet.Add(
					$"{capability.Name} (GetService returned {resolved.GetType().Name}, which is not "
					+ "assignable to it)");
			}
		}

		if (unmet.Count > 0)
		{
			throw new TestFixtureAssertionException(
				$"{GetType().Name} declares {RequiredCapabilities.Count} required capability contract(s), "
				+ $"but the provider under test does not offer {unmet.Count} of them. Arms that resolve a "
				+ "declined capability return without asserting and report Passed having tested nothing, so "
				+ "the declaration exists to make that loud. Unmet: "
				+ string.Join(", ", unmet));
		}
	}

}

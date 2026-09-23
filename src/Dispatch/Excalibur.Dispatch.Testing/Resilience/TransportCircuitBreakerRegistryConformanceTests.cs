// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Testing.Resilience;

/// <summary>
/// The contract every <see cref="ITransportCircuitBreakerRegistry"/> implementation must satisfy,
/// expressed once so that no implementation is only ever tested against its own expectations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a shared suite and not per-implementation tests.</b> This family shipped a strengthened
/// precondition and a divergent postcondition at the same time, in two implementations, because nothing
/// asked them the same questions. Per-implementation tests cannot catch that by construction: each one
/// encodes the behaviour its own author intended, so two implementations disagreeing looks exactly like
/// two implementations passing.
/// </para>
/// <para>
/// <b>The substitution is invisible at the call site, which is what makes the parity load-bearing.</b>
/// Adding the resilience package removes the core registry from the container and registers the other in
/// its place. A consumer performs that swap by editing a project file, never a source file, so any
/// behavioural difference between the two arrives without a diff to read.
/// </para>
/// <para>
/// <b>The contract.</b> <c>GetOrCreate</c> requires a non-blank transport name and a non-null options
/// object; it returns a policy that starts closed, opens on the failure condition the options describe,
/// and admits a probe once the break duration has elapsed. It throws only
/// <see cref="ArgumentException"/> and <see cref="ArgumentNullException"/>. Every implementation accepts
/// every value the options type admits — no implementation may narrow a range that the options type and
/// its validator did not narrow at startup, and none may silently substitute a value it was handed.
/// </para>
/// <para>
/// Derive once per implementation and wire each arm as a test in the deriving suite; the kit ships no
/// test-framework attributes so that a consumer is not forced onto our runner.
/// </para>
/// </remarks>
public abstract class TransportCircuitBreakerRegistryConformanceTests
{
	/// <summary>
	/// Creates the registry under test, configured as a consumer would receive it.
	/// </summary>
	/// <returns>The registry implementation this suite binds.</returns>
	protected abstract ITransportCircuitBreakerRegistry CreateRegistry();

	/// <summary>
	/// Refuses an arm's own options when startup validation would reject them.
	/// </summary>
	/// <param name="description">What the options are for, named in the failure.</param>
	/// <param name="options">The options an arm intends to hand to the registry.</param>
	/// <remarks>
	/// An arm that certifies implementations against a configuration a consumer's host would refuse to
	/// start with is testing a state nobody can reach, and a failure it reports is unactionable. The
	/// validator is the authority on what is admissible — the same authority the parity arm defers to —
	/// so every arm that builds its own options asks it first rather than restating the invariant by
	/// hand and drifting from it. This has already happened once: an arm shortened a break duration and
	/// left the operation timeout at a default that is longer, which the cross-property rule rejects.
	/// </remarks>
	private static void RequireOptionsStartupValidationAccepts(
		string description,
		CircuitBreakerOptions options)
	{
		var admissibility = new CircuitBreakerOptionsValidator().Validate(name: null, options);

		if (admissibility.Failed)
		{
			throw new InvalidOperationException(
				$"This suite's OWN options for \"{description}\" are rejected by "
				+ $"CircuitBreakerOptionsValidator: {admissibility.FailureMessage}. Fix the arm's "
				+ "configuration, not the implementation — no consumer could boot with these values, so "
				+ "nothing an implementation does with them can be a conformance failure.");
		}
	}

	/// <summary>
	/// SAFETY. A blank transport name is refused, and refused as the declared exception type.
	/// </summary>
	public virtual void VerifyABlankTransportNameIsRefused()
	{
		var registry = CreateRegistry();

		foreach (var name in new[] { string.Empty, "   " })
		{
			var threw = false;

			try
			{
				_ = registry.GetOrCreate(name);
			}
			catch (ArgumentException)
			{
				threw = true;
			}

			if (!threw)
			{
				throw new InvalidOperationException(
					$"GetOrCreate accepted the blank transport name \"{name}\". A registry keyed on a blank "
					+ "name collapses every unnamed caller onto one shared breaker, so one transport's "
					+ "failures open another's circuit.");
			}
		}
	}

	/// <summary>
	/// SAFETY. A null options object is refused, and refused as the declared exception type.
	/// </summary>
	public virtual void VerifyNullOptionsAreRefused()
	{
		var registry = CreateRegistry();

		var threw = false;

		try
		{
			_ = registry.GetOrCreate("conformance", null!);
		}
		catch (ArgumentNullException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new InvalidOperationException(
				"GetOrCreate accepted null options. An implementation that substitutes its own defaults "
				+ "here gives the caller a breaker configured differently from the one they asked for, "
				+ "with nothing in the call to say so.");
		}
	}

	/// <summary>
	/// THE PARITY ARM. Every value the options type admits is accepted by every implementation.
	/// </summary>
	/// <remarks>
	/// This is the arm the family was missing. A provider that narrows a range the options type and its
	/// validator did not narrow is only detectable by asking every implementation the same question with
	/// the same values — which is exactly what no per-implementation suite does.
	/// </remarks>
	public virtual void VerifyEveryValueTheOptionsTypeAdmitsIsAccepted()
	{
		var registry = CreateRegistry();

		var admissible = new (string Description, CircuitBreakerOptions Options)[]
		{
			("the type's own defaults", new CircuitBreakerOptions()),
			// Every case below is checked against the validator before it is used — see the gate in the
			// loop. Hand-maintaining "what the invariant admits" drifted FOUR times in one session: a
			// Range added, the same Range removed in favour of a validator-only bound, a floor nobody had
			// read, and a cross-property rule (OperationTimeout < BreakDuration) that makes a case
			// unbootable without changing any single field. The list is a starting set, not the authority.
			("the smallest thresholds the invariant admits", new CircuitBreakerOptions
			{
				ConsecutiveFailureThreshold = 1,
				MinimumThroughput = 2,
				FailureRatio = 0.01,
				SamplingDuration = TimeSpan.FromMilliseconds(500),
				BreakDuration = TimeSpan.FromMilliseconds(500),
				OperationTimeout = TimeSpan.FromMilliseconds(100),
			}),
			("a ratio at its upper bound", new CircuitBreakerOptions
			{
				ConsecutiveFailureThreshold = 2,
				MinimumThroughput = 2,
				FailureRatio = 1.0,
			}),
			("large but legal thresholds", new CircuitBreakerOptions
			{
				ConsecutiveFailureThreshold = 1000,
				MinimumThroughput = 1000,
				SamplingDuration = TimeSpan.FromMinutes(10),
				BreakDuration = TimeSpan.FromMinutes(10),
				OperationTimeout = TimeSpan.FromMinutes(1),
			}),
		};

		var index = 0;
		var validator = new CircuitBreakerOptionsValidator();

		foreach (var (description, options) in admissible)
		{
			index++;

			// THE VALIDATOR IS THE AUTHORITY, NOT THIS LIST. A case startup validation would reject is
			// not an admissible value, and asserting that every provider must accept it turns this arm
			// into a lock against the CORRECT behaviour — which it was, twice, before this gate existed.
			var admissibility = validator.Validate(name: null, options);
			if (admissibility.Failed)
			{
				throw new InvalidOperationException(
					$"This arm's own case \"{description}\" is REJECTED by CircuitBreakerOptionsValidator: "
					+ $"{admissibility.FailureMessage}. Fix the case, not the implementation.");
			}

			try
			{
				var policy = registry.GetOrCreate($"parity-{index}", options);

				_ = policy ?? throw new InvalidOperationException(
					$"GetOrCreate returned null for {description}.");
			}
			catch (Exception ex) when (ex is ArgumentException or ArgumentNullException)
			{
				throw new InvalidOperationException(
					$"This implementation REJECTED {description}, which the options type admits and its "
					+ "validator accepts at startup. A configuration that starts on one implementation and "
					+ "throws on the other is a substitution failure a consumer cannot see: swapping the "
					+ "implementation is a project-file edit, not a source change.",
					ex);
			}
		}
	}

	/// <summary>
	/// LIVENESS. A returned policy is usable and starts closed.
	/// </summary>
	/// <remarks>
	/// Without this, every safety arm above is satisfied by a registry that returns a policy which refuses
	/// all work — "nothing was wrongly admitted" is trivially true of something that admits nothing.
	/// </remarks>
	public virtual async Task VerifyANewPolicyIsUsableAndStartsClosed()
	{
		var registry = CreateRegistry();

		var policy = registry.GetOrCreate("liveness");

		if (policy.State != CircuitState.Closed)
		{
			throw new InvalidOperationException(
				$"A freshly created policy reports {policy.State}, not Closed. A breaker that does not "
				+ "start closed rejects the first call of a healthy transport.");
		}

		var executed = await policy.ExecuteAsync(
			_ => Task.FromResult(42), CancellationToken.None).ConfigureAwait(false);

		if (executed != 42)
		{
			throw new InvalidOperationException(
				"A closed policy did not return the action's own result, so callers cannot rely on the "
				+ "breaker being transparent while healthy.");
		}
	}

	/// <summary>
	/// The registry is a REGISTRY: one name, one policy — and DIFFERENT names, different policies.
	/// </summary>
	/// <remarks>
	/// <b>What an implementation must do:</b> return the same policy instance for repeated calls with one
	/// name, and a different instance for a different name. Both halves are obligations, and the second
	/// is the one an implementation can fail without noticing: a registry that returns one policy for
	/// every name still satisfies "same name, same policy", while collapsing every transport onto a
	/// single breaker — so one dependency's failures open the circuit in front of a dependency that is
	/// perfectly healthy.
	/// </remarks>
	public virtual void VerifyTheSameNameReturnsTheSamePolicy()
	{
		var registry = CreateRegistry();

		var first = registry.GetOrCreate("shared");
		var second = registry.GetOrCreate("shared");

		if (!ReferenceEquals(first, second))
		{
			throw new InvalidOperationException(
				"Two calls for the same transport name returned different policies. Each caller then "
				+ "accumulates failures against its own breaker, so the circuit never opens no matter how "
				+ "many failures the transport actually produced.");
		}

		if (!ReferenceEquals(registry.TryGet("shared"), first))
		{
			throw new InvalidOperationException(
				"TryGet did not return the policy GetOrCreate created for the same name.");
		}

		var other = registry.GetOrCreate("separate");

		if (ReferenceEquals(other, first))
		{
			throw new InvalidOperationException(
				"Two DIFFERENT transport names returned the very same policy, so every transport shares "
				+ "one breaker. One dependency's failures then open the circuit in front of a dependency "
				+ "that is perfectly healthy, and the isolation this type exists to provide is absent.");
		}
	}

	/// <summary>
	/// Transport names are matched WITHOUT REGARD TO CASE: one transport, one circuit, however it is spelled.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>What an implementation must do:</b> compare transport names without regard to case, so that one
	/// transport is protected by one circuit however its name is spelled.
	/// </para>
	/// <para>
	/// A transport name reaches this registry from configuration, from a constant, and from the
	/// dispatcher, and nothing forces those three to agree on capitalisation. A registry that treats
	/// "Kafka" and "kafka" as two transports hands out two circuits, so each one observes roughly half
	/// the failures and neither reaches the threshold it was configured with. The failure is silent and
	/// it is worst exactly when it matters: under a broad outage, when every spelling is failing at once
	/// and none of them trips.
	/// </para>
	/// </remarks>
	public virtual void VerifyTransportNamesAreMatchedWithoutRegardToCase()
	{
		var registry = CreateRegistry();

		var lower = registry.GetOrCreate("case-probe");
		var mixed = registry.GetOrCreate("Case-Probe");

		if (!ReferenceEquals(lower, mixed))
		{
			throw new InvalidOperationException(
				"\"case-probe\" and \"Case-Probe\" were given different circuits, so one transport is "
				+ "protected by two breakers. Each sees only the calls that used its spelling, so a "
				+ "transport that is failing every call can sit below the configured threshold on both "
				+ "and never open at all.");
		}

		if (!ReferenceEquals(registry.TryGet("CASE-PROBE"), lower))
		{
			throw new InvalidOperationException(
				"TryGet did not find the circuit under a differently-cased spelling of the name it was "
				+ "created with, so a caller cannot retrieve a circuit it has already been handed.");
		}
	}

	/// <summary>
	/// A bounded registry never hands one circuit to two transports.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A memory bound on a message-derived key is legitimate and is not what this arm objects to. What it
	/// forbids is the REMEDY of substituting a shared overflow circuit: two transports then accumulate
	/// failures against one breaker, so one transport's failures open another's circuit, the later caller
	/// silently runs on the FIRST overflowing caller's thresholds, and nobody is told. A circuit breaker
	/// shared between transports is not a circuit breaker for either.
	/// </para>
	/// <para>
	/// CAP-AGNOSTIC BY CONSTRUCTION. The arm does not know or assert where a cap sits — an implementation
	/// with no cap passes trivially, and one that refuses honestly passes too. It fails only on the shape
	/// that is wrong at any threshold: two distinct names, both calls reporting success, one instance.
	/// </para>
	/// </remarks>
	public virtual void VerifyABoundedRegistryNeverSharesOneCircuitBetweenTransports()
	{
		var registry = CreateRegistry();

		// Enough to cross a four-figure cap, bounded so an uncapped implementation still terminates.
		const int Beyond = 1100;

		var byName = new Dictionary<string, ICircuitBreakerPolicy>(StringComparer.Ordinal);

		for (var i = 0; i < Beyond; i++)
		{
			var name = $"overflow-probe-{i}";

			ICircuitBreakerPolicy policy;

			try
			{
				policy = registry.GetOrCreate(name);
			}
			catch (Exception ex)
			{
				// THROWING IS NOT AN ACCEPTABLE BOUND, and this arm used to accept it. A circuit-breaker
				// registry is cross-cutting resilience infrastructure: making it throw converts unbounded
				// memory growth into total loss of message flow — the protection taking down the thing it
				// protects. The bound must be reached by evicting an idle circuit, or by handing back an
				// isolated policy that is simply not stored.
				throw new InvalidOperationException(
					$"GetOrCreate(\"{name}\") THREW rather than bounding the map without failing: "
					+ $"{ex.GetType().Name}. A resilience component must not fail the caller to protect "
					+ "its own memory.", ex);
			}

			var shared = byName.FirstOrDefault(existing => ReferenceEquals(existing.Value, policy));

			if (shared.Key is not null)
			{
				throw new InvalidOperationException(
					$"GetOrCreate(\"{name}\") reported SUCCESS and returned the very circuit already "
					+ $"serving \"{shared.Key}\". Those two transports now share one breaker, so one "
					+ "transport's failures open the other's, and neither caller was told. A bounded "
					+ "registry must refuse past its cap, not substitute a different transport's circuit.");
			}

			byName[name] = policy;

			// DELIBERATELY NO TryGet ASSERTION HERE. An earlier version of this arm required TryGet to
			// return the policy just handed out, and that would have FAILED THE CORRECT FIX: past the cap
			// the ruled behaviour hands back an isolated policy and does NOT store it, so TryGet answering
			// null is right rather than wrong. The below-cap history constraint is asserted by
			// VerifyTheSameNameReturnsTheSamePolicy, where storage is guaranteed. What survives at every
			// threshold, stored or not, is ISOLATION — which is the single property this arm binds.
		}
	}

	/// <summary>
	/// A name that was never created is absent rather than fabricated.
	/// </summary>
	public virtual void VerifyAnUnknownNameIsAbsentRatherThanFabricated()
	{
		var registry = CreateRegistry();

		if (registry.TryGet("never-created") is not null)
		{
			throw new InvalidOperationException(
				"TryGet fabricated a policy for a transport that was never registered, so a caller cannot "
				+ "distinguish a configured breaker from an absent one.");
		}
	}

	/// <summary>
	/// THE CONFIGURED VALUE IS THE VALUE USED — the circuit opens on the threshold it was given.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>What an implementation must do:</b> open on the threshold it was configured with, REFUSE work
	/// while open by throwing <see cref="CircuitBreakerOpenException"/>, and admit a probe once the break
	/// duration has elapsed. Reporting <see cref="CircuitState.Open"/> is not sufficient on its own — a
	/// policy that reports it and still forwards calls leaves the failing dependency taking exactly the
	/// traffic the breaker exists to stop.
	/// </para>
	/// <para>
	/// Expressed as consecutive failures with the throughput minimum set to match, because that is a
	/// configuration BOTH implementations can express. The arm asserts the transition, not the internal
	/// counter: a policy that opens after a different number of failures than it was configured for is
	/// silently substituting a value, which is the defect this family already shipped once.
	/// </para>
	/// <para>
	/// "Opened" is measured by work being REFUSED, not by the reported state. An implementation that
	/// assigns the state field without refusing anything is the failure a state assertion cannot see,
	/// and it is the more dangerous direction of the two: a breaker that reports Open while still
	/// forwarding every call gives an operator a dashboard saying the dependency is shielded while the
	/// traffic it was meant to stop is still arriving.
	/// </para>
	/// <para>
	/// The break duration is deliberately tiny so the probe half needs no wall-clock patience; the arm
	/// polls rather than sleeping a fixed span. The operation timeout is set below it because startup
	/// validation requires that ordering — the arm's own options are put through that validator first,
	/// so this suite can never certify an implementation against a configuration a consumer's host would
	/// refuse to start with.
	/// </para>
	/// </remarks>
	public virtual async Task VerifyTheCircuitOpensOnTheConfiguredThresholdThenAdmitsAProbe()
	{
		var registry = CreateRegistry();

		const int Threshold = 2;

		var options = new CircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = Threshold,
			MinimumThroughput = Threshold,
			FailureRatio = 1.0,
			SamplingDuration = TimeSpan.FromSeconds(30),
			BreakDuration = TimeSpan.FromMilliseconds(600),

			// Must be STRICTLY below BreakDuration or startup validation rejects the pair. Left at its
			// default, this arm built a configuration no consumer could boot and certified against it.
			OperationTimeout = TimeSpan.FromMilliseconds(200),
		};

		RequireOptionsStartupValidationAccepts("the open-then-probe lifecycle", options);

		var policy = registry.GetOrCreate("lifecycle", options);

		for (var i = 0; i < Threshold; i++)
		{
			try
			{
				_ = await policy.ExecuteAsync<int>(
					_ => throw new InvalidOperationException("conformance-induced failure"),
					CancellationToken.None).ConfigureAwait(false);
			}
			catch (InvalidOperationException)
			{
				// expected: the failure is the point
			}
			catch (CircuitBreakerOpenException)
			{
				// Also legitimate: the circuit may already have opened part-way through this loop, and a
				// refusal is the contract's declared response. NOTHING ELSE is caught here. A blanket
				// catch would swallow a NullReferenceException from a broken implementation and let the
				// arm carry on as though the failure had been delivered, which is how an implementation
				// that is simply crashing reads as one that is correctly refusing.
			}
		}

		// THE CIRCUIT MUST REFUSE, not merely report. Probing once here rather than reading State is
		// what separates a breaker that is protecting the dependency from one that has only said so.
		var refused = false;

		try
		{
			_ = await policy.ExecuteAsync(
				_ => Task.FromResult(0), CancellationToken.None).ConfigureAwait(false);
		}
		catch (CircuitBreakerOpenException)
		{
			refused = true;
		}

		if (!refused)
		{
			throw new InvalidOperationException(
				$"After {Threshold} consecutive failures — the threshold it was configured with — the "
				+ "circuit still ADMITTED work. Either the implementation is using a threshold other than "
				+ "the one it was given, or it opens in name only: the calls this breaker exists to stop "
				+ "are still reaching the failing dependency.");
		}

		// SECONDARY: the reported state must agree with the refusal above.
		if (policy.State == CircuitState.Closed)
		{
			throw new InvalidOperationException(
				"The circuit refused work but still reports Closed, so every operator reading its state "
				+ "is told the transport is healthy while its calls are being rejected.");
		}

		// LIVENESS of the recovery half: an open circuit that never admits a probe is a permanent outage,
		// and it satisfies every "it opened" assertion above.
		var admittedProbe = false;
		var deadline = DateTimeOffset.UtcNow.AddSeconds(5);

		while (DateTimeOffset.UtcNow < deadline)
		{
			try
			{
				_ = await policy.ExecuteAsync(
					_ => Task.FromResult(1), CancellationToken.None).ConfigureAwait(false);
				admittedProbe = true;
				break;
			}
			catch (CircuitBreakerOpenException)
			{
				// Still open; wait for the break duration to elapse. Narrow on purpose: catching every
				// exception here cannot tell "still refusing" from "throwing NullReferenceException on
				// every call", and spins out the whole window before reporting the same failure either
				// way — so the arm would name the wrong defect.
				await Task.Delay(10, CancellationToken.None).ConfigureAwait(false);
			}
		}

		if (!admittedProbe)
		{
			throw new InvalidOperationException(
				"The circuit never admitted a probe after its break duration elapsed, so it can only ever "
				+ "open — a transport that recovers stays cut off.");
		}
	}
	/// <summary>
	/// Supplies a policy whose close FAILS, or <see langword="null"/> when this implementation has no
	/// reachable failure path for a close.
	/// </summary>
	/// <returns>A policy whose <c>ResetAsync</c> will fail, or <see langword="null"/>.</returns>
	/// <remarks>
	/// <para>
	/// Override this to have <see cref="VerifyAFailedCloseSurfacesAsAFaultedTask"/> actually bind the
	/// contract. The default returns <see langword="null"/>, which the arm reports as UNVERIFIED rather
	/// than as passing — an implementation that cannot express a failing close has not demonstrated the
	/// clause, and saying so is the honest answer.
	/// </para>
	/// <para>
	/// Neither in-box registry can be made to fail a close from here: the core policy transitions under a
	/// lock and the resilience-package adapter drives a manual control, and neither exposes a failure path
	/// a conformance arm can reach without mutating the implementation. That is why this is a hook rather
	/// than something the suite arranges for you.
	/// </para>
	/// </remarks>
	protected virtual ICircuitBreakerPolicy? CreatePolicyWhoseCloseFails() => null;

	/// <summary>
	/// SAFETY. A close that FAILS surfaces as a faulted task, rather than completing successfully and
	/// leaving the failure in a log line.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> when the clause was BOUND against a failing close; <see langword="false"/>
	/// when this implementation could not produce one, so the clause is UNVERIFIED for it.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>Why this returns a value instead of just passing.</b> The interface publishes this postcondition
	/// in a doc comment that ships in the NuGet <c>.xml</c>, and until now nothing in this kit checked it —
	/// so an implementation that catches a close failure, logs it, and returns a completed task was
	/// certified as conforming, and its operator would be told a transport was back in service while every
	/// call was still rejected. An arm that quietly passed when it could not run would reproduce exactly
	/// that: a success that means "not checked". The three answers are PASSED, FAILED and NOT DETERMINED,
	/// and the third one must be distinguishable from the first.
	/// </para>
	/// <para>
	/// <b>Caller obligation.</b> Treat <see langword="false"/> as "this clause is unverified for this
	/// implementation" and record it where the implementation's guarantees are documented. Do not read it
	/// as a pass.
	/// </para>
	/// </remarks>
	public virtual async Task<bool> VerifyAFailedCloseSurfacesAsAFaultedTask()
	{
		var policy = CreatePolicyWhoseCloseFails();

		if (policy is null)
		{
			return false;
		}

		var closeFaulted = false;

		try
		{
			await policy.ResetAsync(CancellationToken.None).ConfigureAwait(false);
		}
#pragma warning disable CA1031 // Any failure is the observable outcome under test; the type is not.
		catch (Exception)
#pragma warning restore CA1031
		{
			closeFaulted = true;
		}

		if (!closeFaulted)
		{
			throw new InvalidOperationException(
				"ResetAsync completed successfully for a policy whose close FAILS. The interface states "
				+ "that a close which fails must surface as a faulted task rather than a log line, because "
				+ "an operator who issued a reset would otherwise be told the transport is back in service "
				+ "while every call is still rejected. Let the failure propagate out of ResetAsync.");
		}

		return true;
	}


	/// <summary>
	/// Reset returns an opened circuit to service — measured by work being ADMITTED, not by a field.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>What an implementation must do:</b> when the task returned by <c>ResetAsync</c> completes, the
	/// circuit must ADMIT work, and <see cref="CircuitState"/> must report
	/// <see cref="CircuitState.Closed"/>. Publishing the closed state without closing the underlying
	/// breaker does not satisfy it: the operator who issued the reset is told the transport is back in
	/// service while every call is still being rejected.
	/// </para>
	/// <para>
	/// "In service" is a statement about what the circuit DOES, so this arm executes through it. An
	/// assertion on the reported state alone is satisfied by an implementation that merely publishes
	/// <see cref="CircuitState.Closed"/> while the underlying breaker is still rejecting calls — the
	/// field and the behaviour are separate things, and only one of them is what an operator wanted when
	/// they reset the circuit. The state assertion is kept afterwards as a secondary check that the two
	/// agree; the admission is the lock.
	/// </para>
	/// </remarks>
	public virtual async Task VerifyResetReturnsAnOpenedCircuitToService()
	{
		var registry = CreateRegistry();

		var options = new CircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 1,
			MinimumThroughput = 2,
			FailureRatio = 1.0,
			BreakDuration = TimeSpan.FromMinutes(5),
		};

		RequireOptionsStartupValidationAccepts("the reset lifecycle", options);

		var policy = registry.GetOrCreate("reset", options);

		// Drive the throughput minimum, not a single failure. Implementations legitimately differ on
		// which admitted option opens the circuit first -- one honours the consecutive-failure threshold,
		// another needs the sampling window to reach MinimumThroughput -- and this arm is about RESET,
		// not about that difference. Failing enough times to open under either reading keeps it so.
		for (var i = 0; i < 2; i++)
		{
			try
			{
				_ = await policy.ExecuteAsync<int>(
					_ => throw new InvalidOperationException("conformance-induced failure"),
					CancellationToken.None).ConfigureAwait(false);
			}
			catch (InvalidOperationException)
			{
				// expected: the failure is the point
			}
			catch (CircuitBreakerOpenException)
			{
				// also expected once the circuit has opened part-way through the loop
			}
		}

		// THE PRECONDITION IS MEASURED THE SAME WAY AS THE POSTCONDITION, and for the same reason. If
		// this arm established "the circuit is open" by reading State, then against an implementation
		// that only ever assigns that field the arm would confirm an open circuit that was never
		// refusing anything, and go on to "verify" a reset that had nothing to undo. An arm is only as
		// strong as the weaker of the two observations it makes.
		var refusedBeforeReset = false;

		try
		{
			_ = await policy.ExecuteAsync(
				_ => Task.FromResult(0), CancellationToken.None).ConfigureAwait(false);
		}
		catch (CircuitBreakerOpenException)
		{
			refusedBeforeReset = true;
		}

		if (!refusedBeforeReset)
		{
			throw new InvalidOperationException(
				"The circuit is still admitting work after the failures that should have opened it, so "
				+ "this arm cannot observe a reset: there is nothing to return to service, and every "
				+ "assertion after this point would pass without the circuit ever having been open.");
		}

		if (policy.State == CircuitState.Closed)
		{
			throw new InvalidOperationException(
				"The circuit is refusing work but reports Closed, so its state and its behaviour "
				+ "disagree before the reset has even been attempted.");
		}

		await policy.ResetAsync(CancellationToken.None).ConfigureAwait(false);

		// THE LOCK IS ADMISSION OF WORK, NOT A PUBLISHED FIELD.
		//
		// The break duration above is five minutes, so nothing but the reset can put this circuit back
		// into service within the life of this arm. Executing through the policy therefore asks the only
		// question an operator actually cares about -- is the transport carrying traffic again -- and it
		// asks it of the breaker itself rather than of a value some implementation assigned. A circuit
		// that reports Closed and still rejects every call is the failure this ordering exists to catch,
		// and no assertion on State can see it.
		//
		// NOT POLLED, deliberately. The contract states the strong form: when the returned task
		// completes, the circuit is back in service. A polled wait would not be caution, it would be a
		// HOLE -- it cannot distinguish an implementation that honours the postcondition from one that
		// returns early and happens to finish within the window. Probing once, immediately after the
		// await, is what makes such an implementation fail here.
		const int ProbeValue = 7;

		var admitted = false;
		var returned = 0;

		try
		{
			returned = await policy.ExecuteAsync(
				_ =>
				{
					admitted = true;
					return Task.FromResult(ProbeValue);
				},
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException(
				$"ResetAsync completed, but the next execution was REJECTED with {ex.GetType().Name}. A "
				+ "reset that does not admit work has not returned the circuit to service: the operator "
				+ "who issued it is left waiting out the break duration, with nothing in the call to say "
				+ "the reset did not take effect.",
				ex);
		}

		if (!admitted)
		{
			throw new InvalidOperationException(
				"ResetAsync completed and the execution returned without the action ever running, so the "
				+ "circuit is neither rejecting calls nor performing them. A breaker that answers on the "
				+ "transport's behalf is worse than one that refuses, because the caller is told the work "
				+ "was done.");
		}

		if (returned != ProbeValue)
		{
			throw new InvalidOperationException(
				"The execution admitted after a reset did not return the action's own result, so a "
				+ "reset circuit is not transparent to the work it carries.");
		}

		// SECONDARY, and only meaningful because the arm above already proved the circuit is carrying
		// work: State must AGREE with the behaviour. A published value that disagreed with an admitted
		// execution would leave every operator dashboard reading the opposite of the truth.
		if (policy.State != CircuitState.Closed)
		{
			throw new InvalidOperationException(
				$"ResetAsync admitted work but still reports {policy.State}. The contract is that when the "
				+ "returned task completes the circuit is Closed, so an implementation that starts a close "
				+ "and returns before it lands does not satisfy it: an operator reading State is told the "
				+ "transport is cut off while it is in fact carrying traffic.");
		}
	}
}

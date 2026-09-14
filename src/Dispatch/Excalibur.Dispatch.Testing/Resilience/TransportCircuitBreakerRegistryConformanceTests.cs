// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	/// The registry is a REGISTRY: one name, one policy.
	/// </summary>
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
	/// Expressed as consecutive failures with the throughput minimum set to match, because that is a
	/// configuration BOTH implementations can express. The arm asserts the transition, not the internal
	/// counter: a policy that opens after a different number of failures than it was configured for is
	/// silently substituting a value, which is the defect this family already shipped once.
	/// </para>
	/// <para>
	/// The break duration is deliberately tiny so the probe half needs no wall-clock patience; the arm
	/// polls rather than sleeping a fixed span.
	/// </para>
	/// </remarks>
	public virtual async Task VerifyTheCircuitOpensOnTheConfiguredThresholdThenAdmitsAProbe()
	{
		var registry = CreateRegistry();

		const int Threshold = 2;

		var policy = registry.GetOrCreate("lifecycle", new CircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = Threshold,
			MinimumThroughput = Threshold,
			FailureRatio = 1.0,
			SamplingDuration = TimeSpan.FromSeconds(30),
			BreakDuration = TimeSpan.FromMilliseconds(600),
		});

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
			catch (Exception)
			{
				// an implementation may wrap or surface a broken-circuit exception; the state assertion
				// below is what this arm measures, not the exception type on the failing path.
			}
		}

		if (policy.State == CircuitState.Closed)
		{
			throw new InvalidOperationException(
				$"The circuit is still Closed after {Threshold} consecutive failures, which is the "
				+ "threshold it was configured with. The implementation is using a threshold other than "
				+ "the one it was given.");
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
			catch (Exception)
			{
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
	/// Reset returns an opened circuit to service.
	/// </summary>
	public virtual async Task VerifyResetReturnsAnOpenedCircuitToService()
	{
		var registry = CreateRegistry();

		var policy = registry.GetOrCreate("reset", new CircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 1,
			MinimumThroughput = 2,
			FailureRatio = 1.0,
			BreakDuration = TimeSpan.FromMinutes(5),
		});

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
			catch (Exception)
			{
				// expected
			}
		}

		if (policy.State == CircuitState.Closed)
		{
			throw new InvalidOperationException(
				"The circuit did not open after its configured single failure, so this arm cannot observe "
				+ "a reset: it would report success without the circuit ever having been open.");
		}

		await policy.ResetAsync(CancellationToken.None).ConfigureAwait(false);

		// NOT POLLED, deliberately, and the difference is the whole point of this arm.
		//
		// It used to poll for five seconds, because the two shipped implementations disagreed about what
		// Reset promised: one transitioned under a lock and held "closed on return", the other started an
		// asynchronous close and returned before it finished. Against that divergence a polled wait was
		// the honest choice -- it bound the weaker property BOTH satisfied, and asserting the strong form
		// would have produced an intermittent red rather than a deterministic one.
		//
		// The contract now states the strong form: when the returned task completes, State is Closed. So
		// the poll would no longer be caution, it would be a HOLE -- a polled assertion cannot distinguish
		// an implementation that honours the postcondition from one that returns early and happens to
		// finish within the window, which is exactly the defect this family just spent a night removing.
		// Reading State immediately after the await is what makes the next such implementation fail here.
		if (policy.State != CircuitState.Closed)
		{
			throw new InvalidOperationException(
				$"ResetAsync completed but left the circuit {policy.State}. The contract is that when the "
				+ "returned task completes the circuit is Closed, so an implementation that starts a close "
				+ "and returns before it lands does not satisfy it: an operator is left polling State, or "
				+ "waiting out the break duration, with no signal that the reset has taken effect.");
		}
	}
}

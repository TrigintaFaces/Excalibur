// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// A controllable clock, hand-rolled so this lock needs no extra package reference.
/// </summary>
internal sealed class SettableTimeProvider(DateTimeOffset now) : TimeProvider
{
	private DateTimeOffset _now = now;

	public override DateTimeOffset GetUtcNow() => _now;

	public void Advance(TimeSpan by) => _now = _now.Add(by);
}

/// <summary>
/// Locks the circuit-breaker's time source to the container's <see cref="TimeProvider"/>.
/// </summary>
/// <remarks>
/// Two distinct failures are covered, and neither was reachable before.
///
/// The wiring arm resolves the middleware from a container built by the PRODUCTION registration path
/// rather than constructing it with hand-supplied dependencies. That distinction is the whole point:
/// the middleware gained a <see cref="TimeProvider"/> parameter, and nothing in the Dispatch
/// registration placed one in the container — the public <c>AddSystemTimeProvider</c> extension that
/// would have done so had zero callers. A test that hand-injected a TimeProvider would pass while every
/// real consumer got an unresolvable service. The existing DI smoke test could not catch it either: it
/// only asserts that BuildServiceProvider does not throw, and building a provider never constructs
/// anything.
///
/// The determinism arm exercises the state machine through a clock it controls. The open-duration
/// deadline decides when a half-open probe is admitted, and it was previously read from
/// <c>DateTimeOffset.UtcNow</c>, so the recovery transition could only be reached in a test by sleeping
/// for real. That is why it had no deterministic coverage.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class CircuitBreakerTimeProviderWiringShould
{
	[Fact]
	public void Resolve_TheMiddleware_FromTheRealContainer()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// The production path. UseCircuitBreaker() delegates to UseMiddleware<CircuitBreakerMiddleware>(),
		// which does Services.AddScoped(typeof(TMiddleware)) — so the CONTAINER constructs the middleware
		// and every constructor parameter must be resolvable from it. An earlier draft of this test called
		// GetRequiredService directly off AddDispatchPipeline and failed, correctly: the middleware is not
		// registered until a pipeline actually uses it.
		_ = services.AddDispatch(dispatch => dispatch.UseCircuitBreaker());

		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();

		// RED before TimeProvider was registered: the container had no TimeProvider, so activating the
		// middleware threw rather than returning an instance.
		var middleware = scope.ServiceProvider.GetRequiredService<CircuitBreakerMiddleware>();
		middleware.ShouldNotBeNull();
	}

	[Fact]
	public void LetAConsumerSubstituteTheClock()
	{
		var fake = new SettableTimeProvider(DateTimeOffset.UnixEpoch);
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<TimeProvider>(fake);
		_ = services.AddDispatchPipeline();

		using var provider = services.BuildServiceProvider();

		// The registration is TryAdd, so the consumer's clock must win. If it were a plain Add, the
		// framework's system clock could displace a test's fake one depending on ordering.
		provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(fake);
	}

	[Fact]
	public void OpenAfterTheFailureThreshold_AndDateTheRetryFromTheInjectedClock()
	{
		var start = DateTimeOffset.UnixEpoch;
		var clock = new SettableTimeProvider(start);
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 2,
			OpenDuration = TimeSpan.FromMinutes(5),
		};

		var state = new CircuitBreakerState(options, clock);

		state.State.ShouldBe(CircuitState.Closed);

		state.RecordFailure();
		state.State.ShouldBe(CircuitState.Closed, "one failure is below the threshold of two");

		state.RecordFailure();
		state.State.ShouldBe(CircuitState.Open);

		// The load-bearing assertion: the deadline is derived from the INJECTED clock, not the wall
		// clock. Against DateTimeOffset.UtcNow this would be "about five minutes from whenever the
		// suite happened to run" and could only be asserted loosely.
		state.NextAttemptTime.ShouldBe(start.AddMinutes(5));
	}

	[Fact]
	public void AdmitAHalfOpenProbe_OnceTheClockPassesTheDeadline_WithoutSleeping()
	{
		var start = DateTimeOffset.UnixEpoch;
		var clock = new SettableTimeProvider(start);
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 1,
			OpenDuration = TimeSpan.FromMinutes(5),
		};

		var state = new CircuitBreakerState(options, clock);
		state.RecordFailure();
		state.State.ShouldBe(CircuitState.Open);

		// Still inside the open window: a probe must not be admitted.
		clock.Advance(TimeSpan.FromMinutes(4));
		clock.GetUtcNow().ShouldBeLessThan(state.NextAttemptTime);

		// Past the deadline: the caller admits a probe and the breaker moves to half-open.
		clock.Advance(TimeSpan.FromMinutes(2));
		clock.GetUtcNow().ShouldBeGreaterThan(state.NextAttemptTime);

		state.TransitionToHalfOpen();
		state.State.ShouldBe(CircuitState.HalfOpen);

		// One success closes it, matching Polly's behaviour.
		state.RecordSuccess();
		state.State.ShouldBe(CircuitState.Closed);
	}
}

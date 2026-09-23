// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Testing.Resilience;

namespace Excalibur.Dispatch.Messaging.Tests.Messaging.Resilience;

/// <summary>
/// Proves the shipped conformance kit's failed-close arm is NON-VACUOUS: it detects the exact defect the
/// interface's postcondition forbids, and it does not fire on a correct implementation.
/// </summary>
/// <remarks>
/// <para>
/// The clause under test is on <see cref="ICircuitBreakerPolicy.ResetAsync"/>: <i>a close that fails must
/// surface as a faulted task rather than a log line</i>. Until the arm existed, an implementation that
/// caught its close failure, logged it, and returned a completed task was certified as conforming — and
/// its operator would be told a transport was back in service while every call was still being rejected.
/// </para>
/// <para>
/// <b>Why the broken policy lives here and not in the kit.</b> Binding this clause needs an implementation
/// whose close fails, and neither shipped registry has a reachable failure path. Shipping a
/// deliberately-broken policy as public surface to solve that would put a type consumers can resolve into
/// the package. So the kit exposes a HOOK, the hook defaults to "not determined", and the broken policy
/// that proves the arm works stays inside our own tests where nobody can take a dependency on it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FailedCloseConformanceArmShould
{
	/// <summary>
	/// SAFETY. Against a policy that SWALLOWS its close failure — the defect — the arm must report a
	/// violation. This is the whole point: green here would mean the kit certifies the broken shape.
	/// </summary>
	[Fact]
	public async Task RejectAPolicyThatSwallowsItsCloseFailure()
	{
		var suite = new SuiteOver(new SwallowsTheCloseFailure());

		var violation = await Should.ThrowAsync<InvalidOperationException>(
			async () => await suite.VerifyAFailedCloseSurfacesAsAFaultedTask());

		violation.Message.ShouldContain("faulted task");
	}

	/// <summary>
	/// LIVENESS. Against a policy that lets the failure propagate — the correct shape — the arm must pass
	/// AND report that it actually bound the clause. Without this, an arm that threw at everything would
	/// satisfy the safety arm above.
	/// </summary>
	[Fact]
	public async Task AcceptAPolicyThatLetsItsCloseFailurePropagate()
	{
		var suite = new SuiteOver(new SurfacesTheCloseFailure());

		var bound = await suite.VerifyAFailedCloseSurfacesAsAFaultedTask();

		bound.ShouldBeTrue("the arm ran against a failing close, so the clause was bound, not skipped");
	}

	/// <summary>
	/// SAFETY. With no hook supplied, the arm must answer NOT DETERMINED rather than passing. A pass that
	/// means "not checked" is the shape that let this clause ship unverified in the first place.
	/// </summary>
	[Fact]
	public async Task ReportNotDeterminedRatherThanPassingWhenNoFailingCloseCanBeProduced()
	{
		var suite = new SuiteOver(policyWhoseCloseFails: null);

		var bound = await suite.VerifyAFailedCloseSurfacesAsAFaultedTask();

		bound.ShouldBeFalse("nothing was checked, and the arm must not report that as a pass");
	}

	/// <summary>
	/// The kit, pointed at a supplied policy. Only the hook is overridden; every other arm is irrelevant
	/// here and none of them run.
	/// </summary>
	private sealed class SuiteOver(ICircuitBreakerPolicy? policyWhoseCloseFails)
		: TransportCircuitBreakerRegistryConformanceTests
	{
		protected override ITransportCircuitBreakerRegistry CreateRegistry() =>
			new TransportCircuitBreakerRegistry(new CircuitBreakerOptions());

		protected override ICircuitBreakerPolicy? CreatePolicyWhoseCloseFails() => policyWhoseCloseFails;
	}

	/// <summary>
	/// THE DEFECT. Its close fails, and it reports success anyway — implemented directly against
	/// <see cref="ICircuitBreakerPolicy"/>, inheriting nothing that could supply the member under test.
	/// </summary>
	private sealed class SwallowsTheCloseFailure : ICircuitBreakerPolicy
	{
		public CircuitState State => CircuitState.Open;

		public Task<TResult> ExecuteAsync<TResult>(
			Func<CancellationToken, Task<TResult>> action,
			CancellationToken cancellationToken) =>
			action(cancellationToken);

		public Task<TResult> ExecuteAsync<TResult>(
			Func<CancellationToken, Task<TResult>> action,
			Func<TResult, bool> isFailure,
			CancellationToken cancellationToken) =>
			action(cancellationToken);

		// The close throws, this catches it, and the caller is told the circuit is back in service.
		public Task ResetAsync(CancellationToken cancellationToken)
		{
			try
			{
				throw new InvalidOperationException("the underlying breaker refused to close");
			}
			catch (InvalidOperationException)
			{
				return Task.CompletedTask;
			}
		}

	}

	/// <summary>
	/// THE CORRECT SHAPE. Its close fails and the failure reaches the caller.
	/// </summary>
	private sealed class SurfacesTheCloseFailure : ICircuitBreakerPolicy
	{
		public CircuitState State => CircuitState.Open;

		public Task<TResult> ExecuteAsync<TResult>(
			Func<CancellationToken, Task<TResult>> action,
			CancellationToken cancellationToken) =>
			action(cancellationToken);

		public Task<TResult> ExecuteAsync<TResult>(
			Func<CancellationToken, Task<TResult>> action,
			Func<TResult, bool> isFailure,
			CancellationToken cancellationToken) =>
			action(cancellationToken);

		public Task ResetAsync(CancellationToken cancellationToken) =>
			Task.FromException(new InvalidOperationException("the underlying breaker refused to close"));

	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Regression lock (bead ufbn29) for the Polly-path <see cref="BackoffStrategy.FullJitter"/> wiring (SA
/// ruling a): on the Polly adapters, <c>FullJitter</c> MUST map to <c>DelayBackoffType.Exponential</c> with
/// Polly's jitter <b>forced on</b>, independent of the caller's <c>UseJitter</c> flag.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bug this catches:</b> pre-fix, all 3 Polly adapters let <c>FullJitter</c> fall into the
/// <c>_ =&gt; DelayBackoffType.Exponential</c> default with <c>UseJitter = options.UseJitter</c> — so selecting
/// <c>FullJitter</c> while leaving <c>UseJitter=false</c> produced <b>exponential-without-jitter</b>:
/// advertised-but-inert on the Polly path. The fix forces <c>UseJitter</c> true for <c>FullJitter</c>.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> the lock builds the calculator via <c>RetryPolicy.CreateBackoffCalculator</c> with
/// <c>UseJitter=false</c> and asserts the produced adapter still has jitter forced on. RED on the pre-fix
/// inert mapping (<c>_useJitter == false</c>); GREEN once <c>FullJitter</c> forces it. The factory + adapter
/// are non-public, so reflection is used rather than widening production visibility (internal-first); a rename
/// fails the lock loudly (method/field-not-found) rather than passing vacuously.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Feature", "Resilience")]
public sealed class PollyFullJitterWiringShould
{
	[Fact]
	public void ForcePollyJitterOn_ForFullJitter_EvenWhenCallerUseJitterIsFalse()
	{
		// Caller explicitly disables jitter; FullJitter must still force it on (the wiring under test).
		var options = new RetryOptions { BackoffStrategy = BackoffStrategy.FullJitter, UseJitter = false };

		var factory = typeof(RetryPolicy).GetMethod(
			"CreateBackoffCalculator",
			BindingFlags.NonPublic | BindingFlags.Static)
			?? throw new InvalidOperationException(
				"ufbn29 — RetryPolicy.CreateBackoffCalculator not found (the Polly backoff factory was renamed; update the lock).");

		var calculator = factory.Invoke(null, [options])
			?? throw new InvalidOperationException("ufbn29 — CreateBackoffCalculator returned null.");

		var adapterType = calculator.GetType();
		adapterType.Name.ShouldBe(
			"PollyBackoffCalculatorAdapter",
			"FullJitter must route through the Polly backoff adapter (not the Fibonacci adapter).");

		var useJitter = (bool)GetPrivateField(adapterType, calculator, "_useJitter");
		var backoffType = GetPrivateField(adapterType, calculator, "_backoffType");

		useJitter.ShouldBeTrue(
			"ufbn29 — FullJitter must force Polly's jitter ON independent of the caller's UseJitter flag (advertised-but-inert otherwise).");
		backoffType.ToString().ShouldBe(
			"Exponential",
			"ufbn29 — FullJitter maps to Polly DelayBackoffType.Exponential (Polly v8 has no distinct FullJitter member).");
	}

	private static object GetPrivateField(Type type, object instance, string name)
	{
		var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException(
				$"ufbn29 — PollyBackoffCalculatorAdapter.{name} not found (field renamed; update the lock).");
		return field.GetValue(instance)
			?? throw new InvalidOperationException($"ufbn29 — {name} was null.");
	}
}

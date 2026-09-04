// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Locks the one property a circuit breaker exists to have: that enough failures stop the calls.
/// </summary>
/// <remarks>
/// <para>
/// The Polly-backed adapter is the default implementation once a host calls AddDispatchResilience,
/// and its RecordSuccess/RecordFailure members mutate a private counter that its own strategy never
/// reads. A caller reporting outcomes that way had a breaker that could not open however badly the
/// backend behaved, while reporting Closed forever.
/// </para>
/// <para>
/// The distinction this locks is therefore not "does the breaker work" but "does it work through
/// the seam callers actually use". A lock written against the in-house policy would pass either way,
/// which is why this one names the adapter.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PollyBreakerOpensThroughExecuteShould
{
	private static CircuitBreakerOptions Options() => new()
	{
		FailureThreshold = 2,
		OpenDuration = TimeSpan.FromMinutes(5),
		SamplingDuration = TimeSpan.FromSeconds(30),
	};

	[Fact]
	public async Task OpenAfterEnoughFailuresDrivenThroughExecute()
	{
		// SAFETY. Failures arriving the way the cache now reports them must trip the circuit.
		var breaker = new PollyCircuitBreakerPolicyAdapter(Options(), "cache-test");

		for (var i = 0; i < 8; i++)
		{
			try
			{
				_ = await breaker.ExecuteAsync<int>(
					_ => throw new InvalidOperationException("backend down"),
					TestContext.Current.CancellationToken);
			}
			catch (InvalidOperationException)
			{
				// expected while the circuit is still closed
			}
			catch (CircuitBreakerOpenException)
			{
				return; // the circuit opened, which is the whole point
			}
		}

		Assert.Fail("the breaker never opened despite a sustained run of failures through ExecuteAsync");
	}

	[Fact]
	public async Task StayClosedWhileTheBackendSucceeds()
	{
		// LIVENESS. A breaker that opens on healthy traffic would satisfy the arm above and break
		// every consumer, so the success path has to be asserted too.
		var breaker = new PollyCircuitBreakerPolicyAdapter(Options(), "cache-healthy");

		for (var i = 0; i < 20; i++)
		{
			var value = await breaker.ExecuteAsync(
				_ => Task.FromResult(i),
				TestContext.Current.CancellationToken);

			value.ShouldBe(i);
		}

		breaker.State.ShouldBe(CircuitState.Closed);
	}
}

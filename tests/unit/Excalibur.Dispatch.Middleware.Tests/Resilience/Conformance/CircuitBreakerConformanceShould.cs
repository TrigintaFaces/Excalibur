// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Concrete conformance subclasses — bd-ccyett.
// Each class supplies one CB implementation; the base class provides all the [Fact] tests.

using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience.Conformance;

// ---------------------------------------------------------------------------
// The hand-rolled CircuitBreakerPattern that used to be conformance-suite #1 has been DELETED.
// It had no production consumer: nothing registered its factory, so the only breaker any
// consumer could resolve was Polly's. The suites below remain the parity contract.
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// 3. DistributedCircuitBreaker — async, distributed-cache-backed state machine
// ---------------------------------------------------------------------------

/// <summary>
/// Conformance suite for <see cref="DistributedCircuitBreaker"/> (bd-ccyett).
///
/// Uses an in-process <see cref="MemoryDistributedCache"/> so the suite remains
/// self-contained and runs without any external infrastructure.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class DistributedCircuitBreakerConformanceShould : CircuitBreakerConformanceBase
{
	/// <inheritdoc/>
	private protected override ICircuitBreakerTestSut CreateSut(
		int failureThreshold,
		TimeSpan openDuration,
		int successThreshold)
	{
		var cache = new MemoryDistributedCache(
			MsOptions.Create(new MemoryDistributedCacheOptions()));

		var options = MsOptions.Create(new DistributedCircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = failureThreshold,
			BreakDuration = openDuration,
			SuccessThresholdToClose = successThreshold,
			// Polly requires MinimumThroughput >= 2. Set to 2 so the local Polly
			// fallback pipeline doesn't interfere — we drive state via ConsecutiveFailureThreshold.
			MinimumThroughput = 2,
			SamplingDuration = TimeSpan.FromSeconds(5),
		});

		var cb = new DistributedCircuitBreaker(
			"conformance-dcb",
			cache,
			options,
			NullLogger<DistributedCircuitBreaker>.Instance);

		return new DistributedCircuitBreakerSut(cb);
	}
}

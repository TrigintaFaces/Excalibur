// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Constructs fresh, independently-owned <see cref="ICircuitBreakerPolicy"/> instances for consumers
/// that need their own breaker (for example, the distributed-cache middleware) rather than a shared,
/// per-transport breaker from <see cref="ITransportCircuitBreakerRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the public construction seam for the unified circuit-breaker contract. Use it when a
/// component owns its breaker lifecycle and supplies its own <see cref="CircuitBreakerOptions"/> at
/// registration time. For shared, named, cached breakers keyed by transport, use
/// <see cref="ITransportCircuitBreakerRegistry.GetOrCreate(string, CircuitBreakerOptions)"/> instead — that
/// returns the same instance for a given name, whereas <see cref="Create"/> always returns a new policy.
/// </para>
/// </remarks>
public interface ICircuitBreakerPolicyFactory
{
	/// <summary>
	/// Creates a new <see cref="ICircuitBreakerPolicy"/> with the supplied name and options.
	/// </summary>
	/// <param name="name">A logical name for the breaker, used in diagnostics and log messages.</param>
	/// <param name="options">The circuit-breaker configuration (thresholds, durations).</param>
	/// <param name="logger">
	/// Optional logger for the policy. When <see langword="null"/>, the factory falls back to its injected
	/// <see cref="ILoggerFactory"/> (if any); when that is also absent, the policy logs nothing.
	/// </param>
	/// <returns>A new, independent circuit-breaker policy instance.</returns>
	/// <exception cref="System.ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
	ICircuitBreakerPolicy Create(string name, CircuitBreakerOptions options, ILogger? logger = null);
}

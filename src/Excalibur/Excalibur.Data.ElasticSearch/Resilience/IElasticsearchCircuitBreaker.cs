// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>
/// Reports whether Elasticsearch calls are currently being rejected.
/// </summary>
/// <remarks>
/// <para>
/// This is a view of the breaker, not a way to drive one. The breaker lives inside the resilience
/// pipeline every call runs through, and it records its own outcomes; a caller reporting successes
/// and failures to a second object would be describing a circuit that decides nothing.
/// </para>
/// <para>
/// Failure ratio and consecutive-failure counts are deliberately absent. The pipeline does not
/// publish them, and reproducing them here would mean counting alongside the breaker and presenting
/// a number that can disagree with the one actually rejecting requests.
/// </para>
/// </remarks>
public interface IElasticsearchCircuitBreaker : IDisposable
{
	/// <summary>Gets a value indicating whether calls are being rejected.</summary>
	/// <value><see langword="true"/> when the circuit is open; otherwise <see langword="false"/>.</value>
	bool IsOpen { get; }

	/// <summary>Gets a value indicating whether the circuit is trialling a single call.</summary>
	/// <value><see langword="true"/> when the circuit is half-open; otherwise <see langword="false"/>.</value>
	bool IsHalfOpen { get; }

	/// <summary>Gets the circuit's current state.</summary>
	/// <value>
	/// The state held by the pipeline. Reads <see cref="CircuitState.Closed"/> when the breaker is
	/// disabled, since nothing is being rejected.
	/// </value>
	CircuitState State { get; }
}

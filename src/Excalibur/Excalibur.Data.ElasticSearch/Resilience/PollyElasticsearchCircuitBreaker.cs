// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

using PollyCircuitState = Polly.CircuitBreaker.CircuitState;

namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>
/// Reports the breaker state held by the resilience pipeline.
/// </summary>
/// <remarks>
/// This observes; it does not decide. The breaker that actually opens and closes lives inside the
/// pipeline every call runs through, so what a consumer reads here is the same state that rejected
/// their request rather than a second counter kept alongside it and hoped to agree.
/// </remarks>
internal sealed class PollyElasticsearchCircuitBreaker(ElasticsearchResiliencePipeline pipeline)
	: IElasticsearchCircuitBreaker
{
	private readonly ElasticsearchResiliencePipeline _pipeline =
		pipeline ?? throw new ArgumentNullException(nameof(pipeline));

	/// <inheritdoc />
	public bool IsOpen => State is CircuitState.Open;

	/// <inheritdoc />
	public bool IsHalfOpen => State is CircuitState.HalfOpen;

	/// <inheritdoc />
	public CircuitState State => _pipeline.StateProvider?.CircuitState switch
	{
		PollyCircuitState.Open => CircuitState.Open,
		PollyCircuitState.HalfOpen => CircuitState.HalfOpen,
		PollyCircuitState.Isolated => CircuitState.Open,
		// Closed, and the disabled-breaker case: with no breaker configured nothing is ever
		// rejected, which is what a closed circuit means to a caller.
		_ => CircuitState.Closed,
	};

	public void Dispose()
	{
		// The pipeline owns the breaker and outlives this view of it.
	}
}

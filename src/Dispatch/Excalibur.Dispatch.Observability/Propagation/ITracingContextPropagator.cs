// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

namespace Excalibur.Dispatch.Observability.Propagation;

/// <summary>
/// Propagates trace context across transport boundaries by injecting and extracting
/// trace identifiers into/from carrier dictionaries (e.g., message headers).
/// </summary>
/// <remarks>
/// <para>
/// Implementations support specific wire formats such as W3C Trace Context or Zipkin B3.
/// This interface follows the OpenTelemetry Propagators API pattern.
/// </para>
/// </remarks>
public interface ITracingContextPropagator
{
	/// <summary>
	/// Gets the format name for this propagator (e.g., "w3c", "b3").
	/// </summary>
	/// <value>The propagation format identifier.</value>
	string FormatName { get; }

	/// <summary>
	/// Injects the current trace context into the carrier dictionary.
	/// </summary>
	/// <param name="context">The activity context to propagate.</param>
	/// <param name="carrier">The carrier dictionary to inject trace headers into.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task InjectAsync(
		ActivityContext context,
		IDictionary<string, string> carrier,
		CancellationToken cancellationToken);

	/// <summary>
	/// Extracts trace context from the carrier dictionary.
	/// </summary>
	/// <param name="carrier">The carrier dictionary containing trace headers.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>The extracted <see cref="ActivityContext"/>, or <see langword="default"/> if extraction fails.</returns>
	Task<ActivityContext> ExtractAsync(
		IDictionary<string, string> carrier,
		CancellationToken cancellationToken);
}

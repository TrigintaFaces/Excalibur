// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.Observability;

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// OpenTelemetry extension methods for Event Sourcing instrumentation.
/// </summary>
public static class EventSourcingOpenTelemetryExtensions
{
	/// <summary>
	/// Adds Event Sourcing tracing instrumentation to OpenTelemetry.
	/// </summary>
	/// <param name="builder">The OpenTelemetry builder.</param>
	/// <returns>The OpenTelemetry builder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
	public static IOpenTelemetryBuilder AddEventSourcingInstrumentation(this IOpenTelemetryBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithTracing(static traceBuilder =>
			traceBuilder.AddSource(EventSourcingActivitySource.Name));
	}

	/// <summary>
	/// Adds Event Sourcing tracing instrumentation to the tracer provider.
	/// </summary>
	/// <param name="builder">The tracer provider builder.</param>
	/// <returns>The tracer provider builder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
	public static TracerProviderBuilder AddEventSourcingInstrumentation(this TracerProviderBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.AddSource(EventSourcingActivitySource.Name);
	}
}

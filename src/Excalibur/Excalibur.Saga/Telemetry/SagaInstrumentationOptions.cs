// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Telemetry;

/// <summary>
/// Configuration options for saga instrumentation.
/// </summary>
/// <remarks>
/// <para>
/// Controls which telemetry signals (metrics, tracing) are enabled for saga operations.
/// When registered via <c>AddSagaInstrumentation</c>, this options class enables
/// consumers to selectively enable or disable instrumentation features.
/// </para>
/// </remarks>
public sealed class SagaInstrumentationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether saga metrics collection is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to enable metrics collection via <see cref="SagaMetrics"/>; otherwise, <see langword="false"/>.
	/// The default is <see langword="true"/>.
	/// </value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether saga distributed tracing is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to enable tracing via <see cref="SagaActivitySource"/>; otherwise, <see langword="false"/>.
	/// The default is <see langword="true"/>.
	/// </value>
	public bool EnableTracing { get; set; } = true;
}

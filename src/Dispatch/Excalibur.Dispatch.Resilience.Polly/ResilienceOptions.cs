// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Full resilience configuration options for the Polly-based resilience middleware.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the full Polly resilience configuration used by <c>AddResilience()</c>
/// on <c>IDispatchBuilder</c>. It controls retry policies, circuit breakers, and timeouts
/// at the middleware level.
/// </para>
/// <para>
/// For lightweight <c>appsettings.json</c> configuration binding, see
/// <c>Excalibur.Dispatch.Options.Configuration.ResilienceOptions</c> which provides a simplified
/// subset of resilience settings on <see cref="Options.Configuration.DispatchOptions"/>.
/// </para>
/// </remarks>
public sealed class ResilienceOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether resilience is enabled.
	/// </summary>
	/// <value>The current <see cref="Enabled"/> value.</value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the default retry count.
	/// </summary>
	/// <value>The current <see cref="DefaultRetryCount"/> value.</value>
	public int DefaultRetryCount { get; set; } = 3;

	/// <summary>
	/// Gets or sets the default timeout in seconds.
	/// </summary>
	/// <value>The current <see cref="DefaultTimeoutSeconds"/> value.</value>
	public int DefaultTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether circuit breaker is enabled.
	/// </summary>
	/// <value>The current <see cref="EnableCircuitBreaker"/> value.</value>
	public bool EnableCircuitBreaker { get; set; } = true;
}

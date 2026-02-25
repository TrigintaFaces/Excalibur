// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Nested resilience options for <see cref="DispatchOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a lightweight configuration-binding class for <c>appsettings.json</c> scenarios.
/// For full resilience configuration including Polly policies, use
/// <c>AddResilience()</c> on <c>IDispatchBuilder</c>.
/// </para>
/// </remarks>
public sealed class ResilienceOptions
{
	/// <summary>
	/// Gets or sets the default retry count.
	/// </summary>
	/// <value>3 by default.</value>
	public int DefaultRetryCount { get; set; } = 3;

	/// <summary>
	/// Gets or sets a value indicating whether circuit breaker is enabled.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EnableCircuitBreaker { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether timeout handling is enabled.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EnableTimeout { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether bulkhead isolation is enabled.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EnableBulkhead { get; set; }
}

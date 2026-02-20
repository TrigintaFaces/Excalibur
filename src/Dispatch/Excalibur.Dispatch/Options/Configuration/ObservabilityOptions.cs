// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Options for Dispatch observability features.
/// </summary>
public sealed class ObservabilityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether observability is globally enabled.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether distributed tracing is enabled.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EnableTracing { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether metrics collection is enabled.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether context flow tracking is enabled.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EnableContextFlow { get; set; } = true;
}

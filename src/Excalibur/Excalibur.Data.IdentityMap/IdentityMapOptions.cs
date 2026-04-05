// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.IdentityMap;

/// <summary>
/// Configuration options for the identity map store.
/// </summary>
public sealed class IdentityMapOptions
{
	/// <summary>
	/// Gets or sets the default external system name used when not explicitly specified.
	/// </summary>
	/// <value>The default external system name, or <see langword="null"/> if not set.</value>
	public string? DefaultExternalSystem { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether telemetry instrumentation is enabled.
	/// </summary>
	/// <value><see langword="true"/> to enable telemetry; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool EnableTelemetry { get; set; } = true;
}

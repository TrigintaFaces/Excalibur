// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for metrics collection.
/// </summary>
public sealed class MetricsOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether metrics collection is enabled.
	/// </summary>
	/// <value> <see langword="true" /> to enable metrics collection; otherwise, <see langword="false" />. </value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the metrics export interval.
	/// </summary>
	/// <value> The interval between metrics export operations. </value>
	public TimeSpan ExportInterval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets custom metric tags to include.
	/// </summary>
	/// <value> The user-defined tags attached to emitted metrics. </value>
	public IDictionary<string, string> CustomTags { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

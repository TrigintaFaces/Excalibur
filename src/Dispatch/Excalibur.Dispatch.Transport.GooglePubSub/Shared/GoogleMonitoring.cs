// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Google Cloud monitoring related types.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible",
	Justification = "Nested type is intentionally used to group related monitoring types under a common static class, following established patterns in telemetry libraries.")]
public static class GoogleMonitoring
{
	/// <summary>
	/// Metric descriptor for Cloud Monitoring.
	/// </summary>
	public sealed class MetricDescriptor
	{
		/// <summary>
		/// Gets or sets the metric type.
		/// </summary>
		/// <value>
		/// The metric type.
		/// </value>
		public string Type { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the display name.
		/// </summary>
		/// <value>
		/// The display name.
		/// </value>
		public string DisplayName { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the description.
		/// </summary>
		/// <value>
		/// The description.
		/// </value>
		public string Description { get; set; } = string.Empty;
	}
}

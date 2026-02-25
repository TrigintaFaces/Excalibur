// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Stub implementation for MetricDescriptor if not available in the library.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible",
	Justification = "MetricDescriptorTypes nested class mirrors Google Cloud API structure and is intentionally visible for API compatibility.")]
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

	/// <summary>
	/// Gets or sets the metric kind.
	/// </summary>
	/// <value>
	/// The metric kind.
	/// </value>
	public MetricDescriptorTypes.MetricKind MetricKind { get; set; }

	/// <summary>
	/// Gets or sets the value type.
	/// </summary>
	/// <value>
	/// The value type.
	/// </value>
	public MetricDescriptorTypes.ValueType ValueType { get; set; }

	//
	// Gets or sets the unit.
	// </summary>
	public string Unit { get; set; } = string.Empty;

	/// <summary>
	/// Gets the labels collection.
	/// </summary>
	/// <value>
	/// The labels collection.
	/// </value>
	public List<LabelDescriptor> Labels { get; } = [];

	/// <summary>
	/// Nested types for MetricDescriptor.
	/// </summary>
	public static class MetricDescriptorTypes
	{
		/// <summary>
		/// Metric kind enumeration.
		/// </summary>
		public enum MetricKind
		{
			/// <summary>
			/// Unspecified metric kind.
			/// </summary>
			Unspecified = 0,

			/// <summary>
			/// Gauge metric kind.
			/// </summary>
			Gauge = 1,

			/// <summary>
			/// Delta metric kind.
			/// </summary>
			Delta = 2,

			/// <summary>
			/// Cumulative metric kind.
			/// </summary>
			Cumulative = 3,
		}

		/// <summary>
		/// Value type enumeration.
		/// </summary>
		public enum ValueType
		{
			/// <summary>
			/// Value type unspecified.
			/// </summary>
			Unspecified = 0,

			/// <summary>
			/// Boolean value type.
			/// </summary>
			Bool = 1,

			/// <summary>
			/// Int64 value type.
			/// </summary>
			Int64 = 2,

			/// <summary>
			/// Double value type.
			/// </summary>
			Double = 3,

			/// <summary>
			/// String value type.
			/// </summary>
			String = 4,

			/// <summary>
			/// Distribution value type.
			/// </summary>
			Distribution = 5,

			/// <summary>
			/// Money value type.
			/// </summary>
			Money = 6,
		}
	}
}

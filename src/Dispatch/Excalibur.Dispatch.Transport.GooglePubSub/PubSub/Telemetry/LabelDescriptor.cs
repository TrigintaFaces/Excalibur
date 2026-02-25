// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Label descriptor for metric labels.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible",
	Justification = "LabelValueTypes nested class mirrors Google Cloud API structure and is intentionally visible for API compatibility.")]
public sealed class LabelDescriptor
{
	/// <summary>
	/// Gets or sets the key.
	/// </summary>
	/// <value>
	/// The key.
	/// </value>
	public string Key { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the value type.
	/// </summary>
	/// <value>
	/// The value type.
	/// </value>
	public LabelValueTypes.ValueType ValueType { get; set; }

	/// <summary>
	/// Gets or sets the description.
	/// </summary>
	/// <value>
	/// The description.
	/// </value>
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Nested types for LabelDescriptor.
	/// </summary>
	public static class LabelValueTypes
	{
		/// <summary>
		/// Value type enumeration.
		/// </summary>
		public enum ValueType
		{
			/// <summary>
			/// String value type.
			/// </summary>
			String = 0,

			/// <summary>
			/// Bool value type.
			/// </summary>
			Bool = 1,

			/// <summary>
			/// Int64 value type.
			/// </summary>
			Int64 = 2,
		}
	}
}

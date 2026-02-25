// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Event representing an adaptation that occurred.
/// </summary>
public sealed class AdaptationEvent
{
	/// <summary>
	/// Gets or sets when the adaptation occurred.
	/// </summary>
	/// <value> The timestamp recorded when the adaptation took place. </value>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the type of adaptation.
	/// </summary>
	/// <value> The category or type describing the adaptation. </value>
	public string AdaptationType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the reason for the adaptation.
	/// </summary>
	/// <value> The explanatory text outlining why the adaptation occurred. </value>
	public string Reason { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the old value before the adaptation.
	/// </summary>
	/// <value> The previous value prior to the adaptation. </value>
	public object? OldValue { get; set; }

	/// <summary>
	/// Gets or sets the new value after the adaptation.
	/// </summary>
	/// <value> The updated value resulting from the adaptation. </value>
	public object? NewValue { get; set; }

	/// <summary>
	/// Gets or sets the impact of the adaptation.
	/// </summary>
	/// <value> The assessed impact level of the adaptation. </value>
	public AdaptationImpact Impact { get; set; } = AdaptationImpact.Minor;
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Result of poison message detection.
/// </summary>
public sealed class PoisonDetectionResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the message is considered poison.
	/// </summary>
	/// <value> The current <see cref="IsPoison" /> value. </value>
	public bool IsPoison { get; set; }

	/// <summary>
	/// Gets or sets the reason why the message is considered poison.
	/// </summary>
	/// <value> The current <see cref="Reason" /> value. </value>
	public string? Reason { get; set; }

	/// <summary>
	/// Gets or sets the detector that identified the message as poison.
	/// </summary>
	/// <value> The current <see cref="DetectorName" /> value. </value>
	public string? DetectorName { get; set; }

	/// <summary>
	/// Gets or sets additional details about the detection.
	/// </summary>
	/// <value> The current <see cref="Details" /> value. </value>
	public Dictionary<string, object> Details { get; set; } = [];

	/// <summary>
	/// Creates a poison detection result.
	/// </summary>
	public static PoisonDetectionResult Poison(string reason, string detectorName, Dictionary<string, object>? details = null) =>
		new() { IsPoison = true, Reason = reason, DetectorName = detectorName, Details = details ?? [] };

	/// <summary>
	/// Creates a non-poison detection result.
	/// </summary>
	public static PoisonDetectionResult NotPoison() => new() { IsPoison = false };
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Defines alert severity based on lag threshold.
/// </summary>
public sealed class AlertSeverityThreshold
{
	/// <summary>
	/// Gets the lag threshold for this severity level.
	/// </summary>
	/// <value>
	/// The lag threshold for this severity level.
	/// </value>
	public required TimeSpan LagThreshold { get; init; }

	/// <summary>
	/// Gets the severity level.
	/// </summary>
	/// <value>
	/// The severity level.
	/// </value>
	public required AlertSeverity Severity { get; init; }

	/// <summary>
	/// Gets the alert message template.
	/// </summary>
	/// <value>
	/// The alert message template.
	/// </value>
	public string? MessageTemplate { get; init; }
}

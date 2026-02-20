// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a threat intelligence update result.
/// </summary>
public sealed class ThreatIntelligenceUpdateResult
{
	/// <summary>
	/// Gets or sets the request ID.
	/// </summary>
	/// <value>
	/// The request ID.
	/// </value>
	public string RequestId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the source name.
	/// </summary>
	/// <value>
	/// The source name.
	/// </value>
	public string SourceName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the update start time.
	/// </summary>
	/// <value>
	/// The update start time.
	/// </value>
	public DateTimeOffset UpdateStartTime { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the update end time.
	/// </summary>
	/// <value>
	/// The update end time.
	/// </value>
	public DateTimeOffset UpdateEndTime { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the update duration.
	/// </summary>
	/// <value>
	/// The update duration.
	/// </value>
	public TimeSpan Duration { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the update was successful.
	/// </summary>
	/// <value>
	/// A value indicating whether the update was successful.
	/// </value>
	public bool Success { get; set; }

	/// <summary>
	/// Gets or sets the number of indicators updated.
	/// </summary>
	/// <value>
	/// The number of indicators updated.
	/// </value>
	public int IndicatorsUpdated { get; set; }

	/// <summary>
	/// Gets or sets the list of errors that occurred during the update.
	/// </summary>
	/// <value>
	/// The list of errors that occurred during the update.
	/// </value>
	public List<string> Errors { get; set; } = [];
}

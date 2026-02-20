// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a threat intelligence update request.
/// </summary>
public sealed class ThreatIntelligenceUpdateRequest
{
	/// <summary>
	/// Gets or sets the request ID.
	/// </summary>
	/// <value>
	/// The request ID.
	/// </value>
	public string RequestId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the source name.
	/// </summary>
	/// <value>
	/// The source name.
	/// </value>
	public string SourceName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the source of the threat intelligence update.
	/// </summary>
	/// <value>
	/// The source of the threat intelligence update.
	/// </value>
	public string Source { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the threat indicators.
	/// </summary>
	/// <value>
	/// The threat indicators.
	/// </value>
	public List<ThreatIndicator> ThreatIndicators { get; set; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to force the update even if the source is up-to-date.
	/// </summary>
	/// <value>
	/// A value indicating whether to force the update even if the source is up-to-date.
	/// </value>
	public bool ForceUpdate { get; set; }
}

/// <summary>
/// Represents a threat indicator.
/// </summary>
public sealed class ThreatIndicator
{
	/// <summary>
	/// Gets or sets the indicator type.
	/// </summary>
	/// <value>
	/// The indicator type.
	/// </value>
	public string Type { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the indicator value.
	/// </summary>
	/// <value>
	/// The indicator value.
	/// </value>
	public string Value { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the threat level.
	/// </summary>
	/// <value>
	/// The threat level.
	/// </value>
	public string ThreatLevel { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the description.
	/// </summary>
	/// <value>
	/// The description.
	/// </value>
	public string Description { get; set; } = string.Empty;
}

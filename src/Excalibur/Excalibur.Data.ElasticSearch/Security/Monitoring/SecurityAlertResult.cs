// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a security alert result.
/// </summary>
public sealed class SecurityAlertResult
{
	/// <summary>
	/// Gets or sets the request identifier.
	/// </summary>
	/// <value>
	/// The request identifier.
	/// </value>
	public string RequestId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the timestamp when alerts were generated.
	/// </summary>
	/// <value>
	/// The timestamp when alerts were generated.
	/// </value>
	public DateTimeOffset GenerationTimestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the generated alerts.
	/// </summary>
	/// <value>
	/// The generated alerts.
	/// </value>
	public List<SecurityAlert> GeneratedAlerts { get; set; } = [];

	/// <summary>
	/// Gets or sets the count of alerts.
	/// </summary>
	/// <value>
	/// The count of alerts.
	/// </value>
	public int AlertCount { get; set; }


}

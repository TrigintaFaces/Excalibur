// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Represents a detected context anomaly.
/// </summary>
public sealed class ContextAnomaly
{
	/// <summary>
	/// Gets or sets the anomaly type.
	/// </summary>
	public required AnomalyType Type { get; set; }

	/// <summary>
	/// Gets or sets the severity.
	/// </summary>
	public required AnomalySeverity Severity { get; set; }

	/// <summary>
	/// Gets or sets the description.
	/// </summary>
	public required string Description { get; set; }

	/// <summary>
	/// Gets or sets the affected message ID.
	/// </summary>
	public required string MessageId { get; set; }

	/// <summary>
	/// Gets or sets when the anomaly was detected.
	/// </summary>
	public DateTimeOffset DetectedAt { get; set; }

	/// <summary>
	/// Gets or sets suggested action to resolve.
	/// </summary>
	public string? SuggestedAction { get; set; }
}

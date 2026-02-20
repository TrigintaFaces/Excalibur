// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Represents a diagnostic issue.
/// </summary>
public sealed class ContextDiagnosticIssue
{
	/// <summary>
	/// Gets or sets the severity.
	/// </summary>
	public required DiagnosticSeverity Severity { get; set; }

	/// <summary>
	/// Gets or sets the category.
	/// </summary>
	public required string Category { get; set; }

	/// <summary>
	/// Gets or sets the description.
	/// </summary>
	public required string Description { get; set; }

	/// <summary>
	/// Gets or sets the affected field.
	/// </summary>
	public string? Field { get; set; }

	/// <summary>
	/// Gets or sets the recommendation.
	/// </summary>
	public string? Recommendation { get; set; }
}

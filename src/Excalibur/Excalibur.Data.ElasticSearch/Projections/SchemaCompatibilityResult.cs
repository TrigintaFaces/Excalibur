// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents a schema compatibility validation result.
/// </summary>
public sealed class SchemaCompatibilityResult
{
	/// <summary>
	/// Gets a value indicating whether the schemas are compatible.
	/// </summary>
	/// <value>
	/// A value indicating whether the schemas are compatible.
	/// </value>
	public required bool IsCompatible { get; init; }

	/// <summary>
	/// Gets the compatibility level.
	/// </summary>
	/// <value>
	/// The compatibility level.
	/// </value>
	public required CompatibilityLevel Level { get; init; }

	/// <summary>
	/// Gets any incompatibilities found.
	/// </summary>
	/// <value>
	/// Any incompatibilities found.
	/// </value>
	public IReadOnlyList<string>? Incompatibilities { get; init; }

	/// <summary>
	/// Gets suggested fixes for incompatibilities.
	/// </summary>
	/// <value>
	/// Suggested fixes for incompatibilities.
	/// </value>
	public IReadOnlyList<string>? SuggestedFixes { get; init; }
}

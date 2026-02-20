// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents the validation result for a projection rebuild.
/// </summary>
public sealed class ProjectionRebuildValidation
{
	/// <summary>
	/// Gets a value indicating whether the projection can be rebuilt.
	/// </summary>
	/// <value>
	/// A value indicating whether the projection can be rebuilt.
	/// </value>
	public required bool CanRebuild { get; init; }

	/// <summary>
	/// Gets validation messages.
	/// </summary>
	/// <value>
	/// Validation messages.
	/// </value>
	public required IReadOnlyList<string> ValidationMessages { get; init; }

	/// <summary>
	/// Gets any warnings about the rebuild.
	/// </summary>
	/// <value>
	/// Any warnings about the rebuild.
	/// </value>
	public IReadOnlyList<string>? Warnings { get; init; }

	/// <summary>
	/// Gets the estimated size of the rebuilt projection.
	/// </summary>
	/// <value>
	/// The estimated size of the rebuilt projection.
	/// </value>
	public long? EstimatedDocumentCount { get; init; }

	/// <summary>
	/// Gets the estimated time to complete the rebuild.
	/// </summary>
	/// <value>
	/// The estimated time to complete the rebuild.
	/// </value>
	public TimeSpan? EstimatedDuration { get; init; }

	/// <summary>
	/// Gets a value indicating whether sufficient resources are available.
	/// </summary>
	/// <value>
	/// A value indicating whether sufficient resources are available.
	/// </value>
	public bool HasSufficientResources { get; init; } = true;
}

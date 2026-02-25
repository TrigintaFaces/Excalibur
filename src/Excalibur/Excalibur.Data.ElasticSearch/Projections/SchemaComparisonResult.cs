// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents the result of comparing two schemas.
/// </summary>
public sealed class SchemaComparisonResult
{
	/// <summary>
	/// Gets a value indicating whether the schemas are identical.
	/// </summary>
	/// <value>
	/// A value indicating whether the schemas are identical.
	/// </value>
	public required bool AreIdentical { get; init; }

	/// <summary>
	/// Gets a value indicating whether the target schema is backwards compatible with the source.
	/// </summary>
	/// <value>
	/// A value indicating whether the target schema is backwards compatible with the source.
	/// </value>
	public required bool IsBackwardsCompatible { get; init; }

	/// <summary>
	/// Gets the fields added in the target schema.
	/// </summary>
	/// <value>
	/// The fields added in the target schema.
	/// </value>
	public required IReadOnlyList<FieldChange> AddedFields { get; init; }

	/// <summary>
	/// Gets the fields removed from the source schema.
	/// </summary>
	/// <value>
	/// The fields removed from the source schema.
	/// </value>
	public required IReadOnlyList<FieldChange> RemovedFields { get; init; }

	/// <summary>
	/// Gets the fields with modified types or properties.
	/// </summary>
	/// <value>
	/// The fields with modified types or properties.
	/// </value>
	public required IReadOnlyList<FieldChange> ModifiedFields { get; init; }

	/// <summary>
	/// Gets any breaking changes detected.
	/// </summary>
	/// <value>
	/// Any breaking changes detected.
	/// </value>
	public IReadOnlyList<string>? BreakingChanges { get; init; }

	/// <summary>
	/// Gets any warnings about the schema changes.
	/// </summary>
	/// <value>
	/// Any warnings about the schema changes.
	/// </value>
	public IReadOnlyList<string>? Warnings { get; init; }
}

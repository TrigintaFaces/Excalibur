// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents a field change in a schema.
/// </summary>
public sealed class FieldChange
{
	/// <summary>
	/// Gets the field name.
	/// </summary>
	/// <value>
	/// The field name.
	/// </value>
	public required string FieldName { get; init; }

	/// <summary>
	/// Gets the field path for nested fields.
	/// </summary>
	/// <value>
	/// The field path for nested fields.
	/// </value>
	public required string FieldPath { get; init; }

	/// <summary>
	/// Gets the change type.
	/// </summary>
	/// <value>
	/// The change type.
	/// </value>
	public required FieldChangeType ChangeType { get; init; }

	/// <summary>
	/// Gets the old field type or definition.
	/// </summary>
	/// <value>
	/// The old field type or definition.
	/// </value>
	public string? OldType { get; init; }

	/// <summary>
	/// Gets the new field type or definition.
	/// </summary>
	/// <value>
	/// The new field type or definition.
	/// </value>
	public string? NewType { get; init; }

	/// <summary>
	/// Gets a value indicating whether this is a breaking change.
	/// </summary>
	/// <value>
	/// A value indicating whether this is a breaking change.
	/// </value>
	public bool IsBreaking { get; init; }

	/// <summary>
	/// Gets the impact description.
	/// </summary>
	/// <value>
	/// The impact description.
	/// </value>
	public string? Impact { get; init; }
}

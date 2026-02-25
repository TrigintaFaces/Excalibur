// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Upcasting;

/// <summary>
/// Defines the operation to apply during a JSON event transformation.
/// </summary>
public enum JsonTransformOperation
{
	/// <summary>
	/// Renames a property from <see cref="JsonTransformRule.Path"/> to <see cref="JsonTransformRule.TargetPath"/>.
	/// </summary>
	Rename,

	/// <summary>
	/// Removes the property at <see cref="JsonTransformRule.Path"/>.
	/// </summary>
	Remove,

	/// <summary>
	/// Adds a property at <see cref="JsonTransformRule.Path"/> with <see cref="JsonTransformRule.DefaultValue"/>
	/// if it does not already exist.
	/// </summary>
	AddDefault,

	/// <summary>
	/// Moves a property from <see cref="JsonTransformRule.Path"/> to <see cref="JsonTransformRule.TargetPath"/>.
	/// Unlike <see cref="Rename"/>, this supports moving between different levels of the JSON hierarchy.
	/// </summary>
	Move
}

/// <summary>
/// A single JSON transformation rule that describes a property-level operation
/// on a serialized event.
/// </summary>
/// <param name="Operation">The transformation operation to apply.</param>
/// <param name="Path">The source JSON property name.</param>
/// <param name="TargetPath">The target JSON property name (for rename and move operations).</param>
/// <param name="DefaultValue">The default value to set (for add default operations).</param>
public sealed record JsonTransformRule(
	JsonTransformOperation Operation,
	string Path,
	string? TargetPath = null,
	object? DefaultValue = null);

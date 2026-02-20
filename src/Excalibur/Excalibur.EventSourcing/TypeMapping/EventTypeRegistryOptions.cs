// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.TypeMapping;

/// <summary>
/// Configuration options for the event type registry.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="Aliases"/> to configure event type renames without code changes.
/// The key is the old event type name, and the value is the current canonical name.
/// </para>
/// <para>
/// Use <see cref="TypeMappings"/> for explicit name-to-type mappings when the type name
/// cannot be resolved by convention.
/// </para>
/// </remarks>
public sealed class EventTypeRegistryOptions
{
	/// <summary>
	/// Gets or sets the alias mappings from old event type names to current canonical names.
	/// </summary>
	/// <value>A dictionary where keys are old names and values are current canonical names.</value>
	public Dictionary<string, string> Aliases { get; set; } = [];

	/// <summary>
	/// Gets or sets the explicit event type name to CLR type mappings.
	/// </summary>
	/// <value>A dictionary where keys are event type names and values are the corresponding CLR types.</value>
	public Dictionary<string, Type> TypeMappings { get; set; } = [];
}

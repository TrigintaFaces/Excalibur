// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Utility methods for generating stable event type identifiers.
/// </summary>
public static class EventTypeNameHelper
{
	/// <summary>
	/// Gets the canonical name used when persisting event types.
	/// </summary>
	/// <param name="type">The event CLR type.</param>
	/// <returns>An assembly-qualified name when available, otherwise the full type name.</returns>
	public static string GetEventTypeName(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.TypeResolution;

/// <summary>
/// Interface for type resolution services.
/// </summary>
/// <remarks>
/// This interface allows different implementations of type resolution, including AOT-safe registries and reflection-based resolvers.
/// </remarks>
public interface ITypeResolver
{
	/// <summary>
	/// Tries to resolve a type by name.
	/// </summary>
	/// <param name="typeName"> The name of the type to resolve. </param>
	/// <param name="type"> The resolved type if found. </param>
	/// <returns> True if the type was resolved, false otherwise. </returns>
	bool TryGetType(string typeName, [NotNullWhen(true)] out Type? type);
}

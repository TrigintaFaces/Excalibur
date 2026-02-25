// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Runtime;

namespace Excalibur.Dispatch.TypeResolution;

/// <summary>
/// Utility class for AOT-compatible type resolution.
/// </summary>
public static class TypeResolver
{
	/// <summary>
	/// Resolves a type by name using the appropriate method based on runtime mode.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Conditional compilation ensures AOT compatibility")]
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "JIT fallback resolves types from loaded assemblies via reflection")]
	public static Type? ResolveType(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			return null;
		}

		// Use compile-time conditional for zero overhead
#if AOT_ENABLED
		// In AOT mode, only use the registry
		return TypeResolverRegistry.TryResolveType(typeName, out var type) ? type : null;
#else
		// In JIT mode, try registry first, then fall back to loaded assemblies
		if (TypeResolverRegistry.TryResolveType(typeName, out var type))
		{
			return type;
		}

		return ResolveFromLoadedAssemblies(typeName);
#endif
	}

	/// <summary>
	/// Resolves a type by name using runtime detection.
	/// </summary>
	/// <remarks>
	/// This method uses runtime detection instead of compile-time conditionals, which has a small performance cost but allows for more
	/// flexible deployment.
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Runtime check ensures AOT compatibility")]
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "JIT fallback resolves types from loaded assemblies via reflection")]
	public static Type? ResolveTypeRuntime(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			return null;
		}

		return AotDetection.Execute(
			aotPath: () =>

				// In AOT mode, only use the registry
				TypeResolverRegistry.TryResolveType(typeName, out var type) ? type : null,
			jitPath: () =>
			{
				// In JIT mode, try registry first, then fall back to loaded assemblies
				if (TypeResolverRegistry.TryResolveType(typeName, out var type))
				{
					return type;
				}

				return ResolveFromLoadedAssemblies(typeName);
			});
	}

	/// <summary>
	/// Ensures a type can be resolved, throwing an exception if not found.
	/// </summary>
	/// <exception cref="TypeLoadException"> Thrown when the type cannot be resolved. </exception>
	public static Type ResolveTypeRequired(string typeName)
	{
		var type = ResolveType(typeName);
		if (type == null)
		{
			var mode = AotDetection.IsAotCompiled ? "AOT" : "JIT";
			throw new TypeLoadException(
				$"Type '{typeName}' could not be resolved in {mode} mode. " +
				(AotDetection.IsAotCompiled
					? "Ensure the type is registered with a type resolver."
					: "Ensure the type exists and is loaded."));
		}

		return type;
	}

	private static Type? ResolveFromLoadedAssemblies(string typeName)
	{
		var lookupName = typeName;
		var separatorIndex = typeName.IndexOf(',', StringComparison.Ordinal);
		if (separatorIndex > 0)
		{
			lookupName = typeName[..separatorIndex].Trim();
		}

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			var resolved = assembly.GetType(typeName, throwOnError: false, ignoreCase: false)
				?? assembly.GetType(lookupName, throwOnError: false, ignoreCase: false);
			if (resolved != null)
			{
				return resolved;
			}
		}

		return null;
	}
}

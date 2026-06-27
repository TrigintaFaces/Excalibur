// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Runtime;

namespace Excalibur.Dispatch.TypeResolution;

/// <summary>
/// Utility class for AOT-compatible type resolution.
/// </summary>
internal static class TypeResolver
{
	/// <summary>
	/// Resolves a type by name using the appropriate method based on runtime mode.
	/// </summary>
	/// <param name="typeName">The type name to resolve.</param>
	/// <param name="allowAssemblyScan">
	/// 6v2z7q: when <see langword="true"/>, an unregistered type name falls back to an unbounded scan of all
	/// loaded assemblies (JIT only). That scan is the gadget-chain vector, so it defaults to <see langword="false"/>:
	/// the secure default resolves only registry-registered types. Pass <see langword="true"/> ONLY when the type
	/// name is trusted (e.g. migrating the consumer's own store); untrusted names (e.g. a remote dead-letter
	/// envelope) MUST leave it <see langword="false"/>. Mirrors the c6wd6f secure default.
	/// </param>
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "RuntimeFeature check ensures AOT path avoids reflection")]
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "JIT fallback resolves types from loaded assemblies via reflection")]
	public static Type? ResolveType(string typeName, bool allowAssemblyScan = false)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			return null;
		}

		// Try registry first (works in both JIT and AOT)
		if (TypeResolverRegistry.TryResolveType(typeName, out var type))
		{
			return type;
		}

		// AOT path: registry is the only resolution mechanism
		if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
		{
			return null;
		}

		// 6v2z7q: the unbounded JIT assembly scan is the gadget-chain vector — an untrusted type name can
		// resolve an attacker-chosen type from any loaded assembly. It is OFF by default; only a caller that
		// KNOWS the type name is trusted opts in. An unregistered type otherwise resolves to null.
		if (!allowAssemblyScan)
		{
			return null;
		}

		// JIT path (opt-in only): fall back to loaded assemblies
		return ResolveFromLoadedAssemblies(typeName);
	}

	/// <summary>
	/// Ensures a type can be resolved, throwing an exception if not found.
	/// </summary>
	/// <exception cref="TypeLoadException"> Thrown when the type cannot be resolved. </exception>
	public static Type ResolveTypeRequired(string typeName, bool allowAssemblyScan = false)
	{
		var type = ResolveType(typeName, allowAssemblyScan);
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

	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "Assembly.GetType is used as a JIT-only fallback; AOT path returns null before reaching this method")]
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

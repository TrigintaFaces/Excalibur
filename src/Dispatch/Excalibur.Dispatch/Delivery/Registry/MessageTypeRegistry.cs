// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery.Registry;

/// <summary>
/// Manual implementation of MessageTypeRegistry for development/testing.
/// </summary>
/// <remarks>
/// This is a temporary workaround while the build environment issue prevents the source generator from running. Once the generator works,
/// this file should be deleted in favor of the generated version.
/// </remarks>
public static class MessageTypeRegistry
{
	private static readonly Dictionary<string, Type> TypeNameToType = [];
	private static readonly Dictionary<string, Type> SimpleNameToType = [];
#if NET9_0_OR_GREATER

	private static readonly Lock RegistryLock = new();

#else

	private static readonly object RegistryLock = new();

#endif
	private static bool _isInitialized;

	/// <summary>
	/// Initializes static members of the <see cref="MessageTypeRegistry"/> class.
	/// Static constructor to initialize known types.
	/// </summary>
	static MessageTypeRegistry() => InitializeKnownTypes();

	/// <summary>
	/// Tries to get a Type from its name.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Using precompiled type registry")]
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "All types are statically referenced")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2057:Unrecognized value passed to the parameter of method. It's not possible to guarantee the availability of the target type.",
		Justification = "Type.GetType is only used in non-AOT builds as a development fallback. In AOT builds, this code is not compiled.")]
	public static bool TryGetType(string typeName, [NotNullWhen(true)] out Type? type)
	{
		EnsureInitialized();

		if (TypeNameToType.TryGetValue(typeName, out type))
		{
			return true;
		}

		if (SimpleNameToType.TryGetValue(typeName, out type))
		{
			return true;
		}

		// Fallback disabled to maintain AOT and banned API compliance. Types must be registered explicitly or via source generation.
		type = null;
		return false;
	}

	/// <summary>
	/// Gets a Type from its name.
	/// </summary>
	/// <exception cref="TypeLoadException"></exception>
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Using precompiled type registry")]
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "All types are statically referenced")]
	public static Type GetType(string typeName)
	{
		if (TryGetType(typeName, out var type))
		{
			return type;
		}

		throw new TypeLoadException(
			$"Type '{typeName}' not found in message type registry. " +
			"Ensure the type implements IDispatchMessage and is registered.");
	}

	/// <summary>
	/// Gets all registered message types.
	/// </summary>
	public static IEnumerable<Type> GetAllMessageTypes()
	{
		EnsureInitialized();
		return TypeNameToType.Values;
	}

	/// <summary>
	/// Registers a type in the registry.
	/// </summary>
	public static void RegisterType(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		lock (RegistryLock)
		{
			// Register by assembly-qualified name
			var assemblyQualifiedName = type.AssemblyQualifiedName;
			if (assemblyQualifiedName != null)
			{
				TypeNameToType[assemblyQualifiedName] = type;
			}

			// Register by simple name
			SimpleNameToType[type.Name] = type;

			// Also register without version for compatibility
			var typeName = $"{type.FullName}, {type.Assembly.GetName().Name}";
			TypeNameToType[typeName] = type;
		}
	}

	/// <summary>
	/// Registers a type in the registry (generic version).
	/// </summary>
	public static void RegisterType<T>()
		where T : IDispatchMessage
		=> RegisterType(typeof(T));

	private static void EnsureInitialized()
	{
		if (!_isInitialized)
		{
			lock (RegistryLock)
			{
				if (!_isInitialized)
				{
					InitializeKnownTypes();
					_isInitialized = true;
				}
			}
		}
	}

	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Assembly scanning is only used in non-AOT builds during development. In AOT builds, this code is conditionally excluded and types are registered via source generation.")]
	private static void InitializeKnownTypes()
	{
		// Manual type registration for known message types In AOT deployments, the source generator will replace this manual registration
		// with compile-time discovery For development/runtime scenarios, assembly scanning below provides automatic discovery

		// Example manual registrations: RegisterType(typeof(PingCommand)); RegisterType(typeof(TestEvent)); RegisterType(typeof(ComplexAction));

		// Assembly scanning for development - in production, use explicit registration or source generation
#if !AOT_ENABLED
		try
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				// Skip system assemblies for performance
				if (assembly.FullName?.StartsWith("System.", StringComparison.Ordinal) == true ||
					assembly.FullName?.StartsWith("Microsoft.", StringComparison.Ordinal) == true)
				{
					continue;
				}

				try
				{
					foreach (var type in assembly.GetTypes())
					{
						if (type is { IsAbstract: false, IsInterface: false } &&
							typeof(IDispatchMessage).IsAssignableFrom(type))
						{
							RegisterType(type);
						}
					}
				}
				catch
				{
					// Ignore types that can't be loaded
				}
			}
		}
		catch
		{
			// Ignore assembly scanning errors
		}
#endif
	}
}

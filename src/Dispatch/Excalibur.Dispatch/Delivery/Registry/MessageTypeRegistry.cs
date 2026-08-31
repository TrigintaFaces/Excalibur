// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Extensions;

namespace Excalibur.Dispatch.Delivery.Registry;

/// <summary>
/// Resolves a stored message type name back to its <see cref="Type"/> for the durable drains (inbox, outbox, scheduler) and for
/// runtime JSON deserialization.
/// </summary>
/// <remarks>
/// <para>
/// A type is indexed under every name form the framework writes to durable storage: its assembly-qualified name, its
/// <see cref="Type.FullName"/>, <c>"FullName, AssemblyName"</c>, and its simple <see cref="MemberInfo.Name"/>. A name a
/// producer stored therefore resolves on the consuming side regardless of which of those forms it chose.
/// </para>
/// <para>
/// Name collisions are refused, never guessed. If two distinct types claim the same name form -- two types sharing a simple name
/// across namespaces is the common case -- that name becomes permanently ambiguous and resolution fails for it, while the more
/// specific forms of both types keep resolving. A caller therefore never receives a type that merely happens to share a name with
/// the one that was stored, which would deserialize into the wrong shape and silently drop the fields that did not match.
/// </para>
/// </remarks>
public static class MessageTypeRegistry
{
	/// <summary>
	/// Maps every registered name form to its type. A <see langword="null"/> value marks a name claimed by more than one type; the
	/// name is then ambiguous and never resolves. Ambiguity is sticky -- once a name is contested no later registration can make it
	/// unambiguous again.
	/// </summary>
	private static readonly ConcurrentDictionary<string, Type?> NameToType = new(StringComparer.Ordinal);

	private static readonly Lock InitializationLock = new();

	private static volatile bool _isInitialized;

	/// <summary>
	/// Tries to resolve a registered type from any of the name forms it was indexed under.
	/// </summary>
	/// <param name="typeName"> The stored type name. </param>
	/// <param name="type"> The resolved type when this method returns <see langword="true"/>; otherwise <see langword="null"/>. </param>
	/// <returns>
	/// <see langword="true"/> when the name resolves to exactly one registered type; <see langword="false"/> when no type is
	/// registered under that name, or when the name is ambiguous across two or more types.
	/// </returns>
	public static bool TryGetType(string typeName, [NotNullWhen(true)] out Type? type)
	{
		EnsureInitialized();

		// A miss and an ambiguity are deliberately the same answer to the caller: in both cases there is no single type the name
		// can honestly be resolved to. Reflection fallback stays disabled to keep the registry AOT-safe.
		return NameToType.TryGetValue(typeName, out type) && type is not null;
	}

	/// <summary>
	/// Gets every distinct registered message type.
	/// </summary>
	/// <returns> The registered types, each appearing once regardless of how many name forms index it. </returns>
	public static IEnumerable<Type> GetAllMessageTypes()
	{
		EnsureInitialized();
		return NameToType.Values.Where(static t => t is not null).Distinct()!;
	}

	/// <summary>
	/// Registers a type under every name form the framework may store for it.
	/// </summary>
	/// <param name="type"> The message type to register. </param>
	public static void RegisterType(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		Claim(type.AssemblyQualifiedName, type);
		Claim(type.FullName, type);
		Claim(type.Name, type);

		if (type.FullName is { } fullName)
		{
			Claim($"{fullName}, {type.Assembly.GetName().Name}", type);
		}
	}

	/// <summary>
	/// Registers a type under every name form the framework may store for it.
	/// </summary>
	/// <typeparam name="T"> The message type to register. </typeparam>
	public static void RegisterType<T>()
		where T : IDispatchMessage
		=> RegisterType(typeof(T));

	/// <summary>
	/// Claims a name for a type, marking the name ambiguous if a different type already holds it.
	/// </summary>
	private static void Claim(string? name, Type type)
	{
		if (string.IsNullOrEmpty(name))
		{
			return;
		}

		// The update leaves the name owned by `type` only when `type` already owns it, so re-registering the same type is
		// idempotent and any contest collapses to the ambiguous marker. There is no argument that makes an ambiguous name
		// unambiguous again, which is what keeps a late-loading assembly from deciding a collision in its own favour.
		_ = NameToType.AddOrUpdate(name, type, (_, existing) => existing == type ? existing : null);
	}

	private static void EnsureInitialized()
	{
		if (_isInitialized)
		{
			return;
		}

		lock (InitializationLock)
		{
			if (_isInitialized)
			{
				return;
			}

			InitializeKnownTypes();
			_isInitialized = true;
		}
	}

	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Assembly scanning runs only where dynamic code is supported. Under AOT the guard below is false and types are registered explicitly or by source generation.")]
	private static void InitializeKnownTypes()
	{
		if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
		{
			return;
		}

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			// Skip framework assemblies: they carry no message types and dominate the scan cost.
			if (assembly.FullName?.StartsWith("System.", StringComparison.Ordinal) == true ||
				assembly.FullName?.StartsWith("Microsoft.", StringComparison.Ordinal) == true)
			{
				continue;
			}

			foreach (var type in assembly.GetLoadableTypes())
			{
				if (type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false } &&
					typeof(IDispatchMessage).IsAssignableFrom(type))
				{
					RegisterType(type);
				}
			}
		}
	}

}

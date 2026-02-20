// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Delivery.Registry;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Extension methods for AOT-friendly JSON serialization.
/// </summary>
public static class AotJsonSerializerExtensions
{
	/// <summary>
	/// Attempts to deserialize a message using the MessageTypeRegistry.
	/// </summary>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Using member 'IJsonSerializer.Deserialize(String, Type)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code",
		Justification =
			"Types are resolved from MessageTypeRegistry which preserves all registered message types. In AOT builds, messages should use source-generated JSON contexts.")]
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	public static bool TryDeserializeMessage(this IJsonSerializer serializer, string json, string messageTypeName,
		[NotNullWhen(true)] out object? message)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		message = null;

		try
		{
			// Try to resolve the type from the registry
			var messageType = MessageTypeRegistry.GetType(messageTypeName);
			if (messageType == null)
			{
				return false;
			}

			message = serializer.Deserialize(json, messageType);
			return message != null;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Gets the type name for a message that can be resolved by the registry.
	/// </summary>
	public static string GetMessageTypeName(this IJsonSerializer serializer, Type messageType)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(messageType);

		// For registered types, we can use the simple name if it's unambiguous
		var simpleName = messageType.Name;
		var resolvedType = MessageTypeRegistry.GetType(simpleName);
		if (resolvedType == messageType)
		{
			return simpleName;
		}

		// Otherwise use the assembly qualified name
		return messageType.AssemblyQualifiedName ?? messageType.FullName ?? messageType.Name;
	}
}

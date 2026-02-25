// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// A subject naming strategy that uses the fully-qualified .NET type name.
/// </summary>
/// <remarks>
/// <para>
/// This strategy derives the subject from the message type's namespace and name,
/// ignoring the topic. This allows the same schema to be used across multiple topics.
/// </para>
/// <para>
/// Subject format: <c>{namespace}.{typename}</c>
/// </para>
/// <para>
/// Example: For type <c>MyApp.Events.OrderCreated</c>, subject is "MyApp.Events.OrderCreated".
/// </para>
/// </remarks>
public sealed class RecordNameStrategy : ISubjectNameStrategy
{
	/// <inheritdoc/>
	public string GetValueSubject(string topic, Type messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);
		return GetFullTypeName(messageType);
	}

	/// <inheritdoc/>
	public string GetKeySubject(string topic, Type keyType)
	{
		ArgumentNullException.ThrowIfNull(keyType);
		return GetFullTypeName(keyType);
	}

	private static string GetFullTypeName(Type type)
	{
		// Use the full name which includes namespace
		// For nested types, this produces "Namespace.OuterType+InnerType"
		var fullName = type.FullName;

		// Handle generic types - remove the backtick notation
		if (fullName != null && type.IsGenericType)
		{
			var genericIndex = fullName.IndexOf('`', StringComparison.Ordinal);
			if (genericIndex > 0)
			{
				fullName = fullName[..genericIndex];
			}
		}

		return fullName ?? type.Name;
	}
}

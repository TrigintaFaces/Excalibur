// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// A subject naming strategy that combines topic name and .NET type name.
/// </summary>
/// <remarks>
/// <para>
/// This strategy derives the subject from both the topic and the message type,
/// providing topic isolation while still identifying the message type.
/// </para>
/// <para>
/// Subject format: <c>{topic}-{namespace}.{typename}</c>
/// </para>
/// <para>
/// Example: For topic "orders" and type <c>MyApp.Events.OrderCreated</c>,
/// subject is "orders-MyApp.Events.OrderCreated".
/// </para>
/// </remarks>
public sealed class TopicRecordNameStrategy : ISubjectNameStrategy
{
	/// <inheritdoc/>
	public string GetValueSubject(string topic, Type messageType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		ArgumentNullException.ThrowIfNull(messageType);

		return $"{topic}-{GetFullTypeName(messageType)}";
	}

	/// <inheritdoc/>
	public string GetKeySubject(string topic, Type keyType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		ArgumentNullException.ThrowIfNull(keyType);

		return $"{topic}-{GetFullTypeName(keyType)}";
	}

	private static string GetFullTypeName(Type type)
	{
		var fullName = type.FullName;

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

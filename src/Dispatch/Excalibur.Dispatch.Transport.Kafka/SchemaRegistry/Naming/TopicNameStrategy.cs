// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// A subject naming strategy that uses the topic name as the subject.
/// </summary>
/// <remarks>
/// <para>
/// This is the default Confluent Schema Registry naming strategy.
/// </para>
/// <para>
/// Subject format:
/// </para>
/// <list type="bullet">
///   <item><description>Value: <c>{topic}-value</c></description></item>
///   <item><description>Key: <c>{topic}-key</c></description></item>
/// </list>
/// <para>
/// Example: For topic "orders", value subject is "orders-value".
/// </para>
/// </remarks>
public sealed class TopicNameStrategy : ISubjectNameStrategy
{
	/// <inheritdoc/>
	public string GetValueSubject(string topic, Type messageType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		return $"{topic}-value";
	}

	/// <inheritdoc/>
	public string GetKeySubject(string topic, Type keyType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		return $"{topic}-key";
	}
}

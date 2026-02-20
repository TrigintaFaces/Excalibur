// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Defines the strategy for generating Schema Registry subject names.
/// </summary>
/// <remarks>
/// <para>
/// The subject name is the unique identifier for a schema in the Schema Registry.
/// Different strategies determine how the subject is derived from the topic
/// and message type.
/// </para>
/// <para>
/// Built-in strategies:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="TopicNameStrategy"/>: <c>{topic}-value</c></description></item>
///   <item><description><see cref="RecordNameStrategy"/>: <c>{namespace}.{type}</c></description></item>
///   <item><description><see cref="TopicRecordNameStrategy"/>: <c>{topic}-{namespace}.{type}</c></description></item>
/// </list>
/// </remarks>
public interface ISubjectNameStrategy
{
	/// <summary>
	/// Gets the subject name for a message value.
	/// </summary>
	/// <param name="topic">The Kafka topic name.</param>
	/// <param name="messageType">The .NET type being serialized.</param>
	/// <returns>The Schema Registry subject name.</returns>
	string GetValueSubject(string topic, Type messageType);

	/// <summary>
	/// Gets the subject name for a message key.
	/// </summary>
	/// <param name="topic">The Kafka topic name.</param>
	/// <param name="keyType">The .NET type of the message key.</param>
	/// <returns>The Schema Registry subject name.</returns>
	string GetKeySubject(string topic, Type keyType);
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Provides helpers for constructing schema registry subject names.
/// </summary>
/// <remarks>
/// <para>
/// This class implements the TopicNameStrategy (default) for subject naming.
/// Future implementations may support RecordNameStrategy and TopicRecordNameStrategy.
/// </para>
/// <para>
/// Subject format: <c>{topic}-key</c> or <c>{topic}-value</c>
/// </para>
/// </remarks>
public static class SchemaRegistrySubject
{
	/// <summary>
	/// The suffix appended to topic names for value schemas.
	/// </summary>
	public const string ValueSuffix = "-value";

	/// <summary>
	/// The suffix appended to topic names for key schemas.
	/// </summary>
	public const string KeySuffix = "-key";

	/// <summary>
	/// Creates a subject name for a value schema using TopicNameStrategy.
	/// </summary>
	/// <param name="topicName">The Kafka topic name.</param>
	/// <returns>The subject name in the format <c>{topic}-value</c>.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="topicName"/> is null or whitespace.
	/// </exception>
	public static string ForValue(string topicName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
		return $"{topicName}{ValueSuffix}";
	}

	/// <summary>
	/// Creates a subject name for a key schema using TopicNameStrategy.
	/// </summary>
	/// <param name="topicName">The Kafka topic name.</param>
	/// <returns>The subject name in the format <c>{topic}-key</c>.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="topicName"/> is null or whitespace.
	/// </exception>
	public static string ForKey(string topicName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
		return $"{topicName}{KeySuffix}";
	}

	/// <summary>
	/// Extracts the topic name from a subject name.
	/// </summary>
	/// <param name="subject">The subject name.</param>
	/// <returns>The topic name, or the original subject if it doesn't match the expected format.</returns>
	public static string ExtractTopicName(string subject)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subject);

		if (subject.EndsWith(ValueSuffix, StringComparison.Ordinal))
		{
			return subject[..^ValueSuffix.Length];
		}

		if (subject.EndsWith(KeySuffix, StringComparison.Ordinal))
		{
			return subject[..^KeySuffix.Length];
		}

		return subject;
	}

	/// <summary>
	/// Determines if a subject is for a value schema.
	/// </summary>
	/// <param name="subject">The subject name to check.</param>
	/// <returns>
	/// <see langword="true"/> if the subject ends with <c>-value</c>; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool IsValueSubject(string subject)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subject);
		return subject.EndsWith(ValueSuffix, StringComparison.Ordinal);
	}

	/// <summary>
	/// Determines if a subject is for a key schema.
	/// </summary>
	/// <param name="subject">The subject name to check.</param>
	/// <returns>
	/// <see langword="true"/> if the subject ends with <c>-key</c>; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool IsKeySubject(string subject)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subject);
		return subject.EndsWith(KeySuffix, StringComparison.Ordinal);
	}
}

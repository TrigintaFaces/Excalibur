// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Specifies the subject naming strategy for Schema Registry subjects.
/// </summary>
/// <remarks>
/// <para>
/// The subject name uniquely identifies a schema in the Schema Registry.
/// Different strategies determine how the subject is derived from the
/// topic and message type.
/// </para>
/// <para>
/// For custom strategies, implement <see cref="ISubjectNameStrategy"/> and use
/// <c>SubjectNameStrategy&lt;TStrategy&gt;()</c> on the builder.
/// </para>
/// </remarks>
public enum SubjectNameStrategy
{
	/// <summary>
	/// Subject is derived from the topic name: <c>{topic}-value</c>.
	/// </summary>
	/// <remarks>
	/// This is the default Confluent strategy. All messages on the same topic
	/// must use the same schema (with compatible evolution).
	/// </remarks>
	TopicName = 0,

	/// <summary>
	/// Subject is derived from the record type: <c>{namespace}.{type}</c>.
	/// </summary>
	/// <remarks>
	/// Allows multiple schemas per topic. Each message type has its own
	/// schema subject independent of the topic.
	/// </remarks>
	RecordName = 1,

	/// <summary>
	/// Subject combines topic and record: <c>{topic}-{namespace}.{type}</c>.
	/// </summary>
	/// <remarks>
	/// Allows different schemas for the same type on different topics.
	/// Provides maximum flexibility at the cost of more subjects.
	/// </remarks>
	TopicRecordName = 2
}

/// <summary>
/// Extension methods for <see cref="SubjectNameStrategy"/>.
/// </summary>
public static class SubjectNameStrategyExtensions
{
	/// <summary>
	/// Creates an <see cref="ISubjectNameStrategy"/> instance for this strategy.
	/// </summary>
	/// <param name="strategy">The subject name strategy.</param>
	/// <returns>An implementation of <see cref="ISubjectNameStrategy"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="strategy"/> is not a valid enum value.
	/// </exception>
	public static ISubjectNameStrategy ToStrategy(this SubjectNameStrategy strategy) => strategy switch
	{
		SubjectNameStrategy.TopicName => new TopicNameStrategy(),
		SubjectNameStrategy.RecordName => new RecordNameStrategy(),
		SubjectNameStrategy.TopicRecordName => new TopicRecordNameStrategy(),
		_ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown subject name strategy")
	};
}

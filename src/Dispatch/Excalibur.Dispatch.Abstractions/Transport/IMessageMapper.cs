// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Defines a mapper that transforms messages between different transport formats.
/// </summary>
/// <remarks>
/// <para>
/// Message mappers enable multi-transport scenarios by transforming messages as they
/// move between different messaging systems (e.g., RabbitMQ to Kafka, or internal to external format).
/// </para>
/// <para>
/// Mappers operate on the transport context level, allowing transformation of:
/// </para>
/// <list type="bullet">
/// <item><description>Message headers and metadata</description></item>
/// <item><description>Transport-specific properties (routing keys, partition keys, etc.)</description></item>
/// <item><description>Correlation and tracing information</description></item>
/// </list>
/// <para>
/// For message payload transformation, implement <see cref="IMessageMapper{TSource,TTarget}"/>.
/// </para>
/// </remarks>
public interface IMessageMapper
{
	/// <summary>
	/// Gets the name of this mapper for registration and logging purposes.
	/// </summary>
	/// <value>A unique name identifying this mapper.</value>
	string Name { get; }

	/// <summary>
	/// Gets the source transport type this mapper handles.
	/// </summary>
	/// <value>The source transport name (e.g., "rabbitmq", "kafka", "*" for any).</value>
	string SourceTransport { get; }

	/// <summary>
	/// Gets the target transport type this mapper produces.
	/// </summary>
	/// <value>The target transport name (e.g., "rabbitmq", "kafka", "*" for any).</value>
	string TargetTransport { get; }

	/// <summary>
	/// Determines whether this mapper can handle the specified source and target transports.
	/// </summary>
	/// <param name="sourceTransport">The source transport name.</param>
	/// <param name="targetTransport">The target transport name.</param>
	/// <returns>
	/// <see langword="true"/> if this mapper can handle the transformation;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	bool CanMap(string sourceTransport, string targetTransport);

	/// <summary>
	/// Maps a source context to a target context for cross-transport delivery.
	/// </summary>
	/// <param name="source">The source transport message context.</param>
	/// <param name="targetTransportName">The name of the target transport.</param>
	/// <returns>A new context configured for the target transport.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="source"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the mapper cannot handle the specified transport combination.
	/// </exception>
	ITransportMessageContext Map(ITransportMessageContext source, string targetTransportName);
}

/// <summary>
/// Defines a typed mapper that transforms messages between specific source and target types.
/// </summary>
/// <typeparam name="TSource">The source message type.</typeparam>
/// <typeparam name="TTarget">The target message type.</typeparam>
/// <remarks>
/// <para>
/// Use this interface when you need to transform the message payload itself,
/// not just the transport context. This is useful for:
/// </para>
/// <list type="bullet">
/// <item><description>Converting between internal and external message formats</description></item>
/// <item><description>Version upgrades/downgrades for message schemas</description></item>
/// <item><description>Enriching messages with additional data</description></item>
/// </list>
/// </remarks>
public interface IMessageMapper<in TSource, out TTarget>
{
	/// <summary>
	/// Maps a source message to a target message type.
	/// </summary>
	/// <param name="source">The source message to transform.</param>
	/// <param name="context">The transport context for the mapping operation.</param>
	/// <returns>The transformed target message.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="source"/> is <see langword="null"/>.
	/// </exception>
	TTarget Map(TSource source, ITransportMessageContext context);
}

/// <summary>
/// Registry for message mappers that enables lookup by transport combination.
/// </summary>
/// <remarks>
/// <para>
/// The mapper registry maintains a collection of <see cref="IMessageMapper"/> instances
/// and provides lookup functionality to find the appropriate mapper for a given
/// source/target transport combination.
/// </para>
/// </remarks>
public interface IMessageMapperRegistry
{
	/// <summary>
	/// Registers a message mapper.
	/// </summary>
	/// <param name="mapper">The mapper to register.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="mapper"/> is <see langword="null"/>.
	/// </exception>
	void Register(IMessageMapper mapper);

	/// <summary>
	/// Gets a mapper for the specified transport combination.
	/// </summary>
	/// <param name="sourceTransport">The source transport name.</param>
	/// <param name="targetTransport">The target transport name.</param>
	/// <returns>
	/// The registered mapper, or <see langword="null"/> if no mapper is registered
	/// for the specified combination.
	/// </returns>
	IMessageMapper? GetMapper(string sourceTransport, string targetTransport);

	/// <summary>
	/// Gets all registered mappers.
	/// </summary>
	/// <returns>An enumerable of all registered mappers.</returns>
	IEnumerable<IMessageMapper> GetAllMappers();

	/// <summary>
	/// Determines whether a mapper exists for the specified transport combination.
	/// </summary>
	/// <param name="sourceTransport">The source transport name.</param>
	/// <param name="targetTransport">The target transport name.</param>
	/// <returns>
	/// <see langword="true"/> if a mapper is registered for the combination;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	bool HasMapper(string sourceTransport, string targetTransport);
}

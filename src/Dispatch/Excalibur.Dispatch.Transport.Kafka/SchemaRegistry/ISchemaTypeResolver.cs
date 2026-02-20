// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Resolves Schema Registry schema IDs to .NET types.
/// </summary>
/// <remarks>
/// <para>
/// This interface bridges the Schema Registry (which stores schemas by ID) with
/// the .NET type system. It uses the JSON Schema <c>title</c> property to map
/// schemas to registered .NET types.
/// </para>
/// <para>
/// Resolution Flow:
/// </para>
/// <list type="number">
///   <item><description>Extract schema ID from wire format header</description></item>
///   <item><description>Fetch schema from Schema Registry</description></item>
///   <item><description>Parse <c>title</c> property from JSON Schema</description></item>
///   <item><description>Look up registered .NET type by message type name</description></item>
///   <item><description>Cache resolution for O(1) subsequent lookups</description></item>
/// </list>
/// </remarks>
public interface ISchemaTypeResolver
{
	/// <summary>
	/// Resolves a schema ID to a .NET type.
	/// </summary>
	/// <param name="schemaId">The Schema Registry schema ID.</param>
	/// <param name="subject">The subject name (for logging/diagnostics).</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The type resolution result.</returns>
	/// <exception cref="SchemaRegistryException">
	/// The schema could not be fetched from the registry.
	/// </exception>
	Task<SchemaTypeResolution> ResolveTypeAsync(
		int schemaId,
		string subject,
		CancellationToken cancellationToken);

	/// <summary>
	/// Registers a message type for schema resolution.
	/// </summary>
	/// <typeparam name="T">The message type to register.</typeparam>
	/// <remarks>
	/// <para>
	/// The type name is extracted from the type and used for matching against
	/// the JSON Schema <c>title</c> property. Call this method at startup for
	/// all message types that may be received.
	/// </para>
	/// </remarks>
	void RegisterType<T>() where T : IDispatchMessage;

	/// <summary>
	/// Registers a message type for schema resolution with a custom type name.
	/// </summary>
	/// <typeparam name="T">The message type to register.</typeparam>
	/// <param name="messageTypeName">The message type name to use for matching.</param>
	/// <remarks>
	/// <para>
	/// Use this overload when the .NET type name differs from the schema <c>title</c>.
	/// </para>
	/// </remarks>
	void RegisterType<T>(string messageTypeName) where T : IDispatchMessage;

	/// <summary>
	/// Registers a message type for schema resolution.
	/// </summary>
	/// <param name="messageType">The message type to register.</param>
	/// <remarks>
	/// <para>
	/// Runtime type registration for scenarios where the type is only known at runtime.
	/// </para>
	/// </remarks>
	void RegisterType(Type messageType);

	/// <summary>
	/// Registers a message type for schema resolution with a custom type name.
	/// </summary>
	/// <param name="messageType">The message type to register.</param>
	/// <param name="messageTypeName">The message type name to use for matching.</param>
	void RegisterType(Type messageType, string messageTypeName);

	/// <summary>
	/// Gets a value indicating whether a type is registered for the given message type name.
	/// </summary>
	/// <param name="messageTypeName">The message type name to check.</param>
	/// <returns>
	/// <see langword="true"/> if a type is registered; otherwise, <see langword="false"/>.
	/// </returns>
	bool IsTypeRegistered(string messageTypeName);
}

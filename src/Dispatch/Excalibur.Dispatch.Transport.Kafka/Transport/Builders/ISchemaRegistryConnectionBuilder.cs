// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Defines connection configuration methods for a Schema Registry builder.
/// </summary>
public interface ISchemaRegistryConnectionBuilder
{
	/// <summary>
	/// Sets the Schema Registry URL.
	/// </summary>
	/// <param name="url">The Schema Registry URL (e.g., "http://localhost:8081").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="url"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// For high availability, use <see cref="SchemaRegistryUrls"/> to specify multiple URLs.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder SchemaRegistryUrl(string url);

	/// <summary>
	/// Sets multiple Schema Registry URLs for high availability.
	/// </summary>
	/// <param name="urls">The Schema Registry URLs.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="urls"/> is null or empty.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The client will attempt to connect to URLs in order, failing over to the next
	/// if the current URL is unavailable.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder SchemaRegistryUrls(params string[] urls);

	/// <summary>
	/// Configures basic authentication credentials.
	/// </summary>
	/// <param name="username">The username.</param>
	/// <param name="password">The password.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="username"/> or <paramref name="password"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Basic auth is sent as a Base64-encoded header. Use SSL/TLS to protect credentials in transit.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder BasicAuth(string username, string password);

	/// <summary>
	/// Configures SSL/TLS settings for the Schema Registry connection.
	/// </summary>
	/// <param name="configure">The SSL configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	IConfluentSchemaRegistryBuilder ConfigureSsl(Action<ISchemaRegistrySslBuilder> configure);
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Fluent builder interface for configuring Confluent Schema Registry integration.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// It provides a discoverable API for configuring Schema Registry options.
/// </para>
/// <para>
/// All methods return <c>this</c> for method chaining, enabling a fluent configuration experience.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddKafkaTransport("events", kafka =>
/// {
///     kafka.BootstrapServers("localhost:9092")
///          .UseConfluentSchemaRegistry(registry =>
///          {
///              registry.SchemaRegistryUrl("http://localhost:8081")
///                      .SubjectNameStrategy(SubjectNameStrategy.TopicNameStrategy)
///                      .CompatibilityMode(CompatibilityMode.Backward)
///                      .AutoRegisterSchemas(true)
///                      .CacheSchemas(true)
///                      .CacheCapacity(1000)
///                      .RequestTimeout(TimeSpan.FromSeconds(30))
///                      .BasicAuth("user", "password");
///          })
///          .MapTopic&lt;OrderCreated&gt;("orders-topic");
/// });
/// </code>
/// </example>
public interface IConfluentSchemaRegistryBuilder
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

	/// <summary>
	/// Sets the subject naming strategy using the built-in enum.
	/// </summary>
	/// <param name="strategy">The subject name strategy.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Default is <see cref="SubjectNameStrategy.TopicName"/> which uses <c>{topic}-value</c>
	/// as the subject name.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder SubjectNameStrategy(SubjectNameStrategy strategy);

	/// <summary>
	/// Sets a custom subject naming strategy.
	/// </summary>
	/// <typeparam name="TStrategy">The custom strategy type implementing <see cref="ISubjectNameStrategy"/>.</typeparam>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this for advanced scenarios where the built-in strategies are insufficient.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder SubjectNameStrategy<TStrategy>()
		where TStrategy : class, ISubjectNameStrategy, new();

	/// <summary>
	/// Sets the schema compatibility mode.
	/// </summary>
	/// <param name="mode">The compatibility mode.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Default is <see cref="CompatibilityMode.Backward"/> which allows new schemas
	/// to read data written with the previous schema version.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder CompatibilityMode(CompatibilityMode mode);

	/// <summary>
	/// Enables or disables automatic schema registration on first use.
	/// </summary>
	/// <param name="enable">Whether to auto-register schemas. Default is true.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, schemas are automatically registered when a message type is first
	/// published. Disable in production if you want explicit schema management.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder AutoRegisterSchemas(bool enable = true);

	/// <summary>
	/// Enables or disables local schema validation before registration.
	/// </summary>
	/// <param name="enable">Whether to validate schemas locally. Default is true.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, schemas are validated locally before being sent to the registry.
	/// This catches schema errors earlier in the development cycle.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder ValidateBeforeRegister(bool enable = true);

	/// <summary>
	/// Enables or disables local schema caching.
	/// </summary>
	/// <param name="enable">Whether to cache schemas. Default is true.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Caching improves performance by avoiding repeated network calls to the registry.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder CacheSchemas(bool enable = true);

	/// <summary>
	/// Sets the maximum number of schemas to cache locally.
	/// </summary>
	/// <param name="capacity">The cache capacity. Default is 1000.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="capacity"/> is not positive.
	/// </exception>
	IConfluentSchemaRegistryBuilder CacheCapacity(int capacity);

	/// <summary>
	/// Sets the timeout for Schema Registry HTTP requests.
	/// </summary>
	/// <param name="timeout">The request timeout. Default is 30 seconds.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="timeout"/> is not positive.
	/// </exception>
	IConfluentSchemaRegistryBuilder RequestTimeout(TimeSpan timeout);
}

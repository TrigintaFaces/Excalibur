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
public interface IConfluentSchemaRegistryBuilder : ISchemaRegistryConnectionBuilder, ISchemaRegistryConfigBuilder, ISchemaRegistryCacheBuilder
{
}

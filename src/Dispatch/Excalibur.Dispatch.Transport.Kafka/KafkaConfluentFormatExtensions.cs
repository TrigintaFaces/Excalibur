// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Confluent wire format with Schema Registry integration.
/// </summary>
public static class KafkaConfluentFormatExtensions
{
	/// <summary>
	/// Configures Kafka transport to use Confluent wire format with Schema Registry.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureRegistry">Configuration action for Schema Registry options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method:
	/// </para>
	/// <list type="bullet">
	///   <item><description>Registers Schema Registry client with caching decorator</description></item>
	///   <item><description>Registers <see cref="ConfluentJsonSerializer"/> as <see cref="IConfluentFormatSerializer"/></description></item>
	///   <item><description>Registers <see cref="ConfluentKafkaMessageBus"/> as the Kafka bus implementation</description></item>
	/// </list>
	/// <para>
	/// <b>IMPORTANT:</b> This replaces the default Kafka registration so the Kafka bus
	/// resolves to <see cref="ConfluentKafkaMessageBus"/>. Consumers must explicitly opt-in to Confluent format.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddKafkaTransport("kafka", kafka =>
	/// {
	///     kafka.BootstrapServers("localhost:9092")
	///          .UseSchemaRegistry(registry => registry.Url = "http://localhost:8081");
	/// });
	///
	/// // Or with explicit Confluent format registration:
	/// services.UseConfluentFormat(registry =>
	/// {
	///     registry.Url = "http://localhost:8081";
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection UseConfluentFormat(
		this IServiceCollection services,
		Action<ConfluentSchemaRegistryOptions> configureRegistry)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureRegistry);

		// Register Schema Registry client with caching
		_ = services.AddConfluentSchemaRegistry(configureRegistry);

		// Register the JSON serializer
		services.TryAddSingleton<IConfluentFormatSerializer, ConfluentJsonSerializer>();

		// Register default subject naming strategy (TopicNameStrategy)
		services.TryAddSingleton<ISubjectNameStrategy, TopicNameStrategy>();

		// Register schema validator
		services.TryAddSingleton<ISchemaValidator, JsonSchemaValidator>();

		services.TryAddSingleton<ConfluentKafkaMessageBus>();
		RemoveKafkaRegistration(services);
		_ = services.AddRemoteMessageBus(
			"kafka",
			static sp => sp.GetRequiredService<ConfluentKafkaMessageBus>());

		return services;
	}

	/// <summary>
	/// Configures Kafka transport to use Confluent wire format with custom caching options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureRegistry">Configuration action for Schema Registry options.</param>
	/// <param name="configureCaching">Configuration action for caching options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This overload allows customizing the cache behavior for Schema Registry lookups.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddKafkaTransport("kafka", kafka =>
	/// {
	///     kafka.BootstrapServers("localhost:9092");
	/// });
	///
	/// services.UseConfluentFormat(
	///     registry => { registry.Url = "http://localhost:8081"; },
	///     caching => { caching.CacheDuration = TimeSpan.FromMinutes(10); });
	/// </code>
	/// </example>
	public static IServiceCollection UseConfluentFormat(
		this IServiceCollection services,
		Action<ConfluentSchemaRegistryOptions> configureRegistry,
		Action<CachingSchemaRegistryOptions> configureCaching)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureRegistry);
		ArgumentNullException.ThrowIfNull(configureCaching);

		// Register Schema Registry client with custom caching
		_ = services.AddConfluentSchemaRegistry(configureRegistry, configureCaching);

		// Register the JSON serializer
		services.TryAddSingleton<IConfluentFormatSerializer, ConfluentJsonSerializer>();

		// Register default subject naming strategy (TopicNameStrategy)
		services.TryAddSingleton<ISubjectNameStrategy, TopicNameStrategy>();

		// Register schema validator
		services.TryAddSingleton<ISchemaValidator, JsonSchemaValidator>();

		services.TryAddSingleton<ConfluentKafkaMessageBus>();
		RemoveKafkaRegistration(services);
		_ = services.AddRemoteMessageBus(
			"kafka",
			static sp => sp.GetRequiredService<ConfluentKafkaMessageBus>());

		return services;
	}

	/// <summary>
	/// Configures a custom subject naming strategy for Schema Registry subjects.
	/// </summary>
	/// <typeparam name="TStrategy">The subject naming strategy type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method replaces the default <see cref="TopicNameStrategy"/> with a custom strategy.
	/// </para>
	/// <para>
	/// Built-in strategies:
	/// </para>
	/// <list type="bullet">
	///   <item><description><see cref="TopicNameStrategy"/>: <c>{topic}-value</c> (default)</description></item>
	///   <item><description><see cref="RecordNameStrategy"/>: <c>{namespace}.{typename}</c></description></item>
	///   <item><description><see cref="TopicRecordNameStrategy"/>: <c>{topic}-{namespace}.{typename}</c></description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.UseConfluentFormat(registry => { ... })
	///         .UseSubjectNaming&lt;RecordNameStrategy&gt;();
	/// </code>
	/// </example>
	public static IServiceCollection UseSubjectNaming<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStrategy>(this IServiceCollection services)
		where TStrategy : class, ISubjectNameStrategy
	{
		ArgumentNullException.ThrowIfNull(services);

		// Replace existing registration
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISubjectNameStrategy));
		if (descriptor != null)
		{
			_ = services.Remove(descriptor);
		}

		_ = services.AddSingleton<ISubjectNameStrategy, TStrategy>();
		return services;
	}

	private static void RemoveKafkaRegistration(IServiceCollection services)
	{
		var registrations = services
			.Where(static d => d.ServiceType == typeof(IMessageBusRegistration))
			.ToList();

		var buses = services
			.Where(static d => d.ServiceType == typeof(IMessageBus))
			.ToList();

		foreach (var registration in registrations)
		{
			if (registration.ImplementationInstance is MessageBusRegistration busRegistration &&
				string.Equals(busRegistration.Name, "kafka", StringComparison.OrdinalIgnoreCase))
			{
				_ = services.Remove(registration);
			}
		}

		foreach (var bus in buses)
		{
			if (bus.ServiceKey is string key &&
				string.Equals(key, "kafka", StringComparison.OrdinalIgnoreCase))
			{
				_ = services.Remove(bus);
			}
		}
	}
}

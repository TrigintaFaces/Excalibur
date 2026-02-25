// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

using DeliveryInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;
using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxOptions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Dispatch inbox, outbox, and scheduling components inside the DI container.
/// </summary>
public static class DeliveryServiceCollectionExtensions
{
	private const int DefaultPerRunTotal = 10_000;
	private const int DefaultQueueCapacity = 5_000;
	private const int DefaultProducerBatchSize = 100;
	private const int DefaultConsumerBatchSize = 10;
	private const int DefaultMaxAttempts = 5;

	/// <summary>
	/// Registers the Dispatch outbox store with the specified implementation.
	/// </summary>
	/// <remarks>
	/// This method only registers the outbox store. To register the full outbox processing
	/// infrastructure (OutboxProcessor, MessageOutbox), use Excalibur.Outbox's DI extensions.
	/// </remarks>
	public static IServiceCollection AddOutbox<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(
		this IServiceCollection services,
		Action<DeliveryOutboxOptions>? configure = null)
		where TStore : class, IOutboxStore
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IOutboxStore, TStore>();

		// Note: IOutboxProcessor and IOutboxDispatcher implementations are now in Excalibur.Outbox
		// Use Excalibur.Outbox DI extensions to register those implementations

		var builder = services.AddOptions<DeliveryOutboxOptions>();
		_ = builder.Configure(static options =>
		{
			options.PerRunTotal = DefaultPerRunTotal;
			options.QueueCapacity = DefaultQueueCapacity;
			options.ProducerBatchSize = DefaultProducerBatchSize;
			options.ConsumerBatchSize = DefaultConsumerBatchSize;
			options.MaxAttempts = DefaultMaxAttempts;
		});

		if (configure is not null)
		{
			_ = builder.Configure(configure);
		}

		_ = builder.Validate(
				static options => DeliveryOutboxOptions.Validate(options) is null,
				"DeliveryOutboxOptions failed validation.")
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Registers the Dispatch inbox store with the specified implementation.
	/// </summary>
	/// <remarks>
	/// This method only registers the inbox store. To register the full inbox processing
	/// infrastructure (InboxProcessor, MessageInbox), use Excalibur.Outbox's DI extensions.
	/// </remarks>
	public static IServiceCollection AddInbox<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(
		this IServiceCollection services,
		Action<DeliveryInboxOptions>? configure = null)
		where TStore : class, IInboxStore
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IInboxStore, TStore>();

		// Note: IInboxProcessor, IInbox, and IInMemoryDeduplicator implementations are now in Excalibur.Outbox
		// Use Excalibur.Outbox DI extensions to register those implementations

		var builder = services.AddOptions<DeliveryInboxOptions>();
		_ = builder.Configure(static options =>
		{
			options.PerRunTotal = DefaultPerRunTotal;
			options.QueueCapacity = DefaultQueueCapacity;
			options.ProducerBatchSize = DefaultProducerBatchSize;
			options.ConsumerBatchSize = DefaultConsumerBatchSize;
			options.MaxAttempts = DefaultMaxAttempts;
		});

		if (configure is not null)
		{
			_ = builder.Configure(configure);
		}

		_ = builder.Validate(
				static options => DeliveryInboxOptions.Validate(options) is null,
				"DeliveryInboxOptions failed validation.")
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Binds <see cref="DeliveryOutboxOptions" /> from configuration.
	/// </summary>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode("Configuration binding requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddOutboxOptions(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<DeliveryOutboxOptions>()
			.Bind(configuration)
			.Validate(
				static options => DeliveryOutboxOptions.Validate(options) is null,
				"DeliveryOutboxOptions failed validation.")
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Binds <see cref="DeliveryInboxOptions" /> from configuration.
	/// </summary>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode("Configuration binding requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddInboxOptions(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<DeliveryInboxOptions>()
			.Bind(configuration)
			.Validate(
				static options => DeliveryInboxOptions.Validate(options) is null,
				"DeliveryInboxOptions failed validation.")
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Registers scheduling infrastructure with default in-memory components.
	/// </summary>
	public static IServiceCollection AddDispatchScheduling(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IScheduleStore, InMemoryScheduleStore>();
		services.TryAddSingleton<ICronScheduler, CronScheduler>();
		services.TryAddSingleton<IDispatchScheduler, RecurringDispatchScheduler>();

		_ = services.AddOptions<SchedulerOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddOptions<CronScheduleOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Registers a custom dispatch scheduler implementation, ensuring base scheduling services are available.
	/// </summary>
	public static IServiceCollection AddDispatchScheduler<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TScheduler>(this IServiceCollection services)
		where TScheduler : class, IDispatchScheduler
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddDispatchScheduling();
		_ = services.Replace(ServiceDescriptor.Singleton<IDispatchScheduler, TScheduler>());

		return services;
	}

	/// <summary>
	/// Binds <see cref="EventStoreDispatcherOptions" /> from configuration.
	/// </summary>
	[RequiresDynamicCode("Configuration binding requires dynamic code generation for property reflection and value conversion.")]
	[RequiresUnreferencedCode("Uses reflection which may break with AOT compilation")]
	public static IServiceCollection AddEventStoreDispatcherOptions(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<EventStoreDispatcherOptions>()
			.Bind(configuration)
			.Validate(
				static options => options.PollInterval > TimeSpan.Zero,
				"EventStoreDispatcherOptions.PollInterval must be greater than zero.")
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}
}

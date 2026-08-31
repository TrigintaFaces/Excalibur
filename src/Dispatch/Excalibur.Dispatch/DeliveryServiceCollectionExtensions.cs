// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using DeliveryInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;
using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

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

		// TStore is activated by the container, so a store whose constructor requires an ITenantContext
		// cannot be built unless one is registered. Provider-specific extensions do this themselves; this
		// open-generic path is reachable directly (AddExactlyOnceMessaging routes through it) and did not.
		services.AddDefaultTenantContext();
		services.AddKeyedSingleton<IOutboxStore, TStore>("default");

		// Note: IOutboxProcessor and IOutboxDispatcher implementations are now in Excalibur.Outbox
		// Use Excalibur.Outbox DI extensions to register those implementations

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DeliveryOutboxOptions>, OutboxDeliveryOptionsValidator>());

		// Anti-silent-absence guard: the polling outbox must durably transition a
		// retry-exhausted message to the terminal DeadLettered status. Fail fast at startup if the registered
		// store cannot (does not implement IDeadLetterableOutboxStore) rather than re-claim it forever.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DeliveryOutboxOptions>, OutboxDeadLetterCapabilityValidator>());

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

		// TStore is activated by the container, so a store whose constructor requires an ITenantContext
		// cannot be built unless one is registered. Provider-specific extensions do this themselves; this
		// open-generic path is reachable directly (AddExactlyOnceMessaging routes through it) and did not.
		services.AddDefaultTenantContext();
		services.AddKeyedSingleton<IInboxStore, TStore>("default");

		// Note: the IInboxProcessor and IInbox implementations live in Excalibur.Outbox; its
		// AddInboxHostedService() registers both alongside the hosted service that drives them.
		// IInMemoryDeduplicator is NOT among them -- its implementation is in this package and
		// AddDispatch() already registers it.

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DeliveryInboxOptions>, InboxOptionsValidator>());

		// Anti-silent-absence guard: the full-inbox at-most-once guard and stuck-processing
		// timeout require the store to durably persist the Processing status. Fail fast at startup if the
		// registered store cannot (does not implement IProcessingTrackingInboxStore) rather than silently degrade.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DeliveryInboxOptions>, InboxProcessingCapabilityValidator>());

		// Anti-silent-race guard: the idempotency middleware's exactly-once admission under
		// concurrent duplicate delivery requires the store to claim atomically (IClaimableInboxStore). Fail fast at
		// startup if the registered store cannot, rather than silently degrading to a non-atomic check-then-act.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DeliveryInboxOptions>, IdempotencyClaimCapabilityValidator>());

		var builder = services.AddOptions<DeliveryInboxOptions>();
		_ = builder.Configure(static options =>
		{
			options.Capacity.PerRunTotal = DefaultPerRunTotal;
			options.Capacity.QueueCapacity = DefaultQueueCapacity;
			options.Capacity.ProducerBatchSize = DefaultProducerBatchSize;
			options.Capacity.ConsumerBatchSize = DefaultConsumerBatchSize;
			options.MaxAttempts = DefaultMaxAttempts;
		});

		if (configure is not null)
		{
			_ = builder.Configure(configure);
		}

		_ = builder.Validate(
				static options => DeliveryInboxOptions.Validate(options) is null,
				"DeliveryInboxOptions failed validation.")
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

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DeliveryOutboxOptions>, OutboxDeliveryOptionsValidator>());

		_ = services.AddOptions<DeliveryOutboxOptions>()
			.Bind(configuration)
			.Validate(
				static options => DeliveryOutboxOptions.Validate(options) is null,
				"DeliveryOutboxOptions failed validation.")
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

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DeliveryInboxOptions>, InboxOptionsValidator>());

		_ = services.AddOptions<DeliveryInboxOptions>()
			.Bind(configuration)
			.Validate(
				static options => DeliveryInboxOptions.Validate(options) is null,
				"DeliveryInboxOptions failed validation.")
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Registers scheduling infrastructure with default in-memory components.
	/// </summary>
	public static IServiceCollection AddDispatchScheduling(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// No durability attestation is emitted here, deliberately: this store forgets pending schedules on
		// restart, and registering the type is not evidence that it keeps anything. Composing scheduling
		// alone stays gate-free so a development host, or one replacing an in-process mediator, is not made
		// to justify a store it never schedules against. The refusal is installed by the compositions that
		// start the scheduler runtime, where the host begins accepting deliveries it owes later.
		services.TryAddSingleton<IScheduleStore, InMemoryScheduleStore>();
		services.TryAddSingleton<ICronScheduler, CronScheduler>();
		// RecurringDispatchScheduler takes the concrete serializer, so scheduling composed on its own must
		// seat it rather than rely on the consumer also having called AddDispatchPipeline/AddDispatchSerializer.
		services.TryAddSingleton<Excalibur.Dispatch.Serialization.DispatchJsonSerializer>();
		services.TryAddSingleton<IDispatchScheduler, RecurringDispatchScheduler>();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SchedulerOptions>, SchedulerOptionsValidator>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CronScheduleOptions>, CronScheduleOptionsValidator>());

		_ = services.AddOptions<SchedulerOptions>()
			.ValidateOnStart();
		_ = services.AddOptions<CronScheduleOptions>()
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
}

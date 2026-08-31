// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

using Excalibur.Outbox;
using Excalibur.Outbox.DependencyInjection;
using Excalibur.Outbox.Outbox;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Excalibur outbox services.
/// </summary>
public static class OutboxServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur outbox services using a fluent builder configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The builder configuration action.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the primary entry point for configuring Excalibur Outbox following
	/// Microsoft-style fluent builder patterns. It provides a single discoverable API
	/// with fluent chaining for all configuration options.
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(sql =>
	///     {
	///         sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;")
	///            .SchemaName("Outbox")
	///            .TableName("Messages");
	///     })
	///     .WithProcessing(p => p.BatchSize(100).PollingInterval(TimeSpan.FromSeconds(5)))
	///     .EnableBackgroundProcessing();
	/// }));
	/// </code>
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Minimal configuration with SQL Server
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(sql => sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;"))
	///           .EnableBackgroundProcessing();
	/// }));
	///
	/// // Full configuration with all options
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(sql =>
	///     {
	///         sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;")
	///            .SchemaName("Messaging")
	///            .TableName("OutboxMessages")
	///            .CommandTimeout(TimeSpan.FromSeconds(60))
	///            .UseRowLocking(true);
	///     })
	///     .WithProcessing(processing =>
	///     {
	///         processing.BatchSize(200)
	///                   .PollingInterval(TimeSpan.FromSeconds(10))
	///                   .MaxRetryCount(5)
	///                   .RetryDelay(TimeSpan.FromMinutes(1))
	///                   .EnableParallelProcessing(4);
	///     })
	///     .EnableBackgroundProcessing();
	/// }));
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	internal static IServiceCollection AddExcaliburOutbox(
		this IServiceCollection services,
		Action<IOutboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Register base infrastructure
		RegisterCoreServices(services);

		// Create mutable configuration and builder
		var config = new OutboxConfiguration();
		var builder = new OutboxBuilder(services, config);

		// Apply configuration
		configure(builder);

		// Now that the provider callback has run, register the non-keyed IOutboxStore/IOutboxStoreAdmin
		// aliases IF a keyed "default" IOutboxStore backs them (see RegisterOutboxStoreAliasesIfBacked).
		RegisterOutboxStoreAliasesIfBacked(services);

		// Build immutable options and register as singleton
		var options = config.ToOptions();
		services.TryAddSingleton(options);

		return services;
	}

	/// <summary>
	/// Adds Excalibur outbox services from an already-built <see cref="OutboxOptions"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">The built outbox options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Internal only, and deliberately not exposed. This overload takes no provider callback, so a
	/// consumer calling it would register the outbox infrastructure with no store behind it and fail
	/// the startup prerequisite check. The supported registration is the builder callback on
	/// <c>AddOutbox</c>, which picks a store and produces the options itself. Options are built here
	/// through the internal preset factories on <see cref="OutboxOptions"/>.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	internal static IServiceCollection AddExcaliburOutbox(
		this IServiceCollection services,
		OutboxOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		// Register base infrastructure
		RegisterCoreServices(services);

		// Register immutable options as singleton
		services.TryAddSingleton(options);

		// No provider callback exists on this overload -- a keyed "default" IOutboxStore can only be
		// present here if the consumer registered one directly against `services` before calling this
		// method. Checked for consistency with the other overloads; ordinarily a no-op.
		RegisterOutboxStoreAliasesIfBacked(services);

		// If background processing is enabled, register the hosted service
		if (options.EnableBackgroundProcessing)
		{
			_ = services.AddHostedService<OutboxBackgroundService>();
		}

		return services;
	}

	/// <summary>
	/// Adds Excalibur outbox services to the service collection with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers the core outbox infrastructure with the Balanced preset defaults.
	/// </para>
	/// <para>
	/// Internal only. Like the options overload above it takes no provider callback, so it registers
	/// no store. The supported registration is the builder callback:
	/// <code>
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(sql => sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;"))
	///           .EnableBackgroundProcessing();
	/// }));
	/// </code>
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	internal static IServiceCollection AddExcaliburOutbox(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		RegisterCoreServices(services);

		// No provider callback exists on this overload -- see the preset-based overload above.
		RegisterOutboxStoreAliasesIfBacked(services);

		// Register default (Balanced) options
		var options = OutboxOptions.Balanced().Build();
		services.TryAddSingleton(options);

		return services;
	}

	/// <summary>
	/// Registers core outbox infrastructure services.
	/// </summary>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private static void RegisterCoreServices(IServiceCollection services)
	{
		// Register Dispatch primitives first (IDispatcher, IMessageBus, etc.)
		//
		// The pipeline entry point, NOT the zero-argument AddDispatch(): that one resolves to the
		// assembly overload, which discovers handlers by scanning the consumer's entry assembly when the
		// caller names none. A consumer registering this subsystem asked for the subsystem, not for a
		// reflective scan of their own application, and had no way to opt out of one taken on their
		// behalf. AddDispatchPipeline registers the primitives and the handler registry -- everything the
		// comment above is after -- and scans nothing. Entry-assembly discovery remains available to the
		// consumer as the named, annotated opt-in AddHandlersFromEntryAssembly().
		_ = services.AddDispatchPipeline();

		// fail loud at host start if the consumer forgot to pick an outbox store.
		services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, OutboxPrerequisiteValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, OutboxPrerequisiteValidator>());

		// The non-keyed IOutboxStore/IOutboxStoreAdmin convenience aliases are registered by
		// RegisterOutboxStoreAliasesIfBacked, called from each AddExcaliburOutbox overload AFTER the
		// provider callback (o => o.UseSqlServer(...) etc.) has had a chance to run -- not here. This
		// registration site runs BEFORE that callback, so registering an alias here unconditionally would
		// leave a dangling forwarder for a change-feed provider (UseCosmosDb/UseDynamoDb/UseFirestore),
		// which never registers a keyed "default" IOutboxStore because it implements the separate
		// ICloudNativeOutboxStore contract instead. See RegisterOutboxStoreAliasesIfBacked for why the
		// alias is a real registered descriptor only when something backs it, rather than a forwarder that
		// resolves to null: a bare descriptor presence check (as the multi-tenancy gate performs) cannot
		// tell "backed" from "dangling" apart, so an unbacked alias must not exist at all.

		// Register the real outbox processor + dispatcher with the outbox subsystem itself, so
		// IOutboxProcessor / IOutboxDispatcher are available whenever AddExcaliburOutbox is used —
		// NOT only when A3 audit (which registers a fail-fast DefaultOutboxDispatcher stub) is added.
		// These registrations were dropped when the implementations moved from Excalibur.Dispatch to
		// Excalibur.Outbox and were never restored, leaving OutboxJob, OutboxBackgroundService and
		// audited dispatch unable to resolve a real dispatcher.
		//
		// Lifetimes: OutboxProcessor is Transient because it carries per-instance state and is keyed
		// via Init(dispatcherId) — each background partition and each dispatcher needs its own. Its
		// dependencies are all root-resolvable, so capture by the Singleton dispatcher is safe.
		//
		// Registered by FACTORY, not by implementation type, and that is load-bearing rather than
		// stylistic. Both types take a non-keyed IOutboxStore, which a host that has not chosen a
		// provider does not have; an implementation-type descriptor is walked by
		// BuildServiceProvider(validateOnBuild: true), so such a host used to fail there with a raw
		// "Unable to resolve service for type 'IOutboxStore'" naming the type but not the call that
		// supplies it -- and it failed BEFORE any startup validator could speak. A factory descriptor
		// is opaque to that walk, which lets the fail-fast below own the message, and keeps the
		// container honest without re-introducing the dangling IOutboxStore alias (see
		// RegisterOutboxStoreAliasesIfBacked). Resolution order is unchanged: the store is still
		// resolved from the provider, so a consumer who registers one AFTER AddOutbox(...) still works.
		services.TryAddTransient<Excalibur.Dispatch.IOutboxProcessor>(static sp =>
		{
			var store = sp.GetService<Excalibur.Dispatch.IOutboxStore>()
				?? throw new InvalidOperationException(OutboxStorePrerequisite.MissingStoreMessage);
			return ActivatorUtilities.CreateInstance<Excalibur.Dispatch.Delivery.OutboxProcessor>(sp, store);
		});

		// MessageOutbox (Singleton) must win even when A3 audit registered its fail-fast
		// DefaultOutboxDispatcher stub FIRST — TryAdd is order-sensitive, so the stub could otherwise
		// shadow the real dispatcher when audit is composed before the outbox. Remove only A3's stub
		// (identified by type, leaving any consumer-supplied dispatcher intact) and then TryAdd the
		// real implementation. A consumer override registered before AddOutbox(...) still wins because
		// the TryAdd below is a no-op when a non-stub IOutboxDispatcher is already present.
		RemoveDefaultOutboxDispatcherStub(services);
		services.TryAddSingleton<Excalibur.Dispatch.IOutboxDispatcher>(static sp =>
		{
			var store = sp.GetService<Excalibur.Dispatch.IOutboxStore>()
				?? throw new InvalidOperationException(OutboxStorePrerequisite.MissingStoreMessage);
			return ActivatorUtilities.CreateInstance<Excalibur.Dispatch.Delivery.MessageOutbox>(sp, store);
		});

		// Fail fast at ValidateOnStart with the message above rather than at first enqueue. This is the
		// earliest hook that sees the FINISHED container, so it does not punish a consumer who registers
		// their own store after this call the way a registration-time throw would. Guarded because
		// ValidateOnStart() re-registers its IStartupValidator on every call, which would otherwise show
		// up as descriptor drift on a second AddOutbox(...).
		if (!services.Any(static sd => sd.ServiceType == typeof(IValidateOptions<OutboxStorePrerequisiteValidationOptions>)))
		{
			services.TryAddEnumerable(ServiceDescriptor.Singleton<
				IValidateOptions<OutboxStorePrerequisiteValidationOptions>, OutboxStorePrerequisiteValidator>());
			_ = services.AddOptions<OutboxStorePrerequisiteValidationOptions>().ValidateOnStart();
		}

		// bridge the consumer-facing parallel knob (OutboxOptions in this package) onto the degree
		// the OutboxProcessor actually reads (core OutboxDeliveryOptions.BatchProcessing.ParallelProcessingDegree).
		// They are separate option types in separate packages with no prior translation, so the advertised
		// EnableParallelProcessing(N) builder was inert. Options-composition resolves OutboxOptions at
		// configure-time (no stale snapshot); Excalibur.Outbox -> Excalibur.Dispatch core is a downward
		// reference. No-op unless the consumer enabled parallel processing on the outbox builder.
		_ = services.AddOptions<Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions>()
			.Configure<OutboxOptions>((delivery, outbox) =>
			{
				if (outbox.EnableParallelProcessing)
				{
					delivery.BatchProcessing.ParallelProcessingDegree = outbox.MaxDegreeOfParallelism;
				}
			});

		// The same bridge for the poll interval. WithPollingInterval(...) lands on OutboxOptions, while the
		// drain loop and every provider's failure-floor validator read OutboxProcessingOptions -- a type
		// nothing bound, so it always yielded its own 5-second default. Two consequences, and the second is
		// the worse one: the loop polled at 5 s whatever the operator asked for, and the F > poll invariant
		// was CHECKED against 5 s rather than against the interval the operator actually configured, so a
		// configuration whose real backoff floor sat below its real poll interval passed startup. A
		// validator holding an invariant against a value the system does not use cannot fail on the case it
		// exists for.
		//
		// Composition rather than a snapshot: OutboxOptions is resolved when OutboxProcessingOptions is
		// first materialised, so a later Configure on either type is still seen.
		_ = services.AddOptions<Excalibur.Outbox.Outbox.OutboxProcessingOptions>()
			.Configure<OutboxOptions>((processing, outbox) => processing.PollingInterval = outbox.PollingInterval);
	}

	/// <summary>
	/// Registers the non-keyed <see cref="Excalibur.Dispatch.IOutboxStore"/> / <see
	/// cref="Excalibur.Dispatch.IOutboxStoreAdmin"/> convenience aliases -- forwarding to the keyed
	/// "default" registrations a provider extension creates -- but ONLY when a keyed "default" <see
	/// cref="Excalibur.Dispatch.IOutboxStore"/> descriptor is actually present.
	/// </summary>
	/// <remarks>
	/// A polling provider (UseSqlServer/UsePostgres/UseMongoDB/...) registers
	/// that keyed descriptor; a change-feed provider (UseCosmosDb/UseDynamoDb/UseFirestore) never does --
	/// it implements the separate <see cref="Excalibur.Data.CloudNative.ICloudNativeOutboxStore"/> contract
	/// instead. Call this AFTER the provider callback has run, so the check sees whatever the consumer
	/// actually configured. When nothing backs it, no alias descriptor is registered at all: IOutboxStore
	/// stays honestly absent rather than present-but-dangling. This matters beyond the immediate
	/// GetService(IOutboxStore) call site -- row-discriminator multi-tenancy gates on DESCRIPTOR PRESENCE
	/// (services.Any(d => d.ServiceType == typeof(IOutboxStore))), not on whether resolving it would
	/// succeed, so a present-but-unbacked descriptor would make the gate demand an IOutboxStore tenancy
	/// capability a change-feed host can never satisfy -- exactly the silent-then-loud failure this fix
	/// removes.
	/// </remarks>
	private static void RegisterOutboxStoreAliasesIfBacked(IServiceCollection services)
	{
		var hasKeyedDefaultStore = services.Any(static sd =>
			sd.ServiceType == typeof(Excalibur.Dispatch.IOutboxStore)
			&& sd.IsKeyedService
			&& string.Equals(sd.ServiceKey as string, "default", StringComparison.Ordinal));

		if (!hasKeyedDefaultStore)
		{
			return;
		}

		// The keyed "default" IOutboxStore descriptor is confirmed present, so GetRequiredKeyedService
		// below can never fail to find it -- it can still fail if the FACTORY throws when invoked (a
		// consumer's own store), which is not this alias's concern to swallow.
		services.TryAddSingleton<Excalibur.Dispatch.IOutboxStore>(sp =>
			sp.GetRequiredKeyedService<Excalibur.Dispatch.IOutboxStore>("default"));

		// Non-keyed IOutboxStoreAdmin convenience alias. Some providers (Elasticsearch) register this as
		// a separate keyed service; others implement it on the same class as IOutboxStore. Try keyed
		// "default" first, then fall back to casting the keyed IOutboxStore.
		services.TryAddSingleton<Excalibur.Dispatch.IOutboxStoreAdmin>(sp =>
			sp.GetKeyedService<Excalibur.Dispatch.IOutboxStoreAdmin>("default")
			?? (Excalibur.Dispatch.IOutboxStoreAdmin)sp.GetRequiredKeyedService<Excalibur.Dispatch.IOutboxStore>("default"));
	}

	// A3.Audit registers a fail-fast DefaultOutboxDispatcher via TryAdd so the audit pipeline is
	// composable without a concrete outbox. It is a placeholder that throws on dispatch. Referenced by
	// full name to avoid an Excalibur.Outbox -> Excalibur.A3 dependency; the contract is guarded by the
	// OutboxDispatcherRegistrationShould regression tests.
	private const string A3DefaultOutboxDispatcherFullName = "Excalibur.A3.Audit.Internal.DefaultOutboxDispatcher";

	private static void RemoveDefaultOutboxDispatcherStub(IServiceCollection services)
	{
		for (var i = services.Count - 1; i >= 0; i--)
		{
			var descriptor = services[i];
			if (descriptor.ServiceType == typeof(Excalibur.Dispatch.IOutboxDispatcher)
				&& descriptor.GetImplementationType()?.FullName == A3DefaultOutboxDispatcherFullName)
			{
				services.RemoveAt(i);
			}
		}
	}

	/// <summary>
	/// Checks if Excalibur outbox services have been registered.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>True if outbox services are registered; otherwise false.</returns>
	public static bool HasExcaliburOutbox(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		return services.Any(s => s.ServiceType == typeof(OutboxOptions));
	}

	/// <summary>
	/// Registers the <see cref="OutboxBackgroundService"/> as a hosted service.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	/// <remarks>
	/// <para>
	/// This registers the outbox background service that periodically polls for pending
	/// messages and publishes them. Use in conjunction with AddExcaliburOutbox() and
	/// an outbox store provider.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddOutboxHostedService(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddHostedService<OutboxBackgroundService>();

		return services;
	}

	/// <summary>
	/// Registers the <see cref="InboxService"/> as a hosted service.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	/// <remarks>
	/// <para>
	/// This registers the inbox background service that continuously processes messages
	/// from the inbox for deduplication. Use in conjunction with an inbox store provider.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddInboxHostedService(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// InboxService takes IInbox. MessageInbox is this package's implementation of it and nothing
		// registered it, so the hosted service could never start. TryAdd, so a consumer's own IInbox wins.
		// IInboxStore is the one genuine provider seam here -- the consumer picks the storage backend.
		//
		// IInboxProcessor is NOT such a seam, and treating it as one is what left this composition
		// unstartable: InboxProcessor is this package's own implementation and the only one that exists,
		// the exact inbox twin of the IOutboxProcessor registration above. Its dependencies are the same
		// ones MessageInbox already requires, so seating it asks nothing further of the consumer.
		//
		// Transient for the same reason the outbox processor is: it carries per-instance state and is
		// keyed via Init(dispatcherId), so each dispatcher needs its own. Its dependencies are all
		// root-resolvable, which makes capture by the singleton MessageInbox safe.
		//
		// DECISION -- transient is KEPT, and it is NOT a fix for the drain's single-in-flight gap.
		// Narrowing this to a singleton was considered and rejected. It would fence one container and
		// nothing else: a second host, a second process, or a serverless invocation each build their own
		// container, so two drains still select and dispatch the same entry. Registering it as a singleton
		// would therefore buy no exclusion while breaking the per-dispatcher Init(dispatcherId) keying that
		// the transient lifetime exists to serve -- a real regression traded for an imagined guarantee.
		// The gap is closed store-side or not at all; a lifetime is not a distributed lock, and must never
		// be recorded as one.
		services.TryAddSingleton<Excalibur.Dispatch.Serialization.DispatchJsonSerializer>();
		services.TryAddTransient<Excalibur.Dispatch.IInboxProcessor, Excalibur.Dispatch.Delivery.InboxProcessor>();
		services.TryAddSingleton<Excalibur.Dispatch.IInbox, Excalibur.Dispatch.Delivery.MessageInbox>();

		_ = services.AddHostedService<InboxService>();

		return services;
	}
}

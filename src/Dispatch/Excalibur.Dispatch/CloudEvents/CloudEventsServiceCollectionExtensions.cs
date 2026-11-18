// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring CloudEvents support in Excalibur.Dispatch.
/// </summary>
public static class CloudEventsServiceCollectionExtensions
{
	/// <summary>
	/// Registers the <see cref="CloudEventOptions"/> validator so a <c>ValidateOnStart()</c> call on the
	/// CloudEvents options is not a no-op. Transport <c>UseCloudEvents(services)</c> entry points call this so
	/// a misconfigured <see cref="CloudEventOptions"/> fails fast at startup on every transport — matching the
	/// core <c>AddCloudEvents</c> path — rather than validating only when the core spine is also wired.
	/// Idempotent (<c>TryAddEnumerable</c>): safe to call alongside <c>AddCloudEvents</c>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>

	public static IServiceCollection AddCloudEventOptionsValidation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CloudEventOptions>, CloudEventOptionsValidator>());

		return services;
	}

	/// <summary>
	/// Registers a fail-fast placeholder for <see cref="CloudEventSerializer"/> so a transport-only
	/// registration reports the missing configuration actionably instead of failing with a bare
	/// unresolved-service error.
	/// </summary>
	/// <remarks>
	/// Payload serialization is chosen once, by whichever <c>AddCloudEvents</c> overload the consumer
	/// calls — a source-generated resolver or the annotated reflection path. A transport's
	/// <c>UseCloudEvents</c> cannot make that choice on the consumer's behalf without either guessing or
	/// dragging a trimming annotation onto every transport entry point, so it registers this guard
	/// instead.
	/// <para>
	/// Registered with <c>TryAdd</c>, so a real serializer registered by <c>AddCloudEvents</c> — in
	/// either order — always wins, and the factory below is never invoked. It throws only when nothing
	/// configured CloudEvents serialization at all and something then tries to use it.
	/// </para>
	/// </remarks>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddCloudEventSerializerGuard(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<CloudEventSerializer>(static _ => throw new InvalidOperationException(
			"CloudEvents payload serialization has not been configured. Call AddCloudEvents on the "
			+ "dispatch builder before using a transport's CloudEvents support: pass an "
			+ "IJsonTypeInfoResolver (a source-generated JsonSerializerContext covering the types you "
			+ "publish as CloudEvent data) for a trim-safe and ahead-of-time-safe path, or call the "
			+ "parameterless overload to serialize payloads reflectively."));

		return services;
	}

	/// <summary>
	/// Adds the core CloudEvents spine to the dispatcher: the envelope⇄CloudEvent bridge, the envelope
	/// converter, and validated <see cref="CloudEventOptions"/>. This is the single canonical entry point —
	/// a core-only application (no transport package) gets a working <see cref="IEnvelopeCloudEventBridge"/>,
	/// and each transport's <c>UseCloudEvents(services)</c> composes on top of it idempotently (all
	/// registrations use <c>TryAdd</c>). This method registers services only; it does not add the ingress
	/// middleware. To enable the ingress middleware, call <c>builder.UseCloudEvents()</c> explicitly on the
	/// dispatch pipeline.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Optional action to configure <see cref="CloudEventOptions"/>.</param>
	/// <returns>The dispatch builder for chaining.</returns>
	/// <remarks>
	/// This overload serializes payloads <b>reflectively</b> and is annotated accordingly. Prefer
	/// <see cref="AddCloudEvents(IDispatchBuilder, IJsonTypeInfoResolver, Action{CloudEventOptions})"/>,
	/// which takes a source-generated resolver and carries no annotation.
	/// <para>
	/// The annotation is deliberate and is the point of the split. A single entry point that chose
	/// reflection when no resolver happened to be configured would make the consumer who opted into
	/// reflection indistinguishable from the consumer who did not — so neither would be warned, and a
	/// trimmed or ahead-of-time publish would fail on the consumer's machine instead of at their build.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode(
		"CloudEvents payloads are serialized reflectively on this path. Call the overload taking an "
		+ "IJsonTypeInfoResolver with a source-generated context covering the types you publish as "
		+ "CloudEvent data.")]
	[RequiresDynamicCode(
		"CloudEvents payloads are serialized reflectively on this path. Call the overload taking an "
		+ "IJsonTypeInfoResolver with a source-generated context covering the types you publish as "
		+ "CloudEvent data.")]
	public static IDispatchBuilder AddCloudEvents(
		this IDispatchBuilder builder,
		Action<CloudEventOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Authoritative, and deliberately not TryAdd. A transport's UseCloudEvents registers a
		// fail-fast guard for this same service, and TryAdd is FIRST-one-wins -- so a guard added
		// before this call would silently displace the consumer's actual choice. Removing any
		// prior registration makes the result independent of the order the two are called in.
		_ = builder.Services.RemoveAll<CloudEventSerializer>();
		builder.Services.TryAddSingleton(new CloudEventSerializer(CloudEventPayloadSerialization.CreateReflectionOptions()));

		return AddCloudEventsCore(builder, configure);
	}

	/// <summary>
	/// Adds the core CloudEvents spine with <b>reflection-free</b> payload serialization, using the
	/// consumer's source-generated resolver. This is the overload to use when the host is trimmed or
	/// published ahead-of-time.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="payloadTypeInfoResolver">
	/// A source-generated resolver covering the types published as CloudEvent data — typically a
	/// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> declared by the consumer.
	/// </param>
	/// <param name="configure">Optional action to configure <see cref="CloudEventOptions"/>.</param>
	/// <returns>The dispatch builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
	public static IDispatchBuilder AddCloudEvents(
		this IDispatchBuilder builder,
		IJsonTypeInfoResolver payloadTypeInfoResolver,
		Action<CloudEventOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(payloadTypeInfoResolver);

		// Authoritative, and deliberately not TryAdd. A transport's UseCloudEvents registers a
		// fail-fast guard for this same service, and TryAdd is FIRST-one-wins -- so a guard added
		// before this call would silently displace the consumer's actual choice. Removing any
		// prior registration makes the result independent of the order the two are called in.
		_ = builder.Services.RemoveAll<CloudEventSerializer>();
		builder.Services.TryAddSingleton(CloudEventSerializer.CreateWithMetadata(CloudEventPayloadSerialization.CreateOptions(payloadTypeInfoResolver)));

		return AddCloudEventsCore(builder, configure);
	}

	private static IDispatchBuilder AddCloudEventsCore(
		IDispatchBuilder builder,
		Action<CloudEventOptions>? configure)
	{
		var optionsBuilder = builder.Services.AddOptions<CloudEventOptions>();
		_ = optionsBuilder.ValidateOnStart();
		if (configure is not null)
		{
			_ = builder.Services.Configure(configure);
		}

		// A real validator so ValidateOnStart() is not a no-op (fail fast on a misconfigured pipeline).
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CloudEventOptions>, CloudEventOptionsValidator>());

		builder.Services.TryAddSingleton<ICloudEventEnvelopeConverter, CloudEventEnvelopeConverter>();
		builder.Services.TryAddSingleton<IEnvelopeCloudEventBridge, EnvelopeCloudEventBridge>();
		builder.Services.TryAddSingleton(
			static sp => sp.GetRequiredService<IOptions<CloudEventOptions>>().Value);

		return builder;
	}

	/// <summary>
	/// Adds a schema registry for CloudEvent schema management.
	/// </summary>
	public static IDispatchBuilder AddCloudEventSchemaRegistry<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TRegistry>(
		this IDispatchBuilder builder)
		where TRegistry : class, ISchemaRegistry
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<ISchemaRegistry, TRegistry>();

		// Registering a schema registry enables schema-version stamping, but does NOT enable schema-payload
		// validation: that has no working path yet (the startup validator rejects ValidateSchema=true), so
		// auto-enabling it here would make merely registering a registry fail at startup. Validation stays
		// opt-in until the payload-validation feature ships.
		_ = builder.Services.Configure<CloudEventOptions>(static options =>
		{
			options.Schema.IncludeSchemaVersion = true;
		});

		return builder;
	}

	/// <summary>
	/// Adds an in-memory schema registry for development/testing.
	/// </summary>
	public static IDispatchBuilder AddInMemorySchemaRegistry(this IDispatchBuilder builder) =>
		builder.AddCloudEventSchemaRegistry<InMemorySchemaRegistry>();

	/// <summary>
	/// Configures automatic schema registration for CloudEvents.
	/// </summary>
	public static IDispatchBuilder AddCloudEventSchemaAutoRegistration(
		this IDispatchBuilder builder,
		Func<Type, string> schemaProvider,
		Func<Type, string>? versionProvider = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(schemaProvider);

		_ = builder.Services.Configure<CloudEventOptions>(options =>
		{
			options.Schema.AutoRegisterSchemas = true;
			options.Schema.SchemaProvider = schemaProvider;
			options.Schema.SchemaVersionProvider = versionProvider ?? (type => "1.0");
		});

		return builder;
	}

	/// <summary>
	/// Configures CloudEvent extension attributes to include/exclude.
	/// </summary>
	public static IDispatchBuilder WithCloudEventExtensions(
		this IDispatchBuilder builder,
		Action<HashSet<string>> configureExclusions)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureExclusions);

		_ = builder.Services.Configure<CloudEventOptions>(options => configureExclusions(options.ExcludedExtensions));

		return builder;
	}
}

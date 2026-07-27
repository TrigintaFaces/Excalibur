// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

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
	public static IDispatchBuilder AddCloudEvents(
		this IDispatchBuilder builder,
		Action<CloudEventOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

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

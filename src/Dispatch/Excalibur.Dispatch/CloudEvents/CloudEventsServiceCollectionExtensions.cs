// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring CloudEvents support in Excalibur.Dispatch.
/// </summary>
public static class CloudEventsServiceCollectionExtensions
{
	/// <summary>
	/// Assembly-qualified type name for <c>ICloudEventMapper&lt;T&gt;</c> in Excalibur.Dispatch.Transport.Abstractions.
	/// Centralized here to avoid duplicate hardcoded strings (also used by <see cref="EnvelopeCloudEventBridge"/>).
	/// </summary>
	internal const string CloudEventMapperTypeName =
		"Excalibur.Dispatch.Transport.ICloudEventMapper`1, Excalibur.Dispatch.Transport.Abstractions";

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

		// Enable schema features
		_ = builder.Services.Configure<CloudEventOptions>(static options =>
		{
			options.Schema.ValidateSchema = true;
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

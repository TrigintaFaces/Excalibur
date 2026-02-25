// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.DynamoDb.Outbox;
using Excalibur.Dispatch.Abstractions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring DynamoDB outbox services.
/// </summary>
public static class DynamoDbOutboxExtensions
{
	/// <summary>
	/// Adds DynamoDB outbox store services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the outbox options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddDynamoDbOutbox(options =>
	/// {
	///     options.Region = "us-east-1";
	///     options.TableName = "outbox_messages";
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddDynamoDbOutbox(
		this IServiceCollection services,
		Action<DynamoDbOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DynamoDbOutboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddSingleton<IOutboxStore, DynamoDbOutboxStore>();

		return services;
	}

	/// <summary>
	/// Adds DynamoDB outbox store services with a named options configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The name of the options configuration.</param>
	/// <param name="configure">Action to configure the outbox options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbOutbox(
		this IServiceCollection services,
		string name,
		Action<DynamoDbOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DynamoDbOutboxOptions>(name)
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddSingleton<IOutboxStore, DynamoDbOutboxStore>();

		return services;
	}
}

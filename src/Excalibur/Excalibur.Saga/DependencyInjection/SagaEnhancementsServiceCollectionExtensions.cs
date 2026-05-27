// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Saga.Correlation;
using Excalibur.Saga.Handlers;
using Excalibur.Saga.Reminders;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering saga enhancement services.
/// </summary>
public static class SagaEnhancementsServiceCollectionExtensions
{
	/// <summary>
	/// Adds the convention-based saga message correlator for automatic message-to-saga routing.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaCorrelation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ConventionBasedCorrelator>();

		return services;
	}

	/// <summary>
	/// Adds the default logging-based saga not-found handler for a specific saga type.
	/// </summary>
	/// <typeparam name="TSaga">The saga type to register the handler for.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaNotFoundHandler<TSaga>(this IServiceCollection services)
		where TSaga : class
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ISagaNotFoundHandler<TSaga>, LoggingNotFoundHandler<TSaga>>();

		return services;
	}

	/// <summary>
	/// Adds a custom saga not-found handler for a specific saga type.
	/// </summary>
	/// <typeparam name="TSaga">The saga type to register the handler for.</typeparam>
	/// <typeparam name="THandler">The handler implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaNotFoundHandler<TSaga,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
		this IServiceCollection services)
		where TSaga : class
		where THandler : class, ISagaNotFoundHandler<TSaga>
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ISagaNotFoundHandler<TSaga>, THandler>();

		return services;
	}

	/// <summary>
	/// Adds saga reminder services with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaReminders(this IServiceCollection services)
	{
		return services.AddSagaReminders(_ => { });
	}

	/// <summary>
	/// Adds saga reminder services with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure reminder options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaReminders(
		this IServiceCollection services,
		Action<SagaReminderOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<SagaReminderOptions>()
			.Configure(configure)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SagaReminderOptions>, SagaReminderOptionsValidator>());

		return services;
	}

	/// <summary>
	/// Adds saga reminder services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddSagaReminders(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<SagaReminderOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SagaReminderOptions>, SagaReminderOptionsValidator>());

		return services;
	}
}

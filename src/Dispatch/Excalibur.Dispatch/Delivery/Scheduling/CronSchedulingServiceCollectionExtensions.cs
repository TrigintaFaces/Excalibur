// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring cron-based scheduling services.
/// </summary>
public static class CronSchedulingServiceCollectionExtensions
{
	/// <summary>
	/// Adds enhanced cron-based scheduling services with timezone support.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure cron schedule options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddCronScheduling(
		this IServiceCollection services,
		Action<CronScheduleOptions>? configureOptions = null)
	{
		// Register options
		if (configureOptions != null)
		{
			_ = services.Configure(configureOptions);
		}
		else
		{
			_ = services.Configure<CronScheduleOptions>(static _ => { });
		}

		// Register core cron services
		services.TryAddSingleton<ICronScheduler, CronScheduler>();

		// Register scheduler as IDispatchScheduler
		services.TryAddSingleton<IDispatchScheduler, RecurringDispatchScheduler>();

		// Register job store
		services.TryAddSingleton<ICronJobStore, InMemoryCronJobStore>();

		// Register scheduled message service
		_ = services.RemoveAll<IHostedService>();

		_ = services.AddHostedService<ScheduledMessageService>();

		return services;
	}

	/// <summary>
	/// Configures cron schedule options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure the options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection ConfigureCronScheduling(
		this IServiceCollection services,
		Action<CronScheduleOptions> configureOptions)
	{
		_ = services.Configure(configureOptions);
		return services;
	}

	/// <summary>
	/// Adds a persistent cron job store implementation.
	/// </summary>
	/// <typeparam name="TStore"> The type of cron job store. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection
		AddCronJobStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(
			this IServiceCollection services)
		where TStore : class, ICronJobStore
	{
		_ = services.RemoveAll<ICronJobStore>();
		_ = services.AddSingleton<ICronJobStore, TStore>();
		return services;
	}

	/// <summary>
	/// Adds timezone-aware cron scheduling with specific timezone support.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="supportedTimeZoneIds"> List of supported timezone IDs. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddCronSchedulingWithTimezones(
		this IServiceCollection services,
		params string[] supportedTimeZoneIds) =>
		services.AddCronScheduling(options =>
		{
			options.SupportedTimeZoneIds.Clear();
			foreach (var timezoneId in supportedTimeZoneIds)
			{
				_ = options.SupportedTimeZoneIds.Add(timezoneId);
			}
		});

	/// <summary>
	/// Adds cron scheduling with extended syntax support.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="includeSeconds"> Whether to include seconds in cron expressions. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddExtendedCronScheduling(
		this IServiceCollection services,
		bool includeSeconds = true) =>
		services.AddCronScheduling(options =>
		{
			options.IncludeSeconds = includeSeconds;
			options.EnableExtendedSyntax = true;
		});
}

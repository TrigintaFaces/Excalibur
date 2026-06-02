// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Transport;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering cron timer transport with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// The cron timer transport is a trigger-only transport that generates synthetic
/// <see cref="CronTimerTriggerMessage"/> instances on a schedule defined by a cron expression.
/// </para>
/// <para>
/// Use the generic <c>AddCronTimerTransport&lt;TTimer&gt;</c> overload to enable type-safe
/// handler routing with <see cref="ICronTimerMarker"/> marker types.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Typed timer for automatic handler routing
/// public struct CleanupTimer : ICronTimerMarker { }
/// services.AddCronTimerTransport&lt;CleanupTimer&gt;("*/5 * * * *");
///
/// // Named timer without marker
/// services.AddCronTimerTransport("health-check", "*/30 * * * * *");
/// </code>
/// </example>
public static class CronTimerTransportServiceCollectionExtensions
{
	/// <summary>
	/// Adds a typed cron timer transport that generates <see cref="CronTimerTriggerMessage{TTimer}"/>
	/// messages on the specified schedule.
	/// </summary>
	/// <typeparam name="TTimer">
	/// The timer marker type implementing <see cref="ICronTimerMarker"/>.
	/// Used for type-safe handler routing.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="cronExpression">
	/// The cron expression defining the schedule. Supports 5-field (minute-level) and
	/// 6-field (second-level) expressions.
	/// </param>
	/// <param name="configure">Optional configuration action for additional timer options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="cronExpression"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload uses the marker type name as the transport name and sets the
	/// <see cref="CronTimerTransportAdapterOptions.MessageFactory"/> to create
	/// <see cref="CronTimerTriggerMessage{TTimer}"/> instances, enabling handlers of
	/// <c>CronTimerTriggerMessage&lt;TTimer&gt;</c> to receive only their specific timer events.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// public struct DailyReportTimer : ICronTimerMarker { }
	///
	/// services.AddCronTimerTransport&lt;DailyReportTimer&gt;("0 0 * * *", options =>
	/// {
	///     options.TimeZone = TimeZoneInfo.Utc;
	///     options.PreventOverlap = true;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddCronTimerTransport<TTimer>(
		this IServiceCollection services,
		string cronExpression,
		Action<CronTimerOptions>? configure = null)
		where TTimer : ICronTimerMarker
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);

		var timerName = typeof(TTimer).Name;

		var consumerOptions = new CronTimerOptions();
		configure?.Invoke(consumerOptions);

		var adapterOptions = new CronTimerTransportAdapterOptions
		{
			Name = timerName,
			CronExpression = cronExpression,
			TimeZone = consumerOptions.TimeZone,
			RunOnStartup = consumerOptions.RunOnStartup,
			PreventOverlap = consumerOptions.PreventOverlap,
			MessageFactory = (name, cron, triggerTime, timeZoneId) => new CronTimerTriggerMessage<TTimer>
			{
				TimerName = name,
				CronExpression = cron,
				TriggerTimeUtc = triggerTime,
				TimeZone = timeZoneId,
			},
		};

		return RegisterCronTimerTransport(services, timerName, adapterOptions);
	}

	/// <summary>
	/// Adds a named cron timer transport that generates <see cref="CronTimerTriggerMessage"/>
	/// messages on the specified schedule.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The transport name for multi-transport routing.</param>
	/// <param name="cronExpression">
	/// The cron expression defining the schedule. Supports 5-field (minute-level) and
	/// 6-field (second-level) expressions.
	/// </param>
	/// <param name="configure">Optional configuration action for additional timer options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> or <paramref name="cronExpression"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload creates untyped <see cref="CronTimerTriggerMessage"/> instances.
	/// All handlers of <c>CronTimerTriggerMessage</c> will receive events from this timer.
	/// For type-safe routing, use the generic <c>AddCronTimerTransport&lt;TTimer&gt;</c> overload.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCronTimerTransport("health-check", "*/30 * * * * *", options =>
	/// {
	///     options.RunOnStartup = true;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddCronTimerTransport(
		this IServiceCollection services,
		string name,
		string cronExpression,
		Action<CronTimerOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);

		var consumerOptions = new CronTimerOptions();
		configure?.Invoke(consumerOptions);

		var adapterOptions = new CronTimerTransportAdapterOptions
		{
			Name = name,
			CronExpression = cronExpression,
			TimeZone = consumerOptions.TimeZone,
			RunOnStartup = consumerOptions.RunOnStartup,
			PreventOverlap = consumerOptions.PreventOverlap,
		};

		return RegisterCronTimerTransport(services, name, adapterOptions);
	}

	private static IServiceCollection RegisterCronTimerTransport(
		IServiceCollection services,
		string name,
		CronTimerTransportAdapterOptions adapterOptions)
	{
		// Validate cron expression format eagerly (5 or 6 fields expected)
		ValidateCronExpressionFormat(adapterOptions.CronExpression, name);

		// Ensure ICronScheduler is registered (idempotent -- may already be registered via AddDelivery)
		services.TryAddSingleton<ICronScheduler, CronScheduler>();

		// Register as a keyed service for multi-transport scenarios
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var logger = sp.GetRequiredService<ILogger<CronTimerTransportAdapter>>();
			var cronScheduler = sp.GetRequiredService<ICronScheduler>();

			// Validate the expression can be parsed by the scheduler at construction time
			if (!cronScheduler.TryParse(adapterOptions.CronExpression, adapterOptions.TimeZone, out var _))
			{
				throw new InvalidOperationException(
					$"Cron timer '{name}' has an invalid cron expression: '{adapterOptions.CronExpression}'. " +
					"Expected a valid 5-field (minute-level) or 6-field (second-level) cron expression.");
			}

			return new CronTimerTransportAdapter(logger, cronScheduler, sp, adapterOptions);
		});

		// Register factory in TransportRegistry for lifecycle management
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.RegisterTransportFactory(
			name,
			CronTimerTransportAdapter.TransportTypeName,
			sp => sp.GetRequiredKeyedService<CronTimerTransportAdapter>(name));

		// Register named options + validator for ValidateOnStart
		services.AddOptions<CronTimerTransportAdapterOptions>(name)
			.Configure(o =>
			{
				o.Name = adapterOptions.Name;
				o.CronExpression = adapterOptions.CronExpression;
				o.TimeZone = adapterOptions.TimeZone;
				o.RunOnStartup = adapterOptions.RunOnStartup;
				o.PreventOverlap = adapterOptions.PreventOverlap;
			})
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CronTimerTransportAdapterOptions>,
				CronTimerTransportAdapterOptionsValidator>());

		// Ensure hosted service lifecycle manager is registered (idempotent)
		_ = services.AddTransportAdapterLifecycle();

		return services;
	}

	/// <summary>
	/// Validates the cron expression has the expected field count (5 or 6 fields).
	/// This catches obviously malformed expressions at registration time before the
	/// service provider is built.
	/// </summary>
	private static void ValidateCronExpressionFormat(string cronExpression, string timerName)
	{
		var fieldCount = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

		if (fieldCount is not (5 or 6))
		{
			throw new ArgumentException(
				$"Cron timer '{timerName}' has an invalid cron expression: '{cronExpression}'. " +
				$"Expected 5 fields (minute-level) or 6 fields (second-level), but found {fieldCount}.",
				nameof(cronExpression));
		}
	}
}

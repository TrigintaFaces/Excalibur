// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Snapshots;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring automatic snapshot policies on <see cref="IEventSourcingBuilder"/>.
/// </summary>
public static class AutoSnapshotServiceCollectionExtensions
{
	/// <summary>
	/// Configures automatic snapshot creation with global default options.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Action to configure snapshot thresholds.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Auto-snapshots are evaluated inline after <c>SaveAsync</c> succeeds.
	/// Snapshot creation is best-effort -- failure does not fail the save.
	/// </para>
	/// <para>
	/// Use <see cref="UseAutoSnapshots{TAggregate}(IEventSourcingBuilder, Action{AutoSnapshotOptions})"/> to override thresholds
	/// for specific aggregate types.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(builder =&gt;
	/// {
	///     builder.UseAutoSnapshots(options =&gt;
	///     {
	///         options.EventCountThreshold = 100;
	///     });
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseAutoSnapshots(
		this IEventSourcingBuilder builder,
		Action<AutoSnapshotOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Services.Configure(configure);
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AutoSnapshotOptions>, AutoSnapshotOptionsValidator>());
		builder.Services.AddOptionsWithValidateOnStart<AutoSnapshotOptions>();

		return builder;
	}

	/// <summary>
	/// Configures automatic snapshot creation with global default options bound from an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configuration">The configuration section to bind <see cref="AutoSnapshotOptions"/> from.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// This overload binds options from configuration (e.g., <c>appsettings.json</c>) instead of
	/// an imperative <see cref="Action{T}"/> delegate. Data annotations are validated on start.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(builder =&gt;
	/// {
	///     builder.UseAutoSnapshots(configuration.GetSection("AutoSnapshot"));
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseAutoSnapshots(
		this IEventSourcingBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		builder.Services.AddOptions<AutoSnapshotOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AutoSnapshotOptions>, AutoSnapshotOptionsValidator>());

		return builder;
	}

	/// <summary>
	/// Configures automatic snapshot creation with per-aggregate-type overrides.
	/// </summary>
	/// <typeparam name="TAggregate">The aggregate type to configure specific thresholds for.</typeparam>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Action to configure snapshot thresholds for this aggregate type.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Per-aggregate-type options are registered as named options using the aggregate type name.
	/// They take precedence over the global defaults configured via
	/// <see cref="UseAutoSnapshots(IEventSourcingBuilder, Action{AutoSnapshotOptions})"/>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(builder =&gt;
	/// {
	///     builder.UseAutoSnapshots(options =&gt;
	///     {
	///         options.EventCountThreshold = 100;  // Global default
	///     });
	///
	///     builder.UseAutoSnapshots&lt;OrderAggregate&gt;(options =&gt;
	///     {
	///         options.EventCountThreshold = 50;   // Orders snapshot more frequently
	///         options.TimeThreshold = TimeSpan.FromHours(1);
	///     });
	/// });
	/// </code>
	/// </example>
#pragma warning disable RS0016 // Add public types and members to the declared API (constrained generic not representable in baseline)
	public static IEventSourcingBuilder UseAutoSnapshots<TAggregate>(
		this IEventSourcingBuilder builder,
		Action<AutoSnapshotOptions> configure)
		where TAggregate : class
#pragma warning restore RS0016
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Register as named options using the aggregate type name
		var aggregateTypeName = typeof(TAggregate).Name;
		builder.Services.Configure(aggregateTypeName, configure);

		return builder;
	}

	/// <summary>
	/// Configures automatic snapshot creation with per-aggregate-type overrides bound from an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <typeparam name="TAggregate">The aggregate type to configure specific thresholds for.</typeparam>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configuration">The configuration section to bind <see cref="AutoSnapshotOptions"/> from.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Per-aggregate-type options are registered as named options using the aggregate type name.
	/// They take precedence over the global defaults configured via
	/// <see cref="UseAutoSnapshots(IEventSourcingBuilder, Action{AutoSnapshotOptions})"/>.
	/// </para>
	/// </remarks>
#pragma warning disable RS0016 // Add public types and members to the declared API (constrained generic not representable in baseline)
	public static IEventSourcingBuilder UseAutoSnapshots<TAggregate>(
		this IEventSourcingBuilder builder,
		IConfiguration configuration)
		where TAggregate : class
#pragma warning restore RS0016
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		var aggregateTypeName = typeof(TAggregate).Name;
		builder.Services.AddOptions<AutoSnapshotOptions>(aggregateTypeName)
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return builder;
	}
}

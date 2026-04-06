// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring outbox options with performance presets.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide a convenient way to configure <see cref="OutboxDeliveryOptions"/>
/// using predefined performance presets:
/// </para>
/// <list type="bullet">
///   <item>
///     <term><see cref="AddOutboxHighThroughput(IServiceCollection, Action{OutboxDeliveryOptions}?)"/></term>
///     <description>Maximum throughput (10K+ msg/s) for event sourcing, analytics.</description>
///   </item>
///   <item>
///     <term><see cref="AddOutboxBalanced(IServiceCollection, Action{OutboxDeliveryOptions}?)"/></term>
///     <description>Good throughput (3-5K msg/s) for general purpose workloads.</description>
///   </item>
///   <item>
///     <term><see cref="AddOutboxHighReliability(IServiceCollection, Action{OutboxDeliveryOptions}?)"/></term>
///     <description>Maximum reliability with smallest failure window for critical messages.</description>
///   </item>
/// </list>
/// <para>
/// Each method accepts an optional configure callback to further customize the preset:
/// </para>
/// <code>
/// services.AddOutboxHighThroughput(options =>
/// {
///     options.ParallelProcessingDegree = 4; // Reduce from 8 to 4
/// });
/// </code>
/// </remarks>
public static class OutboxPresetServiceCollectionExtensions
{
	/// <summary>
	/// Configures outbox options with the high throughput preset.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional action to further customize the preset options.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddOutboxHighThroughput(
		this IServiceCollection services,
		Action<OutboxDeliveryOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OutboxDeliveryOptions>, OutboxDeliveryOptionsValidator>());

		var builder = services.AddOptions<OutboxDeliveryOptions>()
			.Configure(options =>
			{
				var preset = OutboxDeliveryOptions.HighThroughput();
				CopyFrom(options, preset);
				configure?.Invoke(options);
			})
			.Validate(
				static options => OutboxDeliveryOptions.Validate(options) is null,
				"OutboxDeliveryOptions failed validation.")
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Configures outbox options with the high throughput preset, then applies overrides
	/// from an <see cref="IConfiguration"/> section.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddOutboxHighThroughput(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OutboxDeliveryOptions>, OutboxDeliveryOptionsValidator>());

		var builder = services.AddOptions<OutboxDeliveryOptions>()
			.Configure(options =>
			{
				var preset = OutboxDeliveryOptions.HighThroughput();
				CopyFrom(options, preset);
				configuration.Bind(options);
			})
			.Validate(
				static options => OutboxDeliveryOptions.Validate(options) is null,
				"OutboxDeliveryOptions failed validation.")
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Configures outbox options with the balanced preset.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional action to further customize the preset options.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddOutboxBalanced(
		this IServiceCollection services,
		Action<OutboxDeliveryOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OutboxDeliveryOptions>, OutboxDeliveryOptionsValidator>());

		var builder = services.AddOptions<OutboxDeliveryOptions>()
			.Configure(options =>
			{
				var preset = OutboxDeliveryOptions.Balanced();
				CopyFrom(options, preset);
				configure?.Invoke(options);
			})
			.Validate(
				static options => OutboxDeliveryOptions.Validate(options) is null,
				"OutboxDeliveryOptions failed validation.")
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Configures outbox options with the balanced preset, then applies overrides
	/// from an <see cref="IConfiguration"/> section.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddOutboxBalanced(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OutboxDeliveryOptions>, OutboxDeliveryOptionsValidator>());

		var builder = services.AddOptions<OutboxDeliveryOptions>()
			.Configure(options =>
			{
				var preset = OutboxDeliveryOptions.Balanced();
				CopyFrom(options, preset);
				configuration.Bind(options);
			})
			.Validate(
				static options => OutboxDeliveryOptions.Validate(options) is null,
				"OutboxDeliveryOptions failed validation.")
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Configures outbox options with the high reliability preset.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional action to further customize the preset options.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddOutboxHighReliability(
		this IServiceCollection services,
		Action<OutboxDeliveryOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OutboxDeliveryOptions>, OutboxDeliveryOptionsValidator>());

		var builder = services.AddOptions<OutboxDeliveryOptions>()
			.Configure(options =>
			{
				var preset = OutboxDeliveryOptions.HighReliability();
				CopyFrom(options, preset);
				configure?.Invoke(options);
			})
			.Validate(
				static options => OutboxDeliveryOptions.Validate(options) is null,
				"OutboxDeliveryOptions failed validation.")
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Configures outbox options with the high reliability preset, then applies overrides
	/// from an <see cref="IConfiguration"/> section.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddOutboxHighReliability(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OutboxDeliveryOptions>, OutboxDeliveryOptionsValidator>());

		var builder = services.AddOptions<OutboxDeliveryOptions>()
			.Configure(options =>
			{
				var preset = OutboxDeliveryOptions.HighReliability();
				CopyFrom(options, preset);
				configuration.Bind(options);
			})
			.Validate(
				static options => OutboxDeliveryOptions.Validate(options) is null,
				"OutboxDeliveryOptions failed validation.")
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Copies all property values from the source options to the target options.
	/// </summary>
	/// <param name="target">The target options to copy to.</param>
	/// <param name="source">The source options to copy from.</param>
	private static void CopyFrom(OutboxDeliveryOptions target, OutboxDeliveryOptions source)
	{
		target.PerRunTotal = source.PerRunTotal;
		target.QueueCapacity = source.QueueCapacity;
		target.ProducerBatchSize = source.ProducerBatchSize;
		target.ConsumerBatchSize = source.ConsumerBatchSize;
		target.MaxAttempts = source.MaxAttempts;
		target.DefaultMessageTimeToLive = source.DefaultMessageTimeToLive;
		target.BatchProcessing.ParallelProcessingDegree = source.BatchProcessing.ParallelProcessingDegree;
		target.BatchProcessing.EnableDynamicBatchSizing = source.BatchProcessing.EnableDynamicBatchSizing;
		target.BatchProcessing.MinBatchSize = source.BatchProcessing.MinBatchSize;
		target.BatchProcessing.MaxBatchSize = source.BatchProcessing.MaxBatchSize;
		target.BatchProcessing.BatchProcessingTimeout = source.BatchProcessing.BatchProcessingTimeout;
		target.EnableBatchDatabaseOperations = source.EnableBatchDatabaseOperations;
		target.DeliveryGuarantee = source.DeliveryGuarantee;
	}
}

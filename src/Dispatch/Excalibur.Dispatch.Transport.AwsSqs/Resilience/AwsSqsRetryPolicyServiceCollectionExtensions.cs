// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AWS SQS retry policy with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// Configures retry policies that wrap AWS SDK calls with configurable
/// backoff strategies. The retry policy applies to send, receive, and
/// delete operations against SQS queues.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsSqsRetryPolicy(options =>
/// {
///     options.MaxRetryAttempts = 5;
///     options.BaseDelay = TimeSpan.FromMilliseconds(200);
///     options.MaxDelay = TimeSpan.FromSeconds(30);
///     options.RetryStrategy = SqsRetryStrategy.Exponential;
/// });
/// </code>
/// </example>
public static class AwsSqsRetryPolicyServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS SQS retry policy support with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure retry policy options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Registers <see cref="AwsSqsRetryOptions"/> in the DI container with
	/// <see cref="IValidateOptions{TOptions}"/> validation and startup validation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddAwsSqsRetryPolicy(
		this IServiceCollection services,
		Action<AwsSqsRetryOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<AwsSqsRetryOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AwsSqsRetryOptions>, AwsSqsRetryOptionsValidator>());

		return services;
	}

	/// <summary>
	/// Adds AWS SQS retry policy support using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="AwsSqsRetryOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.
	/// </exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAwsSqsRetryPolicy(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<AwsSqsRetryOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AwsSqsRetryOptions>, AwsSqsRetryOptionsValidator>());

		return services;
	}
}

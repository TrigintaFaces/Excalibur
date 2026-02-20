// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AwsSqs;

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
///     options.MaxRetries = 5;
///     options.BaseDelay = TimeSpan.FromMilliseconds(200);
///     options.MaxDelay = TimeSpan.FromSeconds(30);
///     options.RetryStrategy = RetryStrategy.Exponential;
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
	/// Registers <see cref="AwsSqsRetryOptions"/> in the DI container with data annotation
	/// validation and startup validation.
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
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}
}

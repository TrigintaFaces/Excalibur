// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Google Pub/Sub server-side filtering with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// Server-side filtering reduces bandwidth and processing costs by having Pub/Sub evaluate
/// filter expressions against message attributes before delivery. Only matching messages
/// are delivered to subscribers.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddGooglePubSubFilter(options =>
/// {
///     options.Enabled = true;
///     options.FilterExpression = "attributes.type = \"order.created\"";
/// });
/// </code>
/// </example>
public static class PubSubFilterServiceCollectionExtensions
{
	/// <summary>
	/// Adds Google Pub/Sub server-side message filtering with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure filter options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Registers <see cref="PubSubFilterOptions"/> in the DI container with data annotation
	/// validation and startup validation. The filter expression is applied when creating
	/// subscriptions via the Pub/Sub API.
	/// </para>
	/// <para>
	/// Note: Filters can only be set when creating a subscription. Existing subscriptions
	/// cannot have their filter modified.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddGooglePubSubFilter(
		this IServiceCollection services,
		Action<PubSubFilterOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<PubSubFilterOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}
}

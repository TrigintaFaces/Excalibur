// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Google Pub/Sub transport via <see cref="IDispatchBuilder"/>.
/// </summary>
public static class DispatchBuilderGooglePubSubExtensions
{
	/// <summary>
	/// Configures the Google Pub/Sub transport with the default name via the dispatch builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseGooglePubSub(pubsub =>
	///     {
	///         pubsub.ProjectId("my-project")
	///               .TopicId("my-topic")
	///               .SubscriptionId("my-subscription");
	///     });
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseGooglePubSub(
		this IDispatchBuilder builder,
		Action<IGooglePubSubTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddGooglePubSubTransport(configure);
		return builder;
	}

	/// <summary>
	/// Configures a named Google Pub/Sub transport via the dispatch builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="name">The transport name for multi-transport scenarios.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseGooglePubSub("analytics", pubsub =>
	///     {
	///         pubsub.ProjectId("analytics-project")
	///               .TopicId("metrics-topic");
	///     });
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseGooglePubSub(
		this IDispatchBuilder builder,
		string name,
		Action<IGooglePubSubTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddGooglePubSubTransport(name, configure);
		return builder;
	}
}
